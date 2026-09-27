using System;
using LockstepArena.Simulation;
using LockstepArena.Simulation.Verification;

namespace LockstepArena.Server.Verification
{
    internal static class Program
    {
        private static int Main()
        {
            Gate3GoldenVectorResult result = Gate3GoldenVector.Run();
            bool passed = true;
            passed &= Check("Tick", 1_000U, result.State.Tick);
            passed &= Check("PlayerCount", 4, result.State.PlayerCount);
            passed &= CheckPlayer(result.State, 0, 0x0102030405060708UL, 0, -3_000, 13_086);
            passed &= CheckPlayer(result.State, 1, 0x000000000000002AUL, 0, 3_000, 8_699);
            passed &= CheckPlayer(result.State, 2, 0xFFEEDDCCBBAA0099UL, -2_500, -2_000, 51_320);
            passed &= CheckPlayer(result.State, 3, 0x00000000000F4243UL, 2_500, 2_000, 62_539);
            passed &= Check("Digest", 0x89A7DD66F8D9E871UL, result.Digest);

            if (!passed)
            {
                return 1;
            }

            Console.WriteLine(
                $"PASS Gate3GoldenVector Tick={result.State.Tick} Players={result.State.PlayerCount} Digest={result.Digest:X16}");
            return 0;
        }

        private static bool CheckPlayer(
            BattleState state,
            int slotValue,
            ulong expectedPlayerId,
            int expectedX,
            int expectedZ,
            ushort expectedAim)
        {
            PlayerSlot slot = new PlayerSlot(slotValue);
            PlayerState player = state.GetPlayerState(slot);
            bool passed = true;
            passed &= Check($"Slot{slotValue}.PlayerId", expectedPlayerId, state.Roster.GetPlayerId(slot).Value);
            passed &= Check($"Slot{slotValue}.PositionX", expectedX, player.PositionX);
            passed &= Check($"Slot{slotValue}.PositionZ", expectedZ, player.PositionZ);
            passed &= Check($"Slot{slotValue}.Aim", expectedAim, player.Aim);
            return passed;
        }

        private static bool Check<T>(string field, T expected, T actual)
            where T : IEquatable<T>
        {
            if (expected.Equals(actual))
            {
                return true;
            }

            Console.Error.WriteLine($"FAIL {field}: expected <{expected}> actual <{actual}>");
            return false;
        }
    }
}
