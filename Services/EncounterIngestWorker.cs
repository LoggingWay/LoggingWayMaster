using LoggingWayGrpcService.Entities;
using LoggingWayGrpcService.Services;
using LoggingWayMaster.Stores;
using Microsoft.EntityFrameworkCore;

namespace LoggingWayMaster.Services
{
    public record EncounterIngestResult(
    long EncounterId,
    long Rank,
    long TotalRanked,
    float PScore
);
    public class EncounterIngestWorker(
    EncounterIngestQueue queue,
    JobResultStore jobResultStore,
    IDbContextFactory<LoggingwayDbContext> dbFactory,
    ILogger<EncounterIngestWorker> logger) : BackgroundService
    {
        private const int MaxConcurrency = 8;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var semaphore = new SemaphoreSlim(MaxConcurrency);
            await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await semaphore.WaitAsync(stoppingToken);
                _ = Task.Run(async () =>
                {
                    try { await ProcessAsync(job, stoppingToken); }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to process ingest job {JobId}", job.JobId);
                        jobResultStore.TryFail(job.JobId, ex);
                    }
                    finally { semaphore.Release(); }
                }, stoppingToken);
            }
        }

        private async Task ProcessAsync(EncounterIngestJob job, CancellationToken ct)
        {
            logger.LogInformation("Processing ingest job {JobId}", job.JobId);

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            // Parsing logic would go somewhere here
            var encounter = new Encounter
            {
                CfcId = (int?)job.CfcId,//DBs don't like unsigned
                UploadedBy = job.UploadedBy,
                UploadedAt = job.QueuedAt,
                Payload = job.Payload,
            };
            
            var enc = db.Encounters.Add(encounter);
            await db.SaveChangesAsync(ct);

            //Temp attributions, in real logic, this will be dervied from the parse itself
            var character = db.CharacterClaims.FirstOrDefault(c => c.ClaimBy == job.UploadedBy);
            var stats = new EncounterPlayerStat
            {
                Character = character.Id,
                PlayerName = character.CharName,
                EncounterId = encounter.Id,
                
                PlayerId = 0,//Gameobject id relative to parsed data
                JobId = 0,
                TotalPScore = 0,
                TotalDamage = 0,
                TotalCrits = 0,
                TotalDirectHits = 0,
                TotalHealing = 0,
                TotalHits = 0,
                DirectHitRate = 0,
                Dps = 0,
                Hps = 0,
                DurationSeconds = 0,
                UploadedBy = job.UploadedBy,
                CritRate = 0f,
            };
            db.EncounterPlayerStats.Add(stats);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Ingest job {JobId} persisted as encounter {EncounterId}",
                job.JobId, encounter.Id);

            jobResultStore.TryComplete(job.JobId, new EncounterIngestResult(
            EncounterId: encounter.Id,
            Rank: 0, TotalRanked: 0, PScore: 0f));
        }
    }
}
