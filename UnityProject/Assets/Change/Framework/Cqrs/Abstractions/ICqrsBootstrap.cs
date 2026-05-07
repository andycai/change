namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Mutable bootstrap surface used to register handlers and build a runtime.
    /// </summary>
    public interface ICqrsBootstrap : ICqrsRegistry
    {
        ICqrsRuntime Build();
    }
}
