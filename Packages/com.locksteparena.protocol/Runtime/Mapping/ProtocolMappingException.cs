using System;

namespace LockstepArena.Protocol
{
    public sealed class ProtocolMappingException : Exception
    {
        public ProtocolMappingException(string message)
            : base(message)
        {
        }

        public ProtocolMappingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
