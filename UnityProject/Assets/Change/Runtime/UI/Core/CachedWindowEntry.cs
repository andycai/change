using System;
using System.Threading;

namespace Change.Runtime.UI
{
    public sealed class CachedWindowEntry : IDisposable
    {
        private bool _disposed;

        public CachedWindowEntry(IWindowView view, CancellationTokenSource releaseCts)
        {
            View = view ?? throw new ArgumentNullException(nameof(view));
            ReleaseCts = releaseCts ?? throw new ArgumentNullException(nameof(releaseCts));
        }

        public IWindowView View { get; }
        public CancellationTokenSource ReleaseCts { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ReleaseCts.Cancel();
            ReleaseCts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
