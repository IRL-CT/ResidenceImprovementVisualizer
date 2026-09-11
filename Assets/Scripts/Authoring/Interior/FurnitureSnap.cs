using System.Collections.Generic;
using UnityEngine;

// Puts a piece of furniture flush against the wall, or the corner, it is nearly touching.
//
// FurnitureFit keeps an item out of a doorway; this is the other half of placing something by a
// wall, which is getting it AGAINST the wall. Before this the ghost went exactly where the cursor
// was, so a dresser ended up a few centimetres off the wall or a few centimetres into it, and the
// only way to true it up was to zoom in and nudge. Every drawing tool in the app snaps by default and
// frees under Shift; furniture now does the same.
//
// Three things this gets right that a centerline test would not:
//
//   * It measures to the wall FACE, half a thickness out from the centerline on the item's side. An
//     item snapped to the centerline is half buried in the wall.
//   * It uses the TRUE rotated rectangle, not FurnitureFit.Footprint's axis-aligned bound. The bound
//     is the safe direction for a clearance test (overstating keeps a corner out of a doorway) and the
//     wrong one here: at 45 degrees it would hold the item most of a diagonal off the wall.
//   * A CORNER beats either of its walls. Closing the nearer wall's gap and calling it done is what a
//     nightstand does when it will not tuck: one edge lands on its face while the other stays a
//     centimetre off the wall beside it, or a centimetre inside it, with no way to true both up but by
//     eye. Two walls are closed together, exactly, by one 2x2 solve. See Pair.
//
// It lives in CXRAuthoring for the reason FurnitureFit does: the callers (the furniture tool's ghost,
// the controller's move handle) are IMGUI and Input System code no EditMode test can drive, and the
// arithmetic is where the mistakes would be.
public static class FurnitureSnap
{
    public struct Result
    {
        public bool snapped;        // false => nothing was in range; position is the input
        public Vector2 position;    // world XZ of the item's center, always set
        public string wallId;       // the wall it now sits against; null when !snapped
        public string wallId2;      // the OTHER wall of a corner tuck; null for a single-wall snap
    }

    /// <summary>
    /// The wall or corner an item is flush against: <see cref="wall"/> the nearer face,
    /// <see cref="wall2"/> the second wall when it sits in a corner.
    /// </summary>
    public struct Flush
    {
        public WallDef wall;
        public WallDef wall2;
        public bool Any => wall != null;
    }

    // How far a corner tuck may carry the item, as a multiple of what the nearer wall alone would have
    // moved it. The solved shift grows as 1/sin of the corner angle, so this is an angle cut in
    // disguise: two equal gaps at an angle whose cosine is 0.778 put it at exactly 3, which offers the
    // tuck at any corner sharper than about 40 degrees and leaves the rest on the single-wall snap,
    // which is what they get today. Stated in gaps rather than in `range` on purpose: `range` is 0.15
    // by default and 1.2 under Ctrl, so three of it is no cap at all, and the turn re-seat passes
    // infinity, which this form is inert at by construction.
    private const float CORNER_SHIFT_FACTOR = 3f;

