namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Mutable CQRS bootstrap surface used to register handlers before runtime creation.
    /// After <see cref="Build"/> is called, registrations are no longer valid.
    /// </summary>
    public interface ICqrsBootstrap : ICqrsRegistry
    {
        /// <summary>
        /// Freezes registration and returns the runtime dispatch surface.
        /// Repeated calls must follow a deterministic implementation-defined behavior.
        /// </summary>
        ICqrsRuntime Build();
    }
}
