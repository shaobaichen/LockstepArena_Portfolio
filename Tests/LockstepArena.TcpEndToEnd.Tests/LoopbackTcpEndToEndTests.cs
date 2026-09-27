using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.TcpEndToEnd.Tests
{
    internal static class LoopbackTcpEndToEndTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(ListenerBindsIpv4LoopbackOnOsAssignedPort), ListenerBindsIpv4LoopbackOnOsAssignedPort),
            new TestCase(nameof(ServerReadLoopUsesReusableOffsetSegmentAcrossMultipleReads), ServerReadLoopUsesReusableOffsetSegmentAcrossMultipleReads),
            new TestCase(nameof(ContinuousClientStreamRecoversTwelveSubmissionPayloadsInOrder), ContinuousClientStreamRecoversTwelveSubmissionPayloadsInOrder),
            new TestCase(nameof(GapFillPublishesTicks100Through102AsIndependentPayloads), GapFillPublishesTicks100Through102AsIndependentPayloads),
            new TestCase(nameof(ContinuousServerStreamRecoversThreeAuthoritativePayloadsInOrder), ContinuousServerStreamRecoversThreeAuthoritativePayloadsInOrder),
            new TestCase(nameof(ServerReadZeroBeforeTwelvePayloadsThrowsEndOfStreamException), ServerReadZeroBeforeTwelvePayloadsThrowsEndOfStreamException),
            new TestCase(nameof(ClientReadZeroBeforeThreePayloadsThrowsEndOfStreamException), ClientReadZeroBeforeThreePayloadsThrowsEndOfStreamException),
            new TestCase(nameof(RealTcpRoundTripMatchesApprovedAuthoritySequenceStatesAndDigests), RealTcpRoundTripMatchesApprovedAuthoritySequenceStatesAndDigests),
        };

        private static void ListenerBindsIpv4LoopbackOnOsAssignedPort()
        {
            LoopbackTcpGoldenResult actual = LoopbackTcpGoldenVector.Run();

            TestAssert.Equal(IPAddress.Loopback, actual.ListenerAddress);
            TestAssert.True(actual.ListenerPort > 0);
            TestAssert.Equal(IPAddress.Loopback, actual.ClientRemoteAddress);
            TestAssert.Equal(actual.ListenerPort, actual.ClientRemotePort);
        }

        private static void ServerReadLoopUsesReusableOffsetSegmentAcrossMultipleReads()
        {
            LoopbackTcpGoldenResult actual = LoopbackTcpGoldenVector.Run();

            TestAssert.True(actual.ServerReadCallCount > 1);
        }

        private static void ContinuousClientStreamRecoversTwelveSubmissionPayloadsInOrder()
        {
            LoopbackTcpGoldenResult actual = LoopbackTcpGoldenVector.Run();

            TestAssert.Equal(12, actual.SubmissionPayloads.Length);
            TestAssert.Equal(12, actual.RecoveredSubmissionPayloads.Length);
            AssertPayloadSequenceEqual(actual.SubmissionPayloads, actual.RecoveredSubmissionPayloads);
        }

        private static void GapFillPublishesTicks100Through102AsIndependentPayloads()
        {
            LoopbackTcpGoldenResult actual = LoopbackTcpGoldenVector.Run();

            TestAssert.Equal(12, actual.ProcessorOutputCounts.Length);
            for (int index = 0; index < 11; index++)
            {
                TestAssert.Equal(0, actual.ProcessorOutputCounts[index]);
            }

            TestAssert.Equal(3, actual.ProcessorOutputCounts[11]);
            TestAssert.Equal(3, actual.AuthoritativePayloads.Length);
            TestAssert.NotSame(actual.AuthoritativePayloads[0], actual.AuthoritativePayloads[1]);
            TestAssert.NotSame(actual.AuthoritativePayloads[0], actual.AuthoritativePayloads[2]);
            TestAssert.NotSame(actual.AuthoritativePayloads[1], actual.AuthoritativePayloads[2]);
            TestAssert.Equal(3, actual.AuthoritativeFrames.Length);
            TestAssert.Equal(100U, actual.AuthoritativeFrames[0].Tick);
            TestAssert.Equal(101U, actual.AuthoritativeFrames[1].Tick);
            TestAssert.Equal(102U, actual.AuthoritativeFrames[2].Tick);
        }

        private static void ContinuousServerStreamRecoversThreeAuthoritativePayloadsInOrder()
        {
            LoopbackTcpGoldenResult actual = LoopbackTcpGoldenVector.Run();

            TestAssert.True(actual.ClientReadCallCount > 1);
            TestAssert.Equal(3, actual.AuthoritativePayloads.Length);
            TestAssert.Equal(3, actual.RecoveredAuthoritativePayloads.Length);
            AssertPayloadSequenceEqual(actual.AuthoritativePayloads, actual.RecoveredAuthoritativePayloads);
        }

        private static void ServerReadZeroBeforeTwelvePayloadsThrowsEndOfStreamException()
        {
            var recoveredPayloads = new List<byte[]>();
            TestAssert.Throws<EndOfStreamException>(
                () => RunServerEofFixture(recoveredPayloads));
            TestAssert.Equal(11, recoveredPayloads.Count);
        }

        private static void ClientReadZeroBeforeThreePayloadsThrowsEndOfStreamException()
        {
            var recoveredPayloads = new List<byte[]>();
            TestAssert.Throws<EndOfStreamException>(
                () => RunClientEofFixture(recoveredPayloads));
            TestAssert.Equal(2, recoveredPayloads.Count);
        }

        private static void RunServerEofFixture(List<byte[]> recoveredPayloads)
        {
            LoopbackTcpGoldenResult actual = LoopbackTcpGoldenVector.Run();
            byte[] framedStream = FramePayloads(actual.SubmissionPayloads, 11);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                using var client = new TcpClient(AddressFamily.InterNetwork);
                client.Connect(IPAddress.Loopback, endpoint.Port);
                using TcpClient acceptedClient = listener.AcceptTcpClient();
                using NetworkStream clientStream = client.GetStream();
                using NetworkStream serverStream = acceptedClient.GetStream();

                clientStream.Write(framedStream, 0, framedStream.Length);
                client.Client.Shutdown(SocketShutdown.Send);

                var decoder = new LengthPrefixedFrameDecoder(4096);
                var receiveBuffer = new byte[16];
                while (recoveredPayloads.Count < 12)
                {
                    int bytesRead = serverStream.Read(receiveBuffer, 3, 3);
                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException(
                            "TCP stream ended before twelve submissions were recovered.");
                    }

                    recoveredPayloads.AddRange(decoder.Feed(receiveBuffer, 3, bytesRead));
                    Array.Fill(receiveBuffer, (byte)0xA5);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void RunClientEofFixture(List<byte[]> recoveredPayloads)
        {
            LoopbackTcpGoldenResult actual = LoopbackTcpGoldenVector.Run();
            byte[] framedStream = FramePayloads(actual.AuthoritativePayloads, 2);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            try
            {
                var endpoint = (IPEndPoint)listener.LocalEndpoint;
                using var client = new TcpClient(AddressFamily.InterNetwork);
                client.Connect(IPAddress.Loopback, endpoint.Port);
                using TcpClient acceptedClient = listener.AcceptTcpClient();
                using NetworkStream clientStream = client.GetStream();
                using NetworkStream serverStream = acceptedClient.GetStream();

                serverStream.Write(framedStream, 0, framedStream.Length);
                acceptedClient.Client.Shutdown(SocketShutdown.Send);

                var decoder = new LengthPrefixedFrameDecoder(4096);
                var receiveBuffer = new byte[16];
                while (recoveredPayloads.Count < 3)
                {
                    int bytesRead = clientStream.Read(receiveBuffer, 5, 5);
                    if (bytesRead == 0)
                    {
                        throw new EndOfStreamException(
                            "TCP stream ended before three authoritative payloads were recovered.");
                    }

                    recoveredPayloads.AddRange(decoder.Feed(receiveBuffer, 5, bytesRead));
                    Array.Fill(receiveBuffer, (byte)0xA5);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static byte[] FramePayloads(byte[][] payloads, int count)
        {
            var frames = new byte[count][];
            int streamLength = 0;
            for (int index = 0; index < count; index++)
            {
                frames[index] = LengthPrefixedFrameEncoder.Encode(payloads[index], 4096);
                streamLength = checked(streamLength + frames[index].Length);
            }

            var stream = new byte[streamLength];
            int streamOffset = 0;
            for (int index = 0; index < frames.Length; index++)
            {
                Array.Copy(frames[index], 0, stream, streamOffset, frames[index].Length);
                streamOffset += frames[index].Length;
            }

            return stream;
        }

        private static void RealTcpRoundTripMatchesApprovedAuthoritySequenceStatesAndDigests()
        {
            LoopbackTcpGoldenResult first = LoopbackTcpGoldenVector.Run();
            LoopbackTcpGoldenResult second = LoopbackTcpGoldenVector.Run();

            TestAssert.Equal(first.AuthoritativeFrames.Length, second.AuthoritativeFrames.Length);
            for (int index = 0; index < first.AuthoritativeFrames.Length; index++)
            {
                AssertFrameEqual(first.AuthoritativeFrames[index], second.AuthoritativeFrames[index]);
            }

            AssertApprovedClientStates(first);
            AssertApprovedClientStates(second);
            AssertApprovedFinalEquality(first);
            AssertApprovedFinalEquality(second);
        }

        private static void AssertApprovedClientStates(LoopbackTcpGoldenResult actual)
        {
            TestAssert.Equal(3, actual.ClientStates.Length);
            TestAssert.Equal(3, actual.ClientDigests.Length);

            AssertState(actual.ClientStates[0], 101U,
                -200, 0, 10_100,
                200, 0, 20_100,
                0, -200, 30_100,
                0, 200, 40_100);
            TestAssert.Equal(0xD95809E1EB5CDDAAUL, actual.ClientDigests[0]);

            AssertState(actual.ClientStates[1], 102U,
                -200, 100, 10_101,
                200, -100, 20_101,
                100, -200, 30_101,
                -100, 200, 40_101);
            TestAssert.Equal(0xA96B83267DD72A7DUL, actual.ClientDigests[1]);

            AssertState(actual.ClientStates[2], 103U,
                -300, 100, 10_102,
                300, -100, 20_102,
                100, -300, 30_102,
                -100, 300, 40_102);
            TestAssert.Equal(0x386C4BB11A7EB7E0UL, actual.ClientDigests[2]);
        }

        private static void AssertApprovedFinalEquality(LoopbackTcpGoldenResult actual)
        {
            AssertState(actual.ServerState, 103U,
                -300, 100, 10_102,
                300, -100, 20_102,
                100, -300, 30_102,
                -100, 300, 40_102);
            AssertStateEqual(actual.ServerState, actual.ClientState);
            TestAssert.Equal(103U, actual.NextPublishTick);
            TestAssert.Equal(0x386C4BB11A7EB7E0UL, StateDigest.Compute(actual.ServerState));
            TestAssert.Equal(0x386C4BB11A7EB7E0UL, StateDigest.Compute(actual.ClientState));
        }

        private static void AssertFrameEqual(FrameData expected, FrameData actual)
        {
            TestAssert.Equal(expected.Tick, actual.Tick);
            TestAssert.Equal(expected.Roster.Count, actual.Roster.Count);
            for (int slotValue = 0; slotValue < expected.Roster.Count; slotValue++)
            {
                var slot = new PlayerSlot(slotValue);
                TestAssert.Equal(expected.Roster.GetPlayerId(slot), actual.Roster.GetPlayerId(slot));
            }

            TestAssert.Equal(expected.InputCount, actual.InputCount);
            for (int slotValue = 0; slotValue < expected.InputCount; slotValue++)
            {
                var slot = new PlayerSlot(slotValue);
                InputFrame expectedInput = expected.GetInput(slot);
                InputFrame actualInput = actual.GetInput(slot);
                TestAssert.Equal(expectedInput.Tick, actualInput.Tick);
                TestAssert.Equal(expectedInput.PlayerSlot, actualInput.PlayerSlot);
                TestAssert.Equal(expectedInput.MoveX, actualInput.MoveX);
                TestAssert.Equal(expectedInput.MoveZ, actualInput.MoveZ);
                TestAssert.Equal(expectedInput.Aim, actualInput.Aim);
            }
        }

        private static void AssertState(
            BattleState actual,
            uint expectedTick,
            int slot0X, int slot0Z, int slot0Aim,
            int slot1X, int slot1Z, int slot1Aim,
            int slot2X, int slot2Z, int slot2Aim,
            int slot3X, int slot3Z, int slot3Aim)
        {
            TestAssert.Equal(expectedTick, actual.Tick);
            TestAssert.Equal(4, actual.PlayerCount);
            AssertPlayer(actual, 0, slot0X, slot0Z, slot0Aim);
            AssertPlayer(actual, 1, slot1X, slot1Z, slot1Aim);
            AssertPlayer(actual, 2, slot2X, slot2Z, slot2Aim);
            AssertPlayer(actual, 3, slot3X, slot3Z, slot3Aim);
        }

        private static void AssertPlayer(
            BattleState state,
            int slotValue,
            int expectedX,
            int expectedZ,
            int expectedAim)
        {
            PlayerState player = state.GetPlayerState(new PlayerSlot(slotValue));
            TestAssert.Equal(expectedX, player.PositionX);
            TestAssert.Equal(expectedZ, player.PositionZ);
            TestAssert.Equal(checked((ushort)expectedAim), player.Aim);
        }

        private static void AssertStateEqual(BattleState expected, BattleState actual)
        {
            TestAssert.Equal(expected.Tick, actual.Tick);
            TestAssert.True(expected.Roster.HasSameStructure(actual.Roster));
            TestAssert.Equal(expected.PlayerCount, actual.PlayerCount);
            for (int slotValue = 0; slotValue < expected.PlayerCount; slotValue++)
            {
                PlayerSlot slot = new PlayerSlot(slotValue);
                PlayerState expectedPlayer = expected.GetPlayerState(slot);
                PlayerState actualPlayer = actual.GetPlayerState(slot);
                TestAssert.Equal(expectedPlayer.PositionX, actualPlayer.PositionX);
                TestAssert.Equal(expectedPlayer.PositionZ, actualPlayer.PositionZ);
                TestAssert.Equal(expectedPlayer.Aim, actualPlayer.Aim);
            }
        }

        private static void AssertPayloadSequenceEqual(byte[][] expected, byte[][] actual)
        {
            TestAssert.Equal(expected.Length, actual.Length);
            for (int index = 0; index < expected.Length; index++)
            {
                TestAssert.SequenceEqual(expected[index], actual[index]);
            }
        }
    }
}
