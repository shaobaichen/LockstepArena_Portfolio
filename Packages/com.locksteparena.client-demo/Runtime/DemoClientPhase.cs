namespace LockstepArena.Client.Demo
{
    public enum DemoClientPhase
    {
        Disconnected,
        ConnectingControl,
        AwaitingSessionEntry,
        Lobby,
        Room,
        PreparingBattle,
        InBattle,
        SettlementPendingAuthority,
        Settlement,
        Faulted,
        Disposed
    }
}
