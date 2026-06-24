using System.Threading;
using Change.Framework.UI;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.UI
{
    public interface IUiAssetLoader
    {
        UniTask<UiAssetLease> LoadPrefabAsync(WindowId windowId, string location, CancellationToken cancellationToken);
        UniTask LoadPackageAsync(string packageName, CancellationToken cancellationToken);
        void UnloadPackage(string packageName);
    }
}
