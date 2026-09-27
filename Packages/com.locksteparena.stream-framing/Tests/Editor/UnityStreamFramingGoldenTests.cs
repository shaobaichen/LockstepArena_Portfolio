using LockstepArena.StreamFraming.Verification;
using NUnit.Framework;

namespace LockstepArena.StreamFraming.Editor.Tests
{
    public sealed class UnityStreamFramingGoldenTests
    {
        [Test]
        public void UnityExecutesApprovedAbcSegmentationGolden()
        {
            Gate7FramingGoldenResult actual = Gate7FramingGoldenVector.Run();

            Assert.That(
                typeof(LengthPrefixedFrameDecoder).Assembly.GetName().Name,
                Is.EqualTo("LockstepArena.StreamFraming"));
            Assert.That(
                actual.FramedStream,
                Is.EqualTo(new byte[]
                {
                    0x00, 0x00, 0x00, 0x03, 0xDE, 0xAD, 0xBE,
                    0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0x02, 0x03, 0x04,
                    0x00, 0x00, 0x00, 0x08, 0xFF, 0x00, 0x7F, 0x80,
                    0x10, 0x20, 0x30, 0x40,
                }));
            Assert.That(actual.FeedBatchSizes, Is.EqualTo(new[] { 0, 0, 0, 2, 0, 1 }));
            Assert.That(actual.RecoveredPayloads.Length, Is.EqualTo(3));
            Assert.That(actual.RecoveredPayloads[0], Is.EqualTo(new byte[] { 0xDE, 0xAD, 0xBE }));
            Assert.That(actual.RecoveredPayloads[1], Is.EqualTo(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 }));
            Assert.That(
                actual.RecoveredPayloads[2],
                Is.EqualTo(new byte[] { 0xFF, 0x00, 0x7F, 0x80, 0x10, 0x20, 0x30, 0x40 }));
        }
    }
}
