using System;

namespace Change.Framework.UI
{
    public readonly struct WindowOpenOptions
    {
        public WindowOpenOptions(
            WindowLayer layer,
            bool reuseIfLoaded = true,
            bool allowMultipleInstances = false,
            int instanceId = 0)
        {
            Layer = layer;
            ReuseIfLoaded = reuseIfLoaded;
            AllowMultipleInstances = allowMultipleInstances;
            if (instanceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instanceId), "InstanceId must be non-negative.");
            }
            InstanceId = instanceId;
        }

        public static WindowOpenOptions Default => new(WindowLayer.Normal);

        public WindowLayer Layer { get; }
        public bool ReuseIfLoaded { get; }
        public bool AllowMultipleInstances { get; }
        public int InstanceId { get; }
    }
}
