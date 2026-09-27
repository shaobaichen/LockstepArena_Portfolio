using System;
using System.Diagnostics;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync
{
    public sealed class StopwatchTickDriver
    {
        private readonly Stopwatch _stopwatch;
        private readonly ElapsedTickPacer _pacer;
        private long _lastElapsedStopwatchTicks;

        public StopwatchTickDriver(TickDrivenFramePublisher publisher)
        {
            if (publisher is null)
            {
                throw new ArgumentNullException(nameof(publisher));
            }

            _pacer = new ElapsedTickPacer(publisher, Stopwatch.Frequency);
            _stopwatch = Stopwatch.StartNew();
            _lastElapsedStopwatchTicks = _stopwatch.ElapsedTicks;
        }

        public FrameData[] Poll()
        {
            long currentElapsedStopwatchTicks = _stopwatch.ElapsedTicks;
            long elapsedStopwatchTicks = checked(
                currentElapsedStopwatchTicks - _lastElapsedStopwatchTicks);

            FrameData[] publication =
                _pacer.ProcessElapsedStopwatchTicks(elapsedStopwatchTicks);

            _lastElapsedStopwatchTicks = currentElapsedStopwatchTicks;
            return publication;
        }
    }
}
