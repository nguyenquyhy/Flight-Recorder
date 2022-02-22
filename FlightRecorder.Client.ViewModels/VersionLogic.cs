using Microsoft.Extensions.Logging;

namespace FlightRecorder.Client
{
    public class VersionLogic
    {
        private readonly ILogger<VersionLogic> logger;

        public VersionLogic(ILogger<VersionLogic> logger)
        {
            this.logger = logger;
        }

        public string GetVersion()
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
            if (version == null)
            {
                logger.LogWarning("Cannot get assembly version. Revert to 0.0.0.0.");
                return "0.0.0.0";
            }
            else
            {
                return version;
            }
        }
    }
}
