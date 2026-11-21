using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The usage chart under a CLI (M.ORCH-T1887) sizes to its frame on a desk and in the phone layout alike.</summary>
public class TerminalChartHeightTests
{
    [Test]
    public void TakesItsShareOfATypicalFrame() => Assert.That(TerminalChartHeight.For(1000), Is.EqualTo(120).Within(0.001));

    [Test]
    public void IsCappedInATallFrame() => Assert.That(TerminalChartHeight.For(4000), Is.EqualTo(TerminalChartHeight.MaxHeight));

    [Test]
    public void IsFlooredInAShortFrame() => Assert.That(TerminalChartHeight.For(300), Is.EqualTo(TerminalChartHeight.MinHeight));

    [Test]
    public void LeavesThePhoneLayoutsShortestTerminalMostOfTheFrame() =>
        Assert.That(TerminalChartHeight.For(160), Is.EqualTo(40).Within(0.001));

    [TestCase(0d)]
    [TestCase(double.NaN)]
    public void TakesNothingBeforeLayout(double h) => Assert.That(TerminalChartHeight.For(h), Is.Zero);
}
