using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public sealed class CatalogSyncService
    {
        private readonly ICatalogClient _client;
        private readonly List<ContentPackDefinition> _definitions = new();

        public CatalogSyncService(ICatalogClient client)
        {
            _client = client;
        }

        public string CatalogVersion { get; private set; } = string.Empty;

        public IReadOnlyList<ContentPackDefinition> Definitions => _definitions;

        public async UniTask<int> SyncAsync()
        {
            var result = await _client.FetchAsync(CatalogVersion);
            CatalogVersion = result.Version;

            _definitions.Clear();
            for (var i = 0; i < result.Definitions.Count; i++)
            {
                _definitions.Add(result.Definitions[i]);
            }

            return _definitions.Count;
        }
    }
}
