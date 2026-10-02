using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ProxyRetranslator.Logging;

namespace ProxyRetranslator.Services
{
    /// <summary>
    /// Ищет IP телефона (шлюз USB-модема) среди активных Ethernet-адаптеров.
    /// </summary>
    public sealed class GatewayDetector
    {
        private readonly ILogSink _log;

        public GatewayDetector(ILogSink log)
        {
            _log = log;
        }

        public string DetectUsbGateway()
        {
            try
            {
                var candidates = new List<Tuple<int, string, string, string>>();

                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    .Where(ni => !ni.Name.ToLower().Contains("vethernet"))
                    .Where(ni => !ni.Description.ToLower().Contains("vethernet"))
                    .Where(ni => !ni.Description.ToLower().Contains("hyper-v"))
                    .Where(ni => !ni.Name.ToLower().Contains("bluetooth"))
                    .Where(ni => !ni.Description.ToLower().Contains("bluetooth"))
                    .Where(ni => !ni.Description.ToLower().Contains("virtual"))
                    .Where(ni => !ni.Description.ToLower().Contains("loopback"));

                foreach (var ni in nics)
                {
                    var props = ni.GetIPProperties();
                    var gw = props.GatewayAddresses
                        .FirstOrDefault(g => g.Address != null &&
                                             g.Address.AddressFamily == AddressFamily.InterNetwork);

                    if (gw == null) continue;

                    string ip = gw.Address.ToString();
                    string name = ni.Name;
                    string desc = ni.Description;

                    int priority = 2;
                    string lower = (desc + " " + name).ToLower();

                    if (lower.Contains("remote ndis") ||
                        lower.Contains("rndis") ||
                        lower.Contains("usb ethernet") ||
                        lower.Contains("usb tethering") ||
                        lower.Contains("android") ||
                        lower.Contains("cdc") ||
                        lower.Contains("internet sharing") ||
                        lower.Contains("mobile") ||
                        lower.Contains("apple mobile device"))
                    {
                        priority = 0;
                    }
                    else if (!(ip.StartsWith("192.168.0.1") || ip.StartsWith("192.168.1.1") ||
                               ip.StartsWith("192.168.2.1") || ip.StartsWith("10.0.0.1") ||
                               ip.StartsWith("10.0.1.1") || ip.StartsWith("172.16.0.1") ||
                               ip.StartsWith("192.168.10.1") || ip.StartsWith("192.168.100.1")))
                    {
                        priority = 1;
                    }

                    candidates.Add(Tuple.Create(priority, name, desc, ip));
                    _log.Log($"  Кандидат: {name} ({desc}) → {ip}, приоритет {priority}");
                }

                if (candidates.Count == 0)
                    return null;

                var best = candidates.OrderBy(c => c.Item1).First();

                _log.Log($"  Выбран адаптер: {best.Item2} ({best.Item3}) → {best.Item4}");
                return best.Item4;
            }
            catch (Exception ex)
            {
                _log.Log($"  ОШИБКА автопоиска: {ex.Message}");
            }
            return null;
        }
    }
}