using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;

// The catalog and its mirror agree, and the numbers in them are usable.
//
// WHY THIS EXISTS. FurnitureCatalog.asset is the source of truth and SampleFurniture is generated
// from it by CatalogArtBinder's "0 · Write SampleFurniture.cs" button. Nothing forces anyone to press
// that button. The failure that follows is silent and is exactly the failure this whole tool exists
// to prevent: SampleFurniture.Get never throws, it returns Unknown at 0.6 x 0.6 x 0.8, so an item
// missing from the mirror renders and measures at a size nobody chose, and "does the wheelchair fit
// beside the bed" gets answered wrongly with no warning anywhere.
//
// SampleResidenceInstaller.VerifyAgainstCatalog still runs at seed time, because a hand-edited asset
// in a shipped build is past the reach of any test. But it only walks the mirror and looks each id up
// in the catalog, so a catalog id absent from the mirror is the one case it cannot see, and that is
// the common direction of this mistake. The bijection below is checked both ways.
//
// The asset is read as TEXT rather than typed. FurnitureCatalog lives in Assembly-CSharp, which no
// asmdef can reference, so EditModeTests cannot name the type. It does not have to: the asset is
// plain YAML with a flat, fixed-shape entry list, and File.ReadAllText needs no reference at all.
[TestFixture]
public class FurnitureCatalogMirrorTests
{
    private const string AssetPath = "/Resources/FurnitureCatalog.asset";

    // The ten the picker's chip row draws, in the order the asset lists them. Order is a UI decision
    // (FurnitureCatalog.Categories() returns first-appearance order), so it is pinned here.
    private static readonly string[] Categories =
    {
        "mobility", "bedroom", "bathroom", "kitchen", "dining",
        "living", "office", "laundry", "storage", "fixtures",
    };

    private class Row
    {
        public string id, displayName, category;
        public float widthM, depthM, heightM, mountHeightM, clearFront, clearSide;
        public int mount;
        public float decorWidthFrac, decorHeightFrac;
        public bool Wall => mount == 1;    // FurnitureCatalog.MountType.Wall
    }

    // A hand-rolled reader for the one shape this file has: a list of "  - id: x" blocks, each a run
    // of "    key: value" lines. Deliberately not a YAML library. It has to break loudly if the asset
    // stops looking like this, and a general parser would quietly cope.
    private static List<Row> ReadAsset()
    {
        string path = Application.dataPath + AssetPath;
        Assert.IsTrue(File.Exists(path), $"No catalog at {path}.");

        var rows = new List<Row>();
        Row cur = null;

        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.TrimEnd();

            if (line.StartsWith("  - id: "))
            {
                cur = new Row { id = line.Substring("  - id: ".Length).Trim() };
                rows.Add(cur);
                continue;
            }
            if (cur == null || !line.StartsWith("    ")) continue;

            int colon = line.IndexOf(": ", System.StringComparison.Ordinal);
            if (colon < 0) continue;

            string key = line.Substring(0, colon).Trim();
            string val = line.Substring(colon + 2).Trim();

            switch (key)
            {
                case "displayName":      cur.displayName = val; break;
                case "category":         cur.category = val; break;
                case "widthM":           cur.widthM = F(val); break;
                case "depthM":           cur.depthM = F(val); break;
                case "heightM":          cur.heightM = F(val); break;
                case "mount":            cur.mount = (int)F(val); break;
                case "mountHeightM":     cur.mountHeightM = F(val); break;
                case "clearanceFrontM":  cur.clearFront = F(val); break;
                case "clearanceSideM":   cur.clearSide = F(val); break;
                case "decorWidthFrac":   cur.decorWidthFrac = F(val); break;
                case "decorHeightFrac":  cur.decorHeightFrac = F(val); break;
            }
        }

