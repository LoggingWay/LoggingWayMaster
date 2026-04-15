using Google.Protobuf;
using Grpc.Core;
using LoggingWayMaster.Entities;
using LoggingWayMaster.Stores;
using LoggingWayMaster.Services;
using LoggingWayMaster.Stores;
using LoggingWayPlugin.Proto;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static LoggingWayMaster.Stores.SessionStore;

namespace LoggingWayMaster.Services
{
    public class LoggingWayService(OAuthStateStore stateStore,
        SessionStore sessionStore,
        JobResultStore jobResultStore,
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
                logger.LogError(ex, "Error during OAuth exchange");
                throw new RpcException(new Status(StatusCode.Unauthenticated, "OAuth exchange failed"));
            }
            var session = sessionStore.CreateSession(user.Id);
            var code = 0;//for now this only indicate if there are no xivauth chars
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
                    if (characters == null)
                    {
                        code = 1;
                    }
                    var new_user = conn.Users.Add(new Entities.User {Id = Guid.Parse(user.Id),
                                                                      Characters = auth2db,
                                                                    Banned = false,});

                    await conn.SaveChangesAsync();
                        }
                }
                
            return new LoginReply { SessionID = session.SessionId, Code = code };
        }

        public override async Task<LogoutReply> Logout(LogoutRequest request, ServerCallContext context)
        {
            SessionStore.Session user_session = EnsureAuth(context);
            sessionStore.DeleteSession(user_session.SessionId);
            return new LogoutReply { };
        }

        public override Task<SessionRefreshReply> SessionRefresh(SessionRefreshRequest request, ServerCallContext context)
        {
            SessionStore.Session old_session = EnsureAuth(context);
            var newSession = sessionStore.CreateSession(old_session.XivAuthId.ToString());

            sessionStore.DeleteSession(old_session.SessionId);
            return Task.FromResult(new SessionRefreshReply { SessionID = newSession.SessionId });
        }

        public override async Task<EnrollCharactersReply> EnrollCharacters(
    EnrollCharactersRequest request,
    ServerCallContext context)
        {
            SessionStore.Session user_session = EnsureAuth(context);

            if (!stateStore.ValidateAndConsume(request.State))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid or expired state"));

            XivAuthUser user;
            List<XivAuthCharacter>? freshCharacters;
            try
            {
                (user, freshCharacters) = await xivAuth.GetUserInfoFromCode(request.Code);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Error during OAuth exchange");
                throw new RpcException(new Status(StatusCode.Unauthenticated, "OAuth exchange failed"));
            }

            if (freshCharacters is null)
                throw new RpcException(new Status(StatusCode.FailedPrecondition, "characters:all scope was not granted"));
            if (Guid.Parse(user.Id) != user_session.XivAuthId)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "XivAuthId from session does not match XivAuthId from code exchange,please logout and login if you wish to change account"));
            using (var db = await dbFactory.CreateDbContextAsync()){

                // Fetch what we already have on record for this user
                var existingClaims = await db.CharacterClaims
                    .Where(c => c.ClaimBy == user_session.XivAuthId)
                    .ToListAsync();

                var existingKeys = existingClaims.Select(c => c.XivAuthKey).ToHashSet();
                var freshKeys = freshCharacters.Select(c => c.PersistentKey).ToHashSet();

                var toAdd = freshCharacters
                    .Where(c => !existingKeys.Contains(c.PersistentKey))
                    .Select(c => new CharacterClaim
                    {
                        Id = Guid.NewGuid(),
                        ClaimBy = user_session.XivAuthId,
                        XivAuthKey = c.PersistentKey,
                        CharName = c.Name,
                        DataCenter = c.DataCenter,
                        HomeWorld = c.HomeWorld,
                        LodestoneId = int.Parse(c.LodestoneId),
                        AvatarUrl = c.AvatarUrl,
                        PortraitUrl = c.PortraitUrl,
                        ClaimRegistered = DateTimeOffset.UtcNow,
                    });

                //Remove characters that are no longer in the XIVAuth response
                var toRemove = existingClaims
                    .Where(c => !freshKeys.Contains(c.XivAuthKey))
                    .ToList();

                db.CharacterClaims.AddRange(toAdd);
                db.CharacterClaims.RemoveRange(toRemove);
                await db.SaveChangesAsync();

                logger.LogInformation(
                    "EnrollCharacters for user {UserId}: +{Added} added, -{Removed} removed",
                    user_session.XivAuthId, toAdd.Count(), toRemove.Count);

            }
            return new EnrollCharactersReply();
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

            jobResultStore.Register(job.JobId);
            if (!await ingestQueue.TryEnqueueAsync(job, context.CancellationToken))
            {
                jobResultStore.TryFail(job.JobId, new Exception("queue full"));
                throw new RpcException(new Status(StatusCode.Unavailable, "Ingest queue full"));
            }

            return new NewEncounterReply
            {
                Jobid = job.JobId.ToString(),
                QueuedAt = job.QueuedAt.ToUnixTimeSeconds(),
            };

        }

        public override async Task<PollJobResultReply> PollJobResult(
    PollJobResultRequest request, ServerCallContext context)
        {
            EnsureAuth(context);

            if (!Guid.TryParse(request.JobId, out var jobId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid job_id"));

            // wait 10 second before calling it
            var result = await jobResultStore.WaitAsync(jobId, TimeSpan.FromSeconds(10), context.CancellationToken);

            if (result is null)
                return new PollJobResultReply { Ready = false };

            return new PollJobResultReply
            {
                Ready = true,
                EncounterId = result.EncounterId,
                Rank = result.Rank,
                TotalRanked = result.TotalRanked,
                Pscore = result.PScore,
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

                var query = db.LeaderboardEntries
            .Where(e => e.CfcId == (int)request.CfcId);

                if (request.JobId != 0)
                    query = query.Where(e => e.JobId == (int)request.JobId);

                var totalRanked = await query.CountAsync(context.CancellationToken);

                var entries = await query
                    .OrderBy(e => e.PScoreRank)
                    .Take(100)//TODO: implement pagination in proto, then update this
                    .Select(e => new
                    {
                        e.BestPScore,
                        e.PScoreRank,
                        e.JobId,
                        e.Character,
                        CharClaim = e.CharacterClaim == null ? null : new
                        {
                            e.CharacterClaim!.CharName,
                            e.CharacterClaim.HomeWorld,
                            e.CharacterClaim.DataCenter,
                            e.CharacterClaim.Id
                        }
                    })
                    .ToListAsync(context.CancellationToken);

                var reply = new GetLeaderBoardReply
                {
                    TotalRanked = totalRanked
                };

                foreach (var e in entries)
                {
                    var entry = new LeaderBoardEntry
                    {
                        Rank = e.PScoreRank,
                        Psccore = (float)e.BestPScore,
                        Jobid = (uint)e.JobId
                    };

                    if (e.CharClaim is not null)
                    {
                        entry.Char = new Character
                        {
                            Name = e.CharClaim.CharName,
                            Homeworld = e.CharClaim.HomeWorld,
                            Datacenter = e.CharClaim.DataCenter,
                            PersistentKey = e.CharClaim.Id.ToString()
                        };
                    }

                    reply.Entry.Add(entry);
                }

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
