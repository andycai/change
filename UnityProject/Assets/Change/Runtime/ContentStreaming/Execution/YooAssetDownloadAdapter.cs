using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public sealed class YooAssetDownloadAdapter : IAssetDownloadAdapter
    {
        public UniTask<ContentStreamingErrorCode> DownloadAsync(
            ContentPackDefinition definition,
            int rateLimitKbps,
            Action<long> onProgress,
            CancellationToken cancellationToken)
        {
            _ = onProgress;
            return UniTask.FromResult(ContentStreamingErrorCode.None);
        }
    }
}
