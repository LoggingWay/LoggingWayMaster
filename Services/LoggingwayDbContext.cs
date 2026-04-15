using LoggingWayMaster.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Reflection.Emit;

namespace LoggingWayMaster.Services
{
    public class LoggingwayDbContext(DbContextOptions<LoggingwayDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<CharacterClaim> CharacterClaims => Set<CharacterClaim>();
        public DbSet<Encounter> Encounters => Set<Encounter>();
        public DbSet<EncounterPlayerStat> EncounterPlayerStats => Set<EncounterPlayerStat>();

        public DbSet<LeaderboardEntry> LeaderboardEntries => Set<LeaderboardEntry>();

        //Map Model to database columns
        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<User>(e =>
            {
                e.ToTable("users");
                e.Property(u => u.Id).HasColumnName("id");
                e.Property(u => u.Banned).HasColumnName("banned").HasDefaultValue(false);
            });

            b.Entity<CharacterClaim>(e =>
            {
                e.ToTable("characters_claim");
                e.Property(c => c.Id).HasColumnName("id");
                e.Property(c => c.XivAuthKey).HasColumnName("xivauthkey");
                e.Property(c => c.ClaimBy).HasColumnName("claim_by");
                e.Property(c => c.CharName).HasColumnName("charname");
                e.Property(c => c.DataCenter).HasColumnName("datacenter");
                e.Property(c => c.HomeWorld).HasColumnName("homeworld");
                e.Property(c => c.LodestoneId).HasColumnName("lodestone_id");
                e.Property(c => c.AvatarUrl).HasColumnName("avatar_url");
                e.Property(c => c.PortraitUrl).HasColumnName("portrait_url");
                e.Property(c => c.ClaimRegistered).HasColumnName("claim_registered");

                e.HasIndex(c => new { c.XivAuthKey, c.CharName, c.DataCenter, c.HomeWorld }).IsUnique();

                e.HasOne(c => c.Owner)
                    .WithMany(u => u.Characters)
                    .HasForeignKey(c => c.ClaimBy);
            });

            b.Entity<Encounter>(e =>
            {
                e.ToTable("encounters");
                e.Property(en => en.Id).HasColumnName("id");
                e.Property(en => en.CfcId).HasColumnName("cfc_id");
                e.Property(en => en.UploadedAt).HasColumnName("uploaded_at");
                e.Property(en => en.UploadedBy).HasColumnName("uploaded_by");
                e.Property(en => en.Payload).HasColumnName("payload");

                e.HasOne(en => en.Uploader)
                    .WithMany(u => u.Encounters)
                    .HasForeignKey(en => en.UploadedBy);
            });

            b.Entity<EncounterPlayerStat>(e =>
            {
                e.ToTable("encounter_player_stats");
                e.Property(s => s.Id).HasColumnName("id");
                e.Property(s => s.EncounterId).HasColumnName("encounter_id");
                e.Property(s => s.UploadedBy).HasColumnName("uploaded_by");
                e.Property(s => s.Character).HasColumnName("character");
                e.Property(s => s.PlayerId).HasColumnName("player_id");
                e.Property(s => s.PlayerName).HasColumnName("player_name");
                e.Property(s => s.JobId).HasColumnName("job_id");
                e.Property(s => s.TotalDamage).HasColumnName("total_damage");
                e.Property(s => s.TotalPScore).HasColumnName("total_pscore");
                e.Property(s => s.TotalHealing).HasColumnName("total_healing");
                e.Property(s => s.TotalHits).HasColumnName("total_hits");
                e.Property(s => s.TotalCrits).HasColumnName("total_crits");
                e.Property(s => s.TotalDirectHits).HasColumnName("total_direct_hits");
                e.Property(s => s.FirstTimestamp).HasColumnName("first_timestamp");
                e.Property(s => s.LastTimestamp).HasColumnName("last_timestamp");
                e.Property(s => s.DurationSeconds).HasColumnName("duration_seconds");
                e.Property(s => s.Dps).HasColumnName("dps");
                e.Property(s => s.Hps).HasColumnName("hps");
                e.Property(s => s.CritRate).HasColumnName("crit_rate");
                e.Property(s => s.DirectHitRate).HasColumnName("direct_hit_rate");
                e.Property(s => s.CreatedAt).HasColumnName("created_at");

                e.HasIndex(s => new { s.EncounterId, s.PlayerId }).IsUnique();

                e.HasOne(s => s.Encounter)
                    .WithMany(en => en.PlayerStats)
                    .HasForeignKey(s => s.EncounterId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(s => s.Uploader)
                    .WithMany()
                    .HasForeignKey(s => s.UploadedBy);

                e.HasOne(s => s.CharacterClaim)
                    .WithMany(c => c.PlayerStats)
                    .HasForeignKey(s => s.Character);
            });
            b.Entity<LeaderboardEntry>(e =>
            {
                e.HasIndex(x => new { x.CfcId, x.JobId, x.DpsRank })//Index on Cfc+Job+Dps for rank
                 .HasDatabaseName("Leaderboard_CfcJob_DpsRank");

                e.HasIndex(x => new { x.PlayerId, x.JobId, x.CfcId })//Index+unique on Player+job+cfc for lookup+upsert
                 .IsUnique()
                 .HasDatabaseName("Leaderboard_Player_Job_Cfc");

                e.HasOne(x => x.BestDpsEncounter)
                 .WithMany()
                 .HasForeignKey(x => x.BestDpsEncounterId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.BestHpsEncounter)
                 .WithMany()
                 .HasForeignKey(x => x.BestHpsEncounterId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.BestPScoreEncounter)
                 .WithMany()
                 .HasForeignKey(x => x.BestPScoreEncounterId)
                 .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }

    public class LoggingwayDbContextFactory : IDesignTimeDbContextFactory<LoggingwayDbContext>//This is needed for dev tool migrations
    {
        public LoggingwayDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<LoggingwayDbContext>()
                .UseSqlite("Data Source=loggingway.db")
                .Options;

            return new LoggingwayDbContext(options);
        }
    }
}
