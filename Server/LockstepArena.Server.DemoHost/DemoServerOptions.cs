using System;
using System.Net;
using System.Net.Sockets;
using LockstepArena.Simulation;

namespace LockstepArena.Server.DemoHost
{
    public sealed class DemoServerOptions
    {
        private readonly PlayerState[] _spawnStates;

        public DemoServerOptions(
            int controlPort,
            int battlePort,
            int maxSessions,
            int maxRooms,
            int maxRoomCapacity,
            PlayerState[] spawnStatesInSlotOrder,
            uint inputDelayTicks,
            uint maxFutureTickOffset,
            int authoritativeHistoryCapacity,
            uint battleDurationTicks,
            int maxControlPayloadLength,
            int maxPendingControlBytesPerSession,
            int controlReceiveBufferLength,
            int controlReceiveOffset,
            int controlReceiveReadCapacity,
            int maxControlMessagesPerPump,
            int maxControlSendBytesPerPump,
            int maxBattlePayloadLength,
            int battleReceiveBufferLength,
            int battleReceiveOffset,
            int battleReceiveReadCapacity,
            string bindAddress = "127.0.0.1",
            BattleDefinition? battleDefinition = null,
            TimeSpan? battleReadyTimeout = null)
        {
            ValidatePort(controlPort, nameof(controlPort));
            ValidatePort(battlePort, nameof(battlePort));
            if (controlPort != 0 && battlePort != 0 && controlPort == battlePort)
            {
                throw new ArgumentException("Control and battle ports must differ when both are explicit.", nameof(battlePort));
            }

            RequirePositive(maxSessions, nameof(maxSessions));
            RequirePositive(maxRooms, nameof(maxRooms));
            if (maxRoomCapacity < 2) throw new ArgumentOutOfRangeException(nameof(maxRoomCapacity));
            if (maxRoomCapacity > maxSessions) throw new ArgumentOutOfRangeException(nameof(maxRoomCapacity));
            if (spawnStatesInSlotOrder is null) throw new ArgumentNullException(nameof(spawnStatesInSlotOrder));
            if (spawnStatesInSlotOrder.Length < maxRoomCapacity) throw new ArgumentOutOfRangeException(nameof(spawnStatesInSlotOrder));
            for (int index = 0; index < spawnStatesInSlotOrder.Length; index++)
            {
                PlayerState state = spawnStatesInSlotOrder[index];
                if (state.PositionX < SimulationConfig.ArenaMinX || state.PositionX > SimulationConfig.ArenaMaxX ||
                    state.PositionZ < SimulationConfig.ArenaMinZ || state.PositionZ > SimulationConfig.ArenaMaxZ)
                {
                    throw new ArgumentOutOfRangeException(nameof(spawnStatesInSlotOrder));
                }
            }

            RequirePositive(authoritativeHistoryCapacity, nameof(authoritativeHistoryCapacity));
            if (battleDurationTicks == 0) throw new ArgumentOutOfRangeException(nameof(battleDurationTicks));
            ValidatePayload(maxControlPayloadLength, nameof(maxControlPayloadLength));
            if ((long)maxPendingControlBytesPerSession < (long)maxControlPayloadLength + 4L) throw new ArgumentOutOfRangeException(nameof(maxPendingControlBytesPerSession));
            ValidateBuffer(controlReceiveBufferLength, controlReceiveOffset, controlReceiveReadCapacity, nameof(controlReceiveBufferLength), nameof(controlReceiveOffset), nameof(controlReceiveReadCapacity));
            RequirePositive(maxControlMessagesPerPump, nameof(maxControlMessagesPerPump));
            RequirePositive(maxControlSendBytesPerPump, nameof(maxControlSendBytesPerPump));
            ValidatePayload(maxBattlePayloadLength, nameof(maxBattlePayloadLength));
            ValidateBuffer(battleReceiveBufferLength, battleReceiveOffset, battleReceiveReadCapacity, nameof(battleReceiveBufferLength), nameof(battleReceiveOffset), nameof(battleReceiveReadCapacity));
            if (!IPAddress.TryParse(bindAddress, out IPAddress? parsedAddress) ||
                parsedAddress.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Bind address must be a numeric IPv4 address.", nameof(bindAddress));
            TimeSpan readyTimeout = battleReadyTimeout ?? TimeSpan.FromSeconds(15);
            if (readyTimeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(battleReadyTimeout));

            ControlPort = controlPort;
            BattlePort = battlePort;
            MaxSessions = maxSessions;
            MaxRooms = maxRooms;
            MaxRoomCapacity = maxRoomCapacity;
            _spawnStates = (PlayerState[])spawnStatesInSlotOrder.Clone();
            InputDelayTicks = inputDelayTicks;
            MaxFutureTickOffset = maxFutureTickOffset;
            AuthoritativeHistoryCapacity = authoritativeHistoryCapacity;
            BattleDurationTicks = battleDurationTicks;
            MaxControlPayloadLength = maxControlPayloadLength;
            MaxPendingControlBytesPerSession = maxPendingControlBytesPerSession;
            ControlReceiveBufferLength = controlReceiveBufferLength;
            ControlReceiveOffset = controlReceiveOffset;
            ControlReceiveReadCapacity = controlReceiveReadCapacity;
            MaxControlMessagesPerPump = maxControlMessagesPerPump;
            MaxControlSendBytesPerPump = maxControlSendBytesPerPump;
            MaxBattlePayloadLength = maxBattlePayloadLength;
            BattleReceiveBufferLength = battleReceiveBufferLength;
            BattleReceiveOffset = battleReceiveOffset;
            BattleReceiveReadCapacity = battleReceiveReadCapacity;
            BindAddress = parsedAddress;
            BattleDefinition = battleDefinition;
            BattleReadyTimeout = readyTimeout;
        }

        public int ControlPort { get; }
        public int BattlePort { get; }
        public int MaxSessions { get; }
        public int MaxRooms { get; }
        public int MaxRoomCapacity { get; }
        public int SpawnStateCount => _spawnStates.Length;
        public uint InputDelayTicks { get; }
        public uint MaxFutureTickOffset { get; }
        public int AuthoritativeHistoryCapacity { get; }
        public uint BattleDurationTicks { get; }
        public int MaxControlPayloadLength { get; }
        public int MaxPendingControlBytesPerSession { get; }
        public int ControlReceiveBufferLength { get; }
        public int ControlReceiveOffset { get; }
        public int ControlReceiveReadCapacity { get; }
        public int MaxControlMessagesPerPump { get; }
        public int MaxControlSendBytesPerPump { get; }
        public int MaxBattlePayloadLength { get; }
        public int BattleReceiveBufferLength { get; }
        public int BattleReceiveOffset { get; }
        public int BattleReceiveReadCapacity { get; }
        public IPAddress BindAddress { get; }
        public BattleDefinition? BattleDefinition { get; }
        public TimeSpan BattleReadyTimeout { get; }

        public PlayerState GetSpawnState(PlayerSlot slot)
        {
            if (slot.Value >= _spawnStates.Length) throw new ArgumentOutOfRangeException(nameof(slot));
            return _spawnStates[slot.Value];
        }

        private static void ValidatePort(int value, string name)
        {
            if ((uint)value > ushort.MaxValue) throw new ArgumentOutOfRangeException(name);
        }

        private static void RequirePositive(int value, string name)
        {
            if (value < 1) throw new ArgumentOutOfRangeException(name);
        }

        private static void ValidatePayload(int value, string name)
        {
            if (value < 1 || value > int.MaxValue - 4) throw new ArgumentOutOfRangeException(name);
        }

        private static void ValidateBuffer(int length, int offset, int capacity, string lengthName, string offsetName, string capacityName)
        {
            if (length < 1) throw new ArgumentOutOfRangeException(lengthName);
            if (offset < 0 || offset > length) throw new ArgumentOutOfRangeException(offsetName);
            if (capacity < 1 || capacity > length - offset) throw new ArgumentOutOfRangeException(capacityName);
        }
    }
}