    /// <summary>
    /// The nearest wall face, or corner, within <paramref name="range"/> of the footprint's edge, and
    /// where the item's center has to be for that edge to sit on it.
    /// </summary>
    /// <param name="center">World XZ of the item's center.</param>
    /// <param name="widthM">The item's true width, across its front.</param>
    /// <param name="depthM">The item's true depth, front to back.</param>
    /// <param name="yawDeg">The item's <c>rotationY</c>.</param>
    /// <param name="range">How much daylight between edge and face still counts as "near". An edge
    /// already pushed INTO the wall is always in range, and is pushed back out.</param>
    /// <param name="onlyWallId">Restricts the search to one wall: the turn re-snap, which knows which
    /// wall the item was flush with before it turned.</param>
    /// <param name="onlyWallId2">The second wall of that restriction, when it turned in a corner.</param>
    public static Result ToWall(Vector2 center, float widthM, float depthM, float yawDeg,
                                LevelDef level, float range,
                                string onlyWallId = null, string onlyWallId2 = null)
    {
        var result = new Result { snapped = false, position = center, wallId = null, wallId2 = null };
        if (level?.walls == null) return result;

        var corners = Corners(center, widthM, depthM, yawDeg);

        var faces = new List<Face>();
        foreach (var w in level.walls)
        {
            if (!Usable(w)) continue;
            if (onlyWallId != null && w.id != onlyWallId && w.id != onlyWallId2) continue;
            if (!Measure(corners, center, w, level, out Face f)) continue;
            // Signed, so a NEGATIVE gap is always in range: an item overlapping a wall is never a
            // placement, however far into it the drag carried it.
            if (f.gap > range) continue;
            faces.Add(f);
        }
        if (faces.Count == 0) return result;

        // A corner tuck never reaches further than the plain snap range, even under Ctrl. Ctrl is for
        // pulling an item to a wall it is nowhere near; carrying it a metre sideways into a corner it
        // was not aiming at is a different offer. The restricted path is exempt, because it can only
        // pick walls the item was already flush with, and a quarter turn moves its edges by half the
        // difference of its sides, far more than any range.
        float cornerRange = onlyWallId != null
            ? range
            : Mathf.Min(range, ResidenceConventions.FURNITURE_SNAP_RANGE);

        // Least movement wins, the same rule the single-wall pass uses: there a wall's |gap| IS the
        // distance it would move the item, so the two passes are scored alike.
        int bi = -1, bj = -1;
        float bestPair = float.MaxValue;
        Vector2 pairShift = Vector2.zero;
        for (int i = 0; i < faces.Count; i++)
        {
            if (faces[i].gap > cornerRange) continue;
            for (int j = i + 1; j < faces.Count; j++)
            {
                if (faces[j].gap > cornerRange) continue;
                if (!Pair(center, faces[i], faces[j], out Vector2 s)) continue;
                if (s.sqrMagnitude >= bestPair) continue;
                bestPair = s.sqrMagnitude; bi = i; bj = j; pairShift = s;
            }
        }

        if (bi >= 0)
        {
            // wallId keeps meaning "the nearest wall it sits against", so everything downstream reads
            // the same thing out of a corner as out of a wall.
            bool firstNearer = Mathf.Abs(faces[bi].gap) <= Mathf.Abs(faces[bj].gap);
            result.snapped = true;
            result.position = center + pairShift;
            result.wallId = (firstNearer ? faces[bi] : faces[bj]).wall.id;
            result.wallId2 = (firstNearer ? faces[bj] : faces[bi]).wall.id;
            return result;
        }

        WallDef best = null;
        float bestGap = float.MaxValue;
        Vector2 bestShift = Vector2.zero;
        foreach (var f in faces)
        {
            float score = Mathf.Abs(f.gap);
            if (score >= bestGap) continue;
            bestGap = score;
            best = f.wall;
            bestShift = f.shift;
        }

        if (best == null) return result;

        result.snapped = true;
        result.position = center + bestShift;
        result.wallId = best.id;
        return result;
    }

    /// <summary>
    /// The wall, or the corner, this footprint already sits flush against, its edge within
    /// <paramref name="tol"/> of the face, or an empty <see cref="Flush"/>.
    /// </summary>
    // What the turn re-snap reads BEFORE the turn: a bed against a wall that turns a quarter about its
    // center has swapped its width and depth, and the gap that opens (or the overlap that closes) is
    // half their difference, which for a bed is far more than any snap range. So the caller asks
    // first, turns, then snaps back to this wall with no range at all.
    //
    // Every wall within tol is collected, not just the running nearest: an item in a corner is flush
    // against both, so scoring them against each other would throw the second away, and the bed would
    // turn out of the corner and through the wall beside it.
    public static Flush Against(Vector2 center, float widthM, float depthM, float yawDeg,
                                LevelDef level, float tol)
    {
        var flush = new Flush();
        if (level?.walls == null) return flush;

        var corners = Corners(center, widthM, depthM, yawDeg);

        var faces = new List<Face>();
        foreach (var w in level.walls)
        {
            if (!Usable(w)) continue;
            if (!Measure(corners, center, w, level, out Face f)) continue;
            if (Mathf.Abs(f.gap) > tol) continue;
            faces.Add(f);
        }
        if (faces.Count == 0) return flush;

        int near = 0;
        for (int i = 1; i < faces.Count; i++)
            if (Mathf.Abs(faces[i].gap) < Mathf.Abs(faces[near].gap)) near = i;

        flush.wall = faces[near].wall;
        for (int i = 0; i < faces.Count; i++)
        {
            if (i == near) continue;
            if (!Pair(center, faces[near], faces[i], out _)) continue;
            flush.wall2 = faces[i].wall;
            break;
        }
        return flush;
    }

