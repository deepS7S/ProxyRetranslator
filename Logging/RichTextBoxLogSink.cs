using System;
using System.Drawing;
using System.Windows.Forms;
using ProxyRetranslator.Models;

namespace ProxyRetranslator.Logging
{
    /// <summary>
    /// Лог в RichTextBox с защитой от переполнения и потокобезопасностью.
    /// </summary>
    public sealed class RichTextBoxLogSink : ILogSink
    {
        private readonly RichTextBox _box;

        public RichTextBoxLogSink(RichTextBox box)
        {
            _box = box ?? throw new ArgumentNullException(nameof(box));
        }

        public void Log(string message)
        {
            if (_box.IsDisposed) return;

            if (_box.InvokeRequired)
            {
                try { _box.BeginInvoke(new Action<string>(Log), message); } catch { }
                return;
            }

            if (_box.TextLength > AppConfig.MaxLogChars)
            {
                _box.Select(0, _box.TextLength - AppConfig.MaxLogChars / 2);
                _box.SelectedText = string.Empty;
            }

            _box.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
    }
}