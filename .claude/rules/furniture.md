---
paths:
  - "Assets/Editor/CatalogArtBinder.cs"
  - "Assets/Scripts/ResidenceViz/CatalogArtFit.cs"
  - "Assets/Scripts/ResidenceViz/FurnitureCatalog.cs"
  - "Assets/Scripts/ResidenceViz/MountPlacement.cs"
  - "Assets/Scripts/ResidenceViz/ResidenceRenderer.cs"
  - "Assets/Scripts/ResidenceViz/Tools/FurnitureTool.cs"
  - "Assets/Scripts/Authoring/Interior/FurnitureFit.cs"
  - "Assets/Scripts/Authoring/Interior/CustomItems.cs"
  - "Assets/Scripts/TransformGizmo.cs"
  - "Assets/Prefabs/ResidenceViz/Catalog/**"
  - "Assets/Resources/FurnitureCatalog.asset"
  - "Assets/Resources/ResidenceCatalogRegistry.asset"
  - "Assets/Scripts/Authoring/Interior/SampleFurniture.cs"
  - "Assets/Tests/EditMode/FurnitureCatalogMirrorTests.cs"
---

# Furniture catalog and transform handles

> Loaded when a file under the paths above is read. Rules only: the reasoning is in the design note linked at the end. Edit this file when a rule changes; update CLAUDE.md only if something every session needs moves.

## Furniture catalog

**`Assets/Resources/FurnitureCatalog.asset` is the source of truth for the whole catalog**, in ten
categories: `mobility, bedroom, bathroom, kitchen, dining, living, office, laundry, storage,
fixtures`. `ResidenceRenderer` resolves each id against **`ResidenceCatalogRegistry.asset`**
(owned by the binder: never `Assets/Resources/PrefabRegistry.asset`, which the Site tool's Place and
Paint palettes render): a prefab if one exists, otherwise a correctly sized labeled box. Most items
have art; the rest stay boxes on purpose, each with its reason recorded in `CatalogArtBinder`'s header.

- **Asset row order IS chip order and grid order.** `Categories()` takes first appearance and
  `InCategory()` preserves list order, so the categories must stay grouped and in the order the rail
  should read them. Nothing sorts in code, deliberately: the order is an editorial decision.
- **`MountType.Counter` and `Ceiling` are inert.** `Entry.IsWallMounted` is `mount == Wall` alone and
  nothing reads the other two, so a `Counter` entry silently behaves as a floor item. **A counter-top
  item is a Wall mount at counter height** (`microwave` at 1.06, which is a 0.30 m box centered on a
  0.91 m counter; `range_hood` at 1.68).
- **`Assets/Editor/CatalogArtBinder.cs`** (Tools → ResidenceViz → Catalog Art Binder) generates one wrapper
  per bound id at `Assets/Prefabs/ResidenceViz/Catalog/<id>.prefab`: unscaled floor-pivoted root carrying
  `CatalogArtFit`, an `Art` child holding the baked fit scale and pivot offset, the pack prefab nested
  inside with a quarter-turn yaw (both packs face −Z; `rotationY = 0` looks down +Z). **Yaw sits
  inside the scaled node** and must be a multiple of 90°. **The `Rows` table is the source of truth**;
  wrappers are derived and regenerating overwrites them (`CatalogArtFit.handTuned` is the one-off
  escape). The fit stretches each axis independently to the catalog size; `squash` (1.00 =
  undistorted) picks donors; `GasStove` is a cooktop so `range` is a `KitchenOven`; `wall_cabinet`
  uses `PivotZ.Back` because `MountPose` puts the origin on the wall face, and so does every wall
  mount added since.
- **Squash ranks fit and is blind to identity.** Unconstrained it offers a wash basin as a shower
  stall and an extractor hood as a base cabinet. **A donor is drawn from its own family** (by prefab
  name prefix), and squash only chooses within it.
- **The half turn is free; the quarter turn is the trap.** A footprint is identical under 180, so the
  ranking is unaffected. 90 swaps the footprint, so it wins whenever the donor is deeper than wide
  and the target wider than deep, and it buys that fit by standing the item **sideways**. So a seat,
  bed, appliance or case good carries **180** unless the donor itself is authored rotated, which the
  Mega Pack's kitchen cabinets are (`base_cabinet` at 270 faces the room). Symmetric things take
  whichever fits. `armchair` shipped at 270 on a square target, where the swap could not help, and
  faced the side wall for it.
