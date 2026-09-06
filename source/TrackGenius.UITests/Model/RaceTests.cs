using System;
using System.Collections.Generic;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class RaceTests
{
    private static Race CreateRace()
        => new(Guid.Empty, RaceType.FreePractice, new RaceClass("World GT"), new List<RaceData>());

    [Test]
    public void GivenNewRace_WhenPropertiesRead_ThenDefaultRuleAndCalculator()
    {
        var race = CreateRace();

        Assert.Multiple(() =>
        {
            Assert.That(race.OrderRule, Is.EqualTo(RaceOrderRule.LapsThenRaceTime));
            Assert.That(race.OrderCalculator, Is.InstanceOf<LapsRaceTimeOrderCalculator>());
        });
    }

    [Test]
    public void GivenRace_WhenOrderRuleSet_ThenCalculatorMatchesRule()
    {
        var race = CreateRace();

        race.OrderRule = RaceOrderRule.LapsThenRaceTime;   // only rule today

        Assert.That(race.OrderCalculator, Is.InstanceOf<LapsRaceTimeOrderCalculator>());
    }
}
