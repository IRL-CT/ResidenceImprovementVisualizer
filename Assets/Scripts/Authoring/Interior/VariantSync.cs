using System.Collections.Generic;

// Carrying an edit to the base environment into every proposal.
//
// A proposal is a full copy of the baseline, preserving every element id (see VariantDef's header and
// ResidenceEditController.NewProposalFrom). That is what lets VariantDiff report a widened door as one
// modification. It also means the baseline and its proposals drift apart the moment the baseline is
// edited again: a wall drawn into the base environment after a proposal exists is in the base and in
// no proposal, and the change list calls it "Removed" in every proposal, which is true of the data and
// false of what happened. The base environment is the record of how the residence IS; a change to
// that record is a fact every proposal has to inherit, silently, because it is not something the
// proposal did.
//
// THE SHADOW DIFF. The controller keeps a private copy of the baseline as it last stood (Snapshot).
// After an edit, Compare(shadow, baseline) is precisely the list of what that edit did, and each of
// those changes is carried into a proposal by calling VariantRevert.Revert with the NEW baseline as
// the reference: "make the proposal match the baseline for this one element". Revert's three branches
// are exactly the three cases here (present in the reference: copy in or replace; absent: remove,
// with the same cascades SelectTool.DeleteSelected runs), and its refusals are exactly the cases where
// carrying the change would write a wallId or hostId that resolves to nothing. So there is no
// per-tool hook anywhere: every tool already ends in MarkDirty, and this runs from there.
//
// A PROPOSAL'S OWN EDIT WINS. An element the proposal itself changed or removed, relative to the
// shadow, is left alone, and that difference stays in its change list where it belongs. The key is
// the element, not the field: the proposal moved the chair, the base resized it, the proposal keeps
// its chair as it is. Dependents follow a removed host: a proposal's grab bar on a wall the base
// removed goes with the wall, exactly as it would under DeleteSelected.
//
// THE SYNC SEES EXACTLY WHAT THE DIFF SEES. A field VariantDiff does not compare (a note, a marker
// colour, a device's privacy class) neither travels nor shows up in any list. That is the invariant
// that makes "a base edit is absent from every report" true by construction rather than by effort.
//
// STOREYS PAIR BY ID ONLY. Stories.Add gives one storey the same id in every variant, so a level
// missing from the proposal is skipped, and a level missing from the shadow reads as empty: the
// shadow can be a storey stale after Stories.Add and the first thing drawn there simply arrives as
// added. VariantDiff.MatchLevel falls back to position, and after a middle storey is removed that
// would pair two different floors; the diffs here are therefore taken one storey at a time.
//
// Lives in CXRAuthoring for the same reason VariantRevert does: so the property it rests on can be
// tested at all. After any base edit, Compare(baseline, proposal) reports only what the proposal
// itself did.
public static class VariantSync
{
    /// <summary>
    /// A private copy of the baseline as it stands, for the next edit to be measured against. Hand
    /// copies throughout (VariantRevert.RevertAll), so no float[] is shared with the live baseline.
    /// </summary>
    public static VariantDef Snapshot(VariantDef baseline)
    {
        if (baseline == null) return null;
        var copy = new VariantDef
        {
            id = baseline.id,
            name = baseline.name,
            isBaseline = baseline.isBaseline,
            locked = baseline.locked,
        };
        VariantRevert.RevertAll(baseline, copy);
        return copy;
    }

    /// <summary>
    /// Carries every difference between <paramref name="before"/> and <paramref name="after"/> (two
    /// states of the baseline) into each proposal in <paramref name="variants"/>.
    /// </summary>
    /// <returns>
    /// How many baseline changes were examined, over all proposals. Non-zero means the baseline moved
    /// and the caller's shadow is stale, whether or not any proposal took the change.
    /// </returns>
    public static int PropagateAll(VariantDef before, VariantDef after, List<VariantDef> variants)
    {
        if (variants == null) return 0;
        int seen = 0;
        foreach (var v in variants)
            if (v != null && v != after && !v.isBaseline) seen += Propagate(before, after, v);
        return seen;
    }

    /// <summary>
    /// One proposal. Every baseline change the proposal has not itself touched is applied; the rest
    /// are left as the proposal's own. Refusals (a host the proposal removed) are skipped: the change
    /// list then shows that difference, which is the honest answer.
    /// </summary>
    /// <returns>How many baseline changes were examined (applied or deliberately left).</returns>
    public static int Propagate(VariantDef before, VariantDef after, VariantDef proposal)
    {
        if (before == null || after == null || proposal == null) return 0;
        if (proposal == after || proposal.isBaseline) return 0;

        // People first: a worn device hosts on an occupant, and RevertSensor.HostExists looks the
        // person up on the proposal's roster. A person added together with their pendant has to land
        // before the pendant does.
        int seen = ApplyWide(before, after, proposal);

        var toSync = new List<LevelDef>();
        if (after.levels != null)
        {
            foreach (var afterL in after.levels)
            {
                if (afterL == null || string.IsNullOrEmpty(afterL.id)) continue;
                int pi = IndexOfLevel(proposal.levels, afterL.id);
                if (pi < 0) continue;

                var proposalL = proposal.levels[pi];
                var beforeL = LevelById(before.levels, afterL.id) ?? new LevelDef { id = afterL.id };

                seen += ApplyLevel(beforeL, afterL, proposalL, pi, after, proposal, out bool wallsMoved);
                if (wallsMoved) toSync.Add(proposalL);
            }
        }

        // Rooms travelled by id above, so a room the base's own Sync minted already exists here under
        // the same id, and this pass only puts its polygon right for THIS proposal's walls. Where the
        // proposal's walls agree with the base it changes nothing; where they differ, whatever it does
        // is the proposal's own difference. Only after a wall's geometry changed: never on a
        // thickness or height edit, per the rooms rules.
        foreach (var l in toSync) RoomRegions.Sync(l, null);

        return seen;
    }

