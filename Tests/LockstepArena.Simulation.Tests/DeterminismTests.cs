using System;
using System.Collections.Generic;

namespace LockstepArena.Simulation.Tests
{
    internal static class DeterminismTests
    {
        private const int TwinTickCount = 10_000;
        private const int HistoryTickCount = 2_000;

        public static TestCase[] All { get; } =
        {
            new TestCase(nameof(EqualVariablePlayerStatesHaveEqualDigests), EqualVariablePlayerStatesHaveEqualDigests),
            new TestCase(nameof(CanonicalStateChangesAlterDigest), CanonicalStateChangesAlterDigest),
            new TestCase(nameof(RosterIdentityAndOrderChangesAlterDigest), RosterIdentityAndOrderChangesAlterDigest),
            new TestCase(nameof(GoldenDigestLocksVariableRosterFieldAndByteOrder), GoldenDigestLocksVariableRosterFieldAndByteOrder),
            new TestCase(nameof(FourPlayerTwinSimulationsMatchDigestAtEveryTick), FourPlayerTwinSimulationsMatchDigestAtEveryTick),
            new TestCase(nameof(InitialStateAndThreePlayerFrameHistoryRebuildFinalDigest), InitialStateAndThreePlayerFrameHistoryRebuildFinalDigest),
            new TestCase(nameof(GameplayDigestCoversStateAndDefinition), GameplayDigestCoversStateAndDefinition),
            new TestCase(nameof(GameplayStateComparerUsesStructuralValues), GameplayStateComparerUsesStructuralValues),
        };

        private static void EqualVariablePlayerStatesHaveEqualDigests()
        {
            BattleState first = new BattleState(77, Roster(90, 10, 70), new[]
            {
                new PlayerState(-2_300, 1_200, 456),
                new PlayerState(4_500, -2_900, 65_000),
                new PlayerState(300, -400, 12_345),
            });
            BattleState second = new BattleState(77, Roster(90, 10, 70), new[]
            {
                new PlayerState(-2_300, 1_200, 456),
                new PlayerState(4_500, -2_900, 65_000),
                new PlayerState(300, -400, 12_345),
            });

            TestAssert.Equal(StateDigest.Compute(first), StateDigest.Compute(second));
        }

        private static void CanonicalStateChangesAlterDigest()
        {
            ActiveRoster roster = Roster(90, 10, 70);
            BattleState baseline = new BattleState(12, roster, new[]
            {
                new PlayerState(-100, 200, 300),
                new PlayerState(400, -500, 600),
                new PlayerState(700, 800, 900),
            });
            BattleState moved = new BattleState(12, roster, new[]
            {
                new PlayerState(-99, 200, 300),
                new PlayerState(400, -500, 600),
                new PlayerState(700, 800, 900),
            });
            BattleState aimed = new BattleState(12, roster, new[]
            {
                new PlayerState(-100, 200, 300),
                new PlayerState(400, -500, 601),
                new PlayerState(700, 800, 900),
            });
            BattleState nextTick = new BattleState(13, roster, new[]
            {
                new PlayerState(-100, 200, 300),
                new PlayerState(400, -500, 600),
                new PlayerState(700, 800, 900),
            });

            ulong digest = StateDigest.Compute(baseline);
            TestAssert.NotEqual(digest, StateDigest.Compute(moved));
            TestAssert.NotEqual(digest, StateDigest.Compute(aimed));
            TestAssert.NotEqual(digest, StateDigest.Compute(nextTick));
        }

        private static void RosterIdentityAndOrderChangesAlterDigest()
        {
            PlayerState[] players =
            {
                new PlayerState(-100, 200, 300),
                new PlayerState(400, -500, 600),
                new PlayerState(700, 800, 900),
            };
            BattleState baseline = new BattleState(12, Roster(90, 10, 70), players);
            BattleState changedIdentity = new BattleState(12, Roster(90, 11, 70), players);
            BattleState changedOrder = new BattleState(12, Roster(10, 90, 70), players);
            BattleState changedCount = new BattleState(12, Roster(90, 10), new[]
            {
                players[0],
                players[1],
            });

            ulong digest = StateDigest.Compute(baseline);
            TestAssert.NotEqual(digest, StateDigest.Compute(changedIdentity));
            TestAssert.NotEqual(digest, StateDigest.Compute(changedOrder));
            TestAssert.NotEqual(digest, StateDigest.Compute(changedCount));
        }

        private static void GoldenDigestLocksVariableRosterFieldAndByteOrder()
        {
            ActiveRoster roster = new ActiveRoster(new[]
            {
                new PlayerId(0x0102030405060708UL),
                new PlayerId(0x000000000000002AUL),
                new PlayerId(0xFFEEDDCCBBAA0099UL),
                new PlayerId(0x00000000000F4243UL),
            });
            BattleState state = new BattleState(1_000, roster, new[]
            {
                new PlayerState(0, -3_000, 13_086),
                new PlayerState(0, 3_000, 8_699),
                new PlayerState(-2_500, -2_000, 51_320),
                new PlayerState(2_500, 2_000, 62_539),
            });

            TestAssert.Equal(0x89A7DD66F8D9E871UL, StateDigest.Compute(state));
        }

