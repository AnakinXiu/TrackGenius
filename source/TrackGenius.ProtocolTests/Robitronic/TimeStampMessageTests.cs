using System;
using NUnit.Framework;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.ProtocolTests
{
    [TestFixture]
    public class TimeStampMessageTests
    {
        [Test]
        public void GivenNullByteArray_WhenConstructorCalled_ThenThrowNullException()
        {
            Assert.That(() => new TimeStampMessage(null), Throws.ArgumentNullException);
        }

        [TestCase(new byte[] { 1 })]
        [TestCase(new byte[] { 2, 1 })]
        public void GivenByteArrayLengthLessThan3_WhenConstructorCalled_ThenThrowOutOfRangeException(byte[] data)
        {
            Assert.That(() => new TimeStampMessage(data), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }


        [TestCase(new byte[] { 3, 2, 1 })]
        [TestCase(new byte[] { 4, 3, 2, 1 })]
        public void GivenByteArrayWhoseThirdElementIsNot0x83_WhenConstructorCalled_ThenThrowFormatException(byte[] data)
        {
            Assert.That(() => new TimeStampMessage(data), Throws.InstanceOf<FormatException>());
        }

        [TestCase(new byte[] { 0x0b, 0x05, 0x83, 0x58, 0x80, 0x04, 0x00, 0x14, 0xd0, 0x01, 0x02 }, 0x05, 295000)]
        [TestCase(new byte[] { 0x0b, 0x87, 0x83, 0xc8, 0x50, 0xf5, 0x00, 0x14, 0xd0, 0x01, 0x02 }, 0x87, 16077000)]
        public void GivenCorrectMessageDataArray_WhenConstructorCalled_ThenMessageClassCreated(byte[] data, byte checksum, int time)
        {
            var message = new TimeStampMessage(data);

            Assert.That(message.PacketLength, Is.EqualTo(11));
            Assert.That(message.Checksum, Is.EqualTo(checksum));
            Assert.That(message.Milliseconds, Is.EqualTo(time));
            Assert.That(message.PacketType, Is.EqualTo(PacketType.TimeStamp));
        }
    }
}