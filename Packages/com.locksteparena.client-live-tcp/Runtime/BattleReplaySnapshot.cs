using System;
using System.Collections.Generic;
using LockstepArena.Simulation;

namespace LockstepArena.Client.LiveTcp
{
    public sealed class BattleReplaySnapshot
    {
        public BattleReplaySnapshot(BattleState initialState, IReadOnlyList<FrameData> authoritativeFrames)
        {
            InitialState = initialState ?? throw new ArgumentNullException(nameof(initialState));
            if (authoritativeFrames is null) throw new ArgumentNullException(nameof(authoritativeFrames));
            var copy = new FrameData[authoritativeFrames.Count];
            for (int index = 0; index < copy.Length; index++)
                copy[index] = authoritativeFrames[index] ?? throw new ArgumentException(
                    "A replay frame cannot be null.", nameof(authoritativeFrames));
            // BattleState and FrameData are immutable; only the collection needs an owned copy.
            AuthoritativeFrames = Array.AsReadOnly(copy);
        }

        public BattleState InitialState { get; }
        public IReadOnlyList<FrameData> AuthoritativeFrames { get; }
    }
}
