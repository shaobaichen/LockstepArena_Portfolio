using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Server.Protocol.Tests
{
    internal static class InputFrameProtocolTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(InputSubmissionRoundTripPreservesValues), InputSubmissionRoundTripPreservesValues),
            new TestCase(nameof(MissingNestedInputIsRejected), MissingNestedInputIsRejected),
            new TestCase(nameof(InputSlotAboveIntMaxIsRejected), InputSlotAboveIntMaxIsRejected),
            new TestCase(nameof(MoveXBelowMinusOneIsRejected), MoveXBelowMinusOneIsRejected),
            new TestCase(nameof(MoveXAboveOneIsRejected), MoveXAboveOneIsRejected),
            new TestCase(nameof(MoveZBelowMinusOneIsRejected), MoveZBelowMinusOneIsRejected),
            new TestCase(nameof(MoveZAboveOneIsRejected), MoveZAboveOneIsRejected),
            new TestCase(nameof(AimAboveUshortMaxIsRejected), AimAboveUshortMaxIsRejected),
            new TestCase(nameof(Proto3ZeroValuesRemainValidDomainZeros), Proto3ZeroValuesRemainValidDomainZeros),
        };

        private static void InputSubmissionRoundTripPreservesValues()
        {
            var submittedPlayerId = new PlayerId(0xFFEEDDCCBBAA0099UL);
            var input = new InputFrame(42U, new PlayerSlot(2), -1, 1, ushort.MaxValue, true);

            PlayerInputSubmissionMessage wire = ProtocolMapper.ToWire(submittedPlayerId, input);
            (PlayerId mappedPlayerId, InputFrame mappedInput) = ProtocolMapper.ToDomain(wire);

            TestAssert.Equal(submittedPlayerId.Value, wire.SubmittedPlayerId);
            TestAssert.Equal(input.Tick, wire.Input.Tick);
            TestAssert.Equal(2U, wire.Input.PlayerSlot);
            TestAssert.Equal(-1, wire.Input.MoveX);
            TestAssert.Equal(1, wire.Input.MoveZ);
            TestAssert.Equal((uint)ushort.MaxValue, wire.Input.Aim);
            TestAssert.True(wire.Input.Fire);
            TestAssert.Equal(submittedPlayerId, mappedPlayerId);
            AssertInput(input, mappedInput);
        }

        private static void MissingNestedInputIsRejected()
        {
            var wire = new PlayerInputSubmissionMessage { SubmittedPlayerId = 91UL };
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static void InputSlotAboveIntMaxIsRejected()
        {
            PlayerInputSubmissionMessage wire = CreateWire();
            wire.Input.PlayerSlot = uint.MaxValue;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static void MoveXBelowMinusOneIsRejected()
        {
            PlayerInputSubmissionMessage wire = CreateWire();
            wire.Input.MoveX = -2;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static void MoveXAboveOneIsRejected()
        {
            PlayerInputSubmissionMessage wire = CreateWire();
            wire.Input.MoveX = 2;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static void MoveZBelowMinusOneIsRejected()
        {
            PlayerInputSubmissionMessage wire = CreateWire();
            wire.Input.MoveZ = -2;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static void MoveZAboveOneIsRejected()
        {
            PlayerInputSubmissionMessage wire = CreateWire();
            wire.Input.MoveZ = 2;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static void AimAboveUshortMaxIsRejected()
        {
            PlayerInputSubmissionMessage wire = CreateWire();
            wire.Input.Aim = (uint)ushort.MaxValue + 1U;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static void Proto3ZeroValuesRemainValidDomainZeros()
        {
            var wire = new PlayerInputSubmissionMessage { Input = new InputFrameMessage() };

            (PlayerId playerId, InputFrame input) = ProtocolMapper.ToDomain(wire);

            TestAssert.Equal(0UL, playerId.Value);
            TestAssert.Equal(0U, input.Tick);
            TestAssert.Equal(0, input.PlayerSlot.Value);
            TestAssert.Equal((sbyte)0, input.MoveX);
            TestAssert.Equal((sbyte)0, input.MoveZ);
            TestAssert.Equal((ushort)0, input.Aim);
            TestAssert.Equal(false, input.Fire);
        }

        private static PlayerInputSubmissionMessage CreateWire()
        {
            return new PlayerInputSubmissionMessage
            {
                SubmittedPlayerId = 91UL,
                Input = new InputFrameMessage
                {
                    Tick = 7U,
                    PlayerSlot = 0U,
                    MoveX = 0,
                    MoveZ = 0,
                    Aim = 1234U,
                },
            };
        }

        private static void AssertInput(InputFrame expected, InputFrame actual)
        {
            TestAssert.Equal(expected.Tick, actual.Tick);
            TestAssert.Equal(expected.PlayerSlot, actual.PlayerSlot);
            TestAssert.Equal(expected.MoveX, actual.MoveX);
            TestAssert.Equal(expected.MoveZ, actual.MoveZ);
            TestAssert.Equal(expected.Aim, actual.Aim);
            TestAssert.Equal(expected.Fire, actual.Fire);
        }
    }
}
