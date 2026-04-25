namespace Change.Framework.Cqrs
{
    /// <summary>
    /// Marker interface for command messages. Commands represent write-side intent.
    /// Implement as <c>readonly struct</c> for zero-allocation dispatch.
    /// </summary>
    public interface ICommand
    {
    }
}
