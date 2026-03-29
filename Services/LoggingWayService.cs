using Google.Protobuf;
using Grpc.Core;
using LoggingWayGrpcService.Stores;
using LoggingWayMaster.Services;
using LoggingWayPlugin.Proto;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static LoggingWayGrpcService.Stores.SessionStore;

namespace LoggingWayGrpcService.Services
{
    public class LoggingWayService(OAuthStateStore stateStore,
        SessionStore sessionStore,
        XivAuthClient xivAuth,
        IDbContextFactory<LoggingwayDbContext> dbFactory,
        EncounterIngestQueue ingestQueue,
        ILogger<LoggingWayService> logger) : Loggingway.LoggingwayBase
    {
        public override Task<GetXivAuthRedirectReply> GetXivAuthRedirect(
        GetXivAuthRedirectRequest request, ServerCallContext context)
        {
            var state = stateStore.GenerateAndStore();
            var uri = xivAuth.BuildRedirectUri(state);

            return Task.FromResult(new GetXivAuthRedirectReply { Xivauthuri = uri });
        }

        public override async Task<LoginReply> Login(LoginRequest request, ServerCallContext context)
        {
            if (!stateStore.ValidateAndConsume(request.State))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid or expired state"));

            XivAuthUser user;
            List<XivAuthCharacter>? characters;
            try
            {
                (user, characters) = await xivAuth.GetUserInfoFromCode(request.Code);
            }
            catch (HttpRequestException ex)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "OAuth exchange failed"));
            }
            var session = sessionStore.CreateSession(user.Id);
            using (var conn = await dbFactory.CreateDbContextAsync())
            {
                var db_user = conn.Users.FirstOrDefault(u => u.Id == session.XivAuthId);
                if (db_user == null)
                {
                    //another happy user,append all known characters
                    List<Entities.CharacterClaim> auth2db = new List<Entities.CharacterClaim>();
                    if (characters != null)
                    {
                        foreach (var c in characters)
                        {
                            auth2db.Add(new Entities.CharacterClaim
                            {
                                CharName = c.Name,
                                HomeWorld = c.HomeWorld,
                                DataCenter = c.DataCenter,
                                PortraitUrl = c.PortraitUrl,
                                AvatarUrl = c.AvatarUrl,
                                XivAuthKey = c.PersistentKey,
                                LodestoneId = int.Parse(c.LodestoneId),
                            });
                        }
                    }
                    var new_user = conn.Users.Add(new Entities.User {Id = Guid.Parse(user.Id),
                                                                      Characters = auth2db,
                                                                    Banned = false,});

                    await conn.SaveChangesAsync();
                        }
                }
                
            return new LoginReply { SessionID = session.SessionId };
        }

        public override async Task<LogoutReply> Logout(LogoutRequest request, ServerCallContext context)
        {
            SessionStore.Session user_session = EnsureAuth(context);
            sessionStore.DeleteSession(user_session.SessionId);
            return new LogoutReply { };
        }

        public override async Task<GetMyCharactersReply> GetMyCharacters(GetMyCharactersRequest request, ServerCallContext context)
        {
            SessionStore.Session user_se = EnsureAuth(context);
            using (var conn = await dbFactory.CreateDbContextAsync())
            {
                var chars = await conn.CharacterClaims.Where(c => c.ClaimBy == user_se.XivAuthId).ToListAsync();
                var reply = new GetMyCharactersReply();
                reply.Characters.AddRange(chars.Select(c => c.ToProto()));
                return reply;
            }
        }

        public override async Task<GetMyEncountersReply> GetMyEncounters(GetMyEncountersRequest request,ServerCallContext context)
        {
            SessionStore.Session user_se = EnsureAuth(context);
            using (var conn = await dbFactory.CreateDbContextAsync())
            {
                var encounters = await conn.Encounters.Where(c => c.UploadedBy == user_se.XivAuthId).ToListAsync();
                var reply = new GetMyEncountersReply();
                reply.Encounters.AddRange(encounters.Select(e => e.ToProto()));
                return reply;
            }
        }

        public override async Task<NewEncounterReply> EncounterIngest(NewEncounterRequest request, ServerCallContext context)
        {
            SessionStore.Session user_se = EnsureAuth(context);
            using (var conn = await dbFactory.CreateDbContextAsync())
            {
                var chars = await conn.CharacterClaims.Where(c => c.ClaimBy == user_se.XivAuthId).ToListAsync();
                if (!chars.Any())
                {
                    throw new RpcException(new Status(StatusCode.FailedPrecondition, "No characters claimed, cannot upload encounters"));
                }
            }
            var job = new EncounterIngestJob(
        JobId: Guid.NewGuid(),
        UploadedBy: user_se.XivAuthId,
        CfcId: request.CfcId,//At somepoint in the future,we might want to split processing based on Cfc
        Payload: request.ToByteArray(),
        QueuedAt: DateTimeOffset.UtcNow
                                    );

            var queued = await ingestQueue.TryEnqueueAsync(job,context.CancellationToken);
            if (!queued)
                throw new RpcException(new Status(StatusCode.Unavailable, "Ingest queue insertion error"));

            return new NewEncounterReply
            {
                Jobid = job.JobId.ToString(),
                QueuedAt = job.QueuedAt.ToUnixTimeSeconds(),
            };

        }

        public override async Task<GetEncountersStatsReply> GetEncountersStats(GetEncountersStatsRequest request, ServerCallContext context)
        {
            SessionStore.Session user_se = EnsureAuth(context);
            using (var conn = await dbFactory.CreateDbContextAsync())
            {
                var stats = await conn.EncounterPlayerStats.Where(e => e.EncounterId == request.EncounterId).FirstOrDefaultAsync();
                if (stats is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "Could not find EncounterId"));
                if (stats.UploadedBy != user_se.XivAuthId)
                    throw new RpcException(new Status(StatusCode.PermissionDenied, "Naughty little user(UploadedBy and Session user ID does not match"));
                return new GetEncountersStatsReply{ Playerstats = stats.ToProto()};
            }
         }

        public override async Task<GetLeaderBoardReply> GetLeaderBoard(GetLeaderBoardRequest request, ServerCallContext context)
        {
            SessionStore.Session user_se = EnsureAuth(context);
            using (var db = await dbFactory.CreateDbContextAsync())
            {

                // join stats with their encounter and character claim
                var query = db.EncounterPlayerStats
                    .Include(s => s.CharacterClaim)
                    .Include(s => s.Encounter)
                    .Where(s => s.Encounter.CfcId == (int)request.CfcId
                             && s.CharacterClaim != null);

                
                if (request.JobId != 0)
                    query = query.Where(s => s.JobId == (int)request.JobId);

                // For each character, keep only their best pscore run for this cfc_id
                var bestScores = (await query.ToListAsync())//ToListAsync down there seem to make this untranslable?Memory issue probably
                                .GroupBy(s => s.Character)
                                .Select(g => g.OrderByDescending(s => s.TotalPScore).First())
                                .OrderByDescending(s => s.TotalPScore)
                                .ToList();

                var totalRanked = bestScores.Count;

                var entries = bestScores
                    .Select((s, index) => s.CharacterClaim!.ToLeaderBoardEntry(s, rank: index + 1))
                    .ToList();

                var reply = new GetLeaderBoardReply { TotalRanked = totalRanked };
                reply.Entry.AddRange(entries);
                return reply;
            }
        }
        //Extension to deal with auth

        public SessionStore.Session EnsureAuth(ServerCallContext context)
        {
            var authHeader = context.RequestHeaders
                .FirstOrDefault(e => e.Key == "authorization")?.Value;

            if (string.IsNullOrEmpty(authHeader))
                throw new RpcException(new Status(StatusCode.Unauthenticated, "No session ID"));

            var session = sessionStore.GetSession(authHeader);
            if (session is null)
                throw new RpcException(new Status(StatusCode.Unauthenticated, "SessionID invalid or expired"));
            return session;
        }
    }
}
