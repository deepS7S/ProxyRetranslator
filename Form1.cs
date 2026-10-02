using System;
using System.Drawing;
using System.Windows.Forms;
using ProxyRetranslator.Logging;
using ProxyRetranslator.Models;
using ProxyRetranslator.Services;

namespace ProxyRetranslator
{
    public partial class Form1 : Form
    {
        // Контролы
        private CheckBox chkHappEnabled;
        private CheckBox chkAutoSystemProxy;
        private CheckBox chkAutoDetectOnStart;
        private TextBox txtListenPort;
        private TextBox txtHappIp;
        private TextBox txtHappPort;
        private Button btnToggle;
        private Button btnAutoDetect;
        private Button btnClearLog;
        private RichTextBox rtbLog;
        private Label lblStatus;

        // Сервисы
        private ILogSink _log;
        private GatewayDetector _gateway;
        private SystemProxyManager _sysProxy;
        private ProxyServer _proxy;

        public Form1()
        {
            BuildUi();

            rtbLog.ReadOnly = true;
            rtbLog.BackColor = Color.Black;
            rtbLog.ForeColor = Color.LightGreen;
            rtbLog.Font = new Font("Consolas", 9f);
            rtbLog.WordWrap = false;
            rtbLog.HideSelection = false;

            lblStatus.Text = "Статус: остановлен";

            // Инициализация сервисов
            _log = new RichTextBoxLogSink(rtbLog);
            _gateway = new GatewayDetector(_log);
            _sysProxy = new SystemProxyManager(_log);
            _proxy = new ProxyServer(_log, ReadConfig);

            btnToggle.Click += btnToggle_Click;
            btnAutoDetect.Click += btnAutoDetect_Click;
            btnClearLog.Click += btnClearLog_Click;
            chkHappEnabled.CheckedChanged += chkHappEnabled_CheckedChanged;
            FormClosing += Form1_FormClosing;

            // Изначально порт HAPP заблокирован, если галочка не стоит
            UpdateHappPortEnabled();
        }

        // Собираем актуальный конфиг из UI
        private ProxyServer.Config ReadConfig()
        {
            int listenPort = AppConfig.DefaultListenPort;
            int.TryParse(txtListenPort.Text.Trim(), out listenPort);

            int happPort = AppConfig.DefaultHappPort;
            int.TryParse(txtHappPort.Text.Trim(), out happPort);

            return new ProxyServer.Config
            {
                ListenPort = listenPort,
                RedirectToHapp = chkHappEnabled.Checked,
                HappIp = txtHappIp.Text.Trim(),
                HappPort = happPort
            };
        }

