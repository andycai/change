namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Runtime accessor for systems that resolve CQRS dispatch at execution time.
    /// Implementations should throw if runtime access happens before bootstrap/build completion.
    /// </summary>
    public interface ICqrsRuntimeProvider
    {
        ICqrsRuntime Runtime { get; }
    }
}
