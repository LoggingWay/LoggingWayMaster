namespace LoggingWayGrpcService.Entities
{
    public class Encounter
    {
        public long Id { get; set; }
        public int? CfcId { get; set; }
        public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
        public Guid? UploadedBy { get; set; }
        public byte[] Payload { get; set; } = null!;

        public User? Uploader { get; set; }
        public ICollection<EncounterPlayerStat> PlayerStats { get; set; } = [];
    }
}
