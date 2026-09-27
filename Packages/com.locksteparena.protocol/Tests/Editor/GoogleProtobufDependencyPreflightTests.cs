using System;
using System.IO;
using Google.Protobuf;
using NUnit.Framework;

namespace LockstepArena.Protocol.Editor.Tests
{
    public sealed class GoogleProtobufDependencyPreflightTests
    {
        [Test]
        public void RuntimeDependencyLoads()
        {
            byte[] encoded;
            using (var stream = new MemoryStream())
            {
                using (var output = new CodedOutputStream(stream, leaveOpen: true))
                {
                    output.WriteString("gate5-preflight");
                    output.Flush();
                }

                encoded = stream.ToArray();
            }

            var input = new CodedInputStream(encoded);
            ByteString bytes = ByteString.CopyFrom(encoded);
            Assert.That(input.ReadString(), Is.EqualTo("gate5-preflight"));
            Assert.That(bytes.Span.Length, Is.EqualTo(encoded.Length));
            Assert.That(typeof(ByteString).Assembly.GetName().Name, Is.EqualTo("Google.Protobuf"));
            Assert.That(
                typeof(ByteString).Assembly.GetName().Version,
                Is.EqualTo(new Version(3, 36, 0, 0)));
        }
    }
}
