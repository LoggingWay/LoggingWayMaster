using LoggingWayMaster.Services;
using System.Collections.Concurrent;
public record EncounterIngestResult(
    long EncounterId,
    long Rank,
    long TotalRanked,
    float PScore
);

namespace LoggingWayMaster.Stores
{
    public class JobResultStore
    {
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<EncounterIngestResult>>
            _pending = new();

        public TaskCompletionSource<EncounterIngestResult> Register(Guid jobId)
        {
            var tcs = new TaskCompletionSource<EncounterIngestResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[jobId] = tcs;
            return tcs;
        }
        public bool TryComplete(Guid jobId, EncounterIngestResult result)
            => _pending.TryGetValue(jobId, out var tcs) && tcs.TrySetResult(result);

        public bool TryFail(Guid jobId, Exception ex)
            => _pending.TryGetValue(jobId, out var tcs) && tcs.TrySetException(ex);

        public async Task<EncounterIngestResult?> WaitAsync(Guid jobId, TimeSpan timeout, CancellationToken ct)
        {
            if (!_pending.TryGetValue(jobId, out var tcs))
                return null;                       // genuinely unknown job
            try { return await tcs.Task.WaitAsync(timeout, ct); }
            catch (TimeoutException) { return null; }   // still processing
        }

        public void Remove(Guid jobId) => _pending.TryRemove(jobId, out _);
    }
}
