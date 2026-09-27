using System;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Server.Protocol.Tests
{
    internal static class ProtocolParserContractTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(MalformedBytesRemainParserFailure), MalformedBytesRemainParserFailure),
            new TestCase(nameof(NullMapperArgumentsRemainArgumentNull), NullMapperArgumentsRemainArgumentNull),
            new TestCase(nameof(ParsedSemanticFailureUsesProtocolMappingException), ParsedSemanticFailureUsesProtocolMappingException),
        };

        private static void MalformedBytesRemainParserFailure()
        {
            TestAssert.Throws<InvalidProtocolBufferException>(
                () => ActiveRosterMessage.Parser.ParseFrom(new byte[] { 0x0A, 0x05, 0x08 }));
        }

        private static void NullMapperArgumentsRemainArgumentNull()
        {
            TestAssert.Throws<ArgumentNullException>(() => ProtocolMapper.ToWire((ActiveRoster)null!));
            TestAssert.Throws<ArgumentNullException>(() => ProtocolMapper.ToDomain((ActiveRosterMessage)null!));
            TestAssert.Throws<ArgumentNullException>(
                () => ProtocolMapper.ToDomain((PlayerInputSubmissionMessage)null!));
        }

        private static void ParsedSemanticFailureUsesProtocolMappingException()
        {
            TestAssert.Throws<ProtocolMappingException>(
                () => ProtocolMapper.ToDomain(new ActiveRosterMessage()));
        }
    }
}
