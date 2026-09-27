using System;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync
{
    public sealed class TickDrivenFramePublisher
    {
        private readonly uint _initialTick;
        private readonly AuthoritativeFrameCoordinator _coordinator;
        private ulong _successfulAdvanceCount;

        public TickDrivenFramePublisher(
            ActiveRoster roster,
            uint initialTick,
            uint inputDelayTicks,
            uint maxFutureTickOffset,
            int authoritativeHistoryCapacity)
        {
            if (initialTick == uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialTick),
                    "uint.MaxValue cannot be an initial consumable frame Tick.");
            }

            _coordinator = new AuthoritativeFrameCoordinator(
                roster,
                initialTick,
                maxFutureTickOffset,
                authoritativeHistoryCapacity);
            _initialTick = initialTick;
            InputDelayTicks = inputDelayTicks;
        }

        public ActiveRoster Roster => _coordinator.Roster;

        public uint InputDelayTicks { get; }

        public ulong CollectionTick => (ulong)_initialTick + _successfulAdvanceCount;

        public uint? EligibilityCeiling => GetEligibilityCeiling(_successfulAdvanceCount);

        public uint NextPublishTick => _coordinator.NextPublishTick;

        public FrameData[] Submit(PlayerId submittedPlayerId, InputFrame input)
        {
            _coordinator.Collect(submittedPlayerId, input);
            uint? ceiling = EligibilityCeiling;
            return ceiling.HasValue
                ? _coordinator.PublishThrough(ceiling.Value)
                : Array.Empty<FrameData>();
        }

        public FrameData[] AdvanceOneTick()
        {
            if (EligibilityCeiling == uint.MaxValue - 1U)
            {
                throw new InvalidOperationException("No later consumable Tick can become eligible.");
            }

            ulong nextAdvanceCount = checked(_successfulAdvanceCount + 1UL);
            uint? nextCeiling = GetEligibilityCeiling(nextAdvanceCount);
            FrameData[] publication = nextCeiling.HasValue
                ? _coordinator.PublishThrough(nextCeiling.Value)
                : Array.Empty<FrameData>();
            _successfulAdvanceCount = nextAdvanceCount;
            return publication;
        }

        public FrameData[] GetAuthoritativeHistorySnapshot()
        {
            return _coordinator.GetAuthoritativeHistorySnapshot();
        }

        private uint? GetEligibilityCeiling(ulong successfulAdvanceCount)
        {
            if (successfulAdvanceCount < InputDelayTicks)
            {
                return null;
            }

            ulong candidate = (ulong)_initialTick +
                (successfulAdvanceCount - InputDelayTicks);
            return candidate >= uint.MaxValue
                ? uint.MaxValue - 1U
                : (uint)candidate;
        }
    }
}
