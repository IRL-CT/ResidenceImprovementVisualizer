using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// The property VariantSync rests on: after any edit to the base environment, Compare(baseline,
// proposal) reports only what the proposal itself did. Before this, a wall drawn into the base after
// a proposal existed read as "Removed" in every proposal, and the report said so.
//
// Base edits are simulated the way the tools make them: WallLinker + RoomRegions.Sync for a drawn
// wall, the DeleteSelected cascade for a removed one, a plain list edit for furniture and people.
[TestFixture]
public class VariantSyncTests
{
    private Units.UnitSystem _saved;

    [SetUp]
    public void SetUp()
    {
        _saved = Units.Display;
        Units.Display = Units.UnitSystem.Metric;
    }

    [TearDown]
    public void TearDown() => Units.Display = _saved;

    // ---- the plain cases ----

    [Test]
    public void NothingChanged_NothingHappens()
    {
        var b = Baseline();
        var p = Proposal(b);
        var before = VariantSync.Snapshot(b);

        Assert.AreEqual(0, VariantSync.Propagate(before, b, p));
        Assert.AreEqual(0, VariantDiff.Compare(b, p).Count);
    }

    [Test]
    public void BaseAddsFurniture_ProposalReceivesIt()
    {
        var b = Baseline();
        var p = Proposal(b);
        var before = VariantSync.Snapshot(b);

        b.levels[0].furniture.Add(Furniture("f9", "armchair", 4f, 3f));

        Assert.Greater(VariantSync.Propagate(before, b, p), 0);
        var got = Find(p.levels[0].furniture, f => f.instanceId, "f9");
        Assert.IsNotNull(got, "the chair did not arrive");
        Assert.AreNotSame(b.levels[0].furniture[1].position, got.position, "a copy, never the base's own array");
        Assert.AreEqual(0, VariantDiff.Compare(b, p).Count, Describe(VariantDiff.Compare(b, p)));
    }

    [Test]
    public void BaseRemovesWallWithDoorAndSensor_AllGoneFromProposal()
    {
        var b = Baseline();
        var p = Proposal(b);
        var before = VariantSync.Snapshot(b);

        DeleteWall(b.levels[0], "s");

        VariantSync.Propagate(before, b, p);

        var l = p.levels[0];
        Assert.IsNull(Find(l.walls, w => w.id, "s"));
        Assert.IsNull(Find(l.openings, o => o.id, "o1"));
        Assert.IsNull(Find(l.sensors, s => s.id, "sn1"));
        Assert.AreEqual(0, l.rooms.Count, "the room is no longer enclosed in the base, so not here either");
        Assert.AreEqual(0, VariantDiff.Compare(b, p).Count, Describe(VariantDiff.Compare(b, p)));
    }

    [Test]
    public void BaseDrawsWallAcrossRoom_ProposalGetsWallAndNewRoomUnderTheSameIds()
    {
        var b = Baseline();
        var p = Proposal(b);
        var before = VariantSync.Snapshot(b);

        DrawWall(b.levels[0], new Vector2(3f, 0f), new Vector2(3f, 4f));
        Assert.AreEqual(2, b.levels[0].rooms.Count, "the base's own Sync split the room");

        VariantSync.Propagate(before, b, p);

        CollectionAssert.AreEquivalent(Ids(b.levels[0].walls, w => w.id), Ids(p.levels[0].walls, w => w.id));
        CollectionAssert.AreEquivalent(Ids(b.levels[0].rooms, r => r.id), Ids(p.levels[0].rooms, r => r.id));
        Assert.AreEqual(0, VariantDiff.Compare(b, p).Count, Describe(VariantDiff.Compare(b, p)));
    }

    [Test]
    public void BaseWidensDoor_ProposalFollows()
    {
        var b = Baseline();
        var p = Proposal(b);
        var before = VariantSync.Snapshot(b);

        b.levels[0].openings[0].width = 0.9144f;

        VariantSync.Propagate(before, b, p);

        Assert.AreEqual(0.9144f, p.levels[0].openings[0].width, 1e-5f);
        Assert.AreEqual(0, VariantDiff.Compare(b, p).Count);
    }

    // ---- the proposal's own edit wins ----

