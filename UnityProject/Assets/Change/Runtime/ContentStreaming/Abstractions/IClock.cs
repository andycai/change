namespace Change.Runtime.ContentStreaming
{
    public interface IClock
    {
        long UtcNowTicks { get; }
    }
}
