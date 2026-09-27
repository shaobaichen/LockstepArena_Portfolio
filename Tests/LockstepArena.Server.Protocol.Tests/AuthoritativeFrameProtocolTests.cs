using System;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Server.Protocol.Tests
{
    internal static class AuthoritativeFrameProtocolTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(NullWireFrameIsArgumentNull), NullWireFrameIsArgumentNull),
            new TestCase(nameof(NullExpectedRosterIsArgumentNull), NullExpectedRosterIsArgumentNull),
            new TestCase(nameof(MissingWireRosterIsRejected), MissingWireRosterIsRejected),
            new TestCase(nameof(ShuffledStructurallyEqualWireRosterIsAccepted), ShuffledStructurallyEqualWireRosterIsAccepted),
            new TestCase(nameof(WireRosterCountMismatchIsRejected), WireRosterCountMismatchIsRejected),
            new TestCase(nameof(WireRosterIdentityMismatchIsRejected), WireRosterIdentityMismatchIsRejected),
            new TestCase(nameof(MissingFrameInputIsRejectedByDomain), MissingFrameInputIsRejectedByDomain),
            new TestCase(nameof(DuplicateFrameInputSlotIsRejectedByDomain), DuplicateFrameInputSlotIsRejectedByDomain),
            new TestCase(nameof(UnknownFrameInputSlotIsRejectedByDomain), UnknownFrameInputSlotIsRejectedByDomain),
            new TestCase(nameof(InputTickMismatchIsRejectedByDomain), InputTickMismatchIsRejectedByDomain),
            new TestCase(nameof(ShuffledWireInputsCanonicalizeBySlot), ShuffledWireInputsCanonicalizeBySlot),
            new TestCase(nameof(FrameRoundTripRetainsExpectedRosterInstance), FrameRoundTripRetainsExpectedRosterInstance),
        };

        private static void NullWireFrameIsArgumentNull()
        {
            ActiveRoster roster = CreateRoster();
            TestAssert.Throws<ArgumentNullException>(
                () => ProtocolMapper.ToDomain((AuthoritativeFrameMessage)null!, roster));
        }

        private static void NullExpectedRosterIsArgumentNull()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => ProtocolMapper.ToDomain(new AuthoritativeFrameMessage(), (ActiveRoster)null!));
        }

        private static void MissingWireRosterIsRejected()
        {
            var wire = new AuthoritativeFrameMessage { Tick = 7U };
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire, CreateRoster()));
        }

        private static void ShuffledStructurallyEqualWireRosterIsAccepted()
        {
            ActiveRoster expectedRoster = CreateRoster();
            AuthoritativeFrameMessage wire = CreateCompleteWire(expectedRoster, 2, 0, 3, 1);
            wire.Roster = CreateWireRoster((3U, 44UL), (1U, 17UL), (0U, 91UL), (2U, 73UL));

            FrameData mapped = ProtocolMapper.ToDomain(wire, expectedRoster);

            TestAssert.Same(expectedRoster, mapped.Roster);
            TestAssert.Equal(4, mapped.InputCount);
        }

        private static void WireRosterCountMismatchIsRejected()
        {
            ActiveRoster expectedRoster = CreateRoster();
            AuthoritativeFrameMessage wire = CreateCompleteWire(expectedRoster, 0, 1, 2, 3);
            wire.Roster = CreateWireRoster((0U, 91UL), (1U, 17UL), (2U, 73UL));
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire, expectedRoster));
        }

        private static void WireRosterIdentityMismatchIsRejected()
        {
            ActiveRoster expectedRoster = CreateRoster();
            AuthoritativeFrameMessage wire = CreateCompleteWire(expectedRoster, 0, 1, 2, 3);
            wire.Roster.Players[2].PlayerId = 999UL;
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire, expectedRoster));
        }

        private static void MissingFrameInputIsRejectedByDomain()
        {
            ActiveRoster expectedRoster = CreateRoster();
            AuthoritativeFrameMessage wire = CreateCompleteWire(expectedRoster, 0, 1, 2, 3);
            wire.Inputs.RemoveAt(3);
            AssertDomainFrameFailure(wire, expectedRoster);
        }

        private static void DuplicateFrameInputSlotIsRejectedByDomain()
        {
            ActiveRoster expectedRoster = CreateRoster();
            AuthoritativeFrameMessage wire = CreateCompleteWire(expectedRoster, 0, 1, 2, 3);
            wire.Inputs[3].PlayerSlot = 0U;
            AssertDomainFrameFailure(wire, expectedRoster);
        }

        private static void UnknownFrameInputSlotIsRejectedByDomain()
        {
            ActiveRoster expectedRoster = CreateRoster();
            AuthoritativeFrameMessage wire = CreateCompleteWire(expectedRoster, 0, 1, 2, 3);
            wire.Inputs[3].PlayerSlot = 4U;
            AssertDomainFrameFailure(wire, expectedRoster);
        }

        private static void InputTickMismatchIsRejectedByDomain()
        {
            ActiveRoster expectedRoster = CreateRoster();
            AuthoritativeFrameMessage wire = CreateCompleteWire(expectedRoster, 0, 1, 2, 3);
            wire.Inputs[2].Tick = 8U;
            AssertDomainFrameFailure(wire, expectedRoster);
        }

        private static void ShuffledWireInputsCanonicalizeBySlot()
        {
            ActiveRoster expectedRoster = CreateRoster();
            AuthoritativeFrameMessage wire = CreateCompleteWire(expectedRoster, 2, 0, 3, 1);

            FrameData mapped = ProtocolMapper.ToDomain(wire, expectedRoster);

            for (int index = 0; index < expectedRoster.Count; index++)
            {
                InputFrame input = mapped.GetInput(new PlayerSlot(index));
                TestAssert.Equal(index, input.PlayerSlot.Value);
                TestAssert.Equal(checked((ushort)(1000 + index)), input.Aim);
            }
        }

        private static void FrameRoundTripRetainsExpectedRosterInstance()
        {
            ActiveRoster roster = CreateRoster();
            FrameData frame = CreateFrame(roster, 7U);

            AuthoritativeFrameMessage wire = ProtocolMapper.ToWire(frame);
            FrameData mapped = ProtocolMapper.ToDomain(wire, roster);

            TestAssert.Same(roster, mapped.Roster);
            TestAssert.Equal(7U, mapped.Tick);
            TestAssert.Equal(4, mapped.InputCount);
            for (int index = 0; index < roster.Count; index++)
            {
                InputFrame expected = frame.GetInput(new PlayerSlot(index));
                InputFrame actual = mapped.GetInput(new PlayerSlot(index));
                TestAssert.Equal(expected.Tick, actual.Tick);
                TestAssert.Equal(expected.PlayerSlot, actual.PlayerSlot);
                TestAssert.Equal(expected.MoveX, actual.MoveX);
                TestAssert.Equal(expected.MoveZ, actual.MoveZ);
                TestAssert.Equal(expected.Aim, actual.Aim);
                TestAssert.Equal(expected.Fire, actual.Fire);
            }
        }

        private static void AssertDomainFrameFailure(
            AuthoritativeFrameMessage wire,
            ActiveRoster expectedRoster)
        {
            try
            {
                ProtocolMapper.ToDomain(wire, expectedRoster);
            }
            catch (ProtocolMappingException exception)
            {
                TestAssert.True(exception.InnerException is ArgumentException);
                return;
            }

            throw new InvalidOperationException("Expected a ProtocolMappingException from FrameData.Create.");
        }

        private static AuthoritativeFrameMessage CreateCompleteWire(
            ActiveRoster roster,
            params int[] arrivalSlots)
        {
            var wire = new AuthoritativeFrameMessage
            {
                Tick = 7U,
                Roster = ProtocolMapper.ToWire(roster),
            };
            foreach (int slot in arrivalSlots)
            {
                wire.Inputs.Add(CreateWireInput(7U, slot));
            }

            return wire;
        }

        private static ActiveRosterMessage CreateWireRoster(
            params (uint Slot, ulong PlayerId)[] entries)
        {
            var wire = new ActiveRosterMessage();
            foreach ((uint slot, ulong playerId) in entries)
            {
                wire.Players.Add(new RosterEntryMessage
                {
                    PlayerSlot = slot,
                    PlayerId = playerId,
                });
            }

            return wire;
        }

        private static InputFrameMessage CreateWireInput(uint tick, int slot)
        {
            return new InputFrameMessage
            {
                Tick = tick,
                PlayerSlot = checked((uint)slot),
                MoveX = (slot % 3) - 1,
                MoveZ = ((slot + 1) % 3) - 1,
                Aim = checked((uint)(1000 + slot)),
            };
        }

        private static ActiveRoster CreateRoster()
        {
            return new ActiveRoster(new[]
            {
                new PlayerId(91UL),
                new PlayerId(17UL),
                new PlayerId(73UL),
                new PlayerId(44UL),
            });
        }

        private static FrameData CreateFrame(ActiveRoster roster, uint tick)
        {
            var inputs = new InputFrame[roster.Count];
            for (int index = 0; index < inputs.Length; index++)
            {
                inputs[index] = new InputFrame(
                    tick,
                    new PlayerSlot(index),
                    checked((sbyte)((index % 3) - 1)),
                    checked((sbyte)(((index + 1) % 3) - 1)),
                    checked((ushort)(1000 + index)));
            }

            return FrameData.Create(roster, tick, inputs);
        }
    }
}
