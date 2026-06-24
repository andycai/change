using System;
using System.Collections.Generic;
using Change.Framework.UI;
using Change.Runtime.UI.Abstractions;

namespace Change.Runtime.UI.Core
{
    public sealed class WindowRegistry : IWindowRegistry
    {
        private readonly Dictionary<WindowId, WindowMetadata> _metadata = new();

        public void Register(WindowId id, string packageName, string componentName, string group, WindowLayer layer)
        {
            if (_metadata.ContainsKey(id))
            {
                throw new InvalidOperationException($"Window '{id}' is already registered.");
            }

            _metadata[id] = new WindowMetadata(packageName, componentName, group, layer);
        }

        public bool TryGetMetadata(WindowId id, out WindowMetadata metadata)
        {
            return _metadata.TryGetValue(id, out metadata);
        }
    }
}
