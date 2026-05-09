using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.Net
{
    public sealed class NoOpNetworkGateway : INetworkGateway
    {
        public UniTask SendAsync(in NetworkCommandEnvelope envelope, CancellationToken cancellationToken = default)
        {
            return UniTask.CompletedTask;
        }
    }
}
