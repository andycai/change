using System;

namespace Fun.Framework.Cqrs
{
    public sealed class NullCqrsLogger : ICqrsLogger
    {
        public static readonly NullCqrsLogger Instance = new NullCqrsLogger();

        private NullCqrsLogger()
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception exception)
        {
        }
    }
}
