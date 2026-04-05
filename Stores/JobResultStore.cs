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
        {
            if (_pending.TryRemove(jobId, out var tcs))
                return tcs.TrySetResult(result);
            return false;
        }

        public bool TryFail(Guid jobId, Exception ex)
        {
            if (_pending.TryRemove(jobId, out var tcs))
                return tcs.TrySetException(ex);
            return false;
        }

        // Called by PollJobResult — waits up to `timeout` for completion
        public async Task<EncounterIngestResult?> WaitAsync(
            Guid jobId, TimeSpan timeout, CancellationToken ct)
        {
            if (!_pending.TryGetValue(jobId, out var tcs))
                return null; // already completed and removed, or unknown

            try
            {
                return await tcs.Task.WaitAsync(timeout, ct);
            }
            catch (TimeoutException)
            {
                return null; // still processing
            }
        }
    }
}
