using System;

namespace Change.Framework.Network
{
    public sealed class UnsupportedRouteException : InvalidOperationException
    {
        public UnsupportedRouteException(RouteTarget target)
            : base($"Unsupported route target: {target}.")
        {
        }
    }
}
