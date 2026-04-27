using System;
using Change.Framework.UI;
using FairyGUI;

namespace Change.Runtime.UI
{
    public sealed class FairyGuiWindowView : IWindowView
    {
        private readonly GComponent _root;
        private readonly UiAssetLease _lease;
        private bool _disposed;

        public FairyGuiWindowView(WindowId id, GComponent root, UiAssetLease lease)
        {
            Id = id;
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _lease = lease ?? throw new ArgumentNullException(nameof(lease));
            State = WindowState.Closed;
        }

        public WindowId Id { get; }
        public WindowState State { get; private set; }

        public void BringToFront()
        {
            _root.sortingOrder = int.MaxValue;
        }

        public void SetVisible(bool visible)
        {
            _root.visible = visible;
            State = visible ? WindowState.Open : WindowState.Hidden;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            State = WindowState.Closed;
            _lease.Dispose();
        }
    }
}
