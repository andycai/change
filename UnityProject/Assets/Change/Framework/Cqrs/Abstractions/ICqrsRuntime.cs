namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Immutable CQRS dispatch surface returned by <see cref="ICqrsBootstrap.Build"/>.
    /// Implementations must not expose handler registration APIs.
    /// </summary>
    public interface ICqrsRuntime : ICqrsBus
    {
    }
}
