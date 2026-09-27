using System;
using System.Collections.Generic;

namespace LockstepArena.Simulation
{
    public sealed class FrameData
    {
        private readonly InputFrame[] _inputs;

        private FrameData(ActiveRoster roster, uint tick, InputFrame[] canonicalInputs)
        {
            Roster = roster;
            Tick = tick;
            _inputs = canonicalInputs;
        }

        public uint Tick { get; }

        public ActiveRoster Roster { get; }

        public int InputCount => _inputs.Length;

        public static FrameData Create(
            ActiveRoster roster,
            uint tick,
            IReadOnlyList<InputFrame> receivedInputs)
        {
            if (roster is null)
            {
                throw new ArgumentNullException(nameof(roster));
            }

            if (receivedInputs is null)
            {
                throw new ArgumentNullException(nameof(receivedInputs));
            }

            if (receivedInputs.Count != roster.Count)
            {
                throw new ArgumentException(
                    "A frame must contain exactly one input for every active roster slot.",
                    nameof(receivedInputs));
            }

            InputFrame[] canonicalInputs = new InputFrame[roster.Count];
            bool[] present = new bool[roster.Count];
            for (int index = 0; index < receivedInputs.Count; index++)
            {
                InputFrame input = receivedInputs[index];
                if (input.Tick != tick)
                {
                    throw new ArgumentException("Every input must match the frame tick.", nameof(receivedInputs));
                }

                PlayerSlot slot = input.PlayerSlot;
                if (slot.Value >= roster.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(receivedInputs),
                        "Input slot is outside the active roster.");
                }

                if (present[slot.Value])
                {
                    throw new ArgumentException("A frame cannot contain a duplicate player slot.", nameof(receivedInputs));
                }

                canonicalInputs[slot.Value] = input;
                present[slot.Value] = true;
            }

            for (int index = 0; index < present.Length; index++)
            {
                if (!present[index])
                {
                    throw new ArgumentException("A frame is missing an active roster slot.", nameof(receivedInputs));
                }
            }

            return new FrameData(roster, tick, canonicalInputs);
        }

        public InputFrame GetInput(PlayerSlot slot)
        {
            if (slot.Value >= _inputs.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "Player slot is outside the frame roster.");
            }

            return _inputs[slot.Value];
        }
    }
}
