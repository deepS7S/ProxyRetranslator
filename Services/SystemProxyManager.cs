using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ProxyRetranslator.Logging;
using ProxyRetranslator.Models;

namespace ProxyRetranslator.Services
{
    /// <summary>
    /// Управляет системным прокси Windows через реестр и WinINet.
    /// </summary>
    public sealed class SystemProxyManager
    {
        private readonly ILogSink _log;

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        public SystemProxyManager(ILogSink log)
        {
            _log = log;
        }

        public void Enable(int port)
        {
            try
            {
                using (RegistryKey reg = Registry.CurrentUser.OpenSubKey(AppConfig.RegistryPath, true))
                {
                    if (reg == null)
                    {
                        _log.Log("  ОШИБКА: не удалось открыть реестр для системного прокси.");
                        return;
                    }

                    reg.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                    reg.SetValue("ProxyServer", $"127.0.0.1:{port}", RegistryValueKind.String);
                }

                Refresh();
                _log.Log($"  Системный прокси Windows включён: 127.0.0.1:{port}");
            }
            catch (Exception ex)
            {
                _log.Log($"  ОШИБКА включения системного прокси: {ex.Message}");
            }
        }

        public void Disable()
        {
            try
            {
                using (RegistryKey reg = Registry.CurrentUser.OpenSubKey(AppConfig.RegistryPath, true))
                {
                    if (reg == null) return;
                    reg.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                }

                Refresh();
                _log.Log("  Системный прокси Windows выключен.");
            }
            catch (Exception ex)
            {
                _log.Log($"  ОШИБКА выключения системного прокси: {ex.Message}");
            }
        }

        private void Refresh()
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
    }
}