    [Test]
    public void ProposalMovedItem_BaseMovesTheSameItem_ProposalKeepsItsOwn()
    {
        var b = Baseline();
        var p = Proposal(b);
        var before = VariantSync.Snapshot(b);

        p.levels[0].furniture[0].position = new[] { 5f, 0f, 3f };      // the proposal's decision
        b.levels[0].furniture[0].position = new[] { 1f, 0f, 1f };      // the base moves it elsewhere
        b.levels[0].furniture.Add(Furniture("f9", "armchair", 4f, 3f)); // and adds something too

        VariantSync.Propagate(before, b, p);

        Assert.AreEqual(5f, p.levels[0].furniture[0].position[0], "the proposal's position must stand");
        Assert.IsNotNull(Find(p.levels[0].furniture, f => f.instanceId, "f9"), "the unrelated add still arrives");

        var changes = VariantDiff.Compare(b, p);
        Assert.AreEqual(1, changes.Count, Describe(changes));
        Assert.AreEqual(VariantDiff.ElementKind.Furniture, changes[0].kind);
        Assert.AreEqual(VariantDiff.ChangeType.Modified, changes[0].type);
        Assert.AreEqual("f1", changes[0].id);
    }

    [Test]
    public void ProposalDeletedItem_BaseModifiesIt_StaysDeleted()
    {
        var b = Baseline();
        var p = Proposal(b);
        var before = VariantSync.Snapshot(b);

        p.levels[0].furniture.RemoveAll(f => f.instanceId == "f1");
        b.levels[0].furniture[0].rotationY = 90f;

        VariantSync.Propagate(before, b, p);

        Assert.IsNull(Find(p.levels[0].furniture, f => f.instanceId, "f1"));
        var changes = VariantDiff.Compare(b, p);
        Assert.AreEqual(1, changes.Count, Describe(changes));
        Assert.AreEqual(VariantDiff.ChangeType.Removed, changes[0].type);
        Assert.AreEqual("f1", changes[0].id);
    }

    [Test]
    public void ProposalRemovedWall_BaseAddsOpeningOnIt_RefusedAndShownHonestly()
    {
        var b = Baseline();
        var p = Proposal(b);
        var before = VariantSync.Snapshot(b);

        DeleteWall(p.levels[0], "s");
        b.levels[0].openings.Add(new OpeningDef
        {
            id = "o2", wallId = "s", offset = 4.5f, width = 0.9f, height = 2.0f, kind = OpeningKind.Window,
        });

        VariantSync.Propagate(before, b, p);

        Assert.IsNull(Find(p.levels[0].openings, o => o.id, "o2"), "a window on a wall that is gone");
        var changes = VariantDiff.Compare(b, p);
        foreach (var c in changes) Assert.AreNotEqual(VariantDiff.ChangeType.Added, c.type, c.ToString());
        Assert.IsTrue(changes.Exists(c => c.kind == VariantDiff.ElementKind.Opening && c.id == "o2"
                                          && c.type == VariantDiff.ChangeType.Removed),
                      "the list must say the proposal lacks that window");
    }

    // ---- people and their devices ----

    [Test]
    public void BaseAddsPersonWithPendant_BothArrive()
    {
        var b = Baseline();
        var p = Proposal(b);
        var before = VariantSync.Snapshot(b);

        b.occupants = new List<OccupantDef>
        {
            new OccupantDef { id = "p1", name = "Alice", schedule = new List<ActivityDef>() },
        };
        b.levels[0].sensors.Add(new SensorDef
        {
            id = "sn2", deviceType = "panic_pendant", hostKind = SensorHost.Occupant, hostId = "p1",
        });

        VariantSync.Propagate(before, b, p);

        Assert.IsNotNull(Find(p.occupants, o => o.id, "p1"), "the person");
        Assert.IsNotNull(Find(p.levels[0].sensors, s => s.id, "sn2"), "the pendant, hosted on the person");
        Assert.AreEqual(0, VariantDiff.Compare(b, p).Count, Describe(VariantDiff.Compare(b, p)));
    }

    // ---- storeys ----

