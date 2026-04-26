namespace Change.Runtime.ContentStreaming
{
    public enum ContentStreamingErrorCode : byte
    {
        None = 0,
        NetworkTimeout = 1,
        NetworkUnavailable = 2,
        ServerTemporary = 3,
        ManifestInvalid = 4,
        SignatureInvalid = 5,
        DiskWriteFailed = 6,
        VerifyFailed = 7,
        UserCanceled = 8,
    }
}
