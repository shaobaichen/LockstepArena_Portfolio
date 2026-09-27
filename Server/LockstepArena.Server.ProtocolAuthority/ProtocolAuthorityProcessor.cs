using System;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.FrameSync;
using LockstepArena.Simulation;

namespace LockstepArena.Server.ProtocolAuthority
{
    public sealed class ProtocolAuthorityProcessor
    {
        private readonly AuthoritativeFrameCoordinator? _legacyCoordinator;
        private readonly TickDrivenFramePublisher? _scheduledPublisher;
        private readonly StopwatchTickDriver? _scheduledDriver;
        private readonly BattleSimulation _serverSimulation;
        private bool _faulted;

        public ProtocolAuthorityProcessor(
            BattleState initialState,
            uint maxFutureTickOffset,
            int authoritativeHistoryCapacity)
        {
            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            _legacyCoordinator = new AuthoritativeFrameCoordinator(
                initialState.Roster,
                initialState.Tick,
                maxFutureTickOffset,
                authoritativeHistoryCapacity);
            _scheduledPublisher = null;
            _scheduledDriver = null;
            _serverSimulation = new BattleSimulation(initialState);
        }

        public ProtocolAuthorityProcessor(
            BattleState initialState,
            uint inputDelayTicks,
            uint maxFutureTickOffset,
            int authoritativeHistoryCapacity)
        {
            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            _serverSimulation = new BattleSimulation(initialState);
            _scheduledPublisher = new TickDrivenFramePublisher(
                initialState.Roster,
                initialState.Tick,
                inputDelayTicks,
                maxFutureTickOffset,
                authoritativeHistoryCapacity);
            _legacyCoordinator = null;
            _scheduledDriver = new StopwatchTickDriver(_scheduledPublisher);
        }

        public BattleState ServerState => _serverSimulation.State;

        public uint NextPublishTick => _legacyCoordinator?.NextPublishTick
            ?? _scheduledPublisher!.NextPublishTick;

        public byte[][] SubmitPlayerInputPayload(byte[] completePayload)
        {
            if (_faulted)
            {
                throw new InvalidOperationException("The protocol authority processor is faulted.");
            }

            if (completePayload is null)
            {
                throw new ArgumentNullException(nameof(completePayload));
            }

            PlayerInputSubmissionMessage wire =
                PlayerInputSubmissionMessage.Parser.ParseFrom(completePayload);
            (PlayerId submittedPlayerId, InputFrame input) = ProtocolMapper.ToDomain(wire);
            FrameData[] publication = _legacyCoordinator is not null
                ? _legacyCoordinator.Submit(submittedPlayerId, input)
                : _scheduledPublisher!.Submit(submittedPlayerId, input);

            return ProcessPublication(publication);
        }

        public byte[][] PollAuthority()
        {
            if (_faulted)
            {
                throw new InvalidOperationException("The protocol authority processor is faulted.");
            }

            if (_scheduledDriver is null)
            {
                throw new InvalidOperationException("The protocol authority processor is not scheduled.");
            }

            FrameData[] publication;
            try
            {
                publication = _scheduledDriver.Poll();
            }
            catch
            {
                _faulted = true;
                throw;
            }

            return ProcessPublication(publication);
        }

        private byte[][] ProcessPublication(FrameData[] publication)
        {
            if (publication.Length == 0)
            {
                return Array.Empty<byte[]>();
            }

            try
            {
                var payloads = new byte[publication.Length][];
                for (var index = 0; index < publication.Length; index++)
                {
                    FrameData frame = publication[index];
                    _serverSimulation.Step(frame);
                    payloads[index] = ProtocolMapper.ToWire(frame).ToByteArray();
                }

                return payloads;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }
    }
}
