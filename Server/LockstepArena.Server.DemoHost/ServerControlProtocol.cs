using LockstepArena.Protocol.Wire;

namespace LockstepArena.Server.DemoHost
{
    internal static class ServerControlProtocol
    {
        internal static RoomLifecycleMessage ToWire(DemoRoomLifecycle lifecycle)
        {
            return lifecycle switch
            {
                DemoRoomLifecycle.Open => RoomLifecycleMessage.RoomLifecycleOpen,
                DemoRoomLifecycle.PreparingBattle => RoomLifecycleMessage.RoomLifecyclePreparingBattle,
                DemoRoomLifecycle.InBattle => RoomLifecycleMessage.RoomLifecycleInBattle,
                DemoRoomLifecycle.Settled => RoomLifecycleMessage.RoomLifecycleSettled,
                _ => RoomLifecycleMessage.RoomLifecycleUnspecified,
            };
        }
    }
}
