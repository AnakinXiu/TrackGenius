using System;
using NSubstitute.Core.SequenceChecking;
using NUnit.Framework;
using TrackGenius.Const;

namespace TrackGenius.ConstTests
{
    [TestFixture]
    public class ByteArrayExtensionTests
    {
        [Test]
        public void GivenNullByteArray_WhenCutCalled_ThenEmptyArrayReturned()
        {
            byte[] byteArray = null;
            Assert.That(() => byteArray.Cut(1, 1), Throws.ArgumentNullException);
        }

        [Test]
        public void GivenIndexLessThanZero_WhenCutCalled_ThenOutOfRangeExceptionThrown()
        {
            var byteArray = new byte[] {1, 2, 3};
            Assert.That(() => byteArray.Cut(-1, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void GivenIndexGreaterThanArrayLength_WhenCutCalled_ThenOutOfRangeExceptionThrown()
        {
            var byteArray = new byte[] {1, 2, 3};
            Assert.That(() => byteArray.Cut(4, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void GivenLengthGreaterThanAvailable_WhenCutCalled_ThenOutOfRangeExceptionThrown()
        {
            var byteArray = new byte[] {1, 2, 3, 4, 5};
            Assert.That(() => byteArray.Cut(2, 4), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void GivenAvailableIndexAndLength_WhenCutCalled_ThenGetExpectResult()
        {
            var byteArray = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9};
            CollectionAssert.AreEqual(byteArray.Cut(3, 4), new byte[] {4, 5, 6, 7});
        }

        [Test]
        public void GivenNullByteArray_WhenReverseCalled_ThenEmptyArrayReturned()
        {
            byte[] byteArray = null;
            Assert.That(() => byteArray.Reverse(), Throws.ArgumentNullException);
        }

        [Test]
        public void GivenByteArray_WhenReverseCalled_ThenArrayWithOppositeOrderReturned()
        {
            byte[] byteArray = {1, 2, 3, 4, 5};
            var reverseArray = byteArray.Reverse();

            CollectionAssert.AreEqual(new byte[] {5, 4, 3, 2, 1}, reverseArray);
        }

        [Test]
        public void GivenNullByteArray_WhenToInt32Called_ThenEmptyArrayReturned()
        {
            byte[] byteArray = null;
            Assert.That(() => byteArray.ToInt32(), Throws.ArgumentNullException);
        }

        [TestCase(new byte[] {1, 2, 3, 4}, 16909060)]
        [TestCase(new byte[] {4, 3, 2, 1}, 67305985)]
        public void GivenByteArray_WhenToInt32Called_ThenExpectedIntValueReturned(byte[] byteArray, int expected)
        {
            Assert.That(byteArray.ToInt32(), Is.EqualTo(expected));
        }
    }
}