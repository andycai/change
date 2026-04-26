namespace Change.Framework.Network
{
    public interface IRoutePolicy
    {
        RouteTarget Resolve(int cmdId);
    }
}
