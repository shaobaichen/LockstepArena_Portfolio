using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Server.Protocol.Tests
{
    internal static class ActiveRosterProtocolTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(RosterRoundTripPreservesSlotOrder), RosterRoundTripPreservesSlotOrder),
            new TestCase(nameof(ShuffledRosterEntriesCanonicalizeBySlot), ShuffledRosterEntriesCanonicalizeBySlot),
            new TestCase(nameof(DuplicateRosterSlotIsRejected), DuplicateRosterSlotIsRejected),
            new TestCase(nameof(MissingOrNonContiguousRosterSlotIsRejected), MissingOrNonContiguousRosterSlotIsRejected),
            new TestCase(nameof(RosterSlotAboveIntMaxIsRejected), RosterSlotAboveIntMaxIsRejected),
            new TestCase(nameof(EmptyRosterIsRejected), EmptyRosterIsRejected),
            new TestCase(nameof(ZeroPlayerIdIsAccepted), ZeroPlayerIdIsAccepted),
            new TestCase(nameof(DuplicatePlayerIdIsRejectedByDomain), DuplicatePlayerIdIsRejectedByDomain),
        };

        private static void RosterRoundTripPreservesSlotOrder()
        {
            var roster = new ActiveRoster(new[]
            {
                new PlayerId(91UL),
                new PlayerId(17UL),
                new PlayerId(73UL),
            });

            ActiveRosterMessage wire = ProtocolMapper.ToWire(roster);
            ActiveRoster mapped = ProtocolMapper.ToDomain(wire);

            TestAssert.Equal(3, wire.Players.Count);
            TestAssert.Equal(0U, wire.Players[0].PlayerSlot);
            TestAssert.Equal(91UL, wire.Players[0].PlayerId);
            TestAssert.Equal(1U, wire.Players[1].PlayerSlot);
            TestAssert.Equal(17UL, wire.Players[1].PlayerId);
            TestAssert.Equal(2U, wire.Players[2].PlayerSlot);
            TestAssert.Equal(73UL, wire.Players[2].PlayerId);
            TestAssert.True(roster.HasSameStructure(mapped));
        }

        private static void ShuffledRosterEntriesCanonicalizeBySlot()
        {
            ActiveRosterMessage wire = CreateWire((2U, 73UL), (0U, 91UL), (1U, 17UL));

            ActiveRoster mapped = ProtocolMapper.ToDomain(wire);
            wire.Players[0].PlayerId = 999UL;

            TestAssert.Equal(91UL, mapped.GetPlayerId(new PlayerSlot(0)).Value);
            TestAssert.Equal(17UL, mapped.GetPlayerId(new PlayerSlot(1)).Value);
            TestAssert.Equal(73UL, mapped.GetPlayerId(new PlayerSlot(2)).Value);
        }

        private static void DuplicateRosterSlotIsRejected()
        {
            ActiveRosterMessage wire = CreateWire((0U, 91UL), (0U, 17UL));
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static void MissingOrNonContiguousRosterSlotIsRejected()
        {
            ActiveRosterMessage wire = CreateWire((0U, 91UL), (2U, 17UL), (3U, 73UL));
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static void RosterSlotAboveIntMaxIsRejected()
        {
            ActiveRosterMessage wire = CreateWire((uint.MaxValue, 91UL));
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static void EmptyRosterIsRejected()
        {
            TestAssert.Throws<ProtocolMappingException>(
                () => ProtocolMapper.ToDomain(new ActiveRosterMessage()));
        }

        private static void ZeroPlayerIdIsAccepted()
        {
            ActiveRoster mapped = ProtocolMapper.ToDomain(CreateWire((0U, 0UL)));
            TestAssert.Equal(0UL, mapped.GetPlayerId(new PlayerSlot(0)).Value);
        }

        private static void DuplicatePlayerIdIsRejectedByDomain()
        {
            ActiveRosterMessage wire = CreateWire((0U, 7UL), (1U, 7UL));
            TestAssert.Throws<ProtocolMappingException>(() => ProtocolMapper.ToDomain(wire));
        }

        private static ActiveRosterMessage CreateWire(params (uint Slot, ulong PlayerId)[] entries)
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
    }
}
