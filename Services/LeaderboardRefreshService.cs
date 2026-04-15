using LoggingWayMaster.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoggingWayMaster.Services
{
    // Services/LeaderboardRefreshService.cs
    namespace LoggingWayMaster.Services
    {
        public class LeaderboardRefreshService(
    IDbContextFactory<LoggingwayDbContext> dbFactory,
    ILogger<LeaderboardRefreshService> logger) : BackgroundService
        {
            private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

            protected override async Task ExecuteAsync(CancellationToken ct)
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await RefreshLeaderboardAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Leaderboard refresh failed");
                    }

                    await Task.Delay(_interval, ct);
                }
            }

            private async Task RefreshLeaderboardAsync(CancellationToken ct)
            {
                await using var db = await dbFactory.CreateDbContextAsync(ct);

                logger.LogInformation("Starting leaderboard refresh");

                var aggregated = await db.EncounterPlayerStats
                    .Include(s => s.Encounter)
                    .Where(s => s.Encounter != null && s.Encounter.CfcId != null)
                    .GroupBy(s => new { s.PlayerId, s.JobId, s.Encounter!.CfcId })
                    .Select(g => new
                    {
                        g.Key.PlayerId,
                        g.Key.JobId,
                        g.Key.CfcId,
                        PlayerName = g.OrderByDescending(x => x.CreatedAt).First().PlayerName,
                        Character = g.OrderByDescending(x => x.CreatedAt).First().Character,

                        BestDps = g.Max(x => x.Dps),
                        BestDpsEncounterId = g.OrderByDescending(x => x.Dps).First().EncounterId,

                        BestHps = g.Max(x => x.Hps),
                        BestHpsEncounterId = g.OrderByDescending(x => x.Hps).First().EncounterId,

                        BestPScore = g.Max(x => x.TotalPScore),
                        BestPScoreEncounterId = g.OrderByDescending(x => x.TotalPScore).First().EncounterId,

                        TotalKills = g.Count(),
                        AllDps = g.Select(x => x.Dps).ToList()
                    })
                    .ToListAsync(ct);

                foreach (var row in aggregated)
                {
                    var existing = await db.LeaderboardEntries
                        .FirstOrDefaultAsync(e =>
                            e.PlayerId == row.PlayerId &&
                            e.JobId == row.JobId &&
                            e.CfcId == row.CfcId, ct);

                    var median = CalculateMedian(row.AllDps);

                    if (existing is null)
                    {
                        db.LeaderboardEntries.Add(new LeaderboardEntry
                        {
                            CfcId = row.CfcId ?? 0,
                            JobId = row.JobId,
                            PlayerId = row.PlayerId,
                            PlayerName = row.PlayerName,
                            Character = row.Character,
                            BestDps = row.BestDps,
                            BestDpsEncounterId = row.BestDpsEncounterId,
                            BestHps = row.BestHps,
                            BestHpsEncounterId = row.BestHpsEncounterId,
                            BestPScore = row.BestPScore,
                            BestPScoreEncounterId = row.BestPScoreEncounterId,
                            TotalKills = row.TotalKills,
                            MedianDps = median,
                            LastUpdated = DateTimeOffset.UtcNow
                        });
                    }
                    else
                    {
                        existing.PlayerName = row.PlayerName;
                        existing.Character = row.Character;
                        existing.BestDps = row.BestDps;
                        existing.BestDpsEncounterId = row.BestDpsEncounterId;
                        existing.BestHps = row.BestHps;
                        existing.BestHpsEncounterId = row.BestHpsEncounterId;
                        existing.BestPScore = row.BestPScore;
                        existing.BestPScoreEncounterId = row.BestPScoreEncounterId;
                        existing.TotalKills = row.TotalKills;
                        existing.MedianDps = median;
                        existing.LastUpdated = DateTimeOffset.UtcNow;
                    }
                }

                await db.SaveChangesAsync(ct);

                await ComputeRanksAsync(db, ct);

                logger.LogInformation("Leaderboard refresh complete: {Count} entries", aggregated.Count);
            }

            private async Task ComputeRanksAsync(LoggingwayDbContext db, CancellationToken ct)
            {
                await db.Database.ExecuteSqlRawAsync("""
            WITH ranked AS (
                SELECT "Id",
                       DENSE_RANK() OVER (PARTITION BY "CfcId", "JobId" ORDER BY "BestDps"    DESC) AS dps_rank,
                       DENSE_RANK() OVER (PARTITION BY "CfcId", "JobId" ORDER BY "BestHps"    DESC) AS hps_rank,
                       DENSE_RANK() OVER (PARTITION BY "CfcId", "JobId" ORDER BY "BestPScore" DESC) AS pscore_rank
                FROM "LeaderboardEntries"
            )
            UPDATE "LeaderboardEntries" AS le
            SET "DpsRank"    = r.dps_rank,
                "HpsRank"    = r.hps_rank,
                "PScoreRank" = r.pscore_rank
            FROM ranked r
            WHERE le."Id" = r."Id"
            """, ct);
            }

            private static double CalculateMedian(List<double> values)
            {
                if (values.Count == 0) return 0;
                var sorted = values.OrderBy(v => v).ToList();
                int mid = sorted.Count / 2;
                return sorted.Count % 2 == 0
                    ? (sorted[mid - 1] + sorted[mid]) / 2.0
                    : sorted[mid];
            }
        }
    }
}
