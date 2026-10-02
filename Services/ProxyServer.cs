using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProxyRetranslator.Logging;
using ProxyRetranslator.Utils;

namespace ProxyRetranslator.Services
{
    /// <summary>
    /// Локальный HTTP/CONNECT-прокси.
    /// Принимает запросы на 127.0.0.1:port и пересылает их
    /// либо в HAPP-прокси телефона, либо напрямую в целевой хост.
    /// </summary>
    public sealed class ProxyServer
    {
        public sealed class Config
        {
            public int ListenPort;
            public bool RedirectToHapp;
            public string HappIp;
            public int HappPort;
        }

        private readonly ILogSink _log;
        private readonly Func<Config> _getConfig;

        private TcpListener _listener;
        private CancellationTokenSource _cts;
        private volatile bool _running;

        public bool IsRunning => _running;

        public ProxyServer(ILogSink log, Func<Config> getConfig)
        {
            _log = log;
            _getConfig = getConfig;
        }

        public void Start()
        {
            if (_running) return;

            var cfg = _getConfig();
            if (cfg.ListenPort <= 0 || cfg.ListenPort > 65535)
                throw new InvalidOperationException("Некорректный порт прослушивания.");

            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, cfg.ListenPort);
            _listener.Start();
            _running = true;

            _log.Log($"=== Прокси запущен на 127.0.0.1:{cfg.ListenPort} ===");
            _log.Log($"Перенаправление на HAPP: {(cfg.RedirectToHapp ? "ВКЛ" : "ВЫКЛ")}");
            _log.Log($"HAPP адрес: {cfg.HappIp}:{cfg.HappPort}");
            _log.Log("Ожидание подключений...");

            Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            if (!_running) return;
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener = null;
                _running = false;
                _log.Log("=== Прокси остановлен ===");
            }
            catch (Exception ex)
            {
                _log.Log($"ОШИБКА остановки: {ex.Message}");
            }
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client, token));
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        _log.Log($"ОШИБКА Accept: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            string clientEp = client.Client.RemoteEndPoint?.ToString() ?? "?";
            _log.Log($"→ Подключение от {clientEp}");

            try
            {
                client.NoDelay = true;

                using (client)
                using (NetworkStream clientStream = client.GetStream())
                {
                    var header = new StringBuilder();
                    string requestLine = null;

                    while (true)
                    {
                        string line = await NetworkHelper.ReadLineWithTimeoutAsync(clientStream, 10000, token);
                        if (line == null)
                        {
                            _log.Log("  Клиент оборвал соединение до конца заголовков.");
                            return;
                        }

                        if (requestLine == null) requestLine = line;
                        if (line.Length == 0) break;

                        header.Append(line).Append("\r\n");
                    }

                    if (string.IsNullOrEmpty(requestLine))
                    {
                        _log.Log("  Пустой запрос.");
                        return;
                    }

                    _log.Log($"  Запрос: {requestLine}");

                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 3)
                    {
                        _log.Log("  Некорректный запрос.");
                        return;
                    }

                    string method = parts[0];
                    string target = parts[1];

                    bool isConnect = HttpParser.IsConnect(method);

                    string host;
                    int port;

                    if (isConnect)
                    {
                        HttpParser.ParseHostPort(target, 443, out host, out port);
                    }
                    else
                    {
                        Uri uri;
                        if (Uri.TryCreate(target, UriKind.Absolute, out uri))
                        {
                            host = uri.Host;
                            port = uri.Port;
                        }
                        else
                        {
                            _log.Log($"  Не удалось разобрать URL: {target}");
                            return;
                        }
                    }

                    _log.Log($"  Цель: {host}:{port}");

                    var cfg = _getConfig();

                    string upstreamHost;
                    int upstreamPort;
                    bool happOn = cfg.RedirectToHapp &&
                                  !string.IsNullOrEmpty(cfg.HappIp) &&
                                  cfg.HappPort > 0 && cfg.HappPort <= 65535;

                    if (happOn)
                    {
                        upstreamHost = cfg.HappIp;
                        upstreamPort = cfg.HappPort;
                        _log.Log($"  → Перенаправление на HAPP {upstreamHost}:{upstreamPort}");
                    }
                    else
                    {
                        upstreamHost = host;
                        upstreamPort = port;
                        _log.Log($"  → Прямое подключение к {upstreamHost}:{upstreamPort}");
                    }

                    using (TcpClient upstream = new TcpClient())
                    {
                        upstream.NoDelay = true;

                        try
                        {
                            await upstream.ConnectAsync(upstreamHost, upstreamPort);
                        }
                        catch (Exception ex)
                        {
                            _log.Log($"  ОШИБКА подключения к {upstreamHost}:{upstreamPort}: {ex.Message}");
                            byte[] err = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n");
                            try { await clientStream.WriteAsync(err, 0, err.Length); } catch { }
                            return;
                        }

                        NetworkStream upstreamStream = upstream.GetStream();

                        if (isConnect)
                        {
                            if (happOn)
                            {
                                byte[] reqBytes = Encoding.ASCII.GetBytes(header.ToString() + "\r\n");
                                await upstreamStream.WriteAsync(reqBytes, 0, reqBytes.Length);

                                _log.Log("  CONNECT-запрос передан upstream'у, ждём ответ...");

                                string statusLine = await NetworkHelper.ReadLineWithTimeoutAsync(upstreamStream, 10000, token);
                                if (string.IsNullOrEmpty(statusLine))
                                {
                                    _log.Log("  Upstream не ответил на CONNECT (timeout).");
                                    byte[] err = Encoding.ASCII.GetBytes("HTTP/1.1 504 Gateway Timeout\r\n\r\n");
                                    try { await clientStream.WriteAsync(err, 0, err.Length); } catch { }
                                    return;
                                }
                                _log.Log($"  Upstream ответил: {statusLine}");

                                while (true)
                                {
                                    string hdr = await NetworkHelper.ReadLineWithTimeoutAsync(upstreamStream, 10000, token);
                                    if (hdr == null) break;
                                    if (hdr.Length == 0) break;
                                }

                                if (statusLine.IndexOf(" 200", StringComparison.Ordinal) < 0)
                                {
                                    _log.Log("  Upstream отказал в туннеле.");
                                    byte[] err = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n");
                                    try { await clientStream.WriteAsync(err, 0, err.Length); } catch { }
                                    return;
                                }

                                byte[] ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                                await clientStream.WriteAsync(ok, 0, ok.Length);
                                _log.Log("  Туннель CONNECT установлен (через HAPP).");
                            }
                            else
                            {
                                byte[] ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                                await clientStream.WriteAsync(ok, 0, ok.Length);
                                _log.Log("  Туннель CONNECT установлен (напрямую).");
                            }
                        }
                        else
                        {
                            byte[] reqBytes = Encoding.ASCII.GetBytes(header.ToString() + "\r\n");
                            await upstreamStream.WriteAsync(reqBytes, 0, reqBytes.Length);
                            _log.Log("  HTTP-запрос с заголовками отправлен в upstream.");
                        }

                        Task t1 = NetworkHelper.PumpAsync(clientStream, upstreamStream, token);
                        Task t2 = NetworkHelper.PumpAsync(upstreamStream, clientStream, token);
                        await Task.WhenAny(t1, t2);
                        try { await Task.WhenAll(t1, t2); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Log($"  ОШИБКА обработки {clientEp}: {ex.Message}");
            }
            finally
            {
                _log.Log($"← Соединение {clientEp} закрыто");
            }
        }
    }
}