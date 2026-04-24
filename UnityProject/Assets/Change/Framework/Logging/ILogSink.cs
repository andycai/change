namespace Change.Framework.Logging
{
    public interface ILogSink
    {
        void Write(LogLevel level, string message);
    }
}
