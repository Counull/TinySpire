using NUnit.Framework;
using TinySpire.UI.Battle;

public sealed class ParticipantHudPresentationTests
{
    [Test]
    public void FormatHealth_UsesAuthoritativeCurrentAndMaximumValues()
    {
        Assert.That(ParticipantHudPresentation.FormatHealth(19, 80), Is.EqualTo("19 / 80"));
    }

    [Test]
    public void StrengthPresentation_HidesZeroAndKeepsTheLocalizedLabelForNonZeroValues()
    {
        Assert.That(ParticipantHudPresentation.ShouldShowStrength(0), Is.False);
        Assert.That(ParticipantHudPresentation.ShouldShowStrength(-2), Is.True);
        Assert.That(ParticipantHudPresentation.FormatStrength("力量", 2), Is.EqualTo("力量 +2"));
        Assert.That(ParticipantHudPresentation.FormatStrength("Strength", -2), Is.EqualTo("Strength -2"));
    }
}
