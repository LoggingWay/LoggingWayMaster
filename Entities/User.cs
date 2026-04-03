namespace LoggingWayMaster.Entities
{
    public class User
    {
        public Guid Id { get; set; } //Xivauth ID
        public bool Banned { get; set; } = false;

        public ICollection<CharacterClaim> Characters { get; set; } = [];
        public ICollection<Encounter> Encounters { get; set; } = [];
    }
}
