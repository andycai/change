using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public interface IWindowView
    {
        WindowId Id { get; }
        WindowLayer Layer { get; }
        WindowState State { get; }

        void SetState(WindowState state);
        void BringToFront();
        void SetVisible(bool visible);
        void Dispose();
    }
}
