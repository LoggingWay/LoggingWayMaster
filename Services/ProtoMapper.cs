using LoggingWayMaster.Entities;
using LoggingWayPlugin.Proto;
using System.Runtime.CompilerServices;
using DbEncounter = LoggingWayMaster.Entities.Encounter;
namespace LoggingWayMaster.Services
{
    //Helper class that maps Proto<>Entities
    //Most of it should be Entities>Proto conversion
    //but there could be some the other way around...
    public static class ProtoMapper
    {
        // --- CharacterClaim → Character ---

        public static Character ToProto(this CharacterClaim c) => new()
        {
            Name = c.CharName,
            Homeworld = c.HomeWorld,
            Datacenter = c.DataCenter,
            PersistentKey = c.XivAuthKey,
            Visbility = 0u, //should get rid of this field eventually
        };

        // --- Encounter → Encounter proto ---

        public static LoggingWayPlugin.Proto.Encounter ToProto(this DbEncounter e) => new()
        {
            EncounterId = e.Id,
            CfcId = (uint)(e.CfcId ?? 0),
            UploadedAt = e.UploadedAt.ToUnixTimeSeconds(),
        };
  

        // --- EncounterPlayerStat → EncounterPlayerBreakdown ---

        public static EncounterPlayerBreakdown ToProto(
            this EncounterPlayerStat s,
            long rank = 0,
            long totalRanked = 0,
            long globalRank = 0,
            long globalTotalRank = 0) => new()
            {
                Name = s.PlayerName,
                TotalDamage = s.TotalDamage,
                TotalHealing = s.TotalHealing,
                TotalHits = s.TotalHits,
                TotalCrits = s.TotalCrits,
                TotalDirectHits = s.TotalDirectHits,
                Duration = (float)s.DurationSeconds,
                Dps = (float)s.Dps,
                Hps = (float)s.Hps,
                CritRate = (float)s.CritRate,
                DhRate = (float)s.DirectHitRate,
                Pscore = (float)s.TotalPScore,
                JobId = (uint)s.JobId,
                Rank = rank,
                TotalRanked = totalRanked,
                GlobalRank = globalRank,
                GlobalTotalRank = globalTotalRank,
            };

        // --- (CharacterClaim, EncounterPlayerStat) → LeaderBoardEntry ---

        public static LeaderBoardEntry ToLeaderBoardEntry(
            this CharacterClaim c,
            EncounterPlayerStat s,
            long rank) => new()
            {
                Char = c.ToProto(),
                Rank = rank,
                Psccore = (float)s.TotalPScore,
                Jobid = (uint)s.JobId,
            };
    }
}