    // ---------------------------------------------------------------------------------------

    private static int ApplyWide(VariantDef before, VariantDef after, VariantDef proposal)
    {
        var baseChanges = VariantDiff.Compare(WideOnly(before), WideOnly(after));
        if (baseChanges.Count == 0) return 0;

        var own = KeysOf(VariantDiff.Compare(WideOnly(before), WideOnly(proposal)));
        foreach (var c in baseChanges)
        {
            if (own.Contains(KeyOf(c))) continue;
            VariantRevert.Revert(after, proposal, c, out _);
        }
        return baseChanges.Count;
    }

    private static int ApplyLevel(LevelDef beforeL, LevelDef afterL, LevelDef proposalL, int proposalIndex,
                                  VariantDef after, VariantDef proposal, out bool wallsMoved)
    {
        wallsMoved = false;
        var baseChanges = VariantDiff.Compare(LevelOnly(beforeL), LevelOnly(afterL));
        if (baseChanges.Count == 0) return 0;

        var own = KeysOf(VariantDiff.Compare(LevelOnly(beforeL), LevelOnly(proposalL)));

        // Compare emits walls, then openings, rooms, furniture, mounts, sensors: host before hosted,
        // which is the order Revert needs (a door re-homed onto a split-off piece finds that piece).
        foreach (var c0 in baseChanges)
        {
            if (own.Contains(KeyOf(c0))) continue;

            // Addressed by id: LevelFor and MatchingLevel both look the id up first.
            var c = c0;
            c.levelId = afterL.id;
            c.levelIndex = proposalIndex;

            if (!VariantRevert.Revert(after, proposal, c, out _)) continue;
            if (c.kind == VariantDiff.ElementKind.Wall && WallGeometryChanged(beforeL, afterL, c.id))
                wallsMoved = true;
        }
        return baseChanges.Count;
    }

    /// <summary>Added, removed, or an endpoint moved. A thickness or height edit is neither.</summary>
    private static bool WallGeometryChanged(LevelDef beforeL, LevelDef afterL, string id)
    {
        var wa = FindWall(beforeL, id);
        var wb = FindWall(afterL, id);
        if (wa == null || wb == null) return true;
        return !Same(wa.a, wb.a) || !Same(wa.b, wb.b);
    }

    private static WallDef FindWall(LevelDef level, string id)
    {
        if (level?.walls == null) return null;
        foreach (var w in level.walls) if (w != null && w.id == id) return w;
        return null;
    }

    private static bool Same(float[] a, float[] b)
    {
        if (a == null || b == null) return a == b;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (System.Math.Abs(a[i] - b[i]) > 1e-6f) return false;
        return true;
    }

    // ---- wrappers: one storey, or the variant-wide part, and nothing else ----

    private static VariantDef LevelOnly(LevelDef l) => new VariantDef { levels = new List<LevelDef> { l } };

    private static VariantDef WideOnly(VariantDef v) => new VariantDef
    {
        levels = new List<LevelDef>(),
        occupants = v.occupants,
        exterior = v.exterior,
        exteriorObjects = v.exteriorObjects,
    };

    private static LevelDef LevelById(List<LevelDef> levels, string id)
    {
        int i = IndexOfLevel(levels, id);
        return i < 0 ? null : levels[i];
    }

    private static int IndexOfLevel(List<LevelDef> levels, string id)
    {
        if (levels == null || string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < levels.Count; i++)
            if (levels[i] != null && levels[i].id == id) return i;
        return -1;
    }

    // ---- keys ----

    private readonly struct Key
    {
        private readonly VariantDiff.ElementKind _kind;
        private readonly string _id;

        public Key(VariantDiff.ElementKind kind, string id) { _kind = kind; _id = id ?? ""; }

        public override bool Equals(object obj) => obj is Key k && k._kind == _kind && k._id == _id;
        public override int GetHashCode() => ((int)_kind * 397) ^ _id.GetHashCode();
    }

    private static Key KeyOf(VariantDiff.Change c) => new Key(c.kind, c.id);

    private static HashSet<Key> KeysOf(List<VariantDiff.Change> changes)
    {
        var set = new HashSet<Key>();
        foreach (var c in changes) set.Add(KeyOf(c));
        return set;
    }
}
