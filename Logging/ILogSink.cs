namespace ProxyRetranslator.Logging
{
    /// <summary>
    /// Абстракция приёмника логов. Позволяет легко заменить RichTextBox
    /// на файл, консоль или что-то ещё.
    /// </summary>
    public interface ILogSink
    {
        void Log(string message);
    }
}