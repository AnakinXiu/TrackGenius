using NSubstitute;
using NUnit.Framework;
using TrackGenius.Protocol;

namespace TrackGenius.ProtocolTests
{
    [TestFixture]
    public class UnitTest1
    {
        [Test]
        public void TestMethod1()
        {
            var uplinkMessage = Substitute.For<IUplinkMessage>();
        }
    }
}
