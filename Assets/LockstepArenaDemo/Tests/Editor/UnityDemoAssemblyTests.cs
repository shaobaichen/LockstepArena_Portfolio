using System;
using NUnit.Framework;

namespace LockstepArena.Demo.Editor.Tests
{
    public sealed class UnityDemoAssemblyTests
    {
        [Test]
        public void UnityLoadsMigratedGate13ClientLiveTcpTypes()
        {
            Assert.That(Type.GetType("LockstepArena.Client.LiveTcp.TcpClientBattlePump, LockstepArena.Client.LiveTcp"), Is.Not.Null);
            Assert.That(Type.GetType("LockstepArena.Client.LiveTcp.PredictedTcpClientBattleRuntime, LockstepArena.Client.LiveTcp"), Is.Not.Null);
        }
    }
}
