using System.Threading;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.UI
{
    public interface IWindowFactory
    {
        UniTask<IWindowView> CreateAsync(in WindowRequest request, CancellationToken cancellationToken);
    }
}
