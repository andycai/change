using System;
using Change.Framework.UI;
using FairyGUI;

namespace Change.Runtime.UI
{
    public class FairyGuiWindowView : IWindowView
    {
        private readonly GComponent _root;
        private readonly UiAssetLease _lease;
        private bool _disposed;

        public FairyGuiWindowView(WindowId id, WindowLayer layer, GComponent root, UiAssetLease lease)
        {
            Id = id;
            Layer = layer;
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _lease = lease ?? throw new ArgumentNullException(nameof(lease));
            State = WindowState.Closed;
        }

        public WindowId Id { get; }
        public WindowLayer Layer { get; }
        public GComponent Root => _root;
        public WindowState State { get; private set; }

        public void SetState(WindowState state)
        {
            State = state;
        }

        public virtual void BringToFront()
        {
            // P1 Fix: 层级感知的排序。基础值为层级 * 1000，确保高层级永远在低层级之上。
            // 这里的 1000 是预留给层内窗口排序的间距。
            _root.sortingOrder = (int)Layer * 1000 + 1; 
        }

        public virtual void SetVisible(bool visible)
        {
            _root.visible = visible;
            State = visible ? WindowState.Open : WindowState.Hidden;
        }

        public virtual void Dispose()
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
