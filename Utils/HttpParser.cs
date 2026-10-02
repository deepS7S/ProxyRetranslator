using System;
using System.Net.Sockets;

namespace ProxyRetranslator.Utils
{
    /// <summary>
    /// Разбор HTTP-запроса: парсинг target, host/port.
    /// </summary>
    public static class HttpParser
    {
        /// <summary>
        /// Разбирает target вида "host:port", "[::1]:443", "host", "2001:db8::1".
        /// </summary>
        public static void ParseHostPort(string target, int defaultPort, out string host, out int port)
        {
            host = target;
            port = defaultPort;

            if (string.IsNullOrEmpty(target)) return;

            if (target.StartsWith("["))
            {
                int close = target.IndexOf(']');
                if (close > 0)
                {
                    host = target.Substring(1, close - 1);
                    if (target.Length > close + 2 && target[close + 1] == ':')
                    {
                        int p;
                        if (int.TryParse(target.Substring(close + 2), out p)) port = p;
                    }
                }
                return;
            }

            int colonCount = 0;
            foreach (char c in target) if (c == ':') colonCount++;

            if (colonCount == 1)
            {
                int idx = target.LastIndexOf(':');
                host = target.Substring(0, idx);
                int p;
                if (int.TryParse(target.Substring(idx + 1), out p)) port = p;
            }
        }

        /// <summary>
        /// Проверяет, что метод — CONNECT.
        /// </summary>
        public static bool IsConnect(string method) =>
            method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase);
    }
}