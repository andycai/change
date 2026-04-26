using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public interface IAssetDownloadAdapter
    {
        UniTask<ContentStreamingErrorCode> DownloadAsync(
            ContentPackDefinition definition,
            int rateLimitKbps,
            CancellationToken cancellationToken);
    }
}
