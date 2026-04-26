using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public interface IContentDownloadService
    {
        UniTask SyncCatalogAsync(CancellationToken cancellationToken);

        bool EnqueuePack(string packId);

        void PauseAll(ContentStreamingPauseReason reason);

        void ResumeByPolicy();

        bool RemovePack(string packId, bool removeCache);

        bool TryGetPackState(string packId, out DownloadTaskSnapshot snapshot);

        IReadOnlyList<DownloadTaskSnapshot> GetAllTaskSnapshots();
    }
}
