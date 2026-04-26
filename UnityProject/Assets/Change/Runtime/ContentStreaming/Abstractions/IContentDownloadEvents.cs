using System;

namespace Change.Runtime.ContentStreaming
{
    public interface IContentDownloadEvents
    {
        event Action<DownloadTaskSnapshot> TaskAdded;

        event Action<DownloadTaskSnapshot> TaskStateChanged;

        event Action<DownloadTaskSnapshot> TaskProgressChanged;

        event Action<string> TaskRemoved;

        event Action<DownloadPolicySnapshot> GlobalPolicyChanged;

        event Action<int> CatalogUpdated;
    }
}
