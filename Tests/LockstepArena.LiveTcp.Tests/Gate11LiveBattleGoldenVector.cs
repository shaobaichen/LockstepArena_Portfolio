using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Server.LiveTcp;
using LockstepArena.Simulation;

namespace LockstepArena.LiveTcp.Tests
{
    internal sealed class Gate11LiveBattleGoldenResult
    {
        public Gate11LiveBattleGoldenResult(
            FrameData[] authoritativeFrames,
            BattleState serverState,
            BattleState clientState,
            int authoritativeMessageWriteCount,
            uint serverNextPublishTick)
        {
            AuthoritativeFrames = authoritativeFrames;
            ServerState = serverState;
            ClientState = clientState;
            AuthoritativeMessageWriteCount = authoritativeMessageWriteCount;
            ServerNextPublishTick = serverNextPublishTick;
        }

        public FrameData[] AuthoritativeFrames { get; }

        public BattleState ServerState { get; }

        public BattleState ClientState { get; }

        public int AuthoritativeMessageWriteCount { get; }

        public uint ServerNextPublishTick { get; }
    }

    internal static class Gate11LiveBattleGoldenVector
    {
        internal static Gate11LiveBattleGoldenResult Run()
        {
            ActiveRoster serverRoster = CreateRoster();
            ActiveRoster clientRoster = CreateRoster();
            (PlayerId PlayerId, InputFrame Input)[] submissions = CreateSubmissions(serverRoster);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                var clientSocket = new TcpClient(AddressFamily.InterNetwork);
                clientSocket.Connect(IPAddress.Loopback, endpoint.Port);
                TcpClient serverSocket = listener.AcceptTcpClient();
                using var server = new TcpServerBattlePump(
                    serverSocket,
                    CreateInitialState(serverRoster),
                    2U,
                    8U,
                    5,
                    1_048_576,
                    16,
                    3,
                    3);
                using var client = new TcpClientBattlePump(
                    clientSocket,
                    CreateInitialState(clientRoster),
                    1_048_576,
                    16,
                    5,
                    5);

                for (int index = 0; index < submissions.Length; index++)
                {
                    client.SendInput(submissions[index].PlayerId, submissions[index].Input);
                }

                var frames = new List<FrameData>();
                int writeCount = 0;
                Stopwatch deadline = Stopwatch.StartNew();
                while (frames.Count < 3 && deadline.ElapsedMilliseconds < 10_000)
                {
                    writeCount = checked(writeCount + server.PumpOnce());
                    frames.AddRange(client.PumpReceiveOnce());
                }

                return new Gate11LiveBattleGoldenResult(
                    frames.ToArray(),
                    server.ServerState,
                    client.ClientState,
                    writeCount,
                    server.NextPublishTick);
            }
            finally
            {
                listener.Stop();
            }
        }

        internal static ActiveRoster CreateRoster()
        {
            return new ActiveRoster(new[]
            {
                new PlayerId(0x0102030405060708UL),
                new PlayerId(0x000000000000002AUL),
                new PlayerId(0xFFEEDDCCBBAA0099UL),
                new PlayerId(0x00000000000F4243UL),
            });
        }

        internal static BattleState CreateInitialState(ActiveRoster roster)
        {
            return new BattleState(100U, roster, new[]
            {
                new PlayerState(-300, 0, 1_000),
                new PlayerState(300, 0, 2_000),
                new PlayerState(0, -300, 3_000),
                new PlayerState(0, 300, 4_000),
            });
        }

        internal static (PlayerId PlayerId, InputFrame Input)[] CreateSubmissions(
            ActiveRoster roster)
        {
            var arrivalOrder = new (uint Tick, int Slot)[]
            {
                (100U, 0), (100U, 2), (100U, 1),
                (101U, 3), (101U, 1), (101U, 0), (101U, 2),
                (102U, 2), (102U, 0), (102U, 3), (102U, 1),
                (100U, 3),
            };
            var submissions = new (PlayerId PlayerId, InputFrame Input)[arrivalOrder.Length];
            for (int index = 0; index < arrivalOrder.Length; index++)
            {
                (uint tick, int slotValue) = arrivalOrder[index];
                PlayerSlot slot = new PlayerSlot(slotValue);
                submissions[index] = (roster.GetPlayerId(slot), CreateInput(tick, slotValue));
            }

            return submissions;
        }

        private static InputFrame CreateInput(uint tick, int slotValue)
        {
            (int moveX, int moveZ, int aim) = (tick, slotValue) switch
            {
                (100U, 0) => (1, 0, 10_100),
                (100U, 1) => (-1, 0, 20_100),
                (100U, 2) => (0, 1, 30_100),
                (100U, 3) => (0, -1, 40_100),
                (101U, 0) => (0, 1, 10_101),
                (101U, 1) => (0, -1, 20_101),
                (101U, 2) => (1, 0, 30_101),
                (101U, 3) => (-1, 0, 40_101),
                (102U, 0) => (-1, 0, 10_102),
                (102U, 1) => (1, 0, 20_102),
                (102U, 2) => (0, -1, 30_102),
                (102U, 3) => (0, 1, 40_102),
                _ => throw new ArgumentOutOfRangeException(nameof(tick)),
            };

            return new InputFrame(
                tick,
                new PlayerSlot(slotValue),
                checked((sbyte)moveX),
                checked((sbyte)moveZ),
                checked((ushort)aim));
        }
    }
}
