using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrackGenius.Model;

namespace TrackGenius.UITests.Model;

[TestFixture]
public class LapAnalysisCalculatorTests
{
    private static IReadOnlyList<TimeSpan> Laps(params double[] seconds)
        => seconds.Select(s => TimeSpan.FromSeconds(s)).ToList();

    [Test]
    public void GivenFivePlusLaps_WhenTopAverage5_ThenMeanOfFiveFastest()
    {
        var laps = Laps(20.1, 19.8, 20.5, 19.9, 20.3, 21.0);

        var result = LapAnalysisCalculator.TopAverage(laps, 5);

        Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(20.12)));
    }

    [Test]
    public void GivenFewerThanFiveLaps_WhenTopAverage5_ThenNull()
    {
        var laps = Laps(20.1, 19.8, 20.5, 19.9);

        Assert.That(LapAnalysisCalculator.TopAverage(laps, 5), Is.Null);
    }

    [Test]
    public void GivenTenPlusLaps_WhenTopAverage10_ThenMeanOfTenFastest()
    {
        var laps = Laps(20.0, 20.1, 19.9, 20.2, 20.0, 20.3, 19.8, 20.4, 19.9, 20.0, 21.5);

        var result = LapAnalysisCalculator.TopAverage(laps, 10);

        Assert.That(result!.Value.TotalSeconds, Is.EqualTo(20.06).Within(0.0001));
    }

    [Test]
    public void GivenFewerThanTenLaps_WhenTopAverage10_ThenNull()
    {
        Assert.That(LapAnalysisCalculator.TopAverage(Laps(20, 20, 20, 20, 20, 20, 20, 20, 20), 10), Is.Null);
    }

    [Test]
    public void GivenConsecutiveWindow_WhenTop3Consecutive_ThenFastestWindowSum()
    {
        // Windows: 60.2, 60.0, 61.2 → fastest = laps 2-4 = 60.0.
        var laps = Laps(20.5, 19.8, 19.9, 20.3, 21.0);

        var result = LapAnalysisCalculator.Top3Consecutive(laps);

        Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(60.0)));
    }

    [Test]
    public void GivenExactlyThreeLaps_WhenTop3Consecutive_ThenWholeRaceSum()
    {
        Assert.That(LapAnalysisCalculator.Top3Consecutive(Laps(20, 21, 22)), Is.EqualTo(TimeSpan.FromSeconds(63)));
    }

    [Test]
    public void GivenFewerThanThreeLaps_WhenTop3Consecutive_ThenNull()
    {
        Assert.That(LapAnalysisCalculator.Top3Consecutive(Laps(20, 21)), Is.Null);
    }

    [Test]
    public void GivenLaps_WhenStdDeviation_ThenPopulationSigmaInSeconds()
    {
        // μ = 20.1; deviations: -0.1, 0.3, -0.3, 0.1 → σ = √0.05 ≈ 0.2236.
        var result = LapAnalysisCalculator.StdDeviation(Laps(20.0, 20.4, 19.8, 20.2));

        Assert.That(result, Is.EqualTo(0.22360679774997816).Within(0.0001));
    }

    [Test]
    public void GivenIdenticalLaps_WhenStdDeviation_ThenZero()
    {
        Assert.That(LapAnalysisCalculator.StdDeviation(Laps(20, 20, 20, 20)), Is.EqualTo(0).Within(0.0001));
    }

    [Test]
    public void GivenSingleLap_WhenStdDeviation_ThenNull()
    {
        Assert.That(LapAnalysisCalculator.StdDeviation(Laps(20.0)), Is.Null);
    }

    [Test]
    public void GivenLaps_WhenConsistency_ThenOneMinusSigmaOverMuPercent()
    {
        // μ = 20.1, σ = 0.2236... → (1 − 0.2236/20.1) × 100 ≈ 98.8875.
        var result = LapAnalysisCalculator.Consistency(Laps(20.0, 20.4, 19.8, 20.2));

        Assert.That(result, Is.EqualTo(98.887528369403086).Within(0.0001));
    }

    [Test]
    public void GivenIdenticalLaps_WhenConsistency_Then100()
    {
        Assert.That(LapAnalysisCalculator.Consistency(Laps(20, 20, 20)), Is.EqualTo(100).Within(0.0001));
    }

    [Test]
    public void GivenExtremeSpread_WhenConsistency_ThenClampedAtZero()
    {
        // Laps 40, 40, -20, 0: μ = 15, Σd² = 2700, σ = √675 ≈ 25.98
        // → 1 − 25.98/15 = −0.732 → clamped to 0.
        Assert.That(LapAnalysisCalculator.Consistency(Laps(40, 40, -20, 0)), Is.EqualTo(0).Within(0.0001));
    }
}
