using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// A dresser placed by a wall should stand against the wall, not three centimetres off it and not
// three centimetres into it, and a nightstand pushed into a corner should sit in the corner.
// FurnitureSnap is what makes that true for the ghost, the move handle and every turn or resize of an
// item already against a wall. The contract pinned here: it measures to the FACE (half a thickness off
// the centerline, on the item's side), it uses the item's true rotated outline, it only considers
// walls the item is actually alongside, a corner beats either of its walls, and among single walls the
// nearest wins.
[TestFixture]
public class FurnitureSnapTests
{
    private const float RANGE = 0.15f;

    [Test]
    public void WithinRange_SnapsTheEdgeOntoTheFace()
    {
        // Wall along x at z = 0, 0.1 thick, so its faces are at z = ±0.05. A 0.6 m item centred at
        // z = 0.42 has its near edge at 0.12, seven centimetres off the face.
        var level = Level(Wall("w1", 0, 0, 4, 0));

        var r = FurnitureSnap.ToWall(new Vector2(1f, 0.42f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual("w1", r.wallId);
        Assert.IsNull(r.wallId2);
        Assert.AreEqual(1f, r.position.x, 1e-4f);
        Assert.AreEqual(0.35f, r.position.y, 1e-4f);
    }

    [Test]
    public void BeyondRange_LeavesTheItemWhereItIs()
    {
        var level = Level(Wall("w1", 0, 0, 4, 0));

        var r = FurnitureSnap.ToWall(new Vector2(1f, 0.60f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.IsFalse(r.snapped);
        Assert.IsNull(r.wallId);
        Assert.AreEqual(0.60f, r.position.y, 1e-5f);
    }

    [Test]
    public void PushedIntoTheWall_ComesBackOut()
    {
        // Near edge at z = 0.0, buried five centimetres into the wall: always in range, and pushed out.
        var level = Level(Wall("w1", 0, 0, 4, 0));

        var r = FurnitureSnap.ToWall(new Vector2(1f, 0.30f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.35f, r.position.y, 1e-4f);
    }

    [Test]
    public void OtherSideOfTheWall_SnapsToTheOtherFace()
    {
        var level = Level(Wall("w1", 0, 0, 4, 0));

        var r = FurnitureSnap.ToWall(new Vector2(1f, -0.42f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(-0.35f, r.position.y, 1e-4f);
    }

    [Test]
    public void QuarterTurn_SwapsWidthAndDepth()
    {
        // 0.6 wide, 1.0 deep, turned a quarter: Euler(0, 90, 0) maps the item's depth onto world x, so
        // it presents its 1.0 side to a wall along z and the half-extent toward the wall is 0.5.
        var level = Level(Wall("w1", 0, 0, 0, 4));

        var r = FurnitureSnap.ToWall(new Vector2(0.62f, 2f), 0.6f, 1.0f, 90f, level, RANGE);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.55f, r.position.x, 1e-4f);
        Assert.AreEqual(2f, r.position.y, 1e-4f);
    }

    [Test]
    public void DiagonalWall_UsesTheTrueOutlineNotTheAxisAlignedBound()
    {
        // A wall at 45° and a 0.6 m item turned to match it, so its edge is parallel to the face and
        // its half-extent toward the wall is exactly 0.3. FurnitureFit.Footprint's axis-aligned bound
        // would say 0.424 and hold the item 12 cm off the wall.
        var level = Level(Wall("w1", 0, 0, 4, 4));
        var n = new Vector2(-1f, 1f).normalized;            // the left normal of a -> b
        Vector2 center = new Vector2(2f, 2f) + n * 0.42f;   // near edge 0.12 out, face at 0.05

        var r = FurnitureSnap.ToWall(center, 0.6f, 0.6f, 45f, level, RANGE);

        Vector2 expected = new Vector2(2f, 2f) + n * 0.35f;
        Assert.IsTrue(r.snapped);
        Assert.AreEqual(expected.x, r.position.x, 1e-4f);
        Assert.AreEqual(expected.y, r.position.y, 1e-4f);
    }

    [Test]
    public void PastTheEndOfTheWall_IsNotAlongsideIt()
    {
        var level = Level(Wall("w1", 0, 0, 4, 0));

        var r = FurnitureSnap.ToWall(new Vector2(5f, 0.42f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.IsFalse(r.snapped);
    }

    // ---- corners ----------------------------------------------------------------------------

    [Test]
    public void InACorner_TucksFlushAgainstBothWalls()
    {
        // 0.07 m off the wall along x and 0.10 m off the wall along z. Closing only the nearer of the
        // two, which is what a one-wall snap does, leaves the item a centimetre off the other wall with
        // nothing but the eye to true it up: the whole reason a corner is solved as a corner.
        var level = Level(Wall("w1", 0, 0, 4, 0), Wall("w2", 0, 0, 0, 4));

        var r = FurnitureSnap.ToWall(new Vector2(0.45f, 0.42f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual("w1", r.wallId);      // the nearer of the two
        Assert.AreEqual("w2", r.wallId2);
        Assert.AreEqual(0.35f, r.position.x, 1e-4f);
        Assert.AreEqual(0.35f, r.position.y, 1e-4f);
    }

    [Test]
    public void BuriedInThePerpendicularWall_ComesOutOfBothWalls()
    {
        // Seven centimetres off the wall along x and seven centimetres INTO the wall along z. A wall
        // the item overlaps is corrected whether or not it wins: an overlap is never a placement.
        var level = Level(Wall("w1", 0, 0, 4, 0), Wall("w2", 0, 0, 0, 4));

        var r = FurnitureSnap.ToWall(new Vector2(0.28f, 0.42f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.IsTrue(r.snapped);
        Assert.IsNotNull(r.wallId2);          // the two gaps tie at 0.07, so which reads nearer is arbitrary
        CollectionAssert.AreEquivalent(new[] { "w1", "w2" }, new[] { r.wallId, r.wallId2 });
        Assert.AreEqual(0.35f, r.position.x, 1e-4f);
        Assert.AreEqual(0.35f, r.position.y, 1e-4f);
    }

    [Test]
    public void AnAcuteCorner_TucksAgainstBothFaces()
    {
        // A 45 degree corner, the case that separates the 2x2 solve from simply adding the two walls'
        // own shifts. Adding them gives (-0.01, -0.06) and leaves the item eight centimetres off w2,
        // because each shift changes the OTHER wall's gap too.
        var level = Level(Wall("w1", 0, 0, 4, 0), Wall("w2", 0, 0, 4, 4));
        float flushX = 0.95f + 0.05f * Mathf.Sqrt(2f);

        var r = FurnitureSnap.ToWall(new Vector2(flushX + 0.09f, 0.42f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual("w2", r.wallId);      // 0.014 off w2 against 0.07 off w1
        Assert.AreEqual("w1", r.wallId2);
        Assert.AreEqual(flushX, r.position.x, 1e-4f);
        Assert.AreEqual(0.35f, r.position.y, 1e-4f);
    }

    [Test]
    public void AnAcuteCornerTooTightToReach_FallsBackToTheNearerWall()
    {
        // An 11 degree wedge. Its walls are far from parallel, so the sine guard lets it through and
        // the CAP is what stops it: the solution sits 0.70 m up the point, against a cap of 0.26.
        var level = Level(Wall("w1", 0, 0, 6, 0), Wall("w2", 0, 0, 6, 1.2f));

        var r = FurnitureSnap.ToWall(new Vector2(4.5f, 0.40f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual("w1", r.wallId);
        Assert.IsNull(r.wallId2);
        Assert.AreEqual(4.5f, r.position.x, 1e-4f);
        Assert.AreEqual(0.35f, r.position.y, 1e-4f);
    }

    [Test]
    public void TwoParallelWallsInRange_TheNearerWins()
    {
        // The old rule, still deciding the single-wall pass: the near and far side of a narrow alcove
        // have no shared answer, so one of them gets the item.
        var level = Level(Wall("w1", 0, 0, 4, 0), Wall("w2", 0, 0.90f, 4, 0.90f));

        var r = FurnitureSnap.ToWall(new Vector2(2f, 0.42f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.AreEqual("w1", r.wallId);
        Assert.IsNull(r.wallId2);
        Assert.AreEqual(2f, r.position.x, 1e-4f);
        Assert.AreEqual(0.35f, r.position.y, 1e-4f);
    }

    [Test]
    public void NearlyParallelWalls_AreNotACorner()
    {
        // Three degrees apart: inside MinJunctionSin, so no pair, however near both faces are. The
        // telling assertion is x, which only a pair solve would have moved.
        var level = Level(Wall("w1", 0, 0, 4, 0), Wall("w2", 0, 0.80f, 4, 1.00f));

        var r = FurnitureSnap.ToWall(new Vector2(2f, 0.42f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.AreEqual("w1", r.wallId);
        Assert.IsNull(r.wallId2);
        Assert.AreEqual(2f, r.position.x, 1e-3f);
        Assert.AreEqual(0.35f, r.position.y, 1e-4f);
    }

    [Test]
    public void OneWallInRangeAndTheOtherFar_SnapsToTheOneWall()
    {
        // Beside a corner, not in it: 0.85 m off the wall along z. A corner must not reach out for a
        // wall the plain snap would never have taken.
        var level = Level(Wall("w1", 0, 0, 4, 0), Wall("w2", 0, 0, 0, 4));

        var r = FurnitureSnap.ToWall(new Vector2(1.2f, 0.42f), 0.6f, 0.6f, 0f, level, RANGE);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual("w1", r.wallId);
        Assert.IsNull(r.wallId2);
        Assert.AreEqual(1.2f, r.position.x, 1e-4f);
        Assert.AreEqual(0.35f, r.position.y, 1e-4f);
    }

    [Test]
    public void CtrlReach_PullsToOneWallWithoutTuckingIntoTheCorner()
    {
        // Ctrl widens the reach to MOUNT_REACH, and both walls of the corner are inside it (0.65 and
        // 0.85). The tuck still keeps to FURNITURE_SNAP_RANGE: Ctrl offers a wall the item is nowhere
        // near, never a metre sideways into a corner nobody aimed at.
        var level = Level(Wall("w1", 0, 0, 4, 0), Wall("w2", 0, 0, 0, 4));

        var r = FurnitureSnap.ToWall(new Vector2(1.2f, 1.0f), 0.6f, 0.6f, 0f, level,
                                     ResidenceConventions.MOUNT_REACH);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual("w1", r.wallId);
        Assert.IsNull(r.wallId2);
        Assert.AreEqual(1.2f, r.position.x, 1e-4f);
        Assert.AreEqual(0.35f, r.position.y, 1e-4f);
    }

    // ---- staying put through a turn ----------------------------------------------------------

    [Test]
    public void RestrictedToOneWall_ReSeatsAFlushItemAfterAQuarterTurn()
    {
        // The turn re-snap. A 0.9 x 2.0 bed flush against the wall (edge at 0.05, centre at 1.05)
        // turns a quarter about its centre: now 2.0 across and 0.9 deep, its edge at 0.60. Far outside
        // any snap range, so the caller names the wall and lifts the range.
        var level = Level(Wall("w1", 0, 0, 6, 0));
        var flush = new Vector2(3f, 1.05f);
        var before = FurnitureSnap.Against(flush, 0.9f, 2.0f, 0f, level, 0.02f);
        Assert.AreEqual("w1", before.wall.id);

        var r = FurnitureSnap.ToWall(flush, 0.9f, 2.0f, 90f, level, float.PositiveInfinity, before.wall.id);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(0.50f, r.position.y, 1e-4f);
        Assert.AreEqual(3f, r.position.x, 1e-4f);
    }

    [Test]
    public void AQuarterTurnInACorner_ReTucksIntoTheSameCorner()
    {
        // The same bed in a corner. Turning swaps a 0.45 half-depth for a 1.0 one, which opens 0.55 on
        // one wall and drives 0.55 into the other, so both have to be named or the bed comes out of the
        // turn standing half inside w2. Naming w1 alone lands it at (0.50, 0.50).
        var level = Level(Wall("w1", 0, 0, 6, 0), Wall("w2", 0, 0, 0, 6));
        var flush = new Vector2(0.50f, 1.05f);
        var before = FurnitureSnap.Against(flush, 0.9f, 2.0f, 0f, level, 0.02f);
        Assert.IsTrue(before.Any);
        AssertCorner(before, "w1", "w2");

        var r = FurnitureSnap.ToWall(flush, 0.9f, 2.0f, 90f, level, float.PositiveInfinity,
                                     before.wall.id, before.wall2.id);

        Assert.IsTrue(r.snapped);
        Assert.AreEqual(1.05f, r.position.x, 1e-4f);
        Assert.AreEqual(0.50f, r.position.y, 1e-4f);
    }

    [Test]
    public void Against_ReportsOnlyAnItemActuallyOnTheFace()
    {
        var level = Level(Wall("w1", 0, 0, 4, 0));

        var on = FurnitureSnap.Against(new Vector2(1f, 0.36f), 0.6f, 0.6f, 0f, level, 0.02f);
        var off = FurnitureSnap.Against(new Vector2(1f, 0.40f), 0.6f, 0.6f, 0f, level, 0.02f);

        Assert.IsTrue(on.Any);
        Assert.AreEqual("w1", on.wall.id);
        Assert.IsFalse(off.Any);
    }

    [Test]
    public void Against_ReportsBothWallsOfACorner()
    {
        // Flush on both faces means both gaps are zero, so scoring the walls against each other would
        // keep whichever came first and drop the corner on the floor.
        var level = Level(Wall("w1", 0, 0, 4, 0), Wall("w2", 0, 0, 0, 4));

        var corner = FurnitureSnap.Against(new Vector2(0.35f, 0.35f), 0.6f, 0.6f, 0f, level, 0.02f);
        var alone = FurnitureSnap.Against(new Vector2(1.0f, 0.35f), 0.6f, 0.6f, 0f, level, 0.02f);

        AssertCorner(corner, "w1", "w2");
        Assert.AreEqual("w1", alone.wall.id);
        Assert.IsNull(alone.wall2);
    }

    [Test]
    public void Thickness_FallsBackToTheLevelThenTheDefault()
    {
        // No thickness on the wall: the level's 0.2 applies, face at 0.10. No thickness anywhere:
        // the default 0.114, face at 0.057.
        var thick = Level(Wall("w1", 0, 0, 4, 0, thickness: 0f));
        thick.wallThickness = 0.2f;
        var plain = Level(Wall("w1", 0, 0, 4, 0, thickness: 0f));

        var a = FurnitureSnap.ToWall(new Vector2(1f, 0.45f), 0.6f, 0.6f, 0f, thick, RANGE);
        var b = FurnitureSnap.ToWall(new Vector2(1f, 0.45f), 0.6f, 0.6f, 0f, plain, RANGE);

        Assert.AreEqual(0.40f, a.position.y, 1e-4f);
        Assert.AreEqual(0.5f * ResidenceConventions.DEFAULT_WALL_THICKNESS + 0.3f, b.position.y, 1e-4f);
    }

    [Test]
    public void Corners_FollowTheRendererYawConvention()
    {
        // Quaternion.Euler(0, 90, 0) maps local +z onto world +x. An item's front (+z, half the depth)
        // must land on +x, or the ghost is a mirror of what spawns.
        var c = FurnitureSnap.Corners(Vector2.zero, 0.6f, 1.0f, 90f);

        float maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var p in c) { maxX = Mathf.Max(maxX, p.x); maxZ = Mathf.Max(maxZ, p.y); }

        Assert.AreEqual(0.5f, maxX, 1e-4f);
        Assert.AreEqual(0.3f, maxZ, 1e-4f);
    }

    // ---------------------------------------------------------------------------------------

    // An item already flush in a corner has the SAME gap on both walls, near enough that the last bit
    // of the float decides which one reads as nearer. Which of the two lands in `wall` is arbitrary
    // there and means nothing: ToWall takes the pair either way round. So the pair is what is asserted.
    private static void AssertCorner(FurnitureSnap.Flush f, string a, string b)
    {
        Assert.IsNotNull(f.wall2, $"expected a corner of {a} and {b}, got one wall");
        CollectionAssert.AreEquivalent(new[] { a, b }, new[] { f.wall.id, f.wall2.id });
    }

    private static WallDef Wall(string id, float ax, float az, float bx, float bz, float thickness = 0.1f)
        => new WallDef { id = id, a = new[] { ax, az }, b = new[] { bx, bz }, thickness = thickness };

    private static LevelDef Level(params WallDef[] walls)
        => new LevelDef { walls = new List<WallDef>(walls) };
}
