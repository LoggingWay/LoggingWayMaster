namespace LoggingWayMaster.Entities
{
    public class LeaderboardEntry
    {
        public long Id { get; set; }
        public int CfcId { get; set; }          // nullable = overall leaderboard
        public int JobId { get; set; }
        public long PlayerId { get; set; }
        public string PlayerName { get; set; } = null!;
        public Guid? Character { get; set; }

        // Best-of metrics
        public double BestDps { get; set; }
        public long BestDpsEncounterId { get; set; }
        public double BestHps { get; set; }
        public long BestHpsEncounterId { get; set; }
        public double BestPScore { get; set; }
        public long BestPScoreEncounterId { get; set; }

        // Aggregate stats
        public int TotalKills { get; set; }
        public double MedianDps { get; set; }

        // Ranking (computed during refresh)
        public int DpsRank { get; set; }
        public int HpsRank { get; set; }
        public int PScoreRank { get; set; }

        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

        public Encounter? BestDpsEncounter { get; set; }
        public Encounter? BestHpsEncounter { get; set; }
        public Encounter? BestPScoreEncounter { get; set; }
        public CharacterClaim? CharacterClaim { get; set; }
    }
}
