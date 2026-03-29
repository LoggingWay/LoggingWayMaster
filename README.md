You will need to place a `appsettings.json` file in root to configure env variables notably xivauth secrets+Connections strings, like so:
```
{
  "XivAuth": {
    "ClientId": "your client id",
    "ClientSecret": "your client secret",
    "RedirectUri": "http://localhost:6767" //this is the callback used by the plugin
  },
  "ConnectionStrings": {
    "LoggingwayPG": "Host=localhost;Database=loggingway;Username=postgres;Password=yourpassword",
    "LoggingwaySQ": "Data Source=loggingway.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "EndpointDefaults": {
      "Protocols": "Http2"
    }
  }
}
```

To setup:
```
git clone --recurse-submodules https://github.com/LoggingWay/LoggingWayMaster.git
cd LoggingWayMaster

dotnet restore
dotnet ef migrations add InitialCreate 
dotnet ef database update
```
The last two are required to create the initial db migration state, you can switch between SQLite and PGSQL as provider for EF by changing which one gets used in `Program.cs`
Normally you should be prompted by VS to add a localhost certificate to enable TLS locally, it is recommended you do so, but the environement does also open an http port.
