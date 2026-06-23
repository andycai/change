using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public interface IWindowRegistry
    {
        void Register(WindowId id, string packageName, string componentName, string group, WindowLayer layer);
        bool TryGetMetadata(WindowId id, out WindowMetadata metadata);
    }
}
