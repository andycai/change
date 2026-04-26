using System.Collections.Generic;

namespace Change.Runtime.ContentStreaming
{
    public sealed class ContentDownloadStateStore
    {
        private readonly Dictionary<string, DownloadTaskSnapshot> _tasks = new();

        public void Upsert(in DownloadTaskSnapshot snapshot)
        {
            _tasks[snapshot.PackId] = snapshot;
        }

        public bool Remove(string packId)
        {
            return _tasks.Remove(packId);
        }

        public bool TryGet(string packId, out DownloadTaskSnapshot snapshot)
        {
            return _tasks.TryGetValue(packId, out snapshot);
        }

        public void GetAll(List<DownloadTaskSnapshot> output)
        {
            output.Clear();
            foreach (var pair in _tasks)
            {
                output.Add(pair.Value);
            }
        }
    }
}
