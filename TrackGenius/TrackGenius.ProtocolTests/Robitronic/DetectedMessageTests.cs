using System;
using NUnit.Framework;
using TrackGenius.Protocol.Robitronic;

namespace TrackGenius.ProtocolTests
{
    [TestFixture]
    public class DetectedMessageTests
    {
        [Test]
        public void GivenNullByteArray_WhenConstructorCalled_ThenThrowNullException()
        {
            Assert.That(()=>new DetectedMessage(null), Throws.ArgumentNullException);
        }

        [TestCase(new byte[] { 1 })]
        [TestCase(new byte[] { 2, 1 })]
        public void GivenByteArrayLengthLessThan3_WhenConstructorCalled_ThenThrowOutOfRangeException(byte[] data)
        {
            Assert.That(() => new DetectedMessage(data), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [TestCase(new byte[] { 3, 2, 1 })]
        [TestCase(new byte[] { 4, 3, 2, 1 })]
        public void GivenByteArrayWhoseThirdElementIsNot0x84_WhenConstructorCalled_ThenThrowFormatException(byte[] data)
        {
            Assert.That(() => new DetectedMessage(data), Throws.InstanceOf<FormatException>());
        }

        [TestCase(new byte[] { 0x0d, 0x85, 0x84, 0x00, 0x00, 0xad, 0xb4, 0x00, 0x83, 0xee, 0x38, 0x50, 0xf8 }, "44468", 0x85, 80, 8646200, 248)]
        [TestCase(new byte[] { 0x0d, 0x27, 0x84, 0x00, 0x01, 0x38, 0x9b, 0x00, 0x84, 0xe4, 0x07, 0x57, 0xfa }, "80027", 0x27, 87, 8709127, 250)]
        public void GivenCorrectMessageDataArray_WhenConstructorCalled_ThenMessageClassCreated(
            byte[] data, 
            string transponderId, 
            byte checksum, 
            int hits, 
            int time, 
            int signalLevel)
        {
            var message = new DetectedMessage(data);

            CollectionAssert.AreEqual(message.ByteData, data);
            Assert.That(message.TransponderID, Is.EqualTo(transponderId));
            Assert.That(message.Checksum, Is.EqualTo(checksum));
            Assert.That(message.Hits, Is.EqualTo(hits));
            Assert.That(message.Milliseconds, Is.EqualTo(time));
            Assert.That(message.SignalLevel, Is.EqualTo(signalLevel));
            Assert.That(message.PacketType, Is.EqualTo(PacketType.CarDetect));
            Assert.That(message.PacketLength, Is.EqualTo(13));
        }

        [Test]
        public void GivenDetectedMessage_WhenDeserializeCalled_ThenMessageDataInHexFormatReturned()
        {
            var message = new DetectedMessage(new byte[] { 0x0d, 0xa3, 0x84, 0x00, 0x01, 0x3e, 0x8f, 0x00, 0x0e, 0x80, 0xbb, 0x0f, 0x41 });
            Assert.AreEqual(message.Deserialize(), "0D A3 84 00 01 3E 8F 00 0E 80 BB 0F 41");
        }

        [Test]
        public void GivenDetectedMessage_WhenToStringCalled_ThenMessageDataInHexFormatReturned()
        {
            var message = new DetectedMessage(new byte[] { 0x0d, 0xa3, 0x84, 0x00, 0x01, 0x3e, 0x8f, 0x00, 0x0e, 0x80, 0xbb, 0x0f, 0x41 });
            Assert.AreEqual(message.ToString(), "0D A3 84 00 01 3E 8F 00 0E 80 BB 0F 41");
        }
    }
}
