namespace LoggingWayMaster.Entities
{
    public class EncounterPlayerStat
    {
        public long Id { get; set; }
        public long EncounterId { get; set; }
        public Guid? UploadedBy { get; set; }
        public Guid? Character { get; set; }
        public long PlayerId { get; set; }
        public string PlayerName { get; set; } = null!;
        public int JobId { get; set; }
        public long TotalDamage { get; set; }
        public double TotalPScore { get; set; }
        public long TotalHealing { get; set; }
        public long TotalHits { get; set; }
        public long TotalCrits { get; set; }
        public long TotalDirectHits { get; set; }
        public long FirstTimestamp { get; set; }
        public long LastTimestamp { get; set; }
        public double DurationSeconds { get; set; }
        public double Dps { get; set; }
        public double Hps { get; set; }
        public double CritRate { get; set; }
        public double DirectHitRate { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public Encounter Encounter { get; set; } = null!;
        public User? Uploader { get; set; }
        public CharacterClaim? CharacterClaim { get; set; }
    }
}
