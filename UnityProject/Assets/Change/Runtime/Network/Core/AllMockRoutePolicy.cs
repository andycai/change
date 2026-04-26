using Change.Framework.Network;

namespace Change.Runtime.Network
{
    public sealed class AllMockRoutePolicy : IRoutePolicy
    {
        public RouteTarget Resolve(int cmdId)
        {
            return RouteTarget.Mock;
        }
    }
}
