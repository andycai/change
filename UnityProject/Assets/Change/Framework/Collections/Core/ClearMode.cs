namespace Change.Framework.Collections
{
    /// <summary>
    /// Controls how containers clear their internal storage.
    /// All containers are single-threaded and not thread-safe.
    /// </summary>
    public enum ClearMode
    {
        /// <summary>Resets count only; references remain in backing arrays until overwritten.</summary>
        Logical = 0,
        /// <summary>Zeroes backing arrays, releasing all references for immediate GC collection.</summary>
        ZeroMemory = 1
    }
}
