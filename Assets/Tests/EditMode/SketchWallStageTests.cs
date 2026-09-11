using System.Collections.Generic;
using NUnit.Framework;

// The detector's graph stages, pinned one at a time on handcrafted inputs, so a regression names
// the stage that moved rather than the plan that broke.
[TestFixture]
public class SketchWallStageTests
{
    /// <summary>The fixture's gray buffer as a top-down wall mask, no detector stages involved.</summary>
    private static bool[] Mask(SketchTestImages img)
    {
        var pixels = img.Pixels;   // bottom-up, like the detector's input
        var gray = SketchPlanDetector.ToGrayTopDown(pixels, img.Width, img.Height);
        var mask = new bool[gray.Length];
        for (int i = 0; i < gray.Length; i++) mask[i] = gray[i] < 128;
        return mask;
    }

    // ---- segment extraction ---------------------------------------------------------------------

    [Test]
    public void Extract_FindsTheFourWallsOfARectangle_OnTheirCenterlines()
    {
        var img = new SketchTestImages(300, 300);
        img.RectOutline(40, 30, 200, 140, 6);

        var segs = SketchWallSegments.Extract(Mask(img), 300, 300, stroke: 6);

        var majors = new List<WallSeg>();
        foreach (var s in segs) if (s.major) majors.Add(s);
        Assert.AreEqual(4, majors.Count);

        int horizontal = 0, vertical = 0;
        foreach (var s in majors)
        {
            Assert.AreEqual(6, s.thickness, "the stroke it was drawn with");
            if (s.horizontal)
            {
                horizontal++;
                Assert.That(s.Center, Is.EqualTo(32.5f).Within(1f).Or.EqualTo(166.5f).Within(1f));
            }
            else
            {
                vertical++;
                Assert.That(s.Center, Is.EqualTo(42.5f).Within(1f).Or.EqualTo(236.5f).Within(1f));
            }
        }
        Assert.AreEqual(2, horizontal);
        Assert.AreEqual(2, vertical);
    }

    [Test]
    public void Extract_MarksTheDoubleLineWindow_AsOneCrossing()
    {
        var img = new SketchTestImages(300, 300);
        img.RectOutline(40, 30, 220, 140, 8);
        img.Erase(100, 30, 80, 8);          // window break in the north wall...
        img.FillRect(100, 30, 80, 2);       // ...outer pane
        img.FillRect(100, 36, 80, 2);       // ...inner pane

        var segs = SketchWallSegments.Extract(Mask(img), 300, 300, stroke: 8);

        WallSeg north = default;
        bool found = false;
        foreach (var s in segs)
            if (s.major && s.horizontal && s.Center < 60f) { north = s; found = true; }
        Assert.IsTrue(found, "the north wall reads as ONE segment through the window");

        Assert.IsNotNull(north.dbl, "the window positions carry the double-line mark");
        int marked = 0;
        for (int i = 0; i < north.dbl.Length; i++) if (north.dbl[i]) marked++;
        Assert.AreEqual(80, marked, 6, "one mark per window column");
    }

    // ---- snapping -------------------------------------------------------------------------------

    [Test]
    public void Snap_PutsOffsetCollinearSegments_OnOneLine()
    {
        // The two halves of a hand-drawn wall, five pixels apart: one wall line, not two.
        var segs = new List<WallSeg>
        {
            new WallSeg { horizontal = true, center2 = 593, s0 = 40, s1 = 190, thickness = 6, major = true },
            new WallSeg { horizontal = true, center2 = 603, s0 = 260, s1 = 420, thickness = 6, major = true },
        };

        var grid = SketchWallSegments.Snap(segs, stroke: 6);

        Assert.AreEqual(1, grid.hLines.Length);
        Assert.AreEqual(296.5f, grid.hLines[0], 1e-3f, "the lower median member");
        Assert.AreEqual(2, grid.segs.Count);
        Assert.AreEqual(0, grid.segs[0].line);
        Assert.AreEqual(0, grid.segs[1].line);
    }

    // ---- cells ----------------------------------------------------------------------------------

    [Test]
    public void CellMap_CutsAnLShapedRoom_IntoTwoRects()
    {
        // A 200 x 200 square of four cells with the southeast cell open to the outside: an L.
        var xs = new[] { 0f, 100f, 200f };
        var ys = new[] { 0f, 100f, 200f };
        var hCover = new List<SketchCoverRun>[3];
        var vCover = new List<SketchCoverRun>[3];
        hCover[0] = Runs(0f, 200f);          // north wall
        hCover[1] = Runs(100f, 200f);        // the L's inner south wall
        hCover[2] = Runs(0f, 100f);          // south wall, stopping where the notch opens
        vCover[0] = Runs(0f, 200f);          // west wall
        vCover[1] = Runs(100f, 200f);        // the L's inner east wall
        vCover[2] = Runs(0f, 100f);          // east wall, stopping where the notch opens

        var cells = SketchCellMap.Build(xs, ys, hCover, vCover, stroke: 4);

        Assert.AreEqual(1, cells.roomCount);
        Assert.AreEqual(SketchCellMap.OUTSIDE, cells.LabelAt(150f, 150f), "the notch is outside");
        Assert.AreEqual(0, cells.LabelAt(50f, 150f));

        var rects = cells.Partition();
        Assert.AreEqual(2, rects.Count, "an L is two rectangles");
        Assert.AreEqual(0, rects[0].j0);
        Assert.AreEqual(0, rects[0].j1);
        Assert.AreEqual(1, rects[0].i1, "the top strip spans both columns");
        Assert.AreEqual(0, rects[1].i1, "the leg keeps to the west column");
    }

