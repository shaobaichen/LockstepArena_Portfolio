using System;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Protocol
{
    public static class ProtocolMapper
    {
        public static ActiveRosterMessage ToWire(ActiveRoster roster)
        {
            if (roster is null)
            {
                throw new ArgumentNullException(nameof(roster));
            }

            var wire = new ActiveRosterMessage();
            for (int index = 0; index < roster.Count; index++)
            {
                var slot = new PlayerSlot(index);
                wire.Players.Add(new RosterEntryMessage
                {
                    PlayerSlot = checked((uint)index),
                    PlayerId = roster.GetPlayerId(slot).Value,
                });
            }

            return wire;
        }

        public static ActiveRoster ToDomain(ActiveRosterMessage wire)
        {
            if (wire is null)
            {
                throw new ArgumentNullException(nameof(wire));
            }

            int count = wire.Players.Count;
            if (count == 0)
            {
                throw new ProtocolMappingException("Wire roster must contain at least one player.");
            }

            var playerIds = new PlayerId[count];
            var present = new bool[count];
            for (int index = 0; index < count; index++)
            {
                RosterEntryMessage entry = wire.Players[index];
                if (entry is null)
                {
                    throw new ProtocolMappingException("Wire roster cannot contain a null player entry.");
                }

                int slot = ToSlotValue(entry.PlayerSlot, "Wire roster player slot");
                if (slot >= count)
                {
                    throw new ProtocolMappingException("Wire roster player slot is outside the roster count.");
                }

                if (present[slot])
                {
                    throw new ProtocolMappingException("Wire roster cannot contain a duplicate player slot.");
                }

                playerIds[slot] = new PlayerId(entry.PlayerId);
                present[slot] = true;
            }

            for (int index = 0; index < present.Length; index++)
            {
                if (!present[index])
                {
                    throw new ProtocolMappingException("Wire roster player slots must be contiguous.");
                }
            }

            try
            {
                return new ActiveRoster(playerIds);
            }
            catch (ArgumentException exception)
            {
                throw new ProtocolMappingException("Wire roster violates the Simulation roster contract.", exception);
            }
        }

        public static PlayerInputSubmissionMessage ToWire(
            PlayerId submittedPlayerId,
            InputFrame input)
        {
            return new PlayerInputSubmissionMessage
            {
                SubmittedPlayerId = submittedPlayerId.Value,
                Input = ToWire(input),
            };
        }

        public static (PlayerId SubmittedPlayerId, InputFrame Input) ToDomain(
            PlayerInputSubmissionMessage wire)
        {
            if (wire is null)
            {
                throw new ArgumentNullException(nameof(wire));
            }

            if (wire.Input is null)
            {
                throw new ProtocolMappingException("Wire input submission must contain an input message.");
            }

            return (new PlayerId(wire.SubmittedPlayerId), ToDomainInput(wire.Input));
        }

        public static AuthoritativeFrameMessage ToWire(FrameData frame)
        {
            if (frame is null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            var wire = new AuthoritativeFrameMessage
            {
                Tick = frame.Tick,
                Roster = ToWire(frame.Roster),
            };
            for (int index = 0; index < frame.InputCount; index++)
            {
                wire.Inputs.Add(ToWire(frame.GetInput(new PlayerSlot(index))));
            }

            return wire;
        }

        public static FrameData ToDomain(
            AuthoritativeFrameMessage wire,
            ActiveRoster expectedRoster)
        {
            if (wire is null)
            {
                throw new ArgumentNullException(nameof(wire));
            }

            if (expectedRoster is null)
            {
                throw new ArgumentNullException(nameof(expectedRoster));
            }

            if (wire.Roster is null)
            {
                throw new ProtocolMappingException("Wire authoritative frame must contain a roster.");
            }

            ActiveRoster wireRoster = ToDomain(wire.Roster);
            if (!wireRoster.HasSameStructure(expectedRoster))
            {
                throw new ProtocolMappingException("Wire authoritative frame roster does not match the expected roster.");
            }

            var inputs = new InputFrame[wire.Inputs.Count];
            for (int index = 0; index < inputs.Length; index++)
            {
                InputFrameMessage input = wire.Inputs[index];
                if (input is null)
                {
                    throw new ProtocolMappingException("Wire authoritative frame cannot contain a null input.");
                }

                inputs[index] = ToDomainInput(input);
            }

            try
            {
                return FrameData.Create(expectedRoster, wire.Tick, inputs);
            }
            catch (ArgumentException exception)
            {
                throw new ProtocolMappingException(
                    "Wire authoritative frame violates the Simulation frame contract.",
                    exception);
            }
        }

        public static BattleBootstrapMessage ToWireBattleBootstrap(
            ulong battleId,
            BattleState initialState,
            uint battleDurationTicks,
            uint inputDelayTicks,
            uint finalStateTick)
        {
            if (battleId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(battleId));
            }

            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            if (battleDurationTicks == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(battleDurationTicks));
            }

            ulong calculatedFinalTick = (ulong)initialState.Tick + battleDurationTicks;
            if (calculatedFinalTick > uint.MaxValue || finalStateTick != (uint)calculatedFinalTick)
            {
                throw new ArgumentOutOfRangeException(nameof(finalStateTick));
            }

            var wire = new BattleBootstrapMessage
            {
                BattleId = battleId,
                Roster = ToWire(initialState.Roster),
                InitialTick = initialState.Tick,
                BattleDurationTicks = battleDurationTicks,
                InputDelayTicks = inputDelayTicks,
                FinalStateTick = finalStateTick,
                BattleConfigHash = initialState.IsGameplayEnabled
                    ? BattleConfigHash.Compute(initialState.Definition!)
                    : 0UL,
            };
            for (int index = 0; index < initialState.PlayerCount; index++)
            {
                PlayerState state = initialState.GetPlayerState(new PlayerSlot(index));
                wire.PlayerStates.Add(new InitialPlayerStateMessage
                {
                    PlayerSlot = checked((uint)index),
                    PositionX = state.PositionX,
                    PositionZ = state.PositionZ,
                    Aim = state.Aim,
                });
            }

            return wire;
        }

        public static BattleState ToDomainGameplayBattleBootstrap(
            BattleBootstrapMessage wire,
            PlayerId localPlayerId,
            PlayerSlot localPlayerSlot,
            BattleDefinition definition)
        {
            if (wire is null) throw new ArgumentNullException(nameof(wire));
            if (definition is null) throw new ArgumentNullException(nameof(definition));
            ulong expectedHash = BattleConfigHash.Compute(definition);
            if (wire.BattleConfigHash != expectedHash)
            {
                throw new ProtocolMappingException("Battle configuration hash does not match the local deterministic definition.");
            }

            BattleState transported = ToDomainBattleBootstrap(wire, localPlayerId, localPlayerSlot);
            if (transported.Tick != 0)
            {
                throw new ProtocolMappingException("Gameplay battle bootstrap must begin at Tick zero.");
            }

            BattleState gameplay = BattleState.CreateGameplayInitial(transported.Roster, definition);
            for (int index = 0; index < gameplay.PlayerCount; index++)
            {
                PlayerSlot slot = new PlayerSlot(index);
                PlayerState expected = gameplay.GetPlayerState(slot);
                PlayerState actual = transported.GetPlayerState(slot);
                if (expected.PositionX != actual.PositionX ||
                    expected.PositionZ != actual.PositionZ ||
                    expected.Aim != actual.Aim)
                {
                    throw new ProtocolMappingException("Gameplay bootstrap initial state does not match the deterministic definition.");
                }
            }

            return gameplay;
        }

        public static BattleState ToDomainBattleBootstrap(
            BattleBootstrapMessage wire,
            PlayerId localPlayerId,
            PlayerSlot localPlayerSlot)
        {
            if (wire is null)
            {
                throw new ArgumentNullException(nameof(wire));
            }

            if (wire.BattleId == 0)
            {
                throw new ProtocolMappingException("Battle bootstrap must contain a nonzero BattleId.");
            }

            if (wire.Roster is null)
            {
                throw new ProtocolMappingException("Battle bootstrap must contain a roster.");
            }

            ActiveRoster roster = ToDomain(wire.Roster);
            if (localPlayerSlot.Value >= roster.Count || roster.GetPlayerId(localPlayerSlot) != localPlayerId)
            {
                throw new ProtocolMappingException("Local identity does not match the bootstrap roster slot.");
            }

            if (wire.BattleDurationTicks == 0)
            {
                throw new ProtocolMappingException("Battle duration must be positive.");
            }

            ulong calculatedFinalTick = (ulong)wire.InitialTick + wire.BattleDurationTicks;
            if (calculatedFinalTick > uint.MaxValue || wire.FinalStateTick != (uint)calculatedFinalTick)
            {
                throw new ProtocolMappingException("Battle bootstrap Tick arithmetic is invalid.");
            }

            if (wire.PlayerStates.Count != roster.Count)
            {
                throw new ProtocolMappingException("Battle bootstrap state count must match the roster.");
            }

            var states = new PlayerState[roster.Count];
            var present = new bool[roster.Count];
            for (int index = 0; index < wire.PlayerStates.Count; index++)
            {
                InitialPlayerStateMessage state = wire.PlayerStates[index];
                if (state is null)
                {
                    throw new ProtocolMappingException("Battle bootstrap cannot contain a null player state.");
                }

                int slot = ToSlotValue(state.PlayerSlot, "Battle bootstrap player slot");
                if (slot >= roster.Count)
                {
                    throw new ProtocolMappingException("Battle bootstrap player slot is outside the roster.");
                }

                if (present[slot])
                {
                    throw new ProtocolMappingException("Battle bootstrap contains a duplicate player slot.");
                }

                if (state.PositionX < SimulationConfig.ArenaMinX || state.PositionX > SimulationConfig.ArenaMaxX ||
                    state.PositionZ < SimulationConfig.ArenaMinZ || state.PositionZ > SimulationConfig.ArenaMaxZ)
                {
                    throw new ProtocolMappingException("Battle bootstrap position is outside the Simulation arena.");
                }

                if (state.Aim > ushort.MaxValue)
                {
                    throw new ProtocolMappingException("Battle bootstrap aim exceeds the Domain ushort range.");
                }

                states[slot] = new PlayerState(
                    state.PositionX,
                    state.PositionZ,
                    checked((ushort)state.Aim));
                present[slot] = true;
            }

            for (int index = 0; index < present.Length; index++)
            {
                if (!present[index])
                {
                    throw new ProtocolMappingException("Battle bootstrap player-state slots must be contiguous.");
                }
            }

            try
            {
                return new BattleState(wire.InitialTick, roster, states);
            }
            catch (ArgumentException exception)
            {
                throw new ProtocolMappingException("Battle bootstrap violates the Simulation state contract.", exception);
            }
        }

        private static InputFrameMessage ToWire(InputFrame input)
        {
            return new InputFrameMessage
            {
                Tick = input.Tick,
                PlayerSlot = checked((uint)input.PlayerSlot.Value),
                MoveX = input.MoveX,
                MoveZ = input.MoveZ,
                Aim = input.Aim,
                Fire = input.Fire,
            };
        }

        private static InputFrame ToDomainInput(InputFrameMessage wire)
        {
            int slotValue = ToSlotValue(wire.PlayerSlot, "Wire input player slot");
            if (wire.MoveX < -1 || wire.MoveX > 1)
            {
                throw new ProtocolMappingException("Wire move_x must be -1, 0, or 1.");
            }

            if (wire.MoveZ < -1 || wire.MoveZ > 1)
            {
                throw new ProtocolMappingException("Wire move_z must be -1, 0, or 1.");
            }

            if (wire.Aim > ushort.MaxValue)
            {
                throw new ProtocolMappingException("Wire aim exceeds the Domain ushort range.");
            }

            try
            {
                return new InputFrame(
                    wire.Tick,
                    new PlayerSlot(slotValue),
                    checked((sbyte)wire.MoveX),
                    checked((sbyte)wire.MoveZ),
                    checked((ushort)wire.Aim),
                    wire.Fire);
            }
            catch (ArgumentException exception)
            {
                throw new ProtocolMappingException("Wire input violates the Simulation input contract.", exception);
            }
        }

        private static int ToSlotValue(uint wireSlot, string fieldName)
        {
            if (wireSlot > int.MaxValue)
            {
                throw new ProtocolMappingException($"{fieldName} exceeds the Domain int range.");
            }

            return checked((int)wireSlot);
        }
    }
}
