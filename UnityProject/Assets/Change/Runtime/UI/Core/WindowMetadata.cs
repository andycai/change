using Change.Framework.UI;

namespace Change.Runtime.UI.Core
{
    public readonly struct WindowMetadata
    {
        public string PackageName { get; }
        public string ComponentName { get; }
        public string Group { get; }
        public WindowLayer Layer { get; }

        public WindowMetadata(string packageName, string componentName, string group, WindowLayer layer)
        {
            PackageName = packageName;
            ComponentName = componentName;
            Group = group;
            Layer = layer;
        }
    }
}
