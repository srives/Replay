using Microsoft.Extensions.Options;
using STRATUS.CAD.Models.ConfigModels;

namespace Replay
{

    /// <summary>
    /// Inject into code through services to get me the DB string
    /// </summary>
    public class SQLConnectionString
    {
        public string ConnectionString { get; set; }

        public SQLConnectionString(IOptionsMonitor<DatabaseConfig> optionsDatabase)
        {
            ConnectionString = optionsDatabase.CurrentValue.TelemetryDb;
        }
    }
}