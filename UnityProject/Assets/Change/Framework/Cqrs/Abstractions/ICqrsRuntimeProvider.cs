namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Context-scoped runtime provider.
    /// Context ids are explicit (for example, global/battle/lobby) and must be registered before lookup.
    /// </summary>
    public interface ICqrsRuntimeProvider
    {
        void Register(string contextId, ICqrsRuntime runtime);

        ICqrsRuntime Get(string contextId);
    }
}
