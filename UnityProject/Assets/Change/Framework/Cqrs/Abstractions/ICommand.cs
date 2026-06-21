namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Command message that executes itself. Implement as <c>readonly struct</c>
    /// for zero-allocation dispatch. Provides <see cref="Execute"/> as the
    /// single execution entry point, invoked by <c>CqrsBus.Send&lt;T&gt;</c>.
    /// </summary>
    public interface ICommand
    {
        void Execute();
    }
}
