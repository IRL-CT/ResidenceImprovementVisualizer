using System.Collections.Generic;
using UnityEngine;

// Footprints for the FurnitureCatalog ids, mirrored into the CXRAuthoring assembly.
//
// WHY THIS DUPLICATES THE CATALOG: FurnitureCatalog is a ScriptableObject in Assembly-CSharp, and
// CXRAuthoring has no references, so PlanBuilder, which lives here so the sample plans can be unit
// tested, cannot read the .asset.
//
// THE TABLE IN Build() IS GENERATED. Assets/Resources/FurnitureCatalog.asset is the source of truth;
// the region between the two sentinel comments is written by CatalogArtBinder's
// "0 · Write SampleFurniture.cs" button, exactly as the wrapper prefabs under
// Assets/Prefabs/ResidenceViz/Catalog/ are written by "2 · Generate Wrappers". Edit the asset, then
// re-run the button. A hand edit inside the region is lost on the next run; everything outside it,
// this header included, is hand-written and preserved.
//
// FurnitureCatalogMirrorTests fails the build if the two ever disagree, and
// SampleResidenceInstaller re-checks at seed time so a hand-edited asset in a shipped build still
// reports rather than hides.
//
// Field order matches FurnitureCatalog.Entry: widthM is across the item's front (local X), depthM is
// front-to-back (local Z), heightM is up (local Y). Note that ObjectInstance.boxSizeMeters wants
// [w, h, d]. See BoxSize, which does that reorder so callers never get it wrong.
public static class SampleFurniture
{
    public struct Item
    {
        public string id;
        public float width;          // local X, across the front
        public float depth;          // local Z, front to back
        public float height;         // local Y
        public bool  wallMounted;
        public float mountHeight;    // meters AFF; the anchor for wall-mounted items
        public float decorWidthFrac;
        public float decorHeightFrac;
        public float decorSurfaceOffset;

        /// <summary>The [w, h, d] triple ObjectInstance.boxSizeMeters expects.</summary>
        public float[] BoxSize => new[] { width, height, depth };
    }

    /// <summary>Fallback for an unknown key. Matches ResidenceRenderer.ItemSize's own last resort.</summary>
    public static readonly Item Unknown = new Item
    {
        id = null, width = 0.6f, depth = 0.6f, height = 0.8f,
        wallMounted = false, mountHeight = 0.9f,
        decorWidthFrac = 0.6f, decorHeightFrac = 0.4f, decorSurfaceOffset = 0.01f,
    };

    public static bool TryGet(string id, out Item item) => Table.TryGetValue(id ?? "", out item);

    public static Item Get(string id) => Table.TryGetValue(id ?? "", out var item) ? item : Unknown;

    public static bool Exists(string id) => !string.IsNullOrEmpty(id) && Table.ContainsKey(id);

    public static IEnumerable<Item> All => Table.Values;

    // -------------------------------------------------------------------------------------------

    private static Item Floor(string id, float w, float d, float h) => new Item
    {
        id = id, width = w, depth = d, height = h,
        wallMounted = false, mountHeight = 0.9f,
        decorWidthFrac = 0.6f, decorHeightFrac = 0.4f, decorSurfaceOffset = 0.01f,
    };

    private static Item Wall(string id, float w, float d, float h, float mountHeight) => new Item
    {
        id = id, width = w, depth = d, height = h,
        wallMounted = true, mountHeight = mountHeight,
        decorWidthFrac = 0.5f, decorHeightFrac = 0.25f, decorSurfaceOffset = 0.01f,
    };

    private static readonly Dictionary<string, Item> Table = Build();