    /// <summary>
    /// The four world-XZ corners of a <paramref name="widthM"/> x <paramref name="depthM"/> item
    /// turned by <paramref name="yawDeg"/>.
    /// </summary>
    // Quaternion.Euler(0, yaw, 0) maps (x, z) -> (x cos + z sin, -x sin + z cos). The same mapping the
    // furniture tool draws its ghost with; the other sign made a mirror image at every step but the
    // quarter turns, which is why it went unnoticed there once.
    public static Vector2[] Corners(Vector2 center, float widthM, float depthM, float yawDeg)
    {
        float hw = 0.5f * Mathf.Abs(widthM), hd = 0.5f * Mathf.Abs(depthM);
        float rad = yawDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);

        var local = new[] { new Vector2(-hw, -hd), new Vector2(hw, -hd), new Vector2(hw, hd), new Vector2(-hw, hd) };
        var corners = new Vector2[4];
        for (int i = 0; i < 4; i++)
            corners[i] = center + new Vector2(local[i].x * cos + local[i].y * sin,
                                              -local[i].x * sin + local[i].y * cos);
        return corners;
    }

    // ---------------------------------------------------------------------------------------------

    // One wall as the snap sees it: where its face sits relative to this footprint, and what closing
    // that one gap would cost.
    private struct Face
    {
        public WallDef wall;
        public Vector2 a, nrm;
        public float sign;          // which side of the centerline the item's CENTER is on, +1 or -1
        public float gap;           // signed daylight from the near edge to the face
        public Vector2 shift;       // the single-wall shift that closes it
    }

    private static bool Usable(WallDef w)
        => w != null && w.a != null && w.b != null && w.a.Length >= 2 && w.b.Length >= 2;

    // The daylight between the footprint's near edge and this wall's face on the item's side, and
    // the shift that closes it. False when the item is not alongside the wall at all: past its end,
    // or with its center on the centerline (which side would it even go to).
    //
    // The shift is purely along the wall's normal, and translating a rectangle does not change its
    // extents relative to its own center, so the gap is an EXACT linear function of the shift:
    //
    //     gap(center + s) = gap(center) + sign * Dot(s, nrm)
    //
    // which is the whole reason two walls can be closed together in one step rather than fought over.
    //
    // The end test is strict where FurnitureFit.IsAgainst pads by its NEAR (0.10 m), and the two are
    // deliberately left disagreeing: the fit asks whether a wall still matters to an opening check,
    // where a counter run legitimately overhangs the segment it sits on, and this asks whether to MOVE
    // an item onto a face. A missed snap is a nudge by hand; a wrong one teleports an item sideways
    // onto a face that is not there. No corner needs the slack: a tuck always has span on both walls,
    // because the crossing sits at the end of a span and the item reaches inward from it.
    private static bool Measure(Vector2[] corners, Vector2 center, WallDef w, LevelDef level, out Face f)
    {
        f = default;

        var a = new Vector2(w.a[0], w.a[1]);
        var b = new Vector2(w.b[0], w.b[1]);
        float length = (b - a).magnitude;
        if (length <= ResidenceConventions.EPS) return false;

        Vector2 dir = (b - a) / length;
        var nrm = new Vector2(-dir.y, dir.x);

        float tMin = float.MaxValue, tMax = float.MinValue;
        float nMin = float.MaxValue, nMax = float.MinValue;
        foreach (var c in corners)
        {
            Vector2 rel = c - a;
            float t = Vector2.Dot(rel, dir);
            float n = Vector2.Dot(rel, nrm);
            tMin = Mathf.Min(tMin, t); tMax = Mathf.Max(tMax, t);
            nMin = Mathf.Min(nMin, n); nMax = Mathf.Max(nMax, n);
        }

        // Alongside, not past the end: an item off the end of a wall segment is beside the next
        // room's wall, or beside nothing.
        if (tMax <= 0f || tMin >= length) return false;

        float side = Vector2.Dot(center - a, nrm);
        if (Mathf.Abs(side) <= ResidenceConventions.EPS) return false;

        float sign = Mathf.Sign(side);
        float face = sign * 0.5f * WallLayout.EffectiveThickness(w, level);
        float gap = side > 0f ? nMin - face : face - nMax;

        f = new Face
        {
            wall = w, a = a, nrm = nrm, sign = sign,
            gap = gap, shift = -sign * gap * nrm,
        };
        return true;
    }

    // The one shift that closes BOTH walls' gaps, when there is one.
    //
    // Each gap is linear in the shift (see Measure), so "flush against both" is two linear equations,
    //
    //     Dot(s, nrmA) = -signA * gapA
    //     Dot(s, nrmB) = -signB * gapB
    //
    // and the answer is one 2x2 solve, exact, no iteration, no fighting between the walls. The check
    // that these are the right two equations: a single wall's own shift is -sign*gap*nrm, whose
    // component along nrm is -sign*gap, which is what a closed gap demands.
    //
    // det is the cross product of two unit normals, which is the SINE of the angle between the walls,
    // so the conditioning of the solve and the question "is this a corner" are one number. Two parallel
    // walls, the near and far side of a narrow alcove, have no shared solution and are given none.
    //
    // The pair is qualified on geometry, never on a shared endpoint: WallLinker runs from
    // WallTool.CommitSegment and Relink from a drag release, and neither runs on a generated plan, on
    // load, in Migrate or from VariantRevert, so a sample or an imported .riv holds corners welded to
    // nothing. A partition running into the middle of a wall is a corner with no shared endpoint by
    // construction, and it is exactly where a toilet goes.
    private static bool Pair(Vector2 center, in Face A, in Face B, out Vector2 s)
    {
        s = Vector2.zero;

        float det = A.nrm.x * B.nrm.y - A.nrm.y * B.nrm.x;
        if (Mathf.Abs(det) < WallLinker.MinJunctionSin) return false;   // numerical, not behavioural

        float ra = -A.sign * A.gap, rb = -B.sign * B.gap;
        s = new Vector2((ra * B.nrm.y - A.nrm.y * rb) / det,
                        (A.nrm.x * rb - ra * B.nrm.x) / det);

        // The behavioural guard. A shallow wedge has its solution far up its own point, a jump nobody
        // asked for, and CORNER_SHIFT_FACTOR is where a tuck stops being one. It also stands in for
        // "is the corner near the item": a distant crossing shows up here as a large shift, while a
        // test on the crossing itself would be wrong at an acute corner, where the item genuinely
        // cannot get near the apex.
        float cap = CORNER_SHIFT_FACTOR * Mathf.Max(Mathf.Abs(A.gap), Mathf.Abs(B.gap));
        if (s.sqrMagnitude > cap * cap) return false;

        // `sign` was read from the center BEFORE the shift. A solve that carries the center across
        // either centerline has answered with the far face, which is the item driven through the wall,
        // so the corner is refused and the single-wall pass takes it. (Reading the side from the center
        // alone is a known simplification; this keeps its failure down to a fallback.)
        Vector2 moved = center + s;
        if (Mathf.Sign(Vector2.Dot(moved - A.a, A.nrm)) != A.sign) return false;
        if (Mathf.Sign(Vector2.Dot(moved - B.a, B.nrm)) != B.sign) return false;
        return true;
    }
}
