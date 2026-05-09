using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.Net
{
    public interface INetworkGateway
    {
        UniTask SendAsync(in NetworkCommandEnvelope envelope, CancellationToken cancellationToken = default);
    }
}