- **`SampleFurniture.cs` is generated from the asset** by the binder's `0 · Write SampleFurniture.cs`,
  between its `BEGIN GENERATED` / `END GENERATED` sentinels; everything outside them is hand-written
  and preserved. It writes a `.cs` file, so it refreshes last and the domain reload ends the call.
  `FurnitureCatalogMirrorTests` fails the build on any drift, **in both directions**, and
  `SampleResidenceInstaller.VerifyAgainstCatalog` stays as the seed-time backstop.
- **Make your own** (`CustomItems.cs`, `CXRAuthoring`): a name plus width/depth/height, stored as
  `CustomItemDef` on **`ResidenceDoc.customItems`**, so every variant offers the same list and the
  `.riv` carries it. Floor-standing only. **The id is `custom:` + a slug of the name**
  (`custom:reading_chair`), because `VariantDiff` and the placeholder box both recover the label from
  the key alone; the prefix keeps it out of the catalog and registry key space. **Create and delete,
  never edit**: deleting drops the definition and leaves every placement standing. A custom item
  reaches the rest of the app as a synthesized `FurnitureCatalog.Entry`, so **`ResidenceRenderer.EntryFor`
  is the one furniture lookup**: `Catalog.Get` alone reports a custom item as unknown.
- **Real art has the placeholder's shape**: `Item` root, stretch on the `Art` child, a `Label` on every
  item. `FitCollider` runs on every re-pose. `PoseGO` has three branches (`Box`, `CatalogArtFit`, raw
  root-scale); `PoseMount` applies the fit too.
- Transform handles: `TransformGizmo` is reused literally from Site (`minHandleSize`, `Tick(acceptInput)`,
  `yawOnly` added, all defaulting to Site's behaviour; ResidenceViz sets `yawOnly`, so only the Y ring
  draws). **Shift means free here, as in the drawing tools.** The ring and the `MeasureUI.Facing`
  field step `FACING_STEP_DEG` (15°) and Shift releases both (the field to `FACING_FINE_DEG`, 1°; a
  typed value is never quantised). **Scale means resize in real units** (`boxSizeMeters`;
  `ObjectInstance.scale` is not read; `Reset to catalog size` in the rail). **Yaw only, no Y.**
  **The re-fit runs on release**, not per frame; `ResidenceRenderer.PoseFurnitureGO` re-poses the live
  GameObject during a drag and only the release calls `RebuildFurniture()`. **Placing selects what
  you placed.** Wall mounts get no gizmo. Rail controls plus a drag that re-hosts via
  `ResidenceMetrics.NearestWall`.
- **Wall snap (`FurnitureSnap`, `CXRAuthoring`)**: the placement ghost and the Move handle put a
  floor item flush against the nearest wall **face** (true rotated outline, half a thickness off the
  centerline) when its edge is within `FURNITURE_SNAP_RANGE`; Ctrl widens the reach to `MOUNT_REACH`;
  Shift wins over Ctrl. **Snap, then `FurnitureFit.Fit`**, in that order everywhere
  (`ResidenceEditController.SnapToWall`). Arrow nudges do not snap. An item within
  `FURNITURE_FLUSH_TOL` of a face **stays flush through a turn or a resize**: `CaptureFlush` reads the
  wall once before the first write and `CommitFurnitureEdit` re-seats onto that wall with no range.
- **A corner beats either of its walls.** Two walls the item is alongside and in range of are closed
  **together**, by one exact 2x2 solve (`FurnitureSnap.Pair`), so it tucks flush against both; a wall
  the item overlaps is always closed, since the range test is signed. The pair is qualified on
  geometry alone, never on a welded endpoint. Guards, in order: `WallLinker.MinJunctionSin` (numerical
  only), then `CORNER_SHIFT_FACTOR` x the nearer wall's own shift, which admits any corner sharper
  than about 40° and falls back to the single wall otherwise, then a check that the solve keeps the
  center on the same side of both centerlines. **Ctrl does not widen the tuck**: the corner pass keeps
  to `FURNITURE_SNAP_RANGE` whatever the reach, the turn re-seat (`onlyWallId`) excepted. `Against`
  returns a **`Flush` pair** and `CaptureFlush` remembers both walls, so a quarter turn in a corner
  re-tucks into it. At an exact tie which wall reads as `wall` / `wallId` is arbitrary and means
  nothing; `ToWall` takes the pair either way round.
  **`RotateFurniture` is the one writer of `rotationY`** (R and Shift+R, Z/X, the four quarter cells,
  the field, the ring). Esc, or a right click with no drag (`ViewController.LookClicked`), puts the
  armed catalog item back (`IResidenceTool.Cancel`).

→ [`docs/design/furniture.md`](../../docs/design/furniture.md)
