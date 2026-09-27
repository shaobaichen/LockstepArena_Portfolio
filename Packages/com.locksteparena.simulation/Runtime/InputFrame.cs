using System;

namespace LockstepArena.Simulation
{
    public readonly struct InputFrame
    {
        public InputFrame(uint tick, PlayerSlot playerSlot, sbyte moveX, sbyte moveZ, ushort aim)
            : this(tick, playerSlot, moveX, moveZ, aim, false)
        {
        }

        public InputFrame(uint tick, PlayerSlot playerSlot, sbyte moveX, sbyte moveZ, ushort aim, bool fire)
        {
            if (moveX < -1 || moveX > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(moveX), "Movement must be -1, 0, or 1.");
            }

            if (moveZ < -1 || moveZ > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(moveZ), "Movement must be -1, 0, or 1.");
            }

            Tick = tick;
            PlayerSlot = playerSlot;
            MoveX = moveX;
            MoveZ = moveZ;
            Aim = aim;
            Fire = fire;
        }

        public uint Tick { get; }

        public PlayerSlot PlayerSlot { get; }

        public sbyte MoveX { get; }

        public sbyte MoveZ { get; }

        public ushort Aim { get; }

        public bool Fire { get; }
    }
}
