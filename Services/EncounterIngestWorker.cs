using LoggingWayMaster.Entities;
using LoggingWayPlugin.Proto;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LoggingWayMaster.Services
{
    public class EncounterIngestWorker(
    Lumina.GameData lumina,
    EncounterIngestQueue queue,
    IDbContextFactory<LoggingwayDbContext> dbFactory,
    ILogger<EncounterIngestWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessAsync(job, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process ingest job {JobId}", job.JobId);
                    // job is dropped for now,in the future we might want some kind salvage/retry system,this used to be handled by redis but w/e
                }
            }
        }

        private class Buff
        {
            public double Physical = 1.0;
            public double Magical = 1.0;
            public Buff(int Total) { this.Physical = Total; this.Magical = 1.0 + Total / 100.0; }
            public Buff(int Physical, int Magical) { this.Physical = 1.0 + Physical / 100.0; this.Magical = 1.0 + Magical / 100.0; }
        }

        private static Dictionary<int, Buff> Buffs = new Dictionary<int, Buff>()
        {
            {1239, new Buff(0, 10) }
        };
        private class Modifiers
        {
            public int Main = 0;
            public int Sub = 0;
            public int Div = 0;
            public Modifiers(int M, int S, int D) { Main = M; Sub = S; Div = D; }
        }

        private static List<Modifiers> Level_Modifiers = new List<Modifiers> { new Modifiers(20, 56, 56),
new Modifiers(21, 57, 57),
new Modifiers(22, 60, 60),
new Modifiers(24, 62, 62),
new Modifiers(26, 65, 65),
new Modifiers(27, 68, 68),
new Modifiers(29, 70, 70),
new Modifiers(31, 73, 73),
new Modifiers(33, 76, 76),
new Modifiers(35, 78, 78),
new Modifiers(36, 82, 82),
new Modifiers(38, 85, 85),
new Modifiers(41, 89, 89),
new Modifiers(44, 93, 93),
new Modifiers(46, 96, 96),
new Modifiers(49, 100, 100),
new Modifiers(52, 104, 104),
new Modifiers(54, 109, 109),
new Modifiers(57, 113, 113),
new Modifiers(60, 116, 116),
new Modifiers(63, 122, 122),
new Modifiers(67, 127, 127),
new Modifiers(71, 133, 133),
new Modifiers(74, 138, 138),
new Modifiers(78, 144, 144),
new Modifiers(81, 150, 150),
new Modifiers(85, 155, 155),
new Modifiers(89, 162, 162),
new Modifiers(92, 168, 168),
new Modifiers(97, 173, 173),
new Modifiers(101, 181, 181),
new Modifiers(106, 188, 188),
new Modifiers(110, 194, 194),
new Modifiers(115, 202, 202),
new Modifiers(119, 209, 209),
new Modifiers(124, 215, 215),
new Modifiers(128, 223, 223),
new Modifiers(134, 229, 229),
new Modifiers(139, 236, 236),
new Modifiers(144, 244, 244),
new Modifiers(150, 253, 253),
new Modifiers(155, 263, 263),
new Modifiers(161, 272, 272),
new Modifiers(166, 283, 283),
new Modifiers(171, 292, 292),
new Modifiers(177, 302, 302),
new Modifiers(183, 311, 311),
new Modifiers(189, 322, 322),
new Modifiers(196, 331, 331),
new Modifiers(202, 341, 341),
new Modifiers(204, 342, 366),
new Modifiers(205, 344, 392),
new Modifiers(207, 345, 418),
new Modifiers(209, 346, 444),
new Modifiers(210, 347, 470),
new Modifiers(212, 349, 496),
new Modifiers(214, 350, 522),
new Modifiers(215, 351, 548),
new Modifiers(217, 352, 574),
new Modifiers(218, 354, 600),
new Modifiers(224, 355, 630),
new Modifiers(228, 356, 660),
new Modifiers(236, 357, 690),
new Modifiers(244, 358, 720),
new Modifiers(252, 359, 750),
new Modifiers(260, 360, 780),
new Modifiers(268, 361, 810),
new Modifiers(276, 362, 840),
new Modifiers(284, 363, 870),
new Modifiers(292, 364, 900),
new Modifiers(296, 365, 940),
new Modifiers(300, 366, 980),
new Modifiers(305, 367, 1020),
new Modifiers(310, 368, 1060),
new Modifiers(315, 370, 1100),
new Modifiers(320, 372, 1140),
new Modifiers(325, 374, 1180),
new Modifiers(330, 376, 1220),
new Modifiers(335, 378, 1260),
new Modifiers(340, 380, 1300),
new Modifiers(345, 382, 1360),
new Modifiers(350, 384, 1420),
new Modifiers(355, 386, 1480),
new Modifiers(360, 388, 1540),
new Modifiers(365, 390, 1600),
new Modifiers(370, 392, 1660),
new Modifiers(375, 394, 1720),
new Modifiers(380, 396, 1780),
new Modifiers(385, 398, 1840),
new Modifiers(390, 400, 1900),
new Modifiers(395, 402, 1988),
new Modifiers(400, 404, 2076),
new Modifiers(405, 406, 2164),
new Modifiers(410, 408, 2252),
new Modifiers(415, 410, 2340),
new Modifiers(420, 412, 2428),
new Modifiers(425, 414, 2516),
new Modifiers(430, 416, 2604),
new Modifiers(435, 418, 2692),
new Modifiers(440, 420, 2780) };

        private static List<string> Guaranteed_Critical_Hit = new();

        private static List<string> Guaranteed_Direct_Hit = new();

        private static Dictionary<string, int[]> Thresholds = new Dictionary<string, int[]>()
{
    { "Riposte", new int[] { 130 } },
    { "Zwerchhau", new int[] { 100, 150 } },
    { "Redoublement", new int[] { 100, 230 } },
    { "Jolt", new int[] { 170 } },
    { "Verthunder", new int[] { 360 } },
    { "Veraero", new int[] { 360 } },
    { "Corps-a-corps", new int[] { 130 } },
    { "Scatter", new int[] { 120, 170 } },
    { "Verthunder II", new int[] { 140 } },
    { "Veraero II", new int[] { 140 } },
    { "Verfire", new int[] { 380 } },
    { "Verstone", new int[] { 380 } },
    { "Displacement", new int[] { 180 } },
    { "Engagement", new int[] { 180 } },
    { "Fleche", new int[] { 480 } },
    { "Moulinet", new int[] { 60 } },
    { "Contre Sixte", new int[] { 420 } },
    { "Jolt II", new int[] { 280 } },
    { "Impact", new int[] { 210, 260 } },
    { "Reprise", new int[] { 100 } },
    { "Verthunder III", new int[] { 440 } },
    { "Veraero III", new int[] { 440 } },
    { "Jolt III", new int[] { 360 } },
    { "Vice of Thorns", new int[] { 950, 427 } },
    { "Prefulgence", new int[] { 1200, 540 } },
    { "Enchanted Riposte", new int[] { 340 } },
    { "Enchanted Zwerchhau", new int[] { 380, 190 } },
    { "Enchanted Redoublement", new int[] { 560, 190 } },
    { "Enchanted Moulinet", new int[] { 130 } },
    { "Enchanted Moulinet Deux", new int[] { 140 } },
    { "Enchanted Moulinet Trois", new int[] { 150 } },
    { "Verflare", new int[] { 650, 292 } },
    { "Verholy", new int[] { 650, 292 } },
    { "Enchanted Reprise", new int[] { 420 } },
    { "Scorch", new int[] { 750, 337 } },
    { "Resolution", new int[] { 850, 382 } },
    { "Grand Impact", new int[] { 600, 270 } },
};

        private static List<uint> Casters = [6, 7, 24, 25, 26, 27, 28, 33, 35, 36, 40, 42];

        private static List<uint> Physical_Ranged = [5, 23, 31, 38];

        private static List<uint> Tanks = [1, 3, 19, 21, 32, 37];
        private async Task ProcessAsync(EncounterIngestJob job, CancellationToken ct)
        {
            logger.LogInformation("Processing ingest job {JobId}", job.JobId);

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var Join_Mapping = new Dictionary<ulong, PlayerEnterCombat>();
            var Potencies = new Dictionary<ulong, double>();
            var Hits = new Dictionary<ulong, int>();
            var Baselines = new Dictionary<ulong, double>();
            var Events = NewEncounterRequest.Parser.ParseFrom(job.Payload).Events;
            long Start = 0;
            foreach (var Message in Events)
            {
                switch (Message.EventDataCase)
                {
                    case CombatEvent.EventDataOneofCase.PlayerJoin:
                        {
                            Join_Mapping[Message.Source.GameobjectId] = Message.PlayerJoin;
                            Potencies[Message.Source.GameobjectId] = 0.0;
                            Hits[Message.Source.GameobjectId] = 0;
                            if (Start == 0) Start = Message.TimestampEpochMs;
                            break;
                        }
                    case CombatEvent.EventDataOneofCase.DamageTaken:
                        {
                            var Player = Join_Mapping[Message.Source.GameobjectId];
                            var State = Message.LocalSnapshot;
                            var Level_Modifier = Level_Modifiers[(int)Player.Level - 1];
                            var Level_Attack_Modifier = Tanks.Contains(Player.JobId) ? (Player.Level - 90) * 3.4 + 156 : (Player.Level - 90) * 4.2 + 195;
                            var Attack = Math.Floor(100 + Level_Attack_Modifier * (State.AttackPower - Level_Modifier.Main) / Level_Modifier.Main) / 100;
                            double Character_Multiplier = (Casters.Contains(Player.JobId) ? 1.3 : (Physical_Ranged.Contains(Player.JobId) ? 1.2 : 1.0)) * (Tanks.Contains(Player.JobId) ? Math.Floor(112d * (State.Tenacity - Level_Modifier.Sub) / Level_Modifier.Div) / 1000d : 1.0) * Math.Floor(100 * Attack * Player.WeaponDamage) / 100;
                            var Statuses = Message.SourceSnapshot.StatusEffects.ToArray().Select(X => (int)X.Id);
                            if (!Statuses.Contains(44) && !Statuses.Contains(43) && Statuses.Contains(48) && !Statuses.Contains(49)) Baselines[Player.GameobjectId] = Character_Multiplier;
                            break;
                        }
                }
            }

            foreach (var Message in Events)
            {
                switch (Message.EventDataCase)
                {
                    case CombatEvent.EventDataOneofCase.Death:
                        {
                            break;
                        }
                    case CombatEvent.EventDataOneofCase.DamageTaken:
                        {
                            if (Baselines.ContainsKey(Message.Source.GameobjectId))
                            {
                                var Player = Join_Mapping[Message.Source.GameobjectId];
                                var ID = Message.DamageTaken.ActionId;
                                var Name = "";
                                var Type = 0;
                                var Speed_Scalar = 1.0;
                                var State = Message.LocalSnapshot;
                                var Level_Modifier = Level_Modifiers[(int)Player.Level - 1];
                                var Level_Attack_Modifier = Tanks.Contains(Player.JobId) ? (Player.Level - 90) * 3.4 + 156 : (Player.Level - 90) * 4.2 + 195;
                                var Attack = Math.Floor(100 + Level_Attack_Modifier * (State.AttackPower - Level_Modifier.Main) / Level_Modifier.Main) / 100;
                                double Character_Multiplier = (Casters.Contains(Player.JobId) ? 1.3 : (Physical_Ranged.Contains(Player.JobId) ? 1.2 : 1.0)) * (Tanks.Contains(Player.JobId) ? Math.Floor(112d * (State.Tenacity - Level_Modifier.Sub) / Level_Modifier.Div) / 1000d : 1.0) * Math.Floor(100 * Attack * Player.WeaponDamage) / 100;
                                double Skill_Speed = 1000.0 + Math.Ceiling(130.0 * (Level_Modifier.Sub - State.Skillspeed) / Level_Modifier.Div);
                                double Spell_Speed = 1000.0 + Math.Ceiling(130.0 * (Level_Modifier.Sub - State.Spellspeed) / Level_Modifier.Div);
                                //if (Player.JobId == 25) if (Gauge_Manager->BlackMage.EnochianActive) New_Internal_Magical_Buff_Multiplier *= 1.27;
                                double Determination_Multiplier = 1.0 + Math.Floor(140d * (State.Determination - Level_Modifier.Main) / Level_Modifier.Div) / 1000d;
                                double Critical_Multiplier = Math.Floor(200d * (State.CriticalHit - Level_Modifier.Sub) / Level_Modifier.Div + 1400) / 1000d;
                                double External_Physical_Buff_Multiplier = 1.0;
                                double External_Magical_Buff_Multiplier = 1.0;
                                double Internal_Physical_Buff_Multiplier = 1.0;
                                double Internal_Magical_Buff_Multiplier = 1.0;
                                List<string> Named = [];
                                foreach (var Status in Message.SourceSnapshot.StatusEffects) if (Buffs.ContainsKey((int)Status.Id)) if (Status.SourceId == Player.GameobjectId)
                                        {
                                            Internal_Physical_Buff_Multiplier *= Buffs[(int)Status.Id].Physical;
                                            Internal_Magical_Buff_Multiplier *= Buffs[(int)Status.Id].Magical;
                                            Named.Add(lumina.GetExcelSheet<Lumina.Excel.Sheets.Status>().GetRow(Status.Id).Name.ExtractText());
                                        }
                                        else
                                        {
                                            External_Physical_Buff_Multiplier *= Buffs[(int)Status.Id].Physical;
                                            External_Magical_Buff_Multiplier *= Buffs[(int)Status.Id].Magical;
                                        }
                                foreach (var Status in Message.TargetSnapshot.StatusEffects)
                                {
                                    // Chain Stratagem, Dokumori
                                }
                                if (lumina.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(ID, out var N))
                                {
                                    Name = N.Name.ExtractText();
                                    var Ability_Type = N.ActionCategory.Value.Name.ToString();
                                    if (Ability_Type == "Spell" || Ability_Type == "Weaponskill")
                                    {
                                        Type = 0;
                                        var Recast = N.Recast100ms / 10.0;
                                        Speed_Scalar = Recast < 2.0 ? 1.0 : Math.Floor(Math.Floor((Ability_Type == "Spell" ? Spell_Speed : Skill_Speed) * Recast * 100 / 100d) / 10d) / 100d / Recast;
                                    }
                                    else if (Ability_Type == "Ability")
                                    {
                                        Type = 1;
                                    }
                                    else if (Name == "Auto") Type = 2;




                                    var Local_Buffs = 1.0;
                                    if (
                                        ((Name == "Fell Cleave" || Name == "Decimate") && Named.Contains("Inner Release"))
                                        ||
                                        Guaranteed_Critical_Hit.Contains(Name)
                                        )
                                    {
                                        if (Named.Contains("Devilment")) Local_Buffs *= 1.2;
                                        if (Named.Contains("Battle Litany")) Local_Buffs *= 1.1;
                                        if (Named.Contains("Chain Stratagem")) Local_Buffs *= 1.1;
                                        if (Named.Contains("Wanderer's Minuet")) Local_Buffs *= 1.02;
                                    }
                                    if (
                                        ((Name == "Fell Cleave" || Name == "Decimate") && Named.Contains("Inner Release"))
                                        ||
                                        Guaranteed_Direct_Hit.Contains(Name)
                                        )
                                    {
                                        if (Named.Contains("Devilment") && !Guaranteed_Critical_Hit.Contains(Name)) Local_Buffs *= 1.2;
                                        if (Named.Contains("Battle Voice")) Local_Buffs *= 1.2;
                                        if (Named.Contains("Army's Paeon")) Local_Buffs *= 1.03;
                                    }
                                    var Magic = Message.DamageTaken.DamageType == DamageType.Magic;
                                    var Typed_Multiplier = Magic ? External_Magical_Buff_Multiplier * Internal_Magical_Buff_Multiplier : External_Physical_Buff_Multiplier * Internal_Physical_Buff_Multiplier;
                                    var Multiplier = Local_Buffs * Typed_Multiplier * Character_Multiplier * Determination_Multiplier * (Message.DamageTaken.DirectHit ? 1.25 : 1.0) * (Message.DamageTaken.Crit ? Critical_Multiplier : 1.0);
                                    var Estimated_Potency = (uint)Math.Round(Message.DamageTaken.Amount / Multiplier) * 1d;
                                    var Original_Potency_Estimation = Estimated_Potency;
                                    logger.LogInformation($"Potency Before ({Name}): {Estimated_Potency}");
                                    if (Thresholds.ContainsKey(Name)) Estimated_Potency = Thresholds[Name].MinBy(X => Math.Abs(X - Estimated_Potency));
                                    logger.LogInformation($"Potency After ({Name}): {Estimated_Potency}");
                                    logger.LogInformation($"Character Multiplier: {Character_Multiplier}");
                                    logger.LogInformation($"Character Baseline: {Baselines[Message.Source.GameobjectId]}");
                                    if (Name == "Flare")
                                    {
                                        if (Message.DamageTaken.MainTarget && Estimated_Potency == 235) Estimated_Potency = 240;
                                        if (Message.DamageTaken.MainTarget && Estimated_Potency == 260) Estimated_Potency = 240;
                                        if (!Message.DamageTaken.MainTarget && Estimated_Potency == 240) if (Original_Potency_Estimation < 240)
                                            {
                                                Estimated_Potency = 235;
                                            }
                                            else Estimated_Potency = 268;
                                    }
                                    if ((Name == "Fell Cleave" || Name == "Decimate") && Named.Contains("Inner Release")) Estimated_Potency *= 2.0;
                                    if (Guaranteed_Critical_Hit.Contains(Name)) Estimated_Potency *= 1.6;
                                    if (Guaranteed_Direct_Hit.Contains(Name)) Estimated_Potency *= 1.25;
                                    Potencies[Message.Source.GameobjectId] += Math.Round(1000.0 *
                                        Estimated_Potency *
                                        (Magic ? Internal_Magical_Buff_Multiplier : Internal_Physical_Buff_Multiplier) *
                                        Character_Multiplier / Baselines[Message.Source.GameobjectId]
                                        ) / 1000d;
                                    Hits[Message.Source.GameobjectId]++;
                                }
                            }
                            break;
                        }
                }
            }

            // Parsing logic would go somewhere here
            var encounter = new Entities.Encounter
            {
                CfcId = (int?)job.CfcId,//DBs don't like unsigned
                UploadedBy = job.UploadedBy,
                UploadedAt = job.QueuedAt,
                Payload = job.Payload,
            };


            var enc = db.Encounters.Add(encounter);
            await db.SaveChangesAsync(ct);

            //Temp attributions, in real logic, this will be dervied from the parse itself
            var Duration = Math.Round((double)(Events.Last().TimestampEpochMs - Start)) / 1000.0;
            foreach (var Character in Baselines.Keys)
            {
                var character = db.CharacterClaims.FirstOrDefault(c => c.ClaimBy == job.UploadedBy);
                var stats = new EncounterPlayerStat
                {
                    Character = character.Id, // This will have to be updated in case we are logging multiple characters at once... Ideally, this would be a character hash that uniquely identifies them in game (even when they change names).
                    PlayerName = character.CharName,
                    EncounterId = encounter.Id,

                    PlayerId = (long)Character,
                    JobId = (int)Join_Mapping[Character].JobId,
                    TotalPScore = 2.5 * Potencies[Character] / Duration,
                    TotalDamage = 0,
                    TotalCrits = 0,
                    TotalDirectHits = 0,
                    TotalHealing = 0,
                    TotalHits = Hits[Character],
                    DirectHitRate = 0,
                    Dps = 0,
                    Hps = 0,
                    DurationSeconds = Duration,
                    UploadedBy = job.UploadedBy,
                    CritRate = 0f,
                };
                db.EncounterPlayerStats.Add(stats);
                await db.SaveChangesAsync(ct);
            }

            logger.LogInformation("Ingest job {JobId} persisted as encounter {EncounterId}",
                job.JobId, encounter.Id);
        }
    }
}
