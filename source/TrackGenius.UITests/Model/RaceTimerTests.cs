using System;
using System.Threading;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class RaceTimerTests
{
    [Test]
    public void GivenNewTimer_WhenStartCalled_ThenIsStartedAndElapsedGrows()
    {
        var timer = new RaceTimer(10);

        timer.Start();
        Thread.Sleep(30);

        Assert.Multiple(() =>
        {
            Assert.That(timer.IsStarted, Is.True);
            Assert.That(timer.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(25)));
        });
    }

    [Test]
    public void GivenUnstartedTimer_WhenPropertiesRead_ThenNotStartedAndZeroElapsed()
    {
        var timer = new RaceTimer(10);

        Assert.Multiple(() =>
        {
            Assert.That(timer.IsStarted, Is.False);
            Assert.That(timer.Elapsed, Is.EqualTo(TimeSpan.Zero));
            Assert.That(timer.Remaining, Is.EqualTo(TimeSpan.FromSeconds(10)));
        });
    }

    [Test]
    public void GivenExpiredCountdown_WhenRemainingRead_ThenNegativeAllowed()
    {
        var timer = new RaceTimer(0);
        timer.Start();

        Assert.That(timer.Remaining, Is.LessThanOrEqualTo(TimeSpan.Zero));
    }

    [Test]
    public void GivenStartedTimer_WhenStopped_ThenElapsedFreezesAndIsStartedFalse()
    {
        var timer = new RaceTimer(10);
        timer.Start();
        Thread.Sleep(20);

        timer.Stop();
        var frozen = timer.Elapsed;
        Thread.Sleep(20);

        Assert.Multiple(() =>
        {
            Assert.That(timer.IsStarted, Is.False);
            Assert.That(timer.Elapsed, Is.EqualTo(frozen));
        });
    }
}