    [Test]
    public void CellMap_FoldsAHollowWallChannel_BetweenThinPanes()
    {
        // A 300 x 200 box split by a hollow divider: two panes 8 px apart, wider than the grouping
        // caps ever merge, so extraction minted two wall lines. The channel between them is wall,
        // not a room.
        var xs = new[] { 0f, 100f, 108f, 300f };
        var ys = new[] { 0f, 200f };
        var hCover = new[] { Runs(0f, 300f, 2), Runs(0f, 300f, 2) };
        var vCover = new[] { Runs(0f, 200f, 2), Runs(0f, 200f, 2), Runs(0f, 200f, 2), Runs(0f, 200f, 2) };

        var cells = SketchCellMap.Build(xs, ys, hCover, vCover, stroke: 2);

        Assert.AreEqual(2, cells.roomCount, "the channel folds; the two real rooms stand");
        Assert.AreEqual(SketchCellMap.FOLDED, cells.LabelAt(104f, 100f), "the channel is wall");
        Assert.AreEqual(0, cells.LabelAt(50f, 100f));
        Assert.AreEqual(1, cells.LabelAt(200f, 100f));
    }

    [Test]
    public void CellMap_FoldsAnLShapedChannel_WhoseBoundingBoxIsWideBothWays()
    {
        // A room walled hollow on its east and south sides: the channel turns the corner, so its
        // bounding box is wide in both axes. The old bounding-box fold kept it; per-cell judging
        // folds it, the corner junction cell included.
        var xs = new[] { 0f, 150f, 158f, 300f };
        var ys = new[] { 0f, 150f, 158f, 300f };
        var hCover = new[] { Runs(0f, 158f, 2), Runs(0f, 150f, 2), Runs(0f, 158f, 2), None() };
        var vCover = new[] { Runs(0f, 158f, 2), Runs(0f, 150f, 2), Runs(0f, 158f, 2), None() };

        var cells = SketchCellMap.Build(xs, ys, hCover, vCover, stroke: 2);

        Assert.AreEqual(1, cells.roomCount, "one room; the L channel is wall");
        Assert.AreEqual(0, cells.LabelAt(75f, 75f));
        Assert.AreEqual(SketchCellMap.FOLDED, cells.LabelAt(154f, 75f), "the east arm");
        Assert.AreEqual(SketchCellMap.FOLDED, cells.LabelAt(75f, 154f), "the south arm");
        Assert.AreEqual(SketchCellMap.FOLDED, cells.LabelAt(154f, 154f), "the corner junction");
    }

    [Test]
    public void CellMap_KeepsADooredStrip_BecauseAWalkableStripIsARoom()
    {
        // The hollow-divider geometry, but a doorway pierces ONE flank: the strip is a walk-in you
        // can enter and stand in, so it keeps its room label however thin it is.
        var xs = new[] { 0f, 100f, 108f, 300f };
        var ys = new[] { 0f, 200f };
        var hCover = new[] { Runs(0f, 300f, 2), Runs(0f, 300f, 2) };
        var vCover = new[] { Runs(0f, 200f, 2), Runs(0f, 200f, 2), Runs(0f, 200f, 2), Runs(0f, 200f, 2) };
        var doorways = new List<SketchDoorwayCandidate>
        {
            new SketchDoorwayCandidate { horizontal = false, line = 1, g0 = 80f, g1 = 120f,
                                         jambA = 60, jambB = 60, thickness = 2 },
        };

        var cells = SketchCellMap.Build(xs, ys, hCover, vCover, stroke: 2, doorways);

        Assert.AreEqual(3, cells.roomCount, "the doored strip stands");
        Assert.AreEqual(1, cells.LabelAt(104f, 100f));
    }

    [Test]
    public void CellMap_KeepsAWideStrip_BetweenThickFlanks()
    {
        // A 40 px passage between thick walls: past four times the thinner flank's thickness, the
        // strip is a corridor, not a channel.
        var xs = new[] { 0f, 100f, 140f, 300f };
        var ys = new[] { 0f, 200f };
        var hCover = new[] { Runs(0f, 300f, 6), Runs(0f, 300f, 6) };
        var vCover = new[] { Runs(0f, 200f, 6), Runs(0f, 200f, 6), Runs(0f, 200f, 6), Runs(0f, 200f, 6) };

        var cells = SketchCellMap.Build(xs, ys, hCover, vCover, stroke: 2);

        Assert.AreEqual(3, cells.roomCount, "a corridor is floor");
        Assert.AreEqual(1, cells.LabelAt(120f, 100f));
    }

    private static List<SketchCoverRun> Runs(float lo, float hi, int thickness = 0)
        => new List<SketchCoverRun> { new SketchCoverRun { lo = lo, hi = hi, realPx = (int)(hi - lo),
                                                           thickness = thickness } };

    private static List<SketchCoverRun> None() => new List<SketchCoverRun>();
}
