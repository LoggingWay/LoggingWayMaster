using LoggingWayMaster.Stores;

namespace LoggingWayMaster.Services
{
    public class StatePurger(OAuthStateStore oAuthStateStore) : BackgroundService //banger class name
    {
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            oAuthStateStore.PurgeExpired();
            return Task.CompletedTask;
        }
    }
}
