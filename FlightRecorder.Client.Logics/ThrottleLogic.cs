using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FlightRecorder.Client.Logics
{
    public class ThrottleLogic
    {
        private readonly SemaphoreSlim sm = new SemaphoreSlim(1);
        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly ILogger<ThrottleLogic> logger;
        private long? lastTime = null;

        public ThrottleLogic(ILogger<ThrottleLogic> logger)
        {
            logger.LogDebug("Creating instance of {class}", nameof(ThrottleLogic));

            this.logger = logger;
            stopwatch.Start();
        }

        public async Task RunAsync(Func<Task> action, int delayMilliseconds)
        {
            var time = stopwatch.ElapsedMilliseconds;
            lastTime = time;
            await sm.WaitAsync();

            try
            {
                logger.LogTrace("Last run {last}. Current time {local}", lastTime, time);
                if (lastTime != null && lastTime != time)
                {
                    // Skip
                    return;
                }

                await action();
                await Task.Delay(delayMilliseconds);
            }
            finally
            {
                sm.Release();
            }
        }
    }
}
