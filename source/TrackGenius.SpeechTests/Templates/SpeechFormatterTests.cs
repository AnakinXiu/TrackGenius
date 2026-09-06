using System;
using NUnit.Framework;
using TrackGenius.Speech;

namespace TrackGenius.SpeechTests.Templates;

[TestFixture]
public class SpeechFormatterTests
{
    [TestCase(0, "zero")]
    [TestCase(7, "seven")]
    [TestCase(15, "fifteen")]
    [TestCase(100, "one hundred")]
    [TestCase(207, "two hundred seven")]
    [TestCase(999, "nine hundred ninety nine")]
    public void GivenNumber_WhenSpoken_ThenWords(int number, string expected)
        => Assert.That(SpeechFormatter.NumberToSpokenWords(number), Is.EqualTo(expected));

    [TestCase(1, "first")]
    [TestCase(2, "second")]
    [TestCase(3, "third")]
    [TestCase(4, "fourth")]
    [TestCase(10, "tenth")]
    [TestCase(11, "11th")]
    public void GivenPosition_WhenOrdinal_ThenWord(int position, string expected)
        => Assert.That(SpeechFormatter.Ordinal(position), Is.EqualTo(expected));

    [Test]
    public void GivenSubMinuteLap_WhenSpoken_ThenSecondsDigits()
        => Assert.That(SpeechFormatter.LapTimeToSpoken(TimeSpan.FromSeconds(12.438)),
            Is.EqualTo("twelve point four three eight seconds"));

    [Test]
    public void GivenOverMinuteLap_WhenSpoken_ThenMinutesThenSeconds()
        => Assert.That(SpeechFormatter.LapTimeToSpoken(TimeSpan.FromMilliseconds(72_400)),
            Is.EqualTo("one minute twelve point four zero zero seconds"));
}
