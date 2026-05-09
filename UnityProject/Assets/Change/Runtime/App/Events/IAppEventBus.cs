using System;

namespace Change.Runtime.App.Events
{
    public interface IAppEventBus
    {
        void Publish<T>(in T evt) where T : struct;
        IDisposable Subscribe<T>(Action<T> handler) where T : struct;
    }
}
