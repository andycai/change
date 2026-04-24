using System;

namespace Fun.Framework.Cqrs
{
    public interface ICqrsLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception exception);
    }
}
