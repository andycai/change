using System;
using System.Collections.Generic;

namespace Change.Runtime.App.Events
{
    public sealed class InProcessAppEventBus : IAppEventBus
    {
        private readonly object _gate = new();
        private readonly Dictionary<Type, List<Delegate>> _subs = new();

        public void Publish<T>(in T evt) where T : struct
        {
            List<Delegate> copy;
            lock (_gate)
            {
                if (!_subs.TryGetValue(typeof(T), out var list))
                {
                    return;
                }

                copy = new List<Delegate>(list);
            }

            var boxed = evt;
            for (var i = 0; i < copy.Count; i++)
            {
                ((Action<T>)copy[i]).Invoke(boxed);
            }
        }

        public IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var key = typeof(T);
            lock (_gate)
            {
                if (!_subs.TryGetValue(key, out var list))
                {
                    list = new List<Delegate>();
                    _subs[key] = list;
                }

                list.Add(handler);
            }

            return new Subscription(this, key, handler);
        }

        private void Remove(Type key, Delegate handler)
        {
            lock (_gate)
            {
                if (!_subs.TryGetValue(key, out var list))
                {
                    return;
                }

                list.Remove(handler);
                if (list.Count == 0)
                {
                    _subs.Remove(key);
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly InProcessAppEventBus _owner;
            private readonly Type _key;
            private readonly Delegate _handler;
            private bool _disposed;

            public Subscription(InProcessAppEventBus owner, Type key, Delegate handler)
            {
                _owner = owner;
                _key = key;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.Remove(_key, _handler);
            }
        }
    }
}
