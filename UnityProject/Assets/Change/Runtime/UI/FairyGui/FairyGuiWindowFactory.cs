using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FairyGUI;

namespace Change.Runtime.UI
{
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
            return CreateAsyncInternal(request, cancellationToken);
        }

        private async UniTask<IWindowView> CreateAsyncInternal(WindowRequest request, CancellationToken cancellationToken)
        {
            var location = _resolver.ResolvePrefabLocation(request.Id);
            var lease = await _loader.LoadPrefabAsync(request.Id, location, cancellationToken);

            UIPanel panel = lease.Instance?.GetComponent<UIPanel>();
            IFairyGuiWindowRootSource rootSource = lease.Instance?.GetComponent<IFairyGuiWindowRootSource>();

            GComponent root = null;
            if (panel != null && panel.ui != null)
            {
                root = panel.ui;
            }
            else if (rootSource != null)
            {
                root = rootSource.GetWindowRoot();
            }

            if (root == null)
            {
                DisposeLeaseNoThrow(lease);
                throw new InvalidOperationException(
                    $"No FairyGUI root on window prefab at: {location}. Add a {nameof(UIPanel)} with valid package/component, or implement {nameof(IFairyGuiWindowRootSource)} on the prefab root.");
            }

            return new FairyGuiWindowView(request.Id, request.Options.Layer, root, lease);
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
