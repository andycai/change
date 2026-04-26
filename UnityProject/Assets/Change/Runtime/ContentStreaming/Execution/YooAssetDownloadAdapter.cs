using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public sealed class YooAssetDownloadAdapter : IAssetDownloadAdapter
    {
        public UniTask<ContentStreamingErrorCode> DownloadAsync(
            ContentPackDefinition definition,
            int rateLimitKbps,
            CancellationToken cancellationToken)
        {
            return UniTask.FromResult(ContentStreamingErrorCode.None);
        }
    }
}
