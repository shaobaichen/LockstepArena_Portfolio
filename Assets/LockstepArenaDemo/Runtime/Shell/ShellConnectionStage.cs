#nullable enable

namespace LockstepArena.Demo
{
    public enum ShellConnectionStage
    {
        Idle = 0,
        StartingServer = 1,
        WaitingForServer = 2,
        Connecting = 3,
        EnteringSession = 4,
        CreatingRoom = 5,
        Ready = 6,
        Failed = 7,
    }
}
