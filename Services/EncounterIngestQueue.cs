using System.Threading.Channels;

namespace LoggingWayMaster.Services
{
    public record EncounterIngestJob(
    Guid JobId,
    Guid UploadedBy,
    uint CfcId,
    byte[] Payload,
    DateTimeOffset QueuedAt
);
    public class EncounterIngestQueue
    {
        private readonly Channel<EncounterIngestJob> _channel =
            Channel.CreateBounded<EncounterIngestJob>(new BoundedChannelOptions(512)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,  //only the background worker reads
                SingleWriter = false, //multiple gRPC calls can enqueue
            });

        public ChannelReader<EncounterIngestJob> Reader => _channel.Reader;

        public async ValueTask<bool> TryEnqueueAsync(
            EncounterIngestJob job,
            CancellationToken ct = default)
        {
            try
            {
                await _channel.Writer.WriteAsync(job, ct);
                return true;
            }
            catch (ChannelClosedException)
            {
                return false;
            }
        }

        public void Complete() => _channel.Writer.Complete();
    }
}
