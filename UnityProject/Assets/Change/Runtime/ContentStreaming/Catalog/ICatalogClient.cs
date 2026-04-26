using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Change.Runtime.ContentStreaming
{
    public readonly struct CatalogSyncResult
    {
        public CatalogSyncResult(string version, IReadOnlyList<ContentPackDefinition> definitions)
        {
            Version = version;
            Definitions = definitions;
        }

        public string Version { get; }

        public IReadOnlyList<ContentPackDefinition> Definitions { get; }
    }

    public interface ICatalogClient
    {
        UniTask<CatalogSyncResult> FetchAsync(string currentVersion);
    }
}
