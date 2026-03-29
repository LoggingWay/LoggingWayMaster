namespace LoggingWayGrpcService.Entities
{
    public class CharacterClaim
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string XivAuthKey { get; set; } = null!;
        public Guid? ClaimBy { get; set; }
        public string CharName { get; set; } = null!;
        public string DataCenter { get; set; } = null!;
        public string HomeWorld { get; set; } = null!;
        public int LodestoneId { get; set; }
        public string? AvatarUrl { get; set; }
        public string? PortraitUrl { get; set; }
        public DateTimeOffset ClaimRegistered { get; set; } = DateTimeOffset.UtcNow;

        public User? Owner { get; set; }
        public ICollection<EncounterPlayerStat> PlayerStats { get; set; } = [];
    }

}