    [Test]
    public void TwoStoreys_AChangeLandsOnItsOwnFloor()
    {
        var b = Baseline();
        var p = Proposal(b);
        var doc = new ResidenceDoc { variants = new List<VariantDef> { b, p } };
        Stories.Add(doc, "Upstairs");
        var before = VariantSync.Snapshot(b);

        DrawWall(b.levels[1], new Vector2(0f, 0f), new Vector2(4f, 0f));

        VariantSync.Propagate(before, b, p);

        Assert.AreEqual(1, p.levels[1].walls.Count, "upstairs got the wall");
        Assert.AreEqual(4, p.levels[0].walls.Count, "downstairs is untouched");
        Assert.AreEqual(0, VariantDiff.Compare(b, p).Count, Describe(VariantDiff.Compare(b, p)));
    }

    [Test]
    public void ShadowStaleByOneFloor_TheFirstDrawingStillArrives()
    {
        var b = Baseline();
        var p = Proposal(b);
        var doc = new ResidenceDoc { variants = new List<VariantDef> { b, p } };
        var before = VariantSync.Snapshot(b);          // one storey
        Stories.Add(doc, "Upstairs");                   // now two, in every variant, same id

        b.levels[1].furniture.Add(Furniture("f9", "bed", 2f, 2f));

        VariantSync.Propagate(before, b, p);

        Assert.IsNotNull(Find(p.levels[1].furniture, f => f.instanceId, "f9"));
        Assert.AreEqual(0, VariantDiff.Compare(b, p).Count);
    }

    // ---- the shadow ----

    [Test]
    public void Snapshot_SharesNoArraysWithTheBaseline()
    {
        var b = Baseline();
        var s = VariantSync.Snapshot(b);

        Assert.AreEqual(0, VariantDiff.Compare(b, s).Count, "a faithful copy");
        Assert.AreNotSame(b.levels[0].walls[0].a, s.levels[0].walls[0].a);
        s.levels[0].walls[0].a[0] += 1f;
        Assert.AreEqual(0f, b.levels[0].walls[0].a[0], "moving the shadow's wall moved the baseline's");
    }

    // ---- over the samples ----

    [Test, TestCaseSource(nameof(Keys))]
    public void Samples_ABaseEditLeavesTheProposalsOwnChangesAlone(string key)
    {
        var doc = SampleResidences.Build(key);
        var b = doc.variants[0];
        Assert.IsTrue(b.isBaseline, key);
        Assert.IsFalse(string.IsNullOrEmpty(b.levels[0].id), key + ": storeys must carry ids");

        VariantDef p;
        if (doc.variants.Count > 1) p = doc.variants[1];
        else { p = Proposal(b); doc.variants.Add(p); }

        // Let the proposal do a few things of its own first.
        p.levels[0].furniture[0].position[0] += 0.3f;
        p.levels[0].openings[0].width += 0.1f;
        var ownBefore = Set(VariantDiff.Compare(b, p));

        var before = VariantSync.Snapshot(b);

        // Base edits that touch no host the proposal depends on.
        b.levels[0].furniture.Add(Furniture("sync_test_item", "armchair", 0.5f, 0.5f));
        b.levels[0].furniture[1].rotationY += 45f;
        if (b.levels[0].openings.Count > 1) b.levels[0].openings[1].thresholdHeight = 0f;
        b.occupants ??= new List<OccupantDef>();
        b.occupants.Add(new OccupantDef { id = "sync_test_person", name = "Visitor", schedule = new List<ActivityDef>() });

        VariantSync.PropagateAll(before, b, doc.variants);

        var ownAfter = Set(VariantDiff.Compare(b, p));
        CollectionAssert.AreEquivalent(ownBefore, ownAfter, key);
    }

    private static IEnumerable<string> Keys
    {
        get { foreach (var s in SampleResidences.All) yield return s.key; }
    }

    // ---------------------------------------------------------------------------------------
    // Fixture: a closed 6 x 4 room, a door in its south wall with a door sensor, one chair.
    // ---------------------------------------------------------------------------------------

