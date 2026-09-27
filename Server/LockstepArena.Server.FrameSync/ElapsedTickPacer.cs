using System;
using System.Collections.Generic;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync
{
    public sealed class ElapsedTickPacer
    {
        private readonly TickDrivenFramePublisher _publisher;
        private readonly ulong _stopwatchFrequency;
        private ulong _fractionalTickNumerator;
        private bool _faulted;

        public ElapsedTickPacer(
            TickDrivenFramePublisher publisher,
            long stopwatchFrequency)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            if (stopwatchFrequency <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(stopwatchFrequency));
            }

            _stopwatchFrequency = checked((ulong)stopwatchFrequency);
            _faulted = false;
        }

        public FrameData[] ProcessElapsedStopwatchTicks(long elapsedStopwatchTicks)
        {
            if (_faulted)
            {
                throw new InvalidOperationException("The elapsed Tick pacer is faulted.");
            }

            if (elapsedStopwatchTicks < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedStopwatchTicks));
            }

            if (_publisher.EligibilityCeiling == uint.MaxValue - 1U)
            {
                return Array.Empty<FrameData>();
            }

            UInt128 scaled =
                (UInt128)_fractionalTickNumerator
                + (UInt128)(ulong)elapsedStopwatchTicks
                * (UInt128)(uint)SimulationConfig.TickRate;
            UInt128 dueAdvances = scaled / (UInt128)_stopwatchFrequency;
            ulong nextRemainder = checked((ulong)(scaled % (UInt128)_stopwatchFrequency));
            List<FrameData> frames = new List<FrameData>();

            while (dueAdvances > UInt128.Zero)
            {
                if (_publisher.EligibilityCeiling == uint.MaxValue - 1U)
                {
                    break;
                }

                FrameData[] publication;
                try
                {
                    publication = _publisher.AdvanceOneTick();
                }
                catch
                {
                    _faulted = true;
                    throw;
                }

                for (int index = 0; index < publication.Length; index++)
                {
                    frames.Add(publication[index]);
                }

                dueAdvances--;
            }

            _fractionalTickNumerator = nextRemainder;
            return frames.Count == 0
                ? Array.Empty<FrameData>()
                : frames.ToArray();
        }
    }
}
