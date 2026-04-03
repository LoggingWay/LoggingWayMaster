using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace LoggingWayMaster.Stores
{
    public class SessionStore
    {
        public record Session(string SessionId, Guid XivAuthId, DateTimeOffset ExpiresAt);

        private readonly ConcurrentDictionary<string, Session> _sessions = new();
        private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(24);

        public Session CreateSession(string xivAuthId)
        {
            var session = new Session(
                SessionId: GenerateSessionId(),
                XivAuthId:  Guid.Parse(xivAuthId),
                ExpiresAt: DateTimeOffset.UtcNow.Add(SessionDuration)
            );
            _sessions[session.SessionId] = session;
            return session;
        }

        public Session? GetSession(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
                return null;

            if (DateTimeOffset.UtcNow >= session.ExpiresAt)
            {
                _sessions.TryRemove(sessionId, out _);
                return null;
            }
            return session;
        }

        public void DeleteSession(string sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
        }

        private static string GenerateSessionId()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
