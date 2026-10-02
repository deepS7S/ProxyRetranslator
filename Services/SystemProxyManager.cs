using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ProxyRetranslator.Logging;
using ProxyRetranslator.Models;

namespace ProxyRetranslator.Services
{
    /// <summary>
    /// Управляет системным прокси Windows через реестр и WinINet.
    /// Умеет сохранять исходное состояние и восстанавливать его.
    /// </summary>
    public sealed class SystemProxyManager
    {
        /// <summary>
        /// Снимок состояния прокси Windows до его изменения.
        /// Хранит только то, что мы меняем: ProxyEnable и ProxyServer.
        /// </summary>
        public sealed class Snapshot
        {
            public int ProxyEnable;
            public string ProxyServer;
            public bool IsValid;
        }

        private readonly ILogSink _log;

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        public SystemProxyManager(ILogSink log)
        {
            _log = log;
        }

        /// <summary>
        /// Сохраняет текущие значения ProxyEnable и ProxyServer из реестра.
        /// </summary>
        public Snapshot Capture()
        {
            var snap = new Snapshot { IsValid = false, ProxyEnable = 0, ProxyServer = null };

            try
            {
                using (RegistryKey reg = Registry.CurrentUser.OpenSubKey(AppConfig.RegistryPath, false))
                {
                    if (reg == null) return snap;

                    object enable = reg.GetValue("ProxyEnable");
                    object server = reg.GetValue("ProxyServer");

                    snap.ProxyEnable = (enable is int) ? (int)enable : 0;
                    snap.ProxyServer = server as string;
                    snap.IsValid = true;

                    _log.Log($"  Сохранено исходное состояние прокси: ProxyEnable={snap.ProxyEnable}, ProxyServer={(snap.ProxyServer ?? "<null>")}");
                }
            }
            catch (Exception ex)
            {
                _log.Log($"  ОШИБКА сохранения исходного состояния прокси: {ex.Message}");
            }

            return snap;
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

        /// <summary>
        /// Восстанавливает состояние прокси из снимка.
        /// Если снимок невалиден — просто выключает прокси (как раньше).
        /// </summary>
        public void Restore(Snapshot snapshot)
        {
            try
            {
                using (RegistryKey reg = Registry.CurrentUser.OpenSubKey(AppConfig.RegistryPath, true))
                {
                    if (reg == null) return;

                    if (snapshot != null && snapshot.IsValid)
                    {
                        reg.SetValue("ProxyEnable", snapshot.ProxyEnable, RegistryValueKind.DWord);

                        if (!string.IsNullOrEmpty(snapshot.ProxyServer))
                            reg.SetValue("ProxyServer", snapshot.ProxyServer, RegistryValueKind.String);
                        else
                            reg.DeleteValue("ProxyServer", false);
                    }
                    else
                    {
                        // Fallback: старое поведение — просто выключаем.
                        reg.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                    }
                }

                Refresh();
                _log.Log("  Системный прокси Windows восстановлен в исходное состояние.");
            }
            catch (Exception ex)
            {
                _log.Log($"  ОШИБКА восстановления системного прокси: {ex.Message}");
            }
        }

        private void Refresh()
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
    }
}