using System.Collections.Generic;

namespace Change.Runtime.ContentStreaming
{
    public sealed class DownloadTaskScheduler
    {
        private readonly Dictionary<string, DownloadTaskSnapshot> _tasks = new();
        private bool _paused;

        public void Enqueue(in DownloadTaskSnapshot task)
        {
            _tasks[task.PackId] = task.WithState(DownloadTaskState.Queued);
        }

        public void MarkDownloading(string packId)
        {
            if (_tasks.TryGetValue(packId, out var task))
            {
                _tasks[packId] = task.WithState(DownloadTaskState.Downloading);
            }
        }

        public bool Remove(string packId)
        {
            return _tasks.Remove(packId);
        }

        public bool TryGet(string packId, out DownloadTaskSnapshot snapshot)
        {
            return _tasks.TryGetValue(packId, out snapshot);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public void DequeueReadyTasks(int maxCount, List<DownloadTaskSnapshot> output)
        {
            output.Clear();
            if (_paused || maxCount <= 0)
            {
                return;
            }

            foreach (var pair in _tasks)
            {
                var task = pair.Value;
                if (task.State == DownloadTaskState.Queued)
                {
                    output.Add(task);
                }
            }

            output.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            if (output.Count > maxCount)
            {
                output.RemoveRange(maxCount, output.Count - maxCount);
            }
        }

        public void MarkTransientFailure(string packId)
        {
            if (!_tasks.TryGetValue(packId, out var task))
            {
                return;
            }

            var retryTask = new DownloadTaskSnapshot(
                task.PackId,
                DownloadTaskState.Queued,
                task.DownloadedBytes,
                task.TotalBytes,
                task.Priority,
                task.RetryCount + 1,
                rateKbps: 0,
                ContentStreamingErrorCode.None,
                task.Sequence);

            _tasks[packId] = retryTask;
        }
    }
}
