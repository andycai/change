using System;
using System.Threading;
using Change.Framework.UI;
using Cysharp.Threading.Tasks;
using FairyGUI;

namespace Change.Runtime.UI
{
    public interface IWindowLocationResolver
    {
        string ResolvePrefabLocation(WindowId id);
    }

    public sealed class FairyGuiWindowFactory : IWindowFactory
    {
        private readonly IUiAssetLoader _loader;
        private readonly IWindowLocationResolver _resolver;

        public FairyGuiWindowFactory(IUiAssetLoader loader, IWindowLocationResolver resolver)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public UniTask<IWindowView> CreateAsync(in WindowRequest request, CancellationToken cancellationToken)
        {
            var requestCopy = request;
            return CreateAsyncInternal(requestCopy, cancellationToken);
        }

        private async UniTask<IWindowView> CreateAsyncInternal(WindowRequest request, CancellationToken cancellationToken)
        {
            var location = _resolver.ResolvePrefabLocation(request.Id);
            var lease = await _loader.LoadPrefabAsync(request.Id, location, cancellationToken);

            UIPanel panel;
            try
            {
                panel = lease.Instance != null ? lease.Instance.GetComponent<UIPanel>() : null;
            }
            catch
            {
                DisposeLeaseNoThrow(lease);
                throw;
            }

            GComponent root;
            try
            {
                root = panel != null ? panel.ui : null;
            }
            catch (OperationCanceledException)
            {
                DisposeLeaseNoThrow(lease);
                throw;
            }
            catch (Exception exception)
            {
                DisposeLeaseNoThrow(lease);
                throw new InvalidOperationException($"Failed to resolve FairyGUI root component. id={request.Id}, location={location}", exception);
            }

            if (root == null)
            {
                DisposeLeaseNoThrow(lease);
                throw new InvalidOperationException($"UIPanel/GComponent missing on window prefab: {location}");
            }

            return new FairyGuiWindowView(request.Id, root, lease);
        }

        private static void DisposeLeaseNoThrow(UiAssetLease lease)
        {
            try
            {
                lease.Dispose();
            }
            catch
            {
            }
        }
    }
}
