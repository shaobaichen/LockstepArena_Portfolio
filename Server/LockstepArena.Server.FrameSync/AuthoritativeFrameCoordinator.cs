using System;
using System.Collections.Generic;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync
{
    public sealed class AuthoritativeFrameCoordinator
    {
        private readonly uint _maxFutureTickOffset;
        private readonly int _authoritativeHistoryCapacity;
        private Dictionary<uint, StrictFrameCollector> _pendingByTick;
        private Queue<FrameData> _authoritativeHistory;

        public AuthoritativeFrameCoordinator(
            ActiveRoster roster,
            uint initialPublishTick,
            uint maxFutureTickOffset,
            int authoritativeHistoryCapacity)
        {
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            if (authoritativeHistoryCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(authoritativeHistoryCapacity));
            }

            NextPublishTick = initialPublishTick;
            _maxFutureTickOffset = maxFutureTickOffset;
            _authoritativeHistoryCapacity = authoritativeHistoryCapacity;
            _pendingByTick = new Dictionary<uint, StrictFrameCollector>();
            _authoritativeHistory = new Queue<FrameData>();
        }

        public ActiveRoster Roster { get; }

        public uint NextPublishTick { get; private set; }

        public FrameData[] Submit(PlayerId submittedPlayerId, InputFrame input)
        {
            Collect(submittedPlayerId, input);
            return PublishThrough(uint.MaxValue - 1U);
        }

        internal void Collect(PlayerId submittedPlayerId, InputFrame input)
        {
            if (input.Tick == uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    "uint.MaxValue cannot be a consumable frame Tick.");
            }

            if (NextPublishTick == uint.MaxValue)
            {
                throw new InvalidOperationException("The coordinator has no publishable Tick remaining.");
            }

            if (input.Tick < NextPublishTick)
            {
                throw new ArgumentOutOfRangeException(nameof(input), "Input Tick is older than NextPublishTick.");
            }

            ulong upperBound = Math.Min(
                (ulong)NextPublishTick + _maxFutureTickOffset,
                (ulong)uint.MaxValue - 1UL);
            if ((ulong)input.Tick > upperBound)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    "Input Tick exceeds the accepted future window.");
            }

            if (!_pendingByTick.TryGetValue(input.Tick, out StrictFrameCollector? collector))
            {
                StrictFrameCollector candidate = new StrictFrameCollector(Roster, input.Tick);
                candidate.Submit(submittedPlayerId, input);
                _pendingByTick.Add(input.Tick, candidate);
                collector = candidate;
            }
            else
            {
                collector.Submit(submittedPlayerId, input);
            }
        }

        internal FrameData[] PublishThrough(uint inclusiveEligibilityCeiling)
        {
            if (inclusiveEligibilityCeiling < NextPublishTick)
            {
                return Array.Empty<FrameData>();
            }

            List<FrameData> frames = new List<FrameData>();
            ulong scanTick = NextPublishTick;
            while (scanTick <= inclusiveEligibilityCeiling && scanTick < uint.MaxValue)
            {
                uint tick = (uint)scanTick;
                if (!_pendingByTick.TryGetValue(tick, out StrictFrameCollector? pending) ||
                    !pending.IsComplete)
                {
                    break;
                }

                frames.Add(pending.GetCompletedFrame());
                scanTick = tick == uint.MaxValue - 1U
                    ? uint.MaxValue
                    : scanTick + 1UL;
            }

            if (frames.Count == 0)
            {
                return Array.Empty<FrameData>();
            }

            FrameData[] publication = frames.ToArray();
            uint nextPublishTickAfterBatch = checked((uint)scanTick);

            Dictionary<uint, StrictFrameCollector> pendingAfter =
                new Dictionary<uint, StrictFrameCollector>(_pendingByTick);
            for (int index = 0; index < publication.Length; index++)
            {
                if (!pendingAfter.Remove(publication[index].Tick))
                {
                    throw new InvalidOperationException(
                        "A planned publication Tick was absent from pending storage.");
                }
            }

            Queue<FrameData> historyAfter = new Queue<FrameData>(_authoritativeHistory);
            for (int index = 0; index < publication.Length; index++)
            {
                historyAfter.Enqueue(publication[index]);
                while (historyAfter.Count > _authoritativeHistoryCapacity)
                {
                    historyAfter.Dequeue();
                }
            }

            _pendingByTick = pendingAfter;
            _authoritativeHistory = historyAfter;
            NextPublishTick = nextPublishTickAfterBatch;
            return publication;
        }

        public FrameData[] GetAuthoritativeHistorySnapshot()
        {
            return _authoritativeHistory.ToArray();
        }
    }
}
