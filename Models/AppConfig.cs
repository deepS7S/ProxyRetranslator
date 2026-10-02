namespace ProxyRetranslator.Models
{
    /// <summary>
    /// Централизованное место для констант и настроек по умолчанию.
    /// </summary>
    public static class AppConfig
    {
        public const int MaxLogChars = 100_000;
        public const int DefaultListenPort = 10811;
        public const int DefaultHappPort = 10808;
        public const string DefaultHappIp = "0.0.0.0";

        public const string RegistryPath =
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    }
}