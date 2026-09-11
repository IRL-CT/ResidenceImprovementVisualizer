using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

// Walls drawn THICK: as two parallel panes around a light channel (the printed double-line
// convention), or as fat strokes with a light seam between them. Extraction mints two wall lines
// inside one physical wall in both cases, and the interval between them is a bounded cell; these
// tests pin that the cell map folds that cell back into wall instead of minting a room in the
// wall's own footprint, and that a door punched through the whole assembly still comes out as one
// door. Same fixture conventions as SketchPlanDetectorTests: 900 x 600 px at one centimetre per
// pixel; every image's global stroke estimate lands on 6 px.
[TestFixture]
public class SketchHollowWallTests
{
    private const float MPP = 0.01f;

    // Two rooms split by a hollow divider: two 6 px panes around a 14 px channel, wider than the
    // grouping cap (2*stroke + 1 = 13) ever merges, so the divider becomes two wall lines.
    private static SketchTestImages HollowDivider()
    {
        var img = new SketchTestImages(900, 600);
        img.RectOutline(100, 80, 700, 440, 6);
        img.FillRect(440, 80, 6, 440);         // west pane
        img.FillRect(460, 80, 6, 440);         // east pane
        return img;
    }

    // The whole perimeter drawn hollow: an outer and an inner outline with a 14 px channel between.
    private static SketchTestImages HollowRing()
    {
        var img = new SketchTestImages(900, 600);
        img.RectOutline(100, 80, 700, 440, 6);
        img.RectOutline(120, 100, 660, 400, 6);
        return img;
    }

    private static SketchDetectResult Detect(SketchTestImages img)
        => SketchPlanDetector.Detect(img.Pixels, img.Width, img.Height, MPP);

    private static List<SketchOpening> Doors(SketchPlanSpec spec)
    {
        var doors = new List<SketchOpening>();
        foreach (var o in spec.Openings)
            if (o.kind == OpeningKind.Door || o.kind == OpeningKind.CasedOpening) doors.Add(o);
        return doors;
    }

    [Test]
    public void TwoRoomsSplitByAHollowDivider_AreTwoRoomsWithOneDoor()
    {
        var img = HollowDivider();
        img.Erase(440, 260, 26, 80);           // a door punched through BOTH panes

        var result = Detect(img);

        Assert.IsTrue(result.Ok, result.refusal);
        Assert.AreEqual(2, result.spec.rooms.Count, "the channel is wall, not a third room");

        var doors = Doors(result.spec);
        Assert.AreEqual(1, doors.Count, "one door through the assembly, not one per pane");
        Assert.IsTrue(doors[0].IsInterior);
        CollectionAssert.AreEquivalent(new[] { "room1", "room2" }, doors[0].between);
        Assert.AreEqual(0.8f, doors[0].widthMeters, 0.12f);
        foreach (var r in result.spec.rooms)
            Assert.AreNotEqual(RoomType.Storage, r.roomType, "the channel is not a phantom closet");
    }

    [Test]
    public void HollowOutline_RingChannel_MintsOneRoom()
    {
        var result = Detect(HollowRing());

        Assert.IsTrue(result.Ok, result.refusal);
        Assert.AreEqual(1, result.spec.rooms.Count, "the ring channel folds, corners included");

        // The room spans the INNER outline's centerlines: 654 x 394 px.
        var room = result.spec.rooms[0];
        Assert.AreEqual(6.54f, room.widthMeters, 0.15f);
        Assert.AreEqual(3.94f, room.depthMeters, 0.15f);
        Assert.AreEqual(0, result.spec.Openings.Count);
    }

    [Test]
    public void ExteriorDoorThroughAHollowWall_IsOneExteriorDoor()
    {
        var img = HollowRing();
        img.Erase(350, 494, 80, 26);           // through inner pane, channel and outer pane

        var result = Detect(img);

        Assert.IsTrue(result.Ok, result.refusal);
        Assert.AreEqual(1, result.spec.rooms.Count);

        var doors = Doors(result.spec);
        Assert.AreEqual(1, doors.Count, "both pane candidates resolve to the same door");
        Assert.IsFalse(doors[0].IsInterior);
        Assert.AreEqual("room1", doors[0].room);
        Assert.AreEqual("south", doors[0].edge);
        Assert.AreEqual(0.8f, doors[0].widthMeters, 0.12f);
    }

    [Test]
    public void DoorThroughOnlyOnePane_IsNotADoor()
    {
        var img = HollowRing();
        img.Erase(350, 514, 80, 6);            // a gap in the outer pane alone; the inner pane holds

        var result = Detect(img);

        Assert.IsTrue(result.Ok, result.refusal);
        Assert.AreEqual(1, result.spec.rooms.Count, "the channel never reaches the spec");
        Assert.AreEqual(0, result.spec.Openings.Count,
            "a gap that does not reach the room is not an opening");
    }

    [Test]
    public void ThickSolidDividerWithALightSeam_MintsNoRoomInItsFootprint()
    {
        // The reported shape: a wall drawn with two fat strokes whose middles never quite met.
        var img = new SketchTestImages(900, 600);
        img.RectOutline(100, 80, 700, 440, 6);
        img.FillRect(430, 80, 12, 440);
        img.FillRect(456, 80, 12, 440);        // 14 px of light between the fat strokes

        var result = Detect(img);

        Assert.IsTrue(result.Ok, result.refusal);
        Assert.AreEqual(2, result.spec.rooms.Count, "the seam is wall, not a room");
        Assert.AreEqual(3.3f, result.spec.rooms[0].widthMeters, 0.2f);
        Assert.AreEqual(3.3f, result.spec.rooms[1].widthMeters, 0.2f);
    }

    [Test]
    public void CleanThickDivider_StillSplitsTheRoomsOnOneLine()
    {
        // A legitimately thick wall under the extraction ceiling (30 px < 6 strokes): one line,
        // two rooms, and its door verified through the full slab. The no-regression pin.
        var img = new SketchTestImages(900, 600);
        img.RectOutline(100, 80, 700, 440, 6);
        img.FillRect(435, 80, 30, 440);
        img.Erase(435, 260, 30, 80);

        var result = Detect(img);

        Assert.IsTrue(result.Ok, result.refusal);
        Assert.AreEqual(2, result.spec.rooms.Count);

        var doors = Doors(result.spec);
        Assert.AreEqual(1, doors.Count);
        Assert.IsTrue(doors[0].IsInterior);
        Assert.AreEqual(0.8f, doors[0].widthMeters, 0.12f);
    }

    [Test]
    public void HollowPlan_IsDeterministic_ByteForByte()
    {
        var img = HollowDivider();
        img.Erase(440, 260, 26, 80);

        var a = Detect(img);
        var b = Detect(img);

        Assert.AreEqual(JsonConvert.SerializeObject(a.spec), JsonConvert.SerializeObject(b.spec));
    }
}
