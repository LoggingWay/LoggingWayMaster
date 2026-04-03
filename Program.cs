using LoggingWayMaster.Services;
using LoggingWayMaster.Stores;
using Microsoft.EntityFrameworkCore;

namespace LoggingWayMaster
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            //Services+Singleton
            builder.Services.AddGrpc();
            builder.Services.AddSingleton<OAuthStateStore>();
            builder.Services.AddSingleton<SessionStore>();
            builder.Services.AddSingleton<JobResultStore>();
            builder.Services.AddHttpClient<XivAuthClient>();
            builder.Services.AddSingleton<EncounterIngestQueue>();
            //Lumina
            var lumina = new Lumina.GameData(builder.Configuration.GetRequiredSection("Lumina")["GameDataPath"]);
            builder.Services.AddSingleton(lumina);

            
            //Hosted Services(things that run continously in the background like the IngestWorker)
            builder.Services.AddHostedService<EncounterIngestWorker>();


            //SQLite entities
            builder.Services.AddDbContextFactory<LoggingwayDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("LoggingwaySQ"))
        );
            //PGSQL Entities, for scaling/separation purposes
            //builder.Services.AddDbContextFactory<LoggingwayDbContext>(options =>
            //options.UseNpgsql(builder.Configuration.GetConnectionString("LoggingwayPG")));

            var app = builder.Build();

            //Run migration from in-app to make sure DB state is synced with app state
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LoggingwayDbContext>();
                db.Database.Migrate();
            }
            app.MapGrpcService<LoggingWayService>();

            app.Run();
        }
    }
}
