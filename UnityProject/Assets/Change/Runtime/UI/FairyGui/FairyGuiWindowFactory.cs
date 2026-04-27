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

            GComponent root;
            if (panel == null || panel.ui == null)
            {
                DisposeLeaseNoThrow(lease);
                throw new InvalidOperationException($"UIPanel or GComponent (panel.ui) missing on window prefab at: {location}. Ensure the prefab has a UIPanel component and it is correctly initialized.");
            }

            root = panel.ui;

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
