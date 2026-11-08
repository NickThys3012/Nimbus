using Nimbus.Domain.Entities;
using Nimbus.Domain.Enums;
using Nimbus.Domain.Services;
namespace Nimbus.Application.Tests;

/// <summary>
///     Issue #38. The limits below are test fixtures, deliberately different from the product defaults,
///     to prove the evaluator reads the limits it is given. Boundaries are inclusive.
/// </summary>
public class GoNoGoEvaluatorTests
{
    // wind yellow 8 / red 12 kt; visibility yellow 6 / red 4 km; CAPE yellow 250 / red 400.
    private static readonly GoNoGoLimits Limits = new(8m, 12m, 6m, 4m, 250m, 400m);
    private const decimal GoodWind = 2m, GoodVis = 20m, GoodCape = 0m;

    [Test]
    public void NoData_IsUnknown_NeverGreen() =>
        Assert.That(GoNoGoEvaluator.Evaluate(null, null, null, Limits), Is.EqualTo(GoNoGoVerdict.Unknown));

    [Test]
    public void AllGood_IsGreen() =>
        Assert.That(GoNoGoEvaluator.Evaluate(GoodWind, GoodVis, GoodCape, Limits), Is.EqualTo(GoNoGoVerdict.Green));

    [TestCase(7.99, GoNoGoVerdict.Green)]
    [TestCase(8, GoNoGoVerdict.Yellow)]
    [TestCase(11.99, GoNoGoVerdict.Yellow)]
    [TestCase(12, GoNoGoVerdict.Red)]
    [TestCase(30, GoNoGoVerdict.Red)]
    public void Wind_Boundaries(double kt, GoNoGoVerdict expected) =>
        Assert.That(GoNoGoEvaluator.Evaluate((decimal)kt, GoodVis, GoodCape, Limits), Is.EqualTo(expected));

    [TestCase(6.01, GoNoGoVerdict.Green)]
    [TestCase(6, GoNoGoVerdict.Yellow)]
    [TestCase(4.01, GoNoGoVerdict.Yellow)]
    [TestCase(4, GoNoGoVerdict.Red)]
    [TestCase(0, GoNoGoVerdict.Red)]
    public void Visibility_Boundaries(double km, GoNoGoVerdict expected) =>
        Assert.That(GoNoGoEvaluator.Evaluate(GoodWind, (decimal)km, GoodCape, Limits), Is.EqualTo(expected));

    [TestCase(249.99, GoNoGoVerdict.Green)]
    [TestCase(250, GoNoGoVerdict.Yellow)]
    [TestCase(399.99, GoNoGoVerdict.Yellow)]
    [TestCase(400, GoNoGoVerdict.Red)]
    [TestCase(2000, GoNoGoVerdict.Red)]
    public void Cape_Boundaries(double j, GoNoGoVerdict expected) =>
        Assert.That(GoNoGoEvaluator.Evaluate(GoodWind, GoodVis, (decimal)j, Limits), Is.EqualTo(expected));

    [Test]
    public void RedWins_OverYellowAndGreen()
    {
        Assert.That(GoNoGoEvaluator.Evaluate(12m, 6m, 0m, Limits), Is.EqualTo(GoNoGoVerdict.Red));
        Assert.That(GoNoGoEvaluator.Evaluate(8m, 4m, 250m, Limits), Is.EqualTo(GoNoGoVerdict.Red));
        Assert.That(GoNoGoEvaluator.Evaluate(GoodWind, GoodVis, 400m, Limits), Is.EqualTo(GoNoGoVerdict.Red));
        Assert.That(GoNoGoEvaluator.Evaluate(8m, GoodVis, GoodCape, Limits), Is.EqualTo(GoNoGoVerdict.Yellow));
    }

    [Test]
    public void MissingMetrics_AreSkipped_ButOthersStillCount()
    {
        Assert.That(GoNoGoEvaluator.Evaluate(null, null, 400m, Limits), Is.EqualTo(GoNoGoVerdict.Red));
        Assert.That(GoNoGoEvaluator.Evaluate(null, 5m, null, Limits), Is.EqualTo(GoNoGoVerdict.Yellow));
        Assert.That(GoNoGoEvaluator.Evaluate(1m, null, null, Limits), Is.EqualTo(GoNoGoVerdict.Green));
    }

    [Test]
    public void DifferentLimits_GiveDifferentVerdict_ForSameValues()
    {
        var strict = new GoNoGoLimits(3m, 5m, 6m, 4m, 250m, 400m);
        Assert.That(GoNoGoEvaluator.Evaluate(6m, GoodVis, GoodCape, Limits), Is.EqualTo(GoNoGoVerdict.Green));
        Assert.That(GoNoGoEvaluator.Evaluate(6m, GoodVis, GoodCape, strict), Is.EqualTo(GoNoGoVerdict.Red));
    }

    [Test]
    public void FlightEntryPoint_UsesMeteoFields_AndMatchesRawEntryPoint()
    {
        var flight = new FlightPreparation { WindSpeedKt = 9m, VisibilityKm = 10m, CapeJPerKg = 0m };
        Assert.That(GoNoGoEvaluator.Evaluate(flight, Limits), Is.EqualTo(GoNoGoVerdict.Yellow));
        Assert.That(GoNoGoEvaluator.Evaluate(new FlightPreparation(), Limits), Is.EqualTo(GoNoGoVerdict.Unknown));
        flight.CapeJPerKg = 400m;
        Assert.That(GoNoGoEvaluator.Evaluate(flight, Limits), Is.EqualTo(GoNoGoEvaluator.Evaluate(9m, 10m, 400m, Limits)));
    }

    [Test]
    public void LimitsFromThresholdEntity_MapAllSixValues()
    {
        var t = new GoNoGoThreshold { WindYellowKt = 1, WindRedKt = 2, VisibilityYellowKm = 3, VisibilityRedKm = 4, CapeYellow = 5, CapeRed = 6 };
        Assert.That(GoNoGoLimits.From(t), Is.EqualTo(new GoNoGoLimits(1, 2, 3, 4, 5, 6)));
    }
}
