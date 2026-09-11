using NUnit.Framework;

// The step a drag-scrubbable field moves by under each modifier. The facing field is why the fine
// override exists: it steps 15 degrees and Shift wants whole degrees, which a tenth of 15 is not.
[TestFixture]
public class ScrubMathTests
{
    [Test]
    public void Fine_IsATenthOfTheStepByDefault()
    {
        Assert.AreEqual(1.5f, ScrubMath.Step(15f, fine: true, coarse: false), 1e-5f);
    }

    [Test]
    public void FineStep_OverridesTheTenth()
    {
        Assert.AreEqual(1f, ScrubMath.Step(15f, fine: true, coarse: false, fineStep: 1f), 1e-5f);
    }

    [Test]
    public void FineStep_OnlyAppliesUnderShift()
    {
        Assert.AreEqual(15f, ScrubMath.Step(15f, fine: false, coarse: false, fineStep: 1f), 1e-5f);
        Assert.AreEqual(150f, ScrubMath.Step(15f, fine: false, coarse: true, fineStep: 1f), 1e-5f);
    }

    [Test]
    public void Fine_StillWinsOverCoarse()
    {
        // The cautious reading of an ambiguous chord is the one that cannot run away with a value.
        Assert.AreEqual(1f, ScrubMath.Step(15f, fine: true, coarse: true, fineStep: 1f), 1e-5f);
    }

    [Test]
    public void Advance_UsesTheFineStepUnderShift()
    {
        // One step's worth of travel at 20 px per unmodified step buys one fine step under Shift.
        float moved = ScrubMath.Advance(0f, 20f, 15f, 20f, fine: true, coarse: false, fineStep: 1f);

        Assert.AreEqual(1f, moved, 1e-5f);
    }
}
