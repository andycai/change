using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public interface IWindowView
    {
        WindowId Id { get; }
        WindowState State { get; }

        void BringToFront();
        void SetVisible(bool visible);
        void Dispose();
    }
}
