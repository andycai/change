namespace Change.Runtime.ContentStreaming
{
    public enum DownloadTaskState : byte
    {
        Pending = 0,
        Queued = 1,
        Downloading = 2,
        Verifying = 3,
        Completed = 4,
        Paused = 5,
        FailedTransient = 6,
        FailedTerminal = 7,
        Canceled = 8,
    }
}
