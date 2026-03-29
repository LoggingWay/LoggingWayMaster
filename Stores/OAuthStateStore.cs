using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace LoggingWayGrpcService.Stores
{
    public class OAuthStateStore
    {
        private readonly ConcurrentDictionary<string, DateTimeOffset> _pendingStates = new();
        private static readonly TimeSpan StateExpiry = TimeSpan.FromMinutes(10);

        public string GenerateAndStore()
        {
            var state = GenerateOAuthState();
            _pendingStates[state] = DateTimeOffset.UtcNow.Add(StateExpiry);
            return state;
        }

        public bool ValidateAndConsume(string state)
        {
            if (!_pendingStates.TryRemove(state, out var expiry))
                return false;

            return DateTimeOffset.UtcNow < expiry;
        }
        public void PurgeExpired()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var key in _pendingStates.Keys)
                if (_pendingStates.TryGetValue(key, out var expiry) && now >= expiry)
                    _pendingStates.TryRemove(key, out _);
        }

        private static string GenerateOAuthState(int byteLength = 32)
        {
            var bytes = RandomNumberGenerator.GetBytes(byteLength);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
