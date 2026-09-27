using System;
using LockstepArena.Simulation;

namespace LockstepArena.Client.Prediction
{
    public sealed class ClientPredictionTimeline
    {
        private BattleState _authoritativeState;
        private BattleState _predictedState;
        private PredictionRecord[] _history;
        private readonly int _maxPredictionTicks;
        private bool _faulted;

        public ClientPredictionTimeline(BattleState initialState, int maxPredictionTicks)
        {
            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            if (maxPredictionTicks < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPredictionTicks),
                    "Maximum prediction ticks must be positive.");
            }

            _authoritativeState = initialState;
            _predictedState = initialState;
            _history = Array.Empty<PredictionRecord>();
            _maxPredictionTicks = maxPredictionTicks;
        }

        public BattleState AuthoritativeState => _authoritativeState;

        public BattleState PredictedState => _predictedState;

        public int MaxPredictionTicks => _maxPredictionTicks;

        public int PendingPredictionCount => _history.Length;

        public void Predict(FrameData predictedFrame)
        {
            ThrowIfFaulted();
            if (predictedFrame is null)
            {
                throw new ArgumentNullException(nameof(predictedFrame));
            }

            if (predictedFrame.Tick != _predictedState.Tick)
            {
                throw new ArgumentException(
                    "Predicted frame tick must match the predicted state tick.",
                    nameof(predictedFrame));
            }

            if (!_predictedState.Roster.HasSameStructure(predictedFrame.Roster))
            {
                throw new ArgumentException(
                    "Predicted frame roster must match the prediction roster.",
                    nameof(predictedFrame));
            }

            if (_predictedState.Tick == uint.MaxValue)
            {
                throw new InvalidOperationException("The predicted timeline is exhausted.");
            }

            EnsureInvariantOrFault(_authoritativeState, _predictedState, _history);

            ulong predictionLead = (ulong)_predictedState.Tick - _authoritativeState.Tick;
            if (predictionLead >= (ulong)_maxPredictionTicks)
            {
                throw new InvalidOperationException("Maximum prediction lead has been reached.");
            }

            try
            {
                var simulation = new BattleSimulation(_predictedState);
                simulation.Step(predictedFrame);

                var candidateHistory = new PredictionRecord[_history.Length + 1];
                Array.Copy(_history, candidateHistory, _history.Length);
                candidateHistory[candidateHistory.Length - 1] =
                    new PredictionRecord(_predictedState, predictedFrame);

                BattleState candidatePredictedState = simulation.State;
                EnsureInvariantOrFault(
                    _authoritativeState,
                    candidatePredictedState,
                    candidateHistory);

                _predictedState = candidatePredictedState;
                _history = candidateHistory;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        public bool ReconcileAuthoritative(FrameData authoritativeFrame)
        {
            ThrowIfFaulted();
            if (authoritativeFrame is null)
            {
                throw new ArgumentNullException(nameof(authoritativeFrame));
            }

            if (authoritativeFrame.Tick != _authoritativeState.Tick)
            {
                throw new ArgumentException(
                    "Authoritative frame tick must match the authoritative state tick.",
                    nameof(authoritativeFrame));
            }

            if (!_authoritativeState.Roster.HasSameStructure(authoritativeFrame.Roster))
            {
                throw new ArgumentException(
                    "Authoritative frame roster must match the prediction roster.",
                    nameof(authoritativeFrame));
            }

            if (_authoritativeState.Tick == uint.MaxValue)
            {
                throw new InvalidOperationException("The authoritative timeline is exhausted.");
            }

            EnsureInvariantOrFault(_authoritativeState, _predictedState, _history);

            if (_history.Length == 0)
            {
                return ReconcileAtAlignedFrontier(authoritativeFrame);
            }

            if (!FramesHaveSameValue(_history[0].PredictedFrame, authoritativeFrame))
            {
                return ReconcileDirty(authoritativeFrame);
            }

            return ReconcileClean(authoritativeFrame);
        }

        private bool ReconcileAtAlignedFrontier(FrameData authoritativeFrame)
        {
            try
            {
                var simulation = new BattleSimulation(_authoritativeState);
                simulation.Step(authoritativeFrame);

                BattleState candidateState = simulation.State;
                PredictionRecord[] candidateHistory = Array.Empty<PredictionRecord>();
                EnsureInvariantOrFault(candidateState, candidateState, candidateHistory);

                _authoritativeState = candidateState;
                _predictedState = candidateState;
                _history = candidateHistory;
                return false;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        private bool ReconcileClean(FrameData authoritativeFrame)
        {
            try
            {
                var simulation = new BattleSimulation(_authoritativeState);
                simulation.Step(authoritativeFrame);

                var candidateHistory = new PredictionRecord[_history.Length - 1];
                Array.Copy(_history, 1, candidateHistory, 0, candidateHistory.Length);

                BattleState candidateAuthoritativeState = simulation.State;
                EnsureInvariantOrFault(
                    candidateAuthoritativeState,
                    _predictedState,
                    candidateHistory);

                _authoritativeState = candidateAuthoritativeState;
                _history = candidateHistory;
                return false;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        private bool ReconcileDirty(FrameData authoritativeFrame)
        {
            try
            {
                PredictionRecord dirtyRecord = _history[0];
                if (!BattleStateValueComparer.HaveSameValue(dirtyRecord.StateBefore, _authoritativeState))
                {
                    _faulted = true;
                    throw new InvalidOperationException(
                        "Dirty rollback state does not match the authoritative frontier.");
                }

                var authoritativeSimulation = new BattleSimulation(_authoritativeState);
                authoritativeSimulation.Step(authoritativeFrame);
                BattleState candidateAuthoritativeState = authoritativeSimulation.State;

                var candidateHistory = new PredictionRecord[_history.Length - 1];
                var predictedSimulation = new BattleSimulation(candidateAuthoritativeState);
                for (int sourceIndex = 1; sourceIndex < _history.Length; sourceIndex++)
                {
                    FrameData retainedFrame = _history[sourceIndex].PredictedFrame;
                    candidateHistory[sourceIndex - 1] =
                        new PredictionRecord(predictedSimulation.State, retainedFrame);
                    predictedSimulation.Step(retainedFrame);
                }

                BattleState candidatePredictedState = predictedSimulation.State;
                if (candidatePredictedState.Tick != _predictedState.Tick)
                {
                    _faulted = true;
                    throw new InvalidOperationException(
                        "Dirty replay did not reconstruct the predicted frontier.");
                }

                EnsureInvariantOrFault(
                    candidateAuthoritativeState,
                    candidatePredictedState,
                    candidateHistory);

                _authoritativeState = candidateAuthoritativeState;
                _predictedState = candidatePredictedState;
                _history = candidateHistory;
                return true;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        private void ThrowIfFaulted()
        {
            if (_faulted)
            {
                throw new InvalidOperationException("The prediction timeline is faulted.");
            }
        }

        private void EnsureInvariantOrFault(
            BattleState authoritativeState,
            BattleState predictedState,
            PredictionRecord[] history)
        {
            if (!HasValidInvariant(authoritativeState, predictedState, history))
            {
                _faulted = true;
                throw new InvalidOperationException("The prediction timeline invariant is invalid.");
            }
        }

        private bool HasValidInvariant(
            BattleState authoritativeState,
            BattleState predictedState,
            PredictionRecord[] history)
        {
            if (authoritativeState is null || predictedState is null || history is null)
            {
                return false;
            }

            uint authoritativeTick = authoritativeState.Tick;
            uint predictedTick = predictedState.Tick;
            if (authoritativeTick > predictedTick ||
                !authoritativeState.Roster.HasSameStructure(predictedState.Roster))
            {
                return false;
            }

            ulong lead = (ulong)predictedTick - authoritativeTick;
            if (lead != (ulong)history.Length || history.Length > _maxPredictionTicks)
            {
                return false;
            }

            if (history.Length == 0)
            {
                return authoritativeTick == predictedTick;
            }

            if (history[0] is null)
            {
                return false;
            }

            if (!BattleStateValueComparer.HaveSameValue(history[0].StateBefore, authoritativeState))
            {
                return false;
            }

            for (int index = 0; index < history.Length; index++)
            {
                PredictionRecord record = history[index];
                if (record is null)
                {
                    return false;
                }

                ulong expectedTickWide = (ulong)authoritativeTick + (uint)index;
                if (expectedTickWide > uint.MaxValue)
                {
                    return false;
                }

                uint expectedTick = (uint)expectedTickWide;
                if (record.PredictedFrame.Tick != expectedTick ||
                    record.StateBefore.Tick != expectedTick ||
                    !authoritativeState.Roster.HasSameStructure(record.PredictedFrame.Roster) ||
                    !authoritativeState.Roster.HasSameStructure(record.StateBefore.Roster))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FramesHaveSameValue(FrameData left, FrameData right)
        {
            if (left.Tick != right.Tick ||
                left.InputCount != right.InputCount ||
                !left.Roster.HasSameStructure(right.Roster))
            {
                return false;
            }

            for (int index = 0; index < left.InputCount; index++)
            {
                PlayerSlot slot = new PlayerSlot(index);
                InputFrame leftInput = left.GetInput(slot);
                InputFrame rightInput = right.GetInput(slot);
                if (leftInput.PlayerSlot != rightInput.PlayerSlot ||
                    leftInput.MoveX != rightInput.MoveX ||
                    leftInput.MoveZ != rightInput.MoveZ ||
                    leftInput.Aim != rightInput.Aim ||
                    leftInput.Fire != rightInput.Fire)
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class PredictionRecord
        {
            public PredictionRecord(BattleState stateBefore, FrameData predictedFrame)
            {
                StateBefore = stateBefore;
                PredictedFrame = predictedFrame;
            }

            public BattleState StateBefore { get; }

            public FrameData PredictedFrame { get; }
        }
    }
}
