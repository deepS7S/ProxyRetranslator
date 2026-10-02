using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyRetranslator.Utils
{
    /// <summary>
    /// Вспомогательные сетевые операции: чтение строки, перекачка байтов.
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>
        /// Читает одну строку из потока (до \n, без \r).
        /// Возвращает null, если соединение закрыто.
        /// </summary>
        public static async Task<string> ReadLineAsync(NetworkStream stream, CancellationToken token)
        {
            var sb = new StringBuilder();
            byte[] one = new byte[1];

            while (!token.IsCancellationRequested)
            {
                int n;
                try { n = await stream.ReadAsync(one, 0, 1, token); }
                catch { return null; }

                if (n == 0) return sb.Length > 0 ? sb.ToString() : null;

                char c = (char)one[0];
                if (c == '\n') break;
                if (c != '\r') sb.Append(c);
                if (sb.Length > 16384) break;
            }
            return sb.ToString();
        }

        /// <summary>
        /// То же самое, но с таймаутом. При таймауте возвращает null.
        /// </summary>
        public static async Task<string> ReadLineWithTimeoutAsync(NetworkStream stream, int timeoutMs, CancellationToken token)
        {
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeoutCts.CancelAfter(timeoutMs);
                try
                {
                    return await ReadLineAsync(stream, timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Перекачивает байты из одного NetworkStream в другой до закрытия.
        /// </summary>
        public static async Task PumpAsync(NetworkStream from, NetworkStream to, CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int n = await from.ReadAsync(buffer, 0, buffer.Length, token);
                    if (n == 0) break;
                    await to.WriteAsync(buffer, 0, n, token);
                }
            }
            catch { }
        }
    }
}