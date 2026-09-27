using Google.Protobuf;
using LockstepArena.Protocol.Wire;
using LockstepArena.StreamFraming;

namespace LockstepArena.Client.Demo
{
    internal static class ClientControlProtocol
    {
        internal static byte[] Frame(ClientControlCommandMessage command, int maxPayloadLength)
        {
            return LengthPrefixedFrameEncoder.Encode(command.ToByteArray(), maxPayloadLength);
        }
    }
}
