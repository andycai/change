using System;
using System.Threading;
using Change.Runtime.UI.Abstractions;
using Change.Runtime.UI.Core;
using Cysharp.Threading.Tasks;
using FairyGUI;

namespace Change.Runtime.UI
{
    public sealed class FairyGuiWindowFactory : IWindowFactory
    {
        private readonly IUiAssetLoader _loader;
        private readonly IWindowLocationResolver _resolver;
        private readonly IWindowRegistry _registry;
        private readonly WindowLayerSortingOrderManager _sortingOrderManager;

        public FairyGuiWindowFactory(
            IUiAssetLoader loader,
            IWindowLocationResolver resolver,
            IWindowRegistry registry = null,
            WindowLayerSortingOrderManager sortingOrderManager = null)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _registry = registry;
            _sortingOrderManager = sortingOrderManager;
        }

        public UniTask<IWindowView> CreateAsync(in WindowRequest request, CancellationToken cancellationToken)
        {
            return CreateAsyncInternal(request, cancellationToken);
        }

        private async UniTask<IWindowView> CreateAsyncInternal(WindowRequest request, CancellationToken cancellationToken)
        {
            // Task 15: look up registry metadata so Task 24 can use PackageName / ComponentName for UIPackage loading.
            WindowMetadata metadata = default;
            var hasMetadata = _registry != null && _registry.TryGetMetadata(request.Id, out metadata);

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

            return new FairyGuiWindowView(request.Id, request.Options.Layer, root, lease, _sortingOrderManager);
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