        // ---------------------------------------------------------------
        // Построение UI
        // ---------------------------------------------------------------
        private void BuildUi()
        {
            Text = "Proxy Retranslator";
            ClientSize = new Size(730-20, 280-30);
            MinimumSize = new Size(730, 280);
            StartPosition = FormStartPosition.CenterScreen;

            int y = 12;

            // --- Автоматически включать системный прокси Windows ---
            chkAutoSystemProxy = new CheckBox
            {
                Text = "Автоматически включать прокси Windows (для подключения)",
                Location = new Point(12, y),
                Size = new Size(620, 22),
                Checked = true
            };
            Controls.Add(chkAutoSystemProxy);
            y += 34;

            // --- Порт локального прокси ---
            var lblListen = new Label
            {
                Text = "Порт локального прокси:",
                Location = new Point(12, y + 3),
                Size = new Size(160, 20)
            };
            Controls.Add(lblListen);

            txtListenPort = new TextBox
            {
                Text = AppConfig.DefaultListenPort.ToString(),
                Location = new Point(180, y),
                Size = new Size(80, 22)
            };
            Controls.Add(txtListenPort);
            y += 32;

            // --- IP телефона + кнопка автоопределения + чекбокс автообновления ---
            var lblIp = new Label
            {
                Text = "IP телефона:",
                Location = new Point(12, y + 3),
                Size = new Size(160, 20)
            };
            Controls.Add(lblIp);

            txtHappIp = new TextBox
            {
                Text = AppConfig.DefaultHappIp,
                Location = new Point(180, y),
                Size = new Size(160, 22)
            };
            Controls.Add(txtHappIp);

            btnAutoDetect = new Button
            {
                Text = "Определить автоматически",
                Location = new Point(350, y - 1),
                Size = new Size(180, 24)
            };
            Controls.Add(btnAutoDetect);

            chkAutoDetectOnStart = new CheckBox
            {
                Text = "Автообновление при запуске",
                Location = new Point(540, y + 2),
                Size = new Size(220, 22),
                Checked = true
            };
            Controls.Add(chkAutoDetectOnStart);
            y += 32;

            // --- Чекбокс перенаправления ПЕРЕД полем порта HAPP ---
            chkHappEnabled = new CheckBox
            {
                Text = "Перенаправлять трафик на HAPP (телефон)",
                Location = new Point(12, y),
                Size = new Size(400, 22),
                Checked = false
            };
            Controls.Add(chkHappEnabled);
            y += 30;

            // --- HAPP порт ---
            var lblPort = new Label
            {
                Text = "HAPP порт:",
                Location = new Point(12, y + 3),
                Size = new Size(160, 20)
            };
            Controls.Add(lblPort);

            txtHappPort = new TextBox
            {
                Text = AppConfig.DefaultHappPort.ToString(),
                Location = new Point(180, y),
                Size = new Size(80, 22)
            };
            Controls.Add(txtHappPort);
            y += 40;

            // --- Кнопки ---
            btnToggle = new Button
            {
                Text = "Запустить прокси",
                Location = new Point(12, y),
                Size = new Size(180, 30),
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat
            };
            Controls.Add(btnToggle);

            btnClearLog = new Button
            {
                Text = "Очистить лог",
                Location = new Point(202, y),
                Size = new Size(120, 30)
            };
            Controls.Add(btnClearLog);
            y += 42;

            // --- Лог ---
            rtbLog = new RichTextBox
            {
                Location = new Point(12, y),
                Size = new Size(ClientSize.Width - 24, ClientSize.Height - y - 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            Controls.Add(rtbLog);

            // --- Статус ---
            lblStatus = new Label
            {
                Text = "Статус: остановлен",
                Location = new Point(12, ClientSize.Height - 26),
                Size = new Size(ClientSize.Width - 24, 20),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                ForeColor = Color.DarkBlue
            };
            Controls.Add(lblStatus);
        }

        private void SetSettingsEnabled(bool enabled)
        {
            chkHappEnabled.Enabled = enabled;
            chkAutoSystemProxy.Enabled = enabled;
            chkAutoDetectOnStart.Enabled = enabled;
            txtListenPort.Enabled = enabled;
            txtHappIp.Enabled = enabled;
            btnAutoDetect.Enabled = enabled;

            // Порт HAPP зависит от чекбокса — управляем отдельно
            UpdateHappPortEnabled();

            // Если прокси запущен — принудительно блокируем порт HAPP
            if (!enabled)
                txtHappPort.Enabled = false;
        }

        // Блокировка/разблокировка поля HAPP порт в зависимости от галочки
        private void UpdateHappPortEnabled()
        {
            txtHappPort.Enabled = chkHappEnabled.Checked;
        }

        // ---------------------------------------------------------------
        // Обработчики кнопок
        // ---------------------------------------------------------------
        private void btnToggle_Click(object sender, EventArgs e)
        {
            if (_proxy.IsRunning) StopProxy();
            else StartProxy();
        }

        private void StartProxy()
        {
            if (_proxy.IsRunning) return;

            if (chkAutoDetectOnStart.Checked)
            {
                _log.Log("Автопоиск IP телефона (шлюз USB-модема)...");
                string detected = _gateway.DetectUsbGateway();
                if (!string.IsNullOrEmpty(detected))
                {
                    txtHappIp.Text = detected;
                    _log.Log($"  → Установлен IP телефона: {detected}");
                }
                else
                {
                    _log.Log("  Автопоиск не удался, используем значение из поля.");
                }
            }
            else
            {
                _log.Log("Автообновление IP отключено — используем значение из поля.");
            }

            int listenPort;
            if (!int.TryParse(txtListenPort.Text.Trim(), out listenPort) ||
                listenPort <= 0 || listenPort > 65535)
            {
                MessageBox.Show("Некорректный порт прослушивания.");
                return;
            }

            try
            {
                _proxy.Start();

                lblStatus.Text = $"Статус: работает на порту {listenPort}";

                if (chkAutoSystemProxy.Checked)
                    _sysProxy.Enable(listenPort);

                SetSettingsEnabled(false);

                btnToggle.Text = "Остановить прокси";
                btnToggle.BackColor = Color.LightCoral;
            }
            catch (Exception ex)
            {
                _log.Log($"ОШИБКА запуска: {ex.Message}");
                MessageBox.Show($"Не удалось запустить прокси: {ex.Message}");

                if (chkAutoSystemProxy.Checked)
                    _sysProxy.Disable();
            }
        }

        private void StopProxy()
        {
            try
            {
                _proxy.Stop();
                lblStatus.Text = "Статус: остановлен";

                if (chkAutoSystemProxy.Checked)
                    _sysProxy.Disable();

                SetSettingsEnabled(true);

                btnToggle.Text = "Запустить прокси";
                btnToggle.BackColor = Color.LightGreen;
            }
            catch (Exception ex)
            {
                _log.Log($"ОШИБКА остановки: {ex.Message}");
            }
        }

        private void btnAutoDetect_Click(object sender, EventArgs e)
        {
            _log.Log("Автопоиск IP телефона (шлюз USB-модема)...");
            string ip = _gateway.DetectUsbGateway();
            if (string.IsNullOrEmpty(ip))
            {
                _log.Log("  Не удалось определить. Убедись, что USB-модем включён.");
                MessageBox.Show("Не удалось определить IP телефона автоматически.\nПроверь, что USB-модем включён и активен.", "Автопоиск");
                return;
            }
            txtHappIp.Text = ip;
            _log.Log($"  → Установлен IP телефона: {ip}");
        }

        private void btnClearLog_Click(object sender, EventArgs e) => rtbLog.Clear();

        private void chkHappEnabled_CheckedChanged(object sender, EventArgs e)
        {
            _log.Log($"HAPP перенаправление: {(chkHappEnabled.Checked ? "ВКЛ" : "ВЫКЛ")}");
            UpdateHappPortEnabled();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_proxy.IsRunning)
            {
                _proxy.Stop();

                if (chkAutoSystemProxy != null && chkAutoSystemProxy.Checked)
                    _sysProxy.Disable();
            }
        }
    }
}