namespace ProxyRetranslator.Models
{
    /// <summary>
    /// Куда направлять запрос: либо напрямую на целевой хост,
    /// либо на HAPP-прокси телефона.
    /// </summary>
    public sealed class UpstreamTarget
    {
        public string Host { get; }
        public int Port { get; }
        public bool IsHapp { get; }

        public UpstreamTarget(string host, int port, bool isHapp)
        {
            Host = host;
            Port = port;
            IsHapp = isHapp;
        }

        public override string ToString() => $"{Host}:{Port}" + (IsHapp ? " (HAPP)" : " (direct)");
    }
}