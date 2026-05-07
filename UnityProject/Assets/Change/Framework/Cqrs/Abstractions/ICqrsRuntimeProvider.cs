namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Runtime accessor for systems that resolve CQRS dispatch at execution time.
    /// </summary>
    public interface ICqrsRuntimeProvider
    {
        ICqrsRuntime Runtime { get; }
    }
}
