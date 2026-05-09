using System;
using System.Collections.Generic;
using Change.Framework.UI;

namespace Change.Runtime.UI
{
    public sealed class StaticWindowLocationResolver : IWindowLocationResolver
    {
        private readonly IReadOnlyDictionary<string, string> _map;

        public StaticWindowLocationResolver(IReadOnlyDictionary<string, string> map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public string ResolvePrefabLocation(WindowId id)
        {
            if (_map.TryGetValue(id.Value, out var path))
            {
                return path;
            }

            throw new InvalidOperationException($"No prefab mapping for window `{id.Value}`.");
        }
    }
}