        private static void FourPlayerTwinSimulationsMatchDigestAtEveryTick()
        {
            ActiveRoster firstRoster = Roster(90, 10, 70, 20);
            ActiveRoster secondRoster = Roster(90, 10, 70, 20);
            PlayerState[] initialPlayers =
            {
                new PlayerState(-1_000, 0, 0),
                new PlayerState(1_000, 0, 0),
                new PlayerState(0, -1_000, 0),
                new PlayerState(0, 1_000, 0),
            };
            BattleSimulation first = new BattleSimulation(BattleState.CreateInitial(firstRoster, initialPlayers));
            BattleSimulation second = new BattleSimulation(BattleState.CreateInitial(secondRoster, initialPlayers));
            int[] canonicalOrder = { 0, 1, 2, 3 };
            int[] shuffledOrder = { 2, 0, 3, 1 };

            for (uint tick = 0; tick < TwinTickCount; tick++)
            {
                first.Step(CreateScriptedFrame(firstRoster, tick, canonicalOrder));
                second.Step(CreateScriptedFrame(secondRoster, tick, shuffledOrder));

                ulong firstDigest = StateDigest.Compute(first.State);
                ulong secondDigest = StateDigest.Compute(second.State);
                if (firstDigest != secondDigest)
                {
                    throw new InvalidOperationException(
                        $"Twin digest mismatch after tick {tick}: {firstDigest:X16} != {secondDigest:X16}.");
                }
            }

            TestAssert.Equal((uint)TwinTickCount, first.State.Tick);
        }

        private static void InitialStateAndThreePlayerFrameHistoryRebuildFinalDigest()
        {
            ActiveRoster roster = Roster(90, 10, 70);
            PlayerState[] initialPlayers =
            {
                new PlayerState(-1_000, 0, 0),
                new PlayerState(1_000, 0, 0),
                new PlayerState(0, -1_000, 0),
            };
            BattleState initial = BattleState.CreateInitial(roster, initialPlayers);
            BattleSimulation original = new BattleSimulation(initial);
            List<FrameData> history = new List<FrameData>(HistoryTickCount);
            int[] shuffledOrder = { 2, 0, 1 };

            for (uint tick = 0; tick < HistoryTickCount; tick++)
            {
                FrameData frame = CreateScriptedFrame(roster, tick, shuffledOrder);
                history.Add(frame);
                original.Step(frame);
            }

            BattleSimulation rebuilt = new BattleSimulation(initial);
            foreach (FrameData frame in history)
            {
                rebuilt.Step(frame);
            }

            TestAssert.Equal((uint)HistoryTickCount, rebuilt.State.Tick);
            TestAssert.Equal(StateDigest.Compute(original.State), StateDigest.Compute(rebuilt.State));
        }

        private static void GameplayDigestCoversStateAndDefinition()
        {
            BattleState baseline = GameplayState();
            BattleState[] changed =
            {
                GameplayState(secondHitPoints: 99),
                GameplayState(firstCooldown: 1),
                GameplayState(phase: BattlePhase.RoundEnded),
                GameplayState(roundTicks: 49),
                GameplayState(projectileX: 11),
                GameplayState(nextProjectileId: 3),
                GameplayState(projectileDamage: 26),
                GameplayState(obstacleMaxX: 101),
            };
            ulong digest = StateDigest.Compute(baseline);
            for (int index = 0; index < changed.Length; index++)
                TestAssert.NotEqual(digest, StateDigest.Compute(changed[index]));
        }

        private static void GameplayStateComparerUsesStructuralValues()
        {
            BattleState first = GameplayState();
            BattleState equal = GameplayState();
            BattleState changed = GameplayState(secondHitPoints: 99);

            TestAssert.Equal(true, BattleStateValueComparer.HaveSameValue(first, equal));
            TestAssert.Equal(false, BattleStateValueComparer.HaveSameValue(first, changed));
            TestAssert.Equal(StateDigest.Compute(first), StateDigest.Compute(equal));
        }

        private static BattleState GameplayState(
            int secondHitPoints = 100,
            uint firstCooldown = 0,
            BattlePhase phase = BattlePhase.Playing,
            uint roundTicks = 50,
            int projectileX = 10,
            ulong nextProjectileId = 2,
            int projectileDamage = 25,
            int obstacleMaxX = 100)
        {
            GameplayConfig gameplay = GameplayContractTests.Config(projectileDamage: projectileDamage);
            ArenaConfig arena = GameplayContractTests.Arena(new ArenaRectangle(-100, obstacleMaxX, -50, 50));
            var definition = new BattleDefinition(gameplay, arena);
            return new BattleState(9, Roster(11, 22), new[]
            {
                new PlayerState(-400, 0, 0, 100, 1, firstCooldown),
                new PlayerState(400, 0, 32_768, secondHitPoints, 0, 0),
            }, definition, phase, 0, roundTicks, RoundResult.None, null,
                new[] { new ProjectileState(1, new PlayerId(11), projectileX, 20, 1000, 0, 7) }, nextProjectileId);
        }

        private static FrameData CreateScriptedFrame(ActiveRoster roster, uint tick, int[] arrivalOrder)
        {
            InputFrame[] canonical = new InputFrame[roster.Count];
            for (int index = 0; index < canonical.Length; index++)
            {
                int movePhase = (int)((tick + (uint)index) % 3U);
                int zPhase = (int)(((tick * 2U) + (uint)index) % 3U);
                sbyte moveX = (sbyte)(movePhase - 1);
                sbyte moveZ = (sbyte)(zPhase - 1);
                ushort aim = unchecked((ushort)((tick * (uint)(97 + (index * 11))) + (uint)(index * 1_000)));
                canonical[index] = new InputFrame(tick, new PlayerSlot(index), moveX, moveZ, aim);
            }

            InputFrame[] received = new InputFrame[roster.Count];
            for (int index = 0; index < received.Length; index++)
            {
                received[index] = canonical[arrivalOrder[index]];
            }

            return FrameData.Create(roster, tick, received);
        }

        private static ActiveRoster Roster(params ulong[] values)
        {
            PlayerId[] ids = new PlayerId[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                ids[index] = new PlayerId(values[index]);
            }

            return new ActiveRoster(ids);
        }
    }
}
