using FairyGUI;

namespace Change.Runtime.UI
{
    /// <summary>
    /// Optional hook when a window prefab has no <see cref="UIPanel"/> (or <see cref="UIPanel.ui"/> is null):
    /// supply the FairyGUI <see cref="GComponent"/> root so <see cref="FairyGuiWindowFactory"/> can still build <see cref="FairyGuiWindowView"/>.
    /// </summary>
    public interface IFairyGuiWindowRootSource
    {
        GComponent GetWindowRoot();
    }
}