        Assert.Greater(rows.Count, 0, "The catalog parsed to nothing. Has the asset's shape changed?");
        return rows;
    }

    private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    // -------------------------------------------------------------------------------------------
    // The bijection
    // -------------------------------------------------------------------------------------------

    [Test]
    public void EveryCatalogId_IsInTheMirror()
    {
        var missing = new List<string>();
        foreach (var r in ReadAsset())
            if (!SampleFurniture.Exists(r.id)) missing.Add(r.id);

        CollectionAssert.IsEmpty(missing,
            "These ids are in FurnitureCatalog.asset and not in SampleFurniture. They resolve to "
            + "Unknown (0.6 x 0.6 x 0.8) everywhere in CXRAuthoring, silently. Re-run the Catalog Art "
            + "Binder's \"0 · Write SampleFurniture.cs\".");
    }

    [Test]
    public void EveryMirrorId_IsInTheCatalog()
    {
        var known = new HashSet<string>();
        foreach (var r in ReadAsset()) known.Add(r.id);

        var extra = new List<string>();
        foreach (var item in SampleFurniture.All)
            if (!known.Contains(item.id)) extra.Add(item.id);

        CollectionAssert.IsEmpty(extra,
            "These ids are in SampleFurniture and not in the catalog. The picker cannot offer them, "
            + "so a sample or a generated plan can place something nobody can place by hand.");
    }

    [Test]
    public void EveryMirroredNumber_MatchesTheCatalog()
    {
        var wrong = new List<string>();

        foreach (var r in ReadAsset())
        {
            if (!SampleFurniture.TryGet(r.id, out var m)) continue;   // the bijection test owns this

            Check(wrong, r.id, "width", r.widthM, m.width);
            Check(wrong, r.id, "depth", r.depthM, m.depth);
            Check(wrong, r.id, "height", r.heightM, m.height);

            if (r.Wall != m.wallMounted)
                wrong.Add($"{r.id}: mount {r.mount} in the asset, wallMounted {m.wallMounted} in the mirror");
            else if (r.Wall)
                Check(wrong, r.id, "mountHeight", r.mountHeightM, m.mountHeight);

            // Nothing else compares these two, and PlanBuilder writes them straight into every
            // WallMountDef it authors, so a mirror that disagreed would render a sample's grab bar at
            // the wrong proportion with nothing complaining.
            Check(wrong, r.id, "decorWidthFrac", r.decorWidthFrac, m.decorWidthFrac);
            Check(wrong, r.id, "decorHeightFrac", r.decorHeightFrac, m.decorHeightFrac);
        }

        CollectionAssert.IsEmpty(wrong,
            "The mirror has drifted from the catalog. Re-run \"0 · Write SampleFurniture.cs\".");
    }

    private static void Check(List<string> into, string id, string field, float asset, float mirror)
    {
        if (Mathf.Abs(asset - mirror) > 1e-4f)
            into.Add($"{id}.{field}: asset {asset:0.###}, mirror {mirror:0.###}");
    }

    // -------------------------------------------------------------------------------------------
    // The numbers on their own
    // -------------------------------------------------------------------------------------------

    [Test]
    public void EveryId_IsUniqueAndInTheSharedKeySpace()
    {
        var seen = new HashSet<string>();
        foreach (var r in ReadAsset())
        {
            Assert.IsTrue(seen.Add(r.id), $"'{r.id}' appears twice. Get() keeps the last one silently.");

            // The key space FurnitureCatalog, PrefabRegistry, SampleFurniture and the wrapper prefab
            // filenames all share. A colon is reserved: CustomItems mints "custom:" + a slug, and
            // FindPrefab missing is what makes a custom item fall through to a labeled box.
            foreach (char c in r.id)
                Assert.IsTrue(c == '_' || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'),
                    $"'{r.id}' is not lowercase snake_case, and the id is also a prefab filename.");

            Assert.IsNotEmpty(r.displayName, $"'{r.id}' has no display name, so the picker shows its id.");
        }
    }

    [Test]
    public void EveryFloorDimension_IsReachableByTheResizeControls()
    {
        // ResidenceEditController.MIN_ITEM_SIZE / MAX_ITEM_SIZE. Restated rather than referenced:
        // they live in Assembly-CSharp. A FLOOR item outside them cannot be reset to its own size,
        // because Reset to catalog size drives the same bounded fields the gizmo does.
        //
        // Wall mounts are exempt and must be: they get no transform gizmo at all (a mount is moved by
        // its offset and side, not by a handle), and the catalog's thinnest rows are wall mounts on
        // purpose. A 24" grab bar really is 40 mm of steel, and rounding it up to clear a bound that
        // never applies to it would put a lie in the one number this catalog exists to get right.
        const float Min = 0.05f, Max = 4f;

        // Floor pieces that are genuinely flatter than the control's floor. Both are things you roll
        // over rather than put down, and their real thickness is the whole point of drawing them: a
        // threshold ramp exists to be 30 mm. Exempt on HEIGHT only. Nothing in the catalog is under
        // 0.05 in plan, and a footprint that small would be unpickable.
        var flat = new HashSet<string> { "threshold_ramp", "roll_in_shower" };

        foreach (var r in ReadAsset())
        {
            foreach (var pair in new[] { ("width", r.widthM), ("depth", r.depthM), ("height", r.heightM) })
            {
                Assert.Greater(pair.Item2, 0f, $"{r.id}.{pair.Item1} is not a size.");
                Assert.LessOrEqual(pair.Item2, Max,
                    $"{r.id}.{pair.Item1} is over MAX_ITEM_SIZE, which no control can reach.");

                if (r.Wall) continue;
                if (pair.Item1 == "height" && flat.Contains(r.id)) continue;

                Assert.GreaterOrEqual(pair.Item2, Min,
                    $"{r.id}.{pair.Item1} is under MIN_ITEM_SIZE, and it is a floor item, so "
                    + "Reset to catalog size cannot restore it.");
            }
        }
    }

    [Test]
    public void EveryWallMount_HangsAboveTheFloor()
    {
        // mountHeightM is the CENTRE, which is why this is not a comparison against zero. A wall
        // cabinet at 1.75 with a 0.76 body has its bottom at 1.37, the clear splashback the kitchen
        // rows are authored around.
        foreach (var r in ReadAsset())
        {
            if (!r.Wall) continue;
            Assert.GreaterOrEqual(r.mountHeightM - r.heightM / 2f, 0f,
                $"{r.id} mounts at {r.mountHeightM} and is {r.heightM} tall, so it sinks into the floor.");
            Assert.LessOrEqual(r.mountHeightM + r.heightM / 2f, ResidenceConventions.MAX_WALL_HEIGHT,
                $"{r.id} mounts above any ceiling this app will draw.");
        }
    }

    [Test]
    public void EveryCategory_IsOneOfTheTenAndHasItems()
    {
        var counts = new Dictionary<string, int>();
        foreach (string c in Categories) counts[c] = 0;

        foreach (var r in ReadAsset())
        {
            Assert.Contains(r.category, Categories,
                $"'{r.id}' is in category '{r.category}', which draws a chip nobody designed.");
            counts[r.category]++;
        }

        foreach (var kv in counts)
            Assert.Greater(kv.Value, 0, $"Category '{kv.Key}' is empty, so its chip opens on nothing.");
    }

    [Test]
    public void CategoriesAreGrouped_BecauseAssetOrderIsChipOrder()
    {
        // FurnitureCatalog.Categories() takes first appearance, and InCategory() preserves list order,
        // so the asset's row order IS the chip order and the grid order. A category split across two
        // runs of the file puts its chip in the wrong place and nothing else would ever say so.
        var order = new List<string>();
        foreach (var r in ReadAsset())
            if (order.Count == 0 || order[order.Count - 1] != r.category) order.Add(r.category);

        CollectionAssert.AreEqual(Categories, order,
            "Either a category is split across the asset, or the chip order changed.");
    }

    [Test]
    public void EveryFloorItemYouApproach_StatesItsClearance()
    {
        // ClearanceRules.Registry is still empty, by decision, so nothing reads these yet. They are
        // checked anyway because a zero here is indistinguishable from an unauthored row, and all 35
        // of the original entries sat at zero for exactly that reason.
        var zero = new List<string>();

        foreach (var r in ReadAsset())
        {
            if (r.Wall) continue;
            if (r.clearFront <= 0f) zero.Add(r.id);
        }

        // The things you walk past rather than up to. Everything else needs a number.
        var noApproach = new HashSet<string>
        {
            "floor_cushion", "threshold_ramp",
        };

        zero.RemoveAll(id => noApproach.Contains(id));
        CollectionAssert.IsEmpty(zero,
            "These floor items have no approach clearance. Give each one a number, or add it to the "
            + "noApproach list above with the reason it needs none.");
    }
}
