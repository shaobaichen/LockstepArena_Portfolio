using System;
using System.Net.Sockets;
using LockstepArena.Client.Prediction;
using LockstepArena.Simulation;

namespace LockstepArena.Client.LiveTcp
{
    public readonly struct LocalInputSample
    {
        public LocalInputSample(sbyte moveX, sbyte moveZ, ushort aim)
            : this(moveX, moveZ, aim, false)
        {
        }

        public LocalInputSample(sbyte moveX, sbyte moveZ, ushort aim, bool fire)
        {
            if (moveX < -1 || moveX > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(moveX));
            }

            if (moveZ < -1 || moveZ > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(moveZ));
            }

            MoveX = moveX;
            MoveZ = moveZ;
            Aim = aim;
            Fire = fire;
        }

        public sbyte MoveX { get; }

        public sbyte MoveZ { get; }

        public ushort Aim { get; }

        public bool Fire { get; }
    }

    public readonly struct PredictedClientUpdateResult
    {
        internal PredictedClientUpdateResult(
            int reconciledAuthoritativeFrameCount,
            int dirtyFrameCount,
            bool localPredictionSent)
        {
            ReconciledAuthoritativeFrameCount = reconciledAuthoritativeFrameCount;
            DirtyFrameCount = dirtyFrameCount;
            LocalPredictionSent = localPredictionSent;
        }

        public int ReconciledAuthoritativeFrameCount { get; }

        public int DirtyFrameCount { get; }

        public bool LocalPredictionSent { get; }
    }

    public sealed class PredictedTcpClientBattleRuntime : IDisposable
    {
        private readonly TcpClientBattlePump _transport;
        private readonly ClientPredictionTimeline _timeline;
        private readonly BattleState _initialState;
        private readonly InputFrame?[] _remoteAuthorityCache;
        private readonly int _maxAuthoritativeFramesPerUpdate;
        private readonly int _maxPendingAuthoritativeFrames;
        private readonly int _maxReplayFrames;
        private FrameData[] _pendingAuthority;
        private FrameData[] _replay;
        private bool _disposed;
        private bool _faulted;

        public PredictedTcpClientBattleRuntime(
            TcpClient connectedClient,
            BattleState initialState,
            PlayerId localPlayerId,
            PlayerSlot localPlayerSlot,
            int maxPredictionTicks,
            int maxAuthoritativeFramesPerUpdate,
            int maxPendingAuthoritativeFrames,
            int maxReplayFrames,
            int maxPayloadLength,
            int receiveBufferLength,
            int receiveOffset,
            int receiveReadCapacity)
        {
            if (connectedClient is null)
            {
                throw new ArgumentNullException(nameof(connectedClient));
            }

            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            if (localPlayerSlot.Value >= initialState.Roster.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerSlot));
            }

            if (initialState.Roster.GetPlayerId(localPlayerSlot) != localPlayerId)
            {
                throw new ArgumentException(
                    "Local PlayerId does not match the local roster slot.",
                    nameof(localPlayerId));
            }

            if (maxPredictionTicks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPredictionTicks));
            }

            if (maxAuthoritativeFramesPerUpdate < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAuthoritativeFramesPerUpdate));
            }

            if (maxPendingAuthoritativeFrames < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPendingAuthoritativeFrames));
            }

            if (maxReplayFrames < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxReplayFrames));
            }

            var timeline = new ClientPredictionTimeline(initialState, maxPredictionTicks);
            TcpClientBattlePump transport = TcpClientBattlePump.CreateTransportOnly(
                connectedClient,
                initialState.Roster,
                maxPayloadLength,
                receiveBufferLength,
                receiveOffset,
                receiveReadCapacity);

            LocalPlayerId = localPlayerId;
            LocalPlayerSlot = localPlayerSlot;
            _initialState = initialState;
            _timeline = timeline;
            _transport = transport;
            _remoteAuthorityCache = new InputFrame?[initialState.Roster.Count];
            _pendingAuthority = Array.Empty<FrameData>();
            _replay = Array.Empty<FrameData>();
            _maxAuthoritativeFramesPerUpdate = maxAuthoritativeFramesPerUpdate;
            _maxPendingAuthoritativeFrames = maxPendingAuthoritativeFrames;
            _maxReplayFrames = maxReplayFrames;
        }

        public PlayerId LocalPlayerId { get; }

        public PlayerSlot LocalPlayerSlot { get; }

        public BattleState AuthoritativeState => _timeline.AuthoritativeState;

        public BattleState PredictedState => _timeline.PredictedState;

        public int PendingPredictionCount => _timeline.PendingPredictionCount;

        public int MaxPredictionTicks => _timeline.MaxPredictionTicks;

        public int PendingAuthoritativeFrameCount => _pendingAuthority.Length;

        public int ReplayFrameCount => _replay.Length;

        public int MaxAuthoritativeFramesPerUpdate => _maxAuthoritativeFramesPerUpdate;

        public int MaxPendingAuthoritativeFrames => _maxPendingAuthoritativeFrames;

        public int MaxReplayFrames => _maxReplayFrames;

        public PredictedClientUpdateResult Update(LocalInputSample? localSample)
        {
            ThrowIfDisposed();
            ThrowIfFaulted();

            try

            {
                if (_pendingAuthority.Length == 0)
                {
                    AdmitReceived(_transport.PumpReceiveTransportOnlyOnce());
                }

                int reconciled = 0;
                int dirty = 0;
                while (reconciled < _maxAuthoritativeFramesPerUpdate &&
                    _pendingAuthority.Length > 0)
                {
                    if (ReconcileOldest())
                    {
                        dirty++;
                    }

                    reconciled++;
                }

                bool sent = false;
                if (localSample.HasValue &&
                    _timeline.PredictedState.Tick != uint.MaxValue &&
                    _timeline.PendingPredictionCount < _timeline.MaxPredictionTicks)
                {
                    FrameData prediction = CreatePrediction(localSample.Value);
                    _timeline.Predict(prediction);
                    _transport.SendInput(
                        LocalPlayerId,
                        prediction.GetInput(LocalPlayerSlot));
                    sent = true;
                }

                return new PredictedClientUpdateResult(reconciled, dirty, sent);
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }

        public BattleReplaySnapshot CaptureReplaySnapshot()
        {
            ThrowIfDisposed();
            ThrowIfFaulted();
            return new BattleReplaySnapshot(_initialState, _replay);
        }

        public BattleState ReconstructAuthoritativeState()
        {
            ThrowIfDisposed();
            ThrowIfFaulted();
            try
            {
                var simulation = new BattleSimulation(_initialState);
                for (int index = 0; index < _replay.Length; index++)
                {
                    simulation.Step(_replay[index]);
                }

                BattleState reconstructed = simulation.State;
                if (!BattleStateValueComparer.HaveSameValue(reconstructed, _timeline.AuthoritativeState) ||
                    StateDigest.Compute(reconstructed) !=
                        StateDigest.Compute(_timeline.AuthoritativeState))
                {
                    throw new InvalidOperationException(
                        "Authoritative Replay does not match the live authoritative state.");
                }

                return reconstructed;
            }
            catch (Exception exception)
            {
                _faulted = true;
                if (exception is InvalidOperationException)
                {
                    throw;
                }

                throw new InvalidOperationException(
                    "Authoritative Replay reconstruction failed.",
                    exception);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _transport.Dispose();
        }

        private void AdmitReceived(FrameData[] received)
        {
            if (received.Length == 0)
            {
                return;
            }

            ulong pendingAfter = (ulong)_pendingAuthority.Length +
                checked((uint)received.Length);
            if (pendingAfter > checked((uint)_maxPendingAuthoritativeFrames))
            {
                throw new InvalidOperationException(
                    "The pending authoritative frame capacity would be exceeded.");
            }

            ulong replayReservation = (ulong)_replay.Length + pendingAfter;
            if (replayReservation > checked((uint)_maxReplayFrames))
            {
                throw new InvalidOperationException(
                    "The authoritative Replay capacity would be exceeded.");
            }

            ulong expectedTick = (ulong)_timeline.AuthoritativeState.Tick +
                checked((uint)_pendingAuthority.Length);
            for (int index = 0; index < received.Length; index++)
            {
                FrameData frame = received[index] ?? throw new InvalidOperationException(
                    "A received authoritative frame cannot be null.");
                if (!_timeline.AuthoritativeState.Roster.HasSameStructure(frame.Roster))
                {
                    throw new InvalidOperationException(
                        "A received authoritative frame has a different roster.");
                }

                if (expectedTick >= uint.MaxValue || frame.Tick != (uint)expectedTick)
                {
                    throw new InvalidOperationException(
                        "Received authoritative frames must be contiguous from the authoritative frontier.");
                }

                expectedTick++;
            }

            var candidate = new FrameData[_pendingAuthority.Length + received.Length];
            Array.Copy(_pendingAuthority, candidate, _pendingAuthority.Length);
            Array.Copy(received, 0, candidate, _pendingAuthority.Length, received.Length);
            _pendingAuthority = candidate;
        }

        private bool ReconcileOldest()
        {
            FrameData authority = _pendingAuthority[0];
            var candidateReplay = new FrameData[_replay.Length + 1];
            Array.Copy(_replay, candidateReplay, _replay.Length);
            candidateReplay[candidateReplay.Length - 1] = authority;

            var candidateRemoteAuthorityCache = new InputFrame?[_remoteAuthorityCache.Length];
            Array.Copy(
                _remoteAuthorityCache,
                candidateRemoteAuthorityCache,
                _remoteAuthorityCache.Length);
            for (int index = 0; index < authority.InputCount; index++)
            {
                var slot = new PlayerSlot(index);
                if (slot != LocalPlayerSlot)
                {
                    candidateRemoteAuthorityCache[index] = authority.GetInput(slot);
                }
            }

            var candidatePending = new FrameData[_pendingAuthority.Length - 1];
            Array.Copy(_pendingAuthority, 1, candidatePending, 0, candidatePending.Length);

            bool dirty = _timeline.ReconcileAuthoritative(authority);
            _replay = candidateReplay;
            Array.Copy(
                candidateRemoteAuthorityCache,
                _remoteAuthorityCache,
                candidateRemoteAuthorityCache.Length);
            _pendingAuthority = candidatePending;
            return dirty;
        }

        private FrameData CreatePrediction(LocalInputSample localSample)
        {
            BattleState predictedState = _timeline.PredictedState;
            uint tick = predictedState.Tick;
            var inputs = new InputFrame[predictedState.Roster.Count];
            for (int index = 0; index < inputs.Length; index++)
            {
                var slot = new PlayerSlot(index);
                if (slot == LocalPlayerSlot)
                {
                    inputs[index] = new InputFrame(
                        tick,
                        slot,
                        localSample.MoveX,
                        localSample.MoveZ,
                        localSample.Aim,
                        localSample.Fire);
                    continue;
                }

                InputFrame? latest = _remoteAuthorityCache[index];
                if (latest.HasValue)
                {
                    InputFrame value = latest.Value;
                    inputs[index] = new InputFrame(
                        tick,
                        slot,
                        value.MoveX,
                        value.MoveZ,
                        value.Aim,
                        value.Fire);
                }
                else
                {
                    inputs[index] = new InputFrame(
                        tick,
                        slot,
                        0,
                        0,
                        predictedState.GetPlayerState(slot).Aim);
                }
            }

            return FrameData.Create(predictedState.Roster, tick, inputs);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PredictedTcpClientBattleRuntime));
            }
        }

        private void ThrowIfFaulted()
        {
            if (_faulted)
            {
                throw new InvalidOperationException("The predicted TCP client runtime is faulted.");
            }
        }

    }
}
