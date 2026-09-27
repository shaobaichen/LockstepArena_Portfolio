using System;
using System.Net;
using System.Net.Sockets;
using LockstepArena.Simulation;

namespace LockstepArena.Client.Demo
{
    public sealed class DemoClientOptions
    {
        public DemoClientOptions(int controlPort, int maxControlPayloadLength, int maxPendingControlBytes, int controlReceiveBufferLength, int controlReceiveOffset, int controlReceiveReadCapacity, int maxControlMessagesPerPump, int maxControlSendBytesPerPump, int maxPredictionTicks, int maxAuthoritativeFramesPerUpdate, int maxPendingAuthoritativeFrames, int maxReplayFrames, int maxBattlePayloadLength, int battleReceiveBufferLength, int battleReceiveOffset, int battleReceiveReadCapacity, string serverAddress = "127.0.0.1", BattleDefinition? battleDefinition = null)
        {
            if (controlPort < 1 || controlPort > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(controlPort));
            ValidatePayload(maxControlPayloadLength, nameof(maxControlPayloadLength));
            if ((long)maxPendingControlBytes < (long)maxControlPayloadLength + 4L) throw new ArgumentOutOfRangeException(nameof(maxPendingControlBytes));
            ValidateBuffer(controlReceiveBufferLength, controlReceiveOffset, controlReceiveReadCapacity, nameof(controlReceiveBufferLength), nameof(controlReceiveOffset), nameof(controlReceiveReadCapacity));
            RequirePositive(maxControlMessagesPerPump, nameof(maxControlMessagesPerPump));
            RequirePositive(maxControlSendBytesPerPump, nameof(maxControlSendBytesPerPump));
            RequirePositive(maxPredictionTicks, nameof(maxPredictionTicks));
            RequirePositive(maxAuthoritativeFramesPerUpdate, nameof(maxAuthoritativeFramesPerUpdate));
            RequirePositive(maxPendingAuthoritativeFrames, nameof(maxPendingAuthoritativeFrames));
            RequirePositive(maxReplayFrames, nameof(maxReplayFrames));
            ValidatePayload(maxBattlePayloadLength, nameof(maxBattlePayloadLength));
            ValidateBuffer(battleReceiveBufferLength, battleReceiveOffset, battleReceiveReadCapacity, nameof(battleReceiveBufferLength), nameof(battleReceiveOffset), nameof(battleReceiveReadCapacity));
            if (!IPAddress.TryParse(serverAddress, out IPAddress? parsedAddress) ||
                parsedAddress.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Server address must be a numeric IPv4 address.", nameof(serverAddress));
            ControlPort = controlPort;
            MaxControlPayloadLength = maxControlPayloadLength;
            MaxPendingControlBytes = maxPendingControlBytes;
            ControlReceiveBufferLength = controlReceiveBufferLength;
            ControlReceiveOffset = controlReceiveOffset;
            ControlReceiveReadCapacity = controlReceiveReadCapacity;
            MaxControlMessagesPerPump = maxControlMessagesPerPump;
            MaxControlSendBytesPerPump = maxControlSendBytesPerPump;
            MaxPredictionTicks = maxPredictionTicks;
            MaxAuthoritativeFramesPerUpdate = maxAuthoritativeFramesPerUpdate;
            MaxPendingAuthoritativeFrames = maxPendingAuthoritativeFrames;
            MaxReplayFrames = maxReplayFrames;
            MaxBattlePayloadLength = maxBattlePayloadLength;
            BattleReceiveBufferLength = battleReceiveBufferLength;
            BattleReceiveOffset = battleReceiveOffset;
            BattleReceiveReadCapacity = battleReceiveReadCapacity;
            ServerAddress = parsedAddress;
            BattleDefinition = battleDefinition;
        }

        public int ControlPort { get; }
        public int MaxControlPayloadLength { get; }
        public int MaxPendingControlBytes { get; }
        public int ControlReceiveBufferLength { get; }
        public int ControlReceiveOffset { get; }
        public int ControlReceiveReadCapacity { get; }
        public int MaxControlMessagesPerPump { get; }
        public int MaxControlSendBytesPerPump { get; }
        public int MaxPredictionTicks { get; }
        public int MaxAuthoritativeFramesPerUpdate { get; }
        public int MaxPendingAuthoritativeFrames { get; }
        public int MaxReplayFrames { get; }
        public int MaxBattlePayloadLength { get; }
        public int BattleReceiveBufferLength { get; }
        public int BattleReceiveOffset { get; }
        public int BattleReceiveReadCapacity { get; }
        public IPAddress ServerAddress { get; }
        public BattleDefinition? BattleDefinition { get; }

        private static void RequirePositive(int value, string name) { if (value < 1) throw new ArgumentOutOfRangeException(name); }
        private static void ValidatePayload(int value, string name) { if (value < 1 || value > int.MaxValue - 4) throw new ArgumentOutOfRangeException(name); }
        private static void ValidateBuffer(int length, int offset, int capacity, string lengthName, string offsetName, string capacityName)
        {
            if (length < 1) throw new ArgumentOutOfRangeException(lengthName);
            if (offset < 0 || offset > length) throw new ArgumentOutOfRangeException(offsetName);
            if (capacity < 1 || capacity > length - offset) throw new ArgumentOutOfRangeException(capacityName);
        }
    }
}
