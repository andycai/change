using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Change.Runtime
{
    public sealed class TimerAwaiter : INotifyCompletion
    {
        private readonly float _seconds;
        private readonly bool _scaled;
        private readonly CancellationToken _ct;
        private Action _continuation;
        private IDisposable _delayHandle;
        private CancellationTokenRegistration _cancelRegistration;
        private bool _hasCancelRegistration;
        private int _isCompleted;

        public TimerAwaiter(float seconds, bool scaled, CancellationToken ct)
        {
            _seconds = seconds;
            _scaled = scaled;
            _ct = ct;
        }

        public TimerAwaiter GetAwaiter() => this;

        public bool IsCompleted => _ct.IsCancellationRequested;

        public void GetResult() => _ct.ThrowIfCancellationRequested();

        public void OnCompleted(Action continuation)
        {
            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }

            if (_ct.IsCancellationRequested)
            {
                continuation();
                return;
            }

            _continuation = continuation;
            _delayHandle = Timer.Delay(_seconds, Complete, CancellationToken.None, _scaled);

            if (_ct.CanBeCanceled)
            {
                _cancelRegistration = _ct.Register(static state =>
                {
                    ((TimerAwaiter)state).Complete();
                }, this);
                _hasCancelRegistration = true;
            }

            if (_ct.IsCancellationRequested)
            {
                Complete();
            }
        }

        private void Complete()
        {
            if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
            {
                return;
            }

            _delayHandle?.Dispose();
            _delayHandle = null;
            if (_hasCancelRegistration)
            {
                _cancelRegistration.Dispose();
                _hasCancelRegistration = false;
            }

            InvokeContinuation();
        }

        private void InvokeContinuation()
        {
            var continuation = _continuation;
            _continuation = null;
            continuation?.Invoke();
        }
    }
}
