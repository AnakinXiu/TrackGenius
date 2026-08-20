using NUnit.Framework;

namespace TrackGenius.UITests;

[TestFixture]
public class SanityTests
{
    [Test]
    public void Framework_Is_Ready() => Assert.That(true, Is.True);
}