    private static VariantDef Baseline()
    {
        var level = new LevelDef
        {
            id = "L0", name = "Ground floor",
            ceilingHeight = 2.44f, wallThickness = 0.114f,
            walls = new List<WallDef>
            {
                Wall("s", 0f, 0f, 6f, 0f),
                Wall("e", 6f, 0f, 6f, 4f),
                Wall("n", 6f, 4f, 0f, 4f),
                Wall("w", 0f, 4f, 0f, 0f),
            },
            openings = new List<OpeningDef>
            {
                new OpeningDef
                {
                    id = "o1", wallId = "s", offset = 1.5f, width = 0.8128f, height = 2.032f,
                    kind = OpeningKind.Door,
                },
            },
            rooms = new List<RoomDef>(),
            furniture = new List<ObjectInstance> { Furniture("f1", "armchair", 4.5f, 3f) },
            wallMounted = new List<WallMountDef>(),
            sensors = new List<SensorDef>
            {
                new SensorDef { id = "sn1", deviceType = "door_sensor", hostKind = SensorHost.Opening, hostId = "o1" },
            },
        };
        RoomRegions.Sync(level, null);
        Assert.AreEqual(1, level.rooms.Count, "fixture: the four walls enclose one room");
        level.rooms[0].name = "Living room";
        level.rooms[0].roomType = RoomType.Living;

        return new VariantDef
        {
            id = "v0", name = "Existing", isBaseline = true, locked = true,
            levels = new List<LevelDef> { level },
        };
    }

    /// <summary>What NewProposalFrom does: a deep copy keeping every element id, under its own identity.</summary>
    private static VariantDef Proposal(VariantDef baseline)
    {
        var p = VariantSync.Snapshot(baseline);
        p.id = "v1";
        p.name = "Proposal";
        p.isBaseline = false;
        p.locked = false;
        return p;
    }

    private static WallDef Wall(string id, float ax, float az, float bx, float bz) => new WallDef
    {
        id = id, a = new[] { ax, az }, b = new[] { bx, bz }, thickness = 0.114f, height = 2.44f,
    };

    private static ObjectInstance Furniture(string id, string type, float x, float z) => new ObjectInstance
    {
        instanceId = id, prefabType = type, position = new[] { x, 0f, z }, scale = 1f, included = true,
        boxSizeMeters = new[] { 0.8f, 0.9f, 0.8f },
    };

    /// <summary>WallTool.CommitSegment, in miniature: link, then bring the rooms into step.</summary>
    private static void DrawWall(LevelDef level, Vector2 a, Vector2 b)
    {
        var template = new WallDef { thickness = 0.114f, height = 2.44f };
        WallLinker.Link(level, new List<Vector2> { a, b }, template, WallLinker.Options.Default);
        RoomRegions.Sync(level, null);
    }

    /// <summary>SelectTool.DeleteSelected for a wall: the wall, what hangs on it, then the rooms.</summary>
    private static void DeleteWall(LevelDef level, string wallId)
    {
        var doomed = new HashSet<string>();
        foreach (var o in level.openings) if (o.wallId == wallId) doomed.Add(o.id);
        level.walls.RemoveAll(w => w.id == wallId);
        level.openings.RemoveAll(o => o.wallId == wallId);
        level.wallMounted?.RemoveAll(m => m.wallId == wallId);
        level.sensors?.RemoveAll(s => (s.hostKind == SensorHost.Wall && s.hostId == wallId)
                                   || (s.hostKind == SensorHost.Opening && doomed.Contains(s.hostId)));
        RoomRegions.Sync(level, null);
    }

    private static T Find<T>(List<T> list, System.Func<T, string> key, string id) where T : class
    {
        if (list == null) return null;
        foreach (var item in list) if (item != null && key(item) == id) return item;
        return null;
    }

    private static List<string> Ids<T>(List<T> list, System.Func<T, string> key)
    {
        var ids = new List<string>();
        if (list != null) foreach (var item in list) if (item != null) ids.Add(key(item));
        return ids;
    }

    private static HashSet<string> Set(List<VariantDiff.Change> changes)
    {
        var set = new HashSet<string>();
        foreach (var c in changes) set.Add($"{c.kind}:{c.id}:{c.type}");
        return set;
    }

    private static string Describe(List<VariantDiff.Change> changes)
    {
        var parts = new List<string>();
        foreach (var c in changes) parts.Add(c.ToString());
        return parts.Count == 0 ? "(no changes)" : string.Join(" | ", parts);
    }
}
