using System;

namespace LockstepArena.Simulation
{
    public sealed class StrictFrameCollector
    {
        private readonly ActiveRoster _roster;
        private readonly uint _targetTick;
        private readonly InputFrame[] _inputs;
        private readonly bool[] _present;
        private int _acceptedCount;
        private FrameData? _completedFrame;

        public StrictFrameCollector(ActiveRoster roster, uint targetTick)
        {
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _targetTick = targetTick;
            _inputs = new InputFrame[roster.Count];
            _present = new bool[roster.Count];
        }

        public bool IsComplete => _completedFrame is not null;

        public bool Submit(PlayerId submittedPlayerId, InputFrame input)
        {
            if (_completedFrame is not null)
            {
                throw new InvalidOperationException("The frame is already complete.");
            }

            if (input.Tick != _targetTick)
            {
                throw new ArgumentException("Input tick must match the collector tick.", nameof(input));
            }

            PlayerSlot slot = input.PlayerSlot;
            if (slot.Value >= _roster.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(input), "Input slot is outside the active roster.");
            }

            if (!_roster.TryGetSlot(submittedPlayerId, out PlayerSlot assignedSlot))
            {
                throw new ArgumentException("Submitted PlayerId is not in the active roster.", nameof(submittedPlayerId));
            }

            if (assignedSlot != slot)
            {
                throw new ArgumentException("Submitted PlayerId does not own the input slot.", nameof(submittedPlayerId));
            }

            if (_present[slot.Value])
            {
                throw new InvalidOperationException("The input slot has already been accepted.");
            }

            if (_acceptedCount + 1 < _roster.Count)
            {
                _inputs[slot.Value] = input;
                _present[slot.Value] = true;
                _acceptedCount++;
                return false;
            }

            InputFrame[] candidate = (InputFrame[])_inputs.Clone();
            candidate[slot.Value] = input;
            FrameData completed = FrameData.Create(_roster, _targetTick, candidate);

            _inputs[slot.Value] = input;
            _present[slot.Value] = true;
            _acceptedCount++;
            _completedFrame = completed;
            return true;
        }

        public FrameData GetCompletedFrame()
        {
            if (_completedFrame is null)
            {
                throw new InvalidOperationException("The frame is not complete.");
            }

            return _completedFrame;
        }
    }
}
