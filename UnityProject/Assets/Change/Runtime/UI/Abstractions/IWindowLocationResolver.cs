using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public interface IWindowLocationResolver
    {
        string ResolvePrefabLocation(WindowId id);
    }
}