    private static Dictionary<string, Item> Build()
    {
        var items = new[]
        {
            // BEGIN GENERATED. CatalogArtBinder, "0 · Write SampleFurniture.cs". Do not hand edit.
            // mobility
            Floor("wheelchair",            0.66f, 1.22f, 0.95f),
            Floor("walker",                0.61f, 0.66f, 0.90f),
            Floor("rollator",              0.64f, 0.74f, 0.94f),
            Floor("hospital_bed",          0.91f, 2.13f, 0.65f),
            Floor("transfer_bench",        0.41f, 0.86f, 0.48f),
            Floor("patient_lift",          0.66f, 1.19f, 1.35f),
            Floor("lift_recliner",         0.89f, 0.97f, 1.07f),
            Floor("bedside_commode",       0.56f, 0.61f, 0.86f),
            Floor("overbed_table",         0.84f, 0.41f, 1.02f),

            // bedroom
            Floor("twin_bed",              0.99f, 2.03f, 0.60f),
            Floor("full_bed",              1.37f, 1.91f, 0.60f),
            Floor("queen_bed",             1.52f, 2.03f, 0.60f),
            Floor("king_bed",              1.93f, 2.03f, 0.60f),
            Floor("daybed",                0.99f, 1.93f, 0.85f),
            Floor("nightstand",            0.46f, 0.41f, 0.61f),
            Floor("dresser",               1.22f, 0.51f, 0.81f),
            Floor("chest_of_drawers",      0.81f, 0.46f, 1.12f),
            Floor("dressing_table",        1.07f, 0.46f, 0.76f),
            Floor("wardrobe",              1.02f, 0.61f, 1.83f),
            Floor("double_wardrobe",       1.83f, 0.61f, 2.03f),
            Floor("bed_bench",             1.22f, 0.41f, 0.46f),
            Floor("blanket_chest",         0.91f, 0.46f, 0.46f),

            // bathroom
            Floor("toilet",                0.51f, 0.71f, 0.79f),
            Floor("comfort_height_toilet", 0.51f, 0.76f, 0.84f),
            Floor("sink_pedestal",         0.56f, 0.46f, 0.84f),
            Wall ("wall_basin",            0.56f, 0.43f, 0.20f, 0.84f),
            Floor("vanity",                0.91f, 0.53f, 0.84f),
            Floor("double_vanity",         1.52f, 0.56f, 0.84f),
            Floor("bathtub",               0.76f, 1.52f, 0.56f),
            Floor("walk_in_tub",           0.76f, 1.42f, 1.35f),
            Floor("roll_in_shower",        0.91f, 1.52f, 0.05f),
            Floor("shower_stall",          0.91f, 0.91f, 2.03f),
            Floor("shower_seat",           0.41f, 0.41f, 0.48f),
            Floor("linen_cabinet",         0.61f, 0.36f, 1.83f),
            Wall ("grab_bar_24",           0.61f, 0.09f, 0.04f, 0.84f),
            Wall ("grab_bar_36",           0.91f, 0.09f, 0.04f, 0.84f),
            Wall ("towel_bar",             0.61f, 0.08f, 0.05f, 1.22f),
            Wall ("paper_holder",          0.15f, 0.08f, 0.10f, 0.66f),

            // kitchen
            Floor("base_cabinet",          0.91f, 0.61f, 0.91f),
            Floor("counter_run",           1.83f, 0.64f, 0.91f),
            Floor("corner_cabinet",        0.91f, 0.91f, 0.91f),
            Floor("sink_base",             0.76f, 0.61f, 0.91f),
            Floor("island",                1.22f, 0.76f, 0.91f),
            Floor("refrigerator",          0.91f, 0.76f, 1.78f),
            Floor("chest_freezer",         0.91f, 0.66f, 0.86f),
            Floor("range",                 0.76f, 0.66f, 0.91f),
            Floor("wall_oven",             0.76f, 0.61f, 1.78f),
            Floor("dishwasher",            0.61f, 0.61f, 0.86f),
            Floor("pantry_cabinet",        0.61f, 0.61f, 2.13f),
            Wall ("wall_cabinet",          0.76f, 0.33f, 0.76f, 1.75f),
            Wall ("wall_cabinet_wide",     1.22f, 0.33f, 0.76f, 1.75f),
            Wall ("microwave",             0.51f, 0.38f, 0.30f, 1.06f),
            Wall ("range_hood",            0.76f, 0.51f, 0.61f, 1.68f),

            // dining
            Floor("dining_table",          1.07f, 1.07f, 0.76f),
            Floor("dining_table_6",        1.83f, 0.91f, 0.76f),
            Floor("dining_table_round",    1.22f, 1.22f, 0.76f),
            Floor("bistro_table",          0.76f, 0.76f, 0.76f),
            Floor("dining_chair",          0.46f, 0.51f, 0.91f),
            Floor("carver_chair",          0.56f, 0.56f, 0.97f),
            Floor("bar_stool",             0.41f, 0.41f, 0.76f),
            Floor("sideboard",             1.52f, 0.46f, 0.81f),
            Floor("china_cabinet",         1.07f, 0.46f, 1.98f),

            // living
            Floor("sofa",                  1.83f, 0.89f, 0.84f),
            Floor("loveseat",              1.42f, 0.89f, 0.84f),
            Floor("sectional",             2.59f, 1.83f, 0.84f),
            Floor("sofa_bed",              1.98f, 0.94f, 0.86f),
            Floor("armchair",              0.85f, 0.85f, 0.84f),
            Floor("accent_chair",          0.71f, 0.76f, 0.81f),
            Floor("recliner",              0.89f, 0.97f, 1.02f),
            Floor("ottoman",               0.61f, 0.61f, 0.41f),
            Floor("floor_cushion",         0.71f, 0.71f, 0.20f),
            Floor("coffee_table",          1.07f, 0.53f, 0.46f),
            Floor("side_table",            0.46f, 0.46f, 0.56f),
            Floor("nest_of_tables",        0.56f, 0.41f, 0.53f),
            Floor("tv_stand",              1.22f, 0.41f, 0.61f),
            Floor("media_unit",            1.83f, 0.41f, 0.51f),

            // office
            Floor("desk",                  1.22f, 0.61f, 0.76f),
            Floor("corner_desk",           1.52f, 1.52f, 0.76f),
            Floor("craft_table",           1.22f, 0.76f, 0.91f),
            Floor("office_chair",          0.66f, 0.66f, 1.07f),
            Floor("filing_cabinet",        0.46f, 0.61f, 1.32f),
            Floor("bookcase",              0.91f, 0.30f, 1.83f),
            Floor("low_bookshelf",         0.91f, 0.30f, 0.91f),
            Floor("exercise_bike",         1.07f, 0.56f, 1.22f),

            // laundry
            Floor("washing_machine",       0.60f, 0.66f, 0.86f),
            Floor("dryer",                 0.60f, 0.66f, 0.86f),
            Floor("stacked_laundry",       0.69f, 0.74f, 1.83f),
            Floor("utility_sink",          0.56f, 0.56f, 0.91f),
            Floor("folding_counter",       1.22f, 0.64f, 0.91f),

            // storage
            Floor("shelving_unit",         0.91f, 0.46f, 1.83f),
            Floor("storage_cabinet",       0.91f, 0.46f, 1.83f),
            Floor("console_table",         1.07f, 0.36f, 0.81f),
            Floor("hall_tree",             1.07f, 0.41f, 1.83f),
            Floor("shoe_bench",            0.91f, 0.36f, 0.46f),
            Floor("coat_rack",             0.46f, 0.46f, 1.83f),
            Floor("trunk",                 0.76f, 0.46f, 0.46f),
            Floor("water_heater",          0.61f, 0.61f, 1.52f),
            Wall ("wall_shelf",            0.91f, 0.25f, 0.05f, 1.40f),

            // fixtures
            Wall ("handrail",              1.22f, 0.08f, 0.04f, 0.91f),
            Floor("threshold_ramp",        0.91f, 0.30f, 0.03f),
            Floor("stair_lift",            0.61f, 0.91f, 1.22f),
            Wall ("light_switch",          0.08f, 0.02f, 0.12f, 1.12f),
            Wall ("outlet",                0.08f, 0.02f, 0.12f, 0.38f),
            Wall ("thermostat",            0.12f, 0.03f, 0.09f, 1.32f),
            Wall ("window_ac",             0.61f, 0.41f, 0.41f, 1.10f),
            Wall ("radiator",              0.76f, 0.12f, 0.60f, 0.35f),
            Wall ("smoke_alarm",           0.13f, 0.13f, 0.05f, 2.30f),
            // END GENERATED
        };

        var map = new Dictionary<string, Item>(items.Length);
        foreach (var i in items) map[i.id] = i;
        return map;
    }

    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// The item's axis-aligned footprint in world XZ once rotated by <paramref name="yaw"/> degrees.
    /// Only multiples of 90 are used by the samples, so this snaps rather than building a full OBB,
    /// an approximate box would make the "furniture is inside its room" test approximate too.
    /// </summary>
    public static Vector2 FootprintXZ(Item item, float yaw)
    {
        int quarter = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 90f) % 4;
        bool swapped = quarter == 1 || quarter == 3;
        return swapped ? new Vector2(item.depth, item.width) : new Vector2(item.width, item.depth);
    }
}
