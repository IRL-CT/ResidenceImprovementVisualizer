# Design notes. Furniture: fit, handles, picker and art

> `FurnitureFit` (the opening rule for anything placed by hand), the transform handles that made
> furniture movable, the footprint-tile picker, and how the two furniture packs are bound to catalog
> ids through generated wrapper prefabs. The rules are summarised in
> [`.claude/rules/furniture.md`](../../.claude/rules/furniture.md); the reasoning lives here.

## The same rule, for everything placed by hand: `FurnitureFit`

All of the builder's fit logic (see [samples-and-planbuilder.md](samples-and-planbuilder.md)) only ever
ran for the authored samples. Anything a *user* placed had no fit logic at
all: `OpeningFit` guarded doors while `FurnitureTool.Place` wrote the cursor position straight into the
level, so clicking in a doorway put furniture in a doorway, silently. `FurnitureFit` is that rule
re-expressed over the **emitted** `WallDef`/`OpeningDef` rather than `PlanBuilder`'s pending records,
and it follows `OpeningFit`'s contract. Slide to the nearest legal spot, refuse only when nothing is
legal, and return a `reason` written to be shown verbatim in the rail.

The two rules that carry over unchanged are the ones that make it usable rather than annoying: only
openings the item is **tall enough to reach** block it (a sofa belongs under a window sill), and only
walls the item is actually **against** are considered (an approach strip in front of a door is the
thing that was tried and reverted). `FitMount` is the bounded form for wall-mounted items, because a
grab bar hanging off the end of its own wall is not a placement.

Two things differ from `ResidenceMetrics.FootprintOf` on purpose. `FurnitureFit.Footprint` bounds the
**truly rotated** rectangle instead of snapping to a quarter turn: the tool hands out 15° steps and a
continuous slider, and at 45° the snap understates the extent by most of a diagonal. And the placement
ghost now draws at the **fitted** position, rotated the way `Quaternion.Euler` actually rotates: it had
the sign of `sin` flipped, so the preview was a mirror image of what spawned at every angle except the
quarter turns, which is why nobody noticed.

Still deliberately unguarded, in the builder only: `PlanBuilder.Free` does no opening check (its
handful of conflicts are placed explicitly, with comments), and wall mounts do not check each other.

## Placing was the only thing you could do to a piece of furniture

`FurnitureTool.Place` wrote an `ObjectInstance` into the level and that was the last time anything
touched it. `SelectTool` offered one rotation slider; **nothing anywhere wrote `position` or
`boxSizeMeters` after placement**, so moving a chair 30 cm meant deleting it and clicking again.
`ResidenceToolBase.LeftHeld`/`LeftReleased` had existed, unused, the whole time: nothing in ResidenceViz
dragged anything.

The Site tool has had the answer since long before: pick from a searchable grid, drop it, then grab /
rotate / scale it with handles. **`TransformGizmo` is reused literally**, not re-expressed the way
`WallLinker` re-expresses `FenceLinker`. It takes GameObjects and a Camera and emits deltas, with
nothing site-shaped in it, and literal reuse is what makes the two editors feel like one tool instead
of two similar ones. It joins `WorldRenderer`, `PrefabRegistry` and `EnvironmentScale` as
shared-by-design. Two knobs were added there, both defaulting to Site's behaviour: `minHandleSize`
(the stock 2 m floor on the handle radius is right for a tree and draws a 0.51 m toilet's gizmo four
times the size of the toilet) and `Tick(acceptInput)`.

**Shift means free, here as in the drawing tools.** It did not always. The Site tool's convention is
free by default and Shift to snap while transforming, and ResidenceViz inherited it for the rotate
ring, so the app had two rules for one key: Shift while drawing a wall meant "stop snapping", Shift
while turning a chair meant "start". The argument for the inversion (drawing wants snapping by
default, transforming wants free by default) is sound for a site, where a tree can sit at any angle,
and wrong for a room, where nearly every item stands square to a wall. So the ring and the Facing
field now step 15° by default and Shift releases both, the same word meaning the same thing
everywhere in ResidenceViz. The gizmo itself keeps Site's default; the controller sets the snap
each frame.

**Furniture snaps to walls (`FurnitureSnap`).** The other half of placing something by a wall is
getting it against the wall, and until this the ghost went exactly where the cursor was: a dresser
landed a few centimetres off the wall or a few centimetres into it, and truing it up meant zooming in
and nudging. Now the ghost and the Move handle put a floor item's near edge on the nearest wall
**face** when it is within `FURNITURE_SNAP_RANGE` (15 cm: it should feel like the wall catching
something aimed at it, and leave the middle of a room free). Shift places free; Ctrl pulls from as
far as `MOUNT_REACH`. Five decisions worth knowing:

- **The face, not the centerline.** `WallLayout.EffectiveThickness` gives the half-thickness on the
  item's side of the line. Snapping to the centerline buries half the item.
- **The true rotated outline, not `FurnitureFit.Footprint`.** The footprint is an axis-aligned bound:
  the safe direction for a clearance test and the wrong one here, since at 45° it would hold the item
  most of a diagonal off the wall. `FurnitureSnap.Corners` is the one yaw mapping, and the ghost draws
  from it too, so the outline you see is the outline that snaps.
- **Snap, then fit.** `FurnitureFit.Fit` slides along the wall, so flushness survives it; the other
  order would let a door slide undo the snap. `ResidenceEditController.SnapToWall` is the shared
  entry so the ghost and the handle cannot drift apart.
- **A flush item stays flush through a turn or a resize.** A bed against a wall that turns a quarter
  about its center has swapped width and depth, and the gap that opens (or the overlap that closes)
  is half their difference, far more than any snap range. So the wall is read **once, before the first
  write** (`CaptureFlush`, guarded by a checked flag because "not flush" is a legitimate answer), and
  `CommitFurnitureEdit` re-seats onto that wall with no range at all. Every yaw write goes through
  `RotateFurniture` so the capture cannot be skipped by a path that forgot. Arrow nudges stay literal.
  A corner is captured as **both** its walls, or the turn seats the bed on the one and drives it
  through the other.
- **A corner beats either of its walls.** The first version closed the nearer wall's gap and stopped,
  which is why a nightstand would not tuck: one edge landed on its face while the other stayed a
  centimetre off the wall beside it, or a centimetre inside it, and nothing but the eye could true
  both up. The fix rests on one observation. The shift is purely along the wall's normal, and
  translating a rectangle does not change its extents relative to its own center, so a gap is an
  **exact linear function of the shift**: `gap(center + s) = gap(center) + sign · Dot(s, nrm)`. Flush
  against two walls is therefore two linear equations and one 2x2 solve, exact, with no iteration and
  no walls fighting each other. The determinant is the cross product of two unit normals, which is the
  **sine of the corner angle**, so the conditioning of the solve and the question "is this a corner"
  turn out to be one number, and two parallel walls (the near and far side of a narrow alcove) simply
  have no shared answer.

  Four things fell out of it or were settled around it:

  - **Closing an overlap is free.** A wall the item is buried in was always a candidate, since the
    range test is signed; it just lost the contest on `|gap|` and its overlap was left open. That
    *was* the "flush against one wall, half inside the other" bug, and the pair solve closes both by
    definition.
  - **The pair is qualified on geometry, never on a shared endpoint.** Welding looks like the right
    test and is not: `WallLinker` runs from `WallTool.CommitSegment` and `Relink` from a drag release,
    and neither runs on a generated plan, on load, in `Migrate` or from `VariantRevert`, so a sample
    or an imported `.riv` holds corners welded to nothing. And a partition running into the middle of
    a wall is a corner with no shared endpoint by construction, which is exactly where a toilet goes.
  - **The cap is stated in gaps, not in `range`.** A tuck may carry the item at most
    `CORNER_SHIFT_FACTOR` (3) times what the nearer wall alone would have moved it. Since the solved
    shift grows as 1/sin, that is an angle cut in disguise: it offers the tuck at any corner sharper
    than about 40 degrees and leaves a shallow wedge on the single-wall snap, which is what it had
    before. Writing it in `range` would have been meaningless (0.15 by default, 1.2 under Ctrl, and
    the turn re-seat passes infinity, which this form is inert at). It also stands in for "is the
    corner near the item": a test on the crossing itself was considered and dropped, because it is
    wrong at an acute corner, where the item genuinely cannot get near the apex, while a distant
    crossing already shows up here as a large shift.
  - **Ctrl does not widen the tuck.** Ctrl offers a wall the item is nowhere near; carrying it a metre
    sideways into a corner nobody aimed at is a different offer, so the corner pass keeps to
    `FURNITURE_SNAP_RANGE` whatever the reach. The restricted path (the turn re-seat) is exempt,
    because it can only pick walls the item was already flush with.

  Two things known and deliberately not done. `FurnitureSnap`'s end test is strict where
  `FurnitureFit.IsAgainst` pads by its `NEAR` (10 cm); the two are left disagreeing because they ask
  different questions (the fit asks whether a wall still matters to an opening check, where a counter
  run legitimately overhangs its segment; the snap asks whether to *move* an item onto a face) and the
  risks are asymmetric, a missed snap being a nudge by hand and a wrong one a teleport onto a face
  that is not there. And which side of a wall the item is on is still read from its center alone; the
  pair solve checks that its answer does not carry the center across either centerline, which keeps
  that simplification's failure down to a fallback rather than curing it.

The raw drag position is accumulated separately from the snapped one (`_moveRaw`, the way the gizmo
accumulates `_rotRaw`): snapping the stored position and adding the next delta to it would make the
wall sticky, the item unable to leave until the cursor had travelled the whole snap range again.

**Cancelling a placement.** Once a thumbnail was picked, every click placed one and nothing put it
back. Esc now asks the active tool first (`IResidenceTool.Cancel`, one rung above deselect), and a
right click with no drag does the same. The click is detected in `ViewController`, not the tool,
because the right button also starts the look: locking the cursor warps it to the screen center and
that warp arrives as one large delta only the view controller knows to discard, so a tool summing raw
deltas would never see a sub-threshold click.

Four things are ResidenceViz's rather than Site's:

- **Scale means resize in REAL UNITS.** `boxSizeMeters` is the item's true size: what `ResidenceRenderer`
  draws, what `FurnitureFit` tests against a doorway, what the occupancy checks stand people clear of.
  A free 0.1-5× multiplier, the way Site scales a tree, would leave a 1.4×-scaled toilet reporting
  clearances for a toilet that does not exist. So the gizmo's additive scale delta is applied as a
  proportional factor to the real dimensions, the rail shows the user's chosen units, and **Reset to catalog
  size** is always one click away. (`ObjectInstance.scale` is *not* the field: ResidenceViz's render path
  has never read it.)
- **Yaw only, no Y.** Every footprint in the app: `FurnitureFit`, `ResidenceMetrics`, `SelectionOverlay`,
  the occupancy checks. Is computed from `rotationY` alone and furniture stands on `Level.elevation`,
  so an X/Z tilt or a lifted item would show on screen and in none of the numbers.
- **The re-fit runs on RELEASE, not per frame.** `FurnitureFit` slides rather than refuses, and an
  item that jumped aside mid-drag would be fighting the cursor still holding it. Growing or turning an
  item reaches into a doorway exactly as moving it does, so all three paths end at the same re-fit.
  The re-fit obeys the placement rules: a sofa stretched wider still passes under a window sill and
  must not be shoved along the wall for growing, while raising it past the sill makes it block the
  window.
- **A wall mount gets no gizmo.** It is parameterised by `(wallId, offset, side, mountHeight)`,
  there is no direction it can travel that is not along a wall, so it gets rail controls plus a drag
  that re-hosts it onto the nearest wall. `ResidenceMetrics.NearestWall` is that answer, lifted out of
  `FurnitureTool` so placing and re-hosting cannot disagree about which side of a wall the cursor is on.

`ResidenceRenderer.PoseFurnitureGO` is what makes a drag affordable: a drag writes the def **and** re-poses
the live GameObject, and only the release rebuilds. The obvious alternative (mutate and `Rebuild()`) 
destroys and respawns every GameObject in the residence each frame, and `BuildPlaceholderBox` does a
`Shader.Find` and a `new Material` per item, so a drag over a furnished plan would allocate hundreds
of materials a second. Worse, it would destroy the very object the gizmo is holding. The long-dead
`RebuildFurniture()` is what the release calls. Spawn and re-pose go through one method, so an item a
drag resized looks identical either way; it also closes an old hole, that a prefab was instantiated at
its authored size and ignored a resize entirely (art now scales *relative* to the catalog size, so an
un-resized item renders exactly as authored).

**Placing now selects what you placed.** One line, and it is what joins "place" to "manipulate",
before it, the handles never appeared on the thing you had just put down.

## The picker is a grid, and the tiles are floor plans

Search plus an **All** chip plus a 3-column `UITheme.Thumb` grid, the shape of Site's Place rail, with
`ThumbnailCache` giving a real preview the day art lands under a catalog key.

Until then a tile is **not** the entry's swatch. The catalog colours by *category*, so a grid of flat
swatches makes every mobility item the same blue and every bedroom item the same purple: the tile
would restate the chip you just clicked and nothing else. Each tile instead draws the item's true
footprint against one fixed 2.3 m reference (the longest thing in the catalog is a 2.13 m hospital
bed), **not** normalised per tile, because the entire point is that a double bed and a nightstand are
not the same size. A bed fills its tile, a nightstand is a dot, a grab bar is a sliver. That is what
the old text rows carried, in the form this catalog exists to be honest about.


## The art is bound through generated wrapper prefabs: `CatalogArtBinder`

Two furniture packs were already in the project and referenced by nothing:
`Assets/Prefabs/Furniture/Cute_Furniture_Free/` (67 toon prefabs, two materials for the whole pack)
and `Assets/Prefabs/Furniture 2/`: the "Furniture Mega Pack", 511 prefabs. Neither can be registered
directly, for three reasons that are each fatal on their own:

- **Neither pack is at catalog scale.** The Mega Pack is roughly 2× oversized at the prefab root: a
  bed measures 5.28 m long, a bathtub 2.91 m. `PoseGO` scales real art *relative* to the catalog size
  and deliberately does not normalise against the prefab's own bounds, so a raw donor renders at
  whatever size it was authored.
- **`PoseMount` applies no scale at all**, so a wall-mounted donor has no correction whatsoever.
- **Both packs are Blender exports, so every model faces −Z**, while `PlanBuilder.YawFacingInto` is
  explicit that *"rotationY = 0 looks down +Z"*.

So `Assets/Editor/CatalogArtBinder.cs` (**Tools → ResidenceViz → Catalog Art Binder**) generates one
wrapper per bound id at `Assets/Prefabs/ResidenceViz/Catalog/<id>.prefab`: an unscaled, floor-pivoted root
carrying `CatalogArtFit`, an `Art` child holding the baked fit scale and pivot offset, and the pack
prefab nested inside that with a quarter-turn yaw. **Yaw sits inside the scaled node**, because Unity
applies `localScale` before `localRotation` on one transform and a non-quarter turn under a non-uniform
parent scale is a shear: the binder refuses any yaw that is not a multiple of 90°.

**The `Rows` table in that file is the source of truth, not the prefabs.** A wrapper is a derived
artifact and regenerating overwrites it; `CatalogArtFit.handTuned` is the escape hatch for a one-off.

**The fit stretches each axis independently** to the exact catalog size, matching what the placeholder
box does, so the picture keeps agreeing with the numbers `FurnitureFit` and `ResidenceMetrics` report. The
cost is distortion when a donor's proportions differ, which the binder reports as a **squash** figure
(1.00 = undistorted) and which is what picks the donors: `Measure Family` ranks a whole folder on it,
turning 50 candidates into a dozen before any screenshot is taken. Donors were chosen best-fit-first
with the Cute pack preferred wherever it lands within 1.30, which is the seven ids its own art
actually covers well.

**Squash cannot settle yaw**, because a footprint is identical under a half turn. That is the one
thing here decided by looking rather than measuring, and skipping it is not cosmetic: the sample
apartment's sofa stood against the west wall with its cushions pressed into the wall and its backrest
facing the living room, and nothing anywhere complained.

Two selection traps the ranking walked into and the table now pins: `GasStove` is a **cooktop**, 0.3 m
tall, so `range` must be a `KitchenOven`; and ranking on aspect alone offered a kitchen extractor hood
for `island` and a sink for `wall_cabinet`.

**Deliberately left as boxes, with the reason, so it does not get re-litigated:** the five mobility
items (`wheelchair`, `walker`, `hospital_bed`, `transfer_bench`, `patient_lift`: neither pack has any
medical equipment, and these are what the tool's whole argument rests on), `shower_seat` and
`roll_in_shower`, the three 0.04 m rails (`grab_bar_24`, `grab_bar_36`, `handrail`), the three
sub-decimetre plates (`light_switch`, `outlet`, `thermostat`) and `threshold_ramp`. Anything stretched
into those reads worse than a labeled box.

## Real art now has the same shape as a placeholder

`BuildPlaceholderBox` has always returned a floor-pivoted `Item` root holding a `Box` and a `Label`,
while real art was the bare instantiated prefab whose **root** `PoseGO` scaled. Art now comes back in
the placeholder's shape (unscaled root, stretch on the `Art` child) and three things follow:

- **Everything is labeled**, not just boxes. A label parented to a scaled root would have its glyphs
  stretched by the fit; on an unscaled root it cannot be.
- **`FitCollider` replaces `AddFittedCollider` and runs on every re-pose, not once at spawn.** The old
  order added the collider and *then* let `PoseGO` write a scale onto the same transform, so a resized
  item with art got its pick box multiplied twice: a bed widened a fifth got one 44% too wide. It was
  dormant only because no catalog id resolved to art.
- **`PoseMount` applies the fit too.** It is the identity today (a mount's size is always the catalog
  size and its wrapper is baked to exactly that), and it runs anyway so a mount that ever gains a size
  override follows it instead of silently rendering wrong.

`PoseGO` therefore has three branches: `Box` → placeholder, `CatalogArtFit` → fitted wrapper, neither →
the original root-scale path, kept so registering a raw prefab by hand still behaves as it always did.

One thing the bake works around rather than fixes: `MountPose` puts the origin **on** the wall face and
the placeholder box straddles it, so half an item's depth is inside the wall. Invisible on a 0.09 m grab
bar, 165 mm on a 0.33 m wall cabinet. Hence `PivotZ.Back` on that row. The proper fix is to push the
placeholder, the ghost and `MountPose` out by half the depth together.

## The registry was split: `ResidenceCatalogRegistry.asset`

`Assets/Resources/PrefabRegistry.asset` is **not** touched, and must not be: besides `WorldRenderer`,
`EditController` renders its `entries` as the Site tool's **Place → Objects** thumbnail grid and its
**Paint Objects** brush list. Twenty-one interior rows there would put a sofa and a toilet in the site
editor's object palette. `ResidenceRenderer.prefabRegistry` in `ResidenceViz.unity` points at
`Assets/Resources/ResidenceCatalogRegistry.asset` instead, which the binder owns outright, which is also
what makes regeneration a safe wholesale rewrite.

Adding art for one of the remaining 14 is a row in `Rows` plus a regenerate. Nothing about the schema
or the data changes, because instances only ever store the key.


## Make your own: items the catalog does not ship

The catalog is a fixed 35. Every real residence has something outside it, and the only recourse was to
place the nearest item and resize it, which leaves the plan asserting that a chest freezer is a
wardrobe. The tool's whole argument rests on dimensional honesty, so an item that is the right size
under the wrong name undermines exactly the thing it is there to support.

A name and three dimensions is the entire schema. That is enough for the question being asked, which is
whether the wheelchair gets past it, and it is deliberately less than `FurnitureCatalog.Entry` carries:
no color, no clearances, no wall mounting. A wall mount needs a second set of decor rules that a name
and three numbers cannot describe, so custom items are floor-standing and the picker never offers
otherwise.

**The definition lives on `ResidenceDoc`**, beside `underlays` and for the same reason: what the
household owns is a record of the dwelling, not a design option, so every variant offers the same list
and one item can stand in both Existing and a proposal for the comparison to be about the room rather
than the furniture. It rides inside the `.riv` because the export is the whole document. `Migrate`
backfills the empty list; nothing else was needed, because the whole `ResidenceDoc` is already the undo
unit, so one `RecordEdit` covers a list that sits outside every variant.

### Why the id is a slug of the name

`ObjectInstance.prefabType` is the only durable link from something standing in a room back to what it
is, and **two readers of it cannot reach the definition list**. `VariantDiff` holds two `VariantDef`s
and no document, by design, so it can never look one up; and the renderer has nothing to look up at all
once a definition has been deleted. Both label furniture from the key alone.

So the key is `custom:` plus a slug of the name. `custom:reading_chair` reads back as "Reading chair"
in a Compare row, in the HTML report, and on the floating label over a box whose definition is gone. A
guid would have been the obvious choice and would have put a hex string in a document a family reads.
Names are never editable, which is what keeps the slug from going stale, and that is the actual reason
editing was left out rather than a scoping decision: an editable name means either a key that lies or a
migration over every placement that stores it.

The prefix is the other half. No catalog id and no `PrefabRegistry` key contains a colon, so
`FindPrefab` misses and `BuildPlaceholderBox` takes over, which is precisely the intended path: a custom
item is a labeled box at its true size, the same treatment the 14 deliberate placeholders get.
`CustomItemsTests.NoCatalogIdLooksCustom` pins the separation, because a catalog id that started with
the prefix would resolve as a custom item and quietly lose its art.

### One lookup, and what deleting means

A custom item reaches the app as a **synthesized `FurnitureCatalog.Entry`**. `Entry` is the currency of
the whole furnish path (`NewInstance`, `FurnitureFit`, the ghost, the tile painter, the Select rail), so
synthesizing one meant `FurnitureTool.Place`, `FitFloor`, `DrawOverlay` and `Report` needed no changes
at all: there is no second placement stack to keep in step with the first.

The cost is that `Catalog.Get` is no longer the right question. It finds the 35 shipped items and
reports a custom one as unknown, which reads downstream as a nameless box with no size and no
**Reset to catalog size**. `ResidenceRenderer.EntryFor` is now the single answer to "what is this
thing", and every call site in the renderer, the controller and `SelectTool` goes through it.

**Deleting drops the definition and leaves every placement standing.** Each instance carries its own
`boxSizeMeters` and its own name inside its key, so nothing moves, resizes or goes nameless; the item
simply stops being one you can place again. That made null a legitimate return from `EntryFor` rather
than an error, and the call sites already handled it: `SelectTool` hides **Reset to catalog size** when
the entry is null, which is exactly the right behavior with no code written for it. The alternative,
refusing to delete anything in use, would have made the list unclearable in the one case where you most
want to clear it, which is after a mistake.

### The rail

The chip is synthetic, drawn by hand after the loop over `Categories()`, the same shape `SensorTool`'s
Fixtures chip uses: the catalog asset has no such row and should not gain one. The form sits above the
grid so it reads as the thing that fills the shelf under it, and delete lives in the Selected block
rather than on a 76 px tile, where a ✕ is never more than a misclick from the thing it sits on.

Category switching is deferred through `Tick` alongside the add and the delete, because picking the chip
adds the whole form to the panel, and a control count that changes between the layout and repaint passes
is the `Mismatched LayoutGroup` the `_pending*` flags exist to prevent. The footprint tile cache gained
the item's dimensions in its key: a catalog id names one footprint forever, but a custom item can be
deleted and remade under the same slug at a different size, and an id-only key hands that one the old
item's picture.

## The everyday-furniture pass: thirty-five items to a hundred and seven

The catalog shipped 35 items, 21 of them with art. That was enough to argue about a doorway and not
enough to draw a home. There was no dining chair, no desk, no bookcase, no washing machine, no shower
stall, so a family looking at their own living room saw a sofa, an armchair, a coffee table and a grey
box. Meanwhile the project already owned 578 furniture prefabs across two packs and used 23 of them.

The pass added 72 ids in four new categories (`dining`, `office`, `laundry`, `storage`) and bound 58
new donors, taking the catalog to 107 items, 79 with art. Three things were learned doing it, and all
three are now rules.

### Squash ranks fit, and fit is blind to what a thing is

`MeasureFamily` had always ranked donors by squash, and the header already recorded two traps caught by
hand: a `GasStove` offered for `range` (it is a cooktop) and an extractor hood offered for `island`. At
sixty targets those stop being anecdotes and become the normal case. Ranked without constraint, the
sweep proposed a wash basin as a shower stall, a bathroom vanity as a toilet, an extractor hood as a
base cabinet, a refrigerator as a utility sink and a low table as an ottoman. Every one of those fits
beautifully. Every one is the wrong object.

So a target now names the donor **family** it may draw from, by prefab-name prefix, and squash chooses
only within it. That is the whole fix, and it is cheap: the families are already spelled out in the
pack's filenames.

It also forced the pass to be honest about where there is simply no donor. Fourteen new ids ship as
labeled boxes because the best fit in the right family is a lie: a walk-in tub is tall and short and
every `BathTub` is long and low (2.46 at best); an overbed table is tall and thin and the best of all
fifty `Table`s is 2.05; a media unit and a bed bench are long and low where every `Drawer` is chunky.
An ottoman is the sharpest case, because the *wrong* family fits it beautifully: a low square table
lands at 1.07 against the only plausible cushion's 1.71. It takes the box.

### The half turn is free. The quarter turn is the trap

The header already explained why every row carries a half turn: both packs are Blender exports facing
minus Z, `rotationY = 0` looks down plus Z, so a donor dropped in unturned stands with its back to the
room. What it did not say is that the half turn is *free* and the quarter turn is not.

A footprint is identical under 180 degrees, so adding it cannot change which donor ranks best. A
quarter turn **swaps** the footprint, so it wins the ranking whenever the donor is deeper than it is
wide and the target is wider than it is deep, and it buys that better fit by standing the item
**sideways**. For a round table, a square corner unit or a cushion that costs nothing. For anything
with a front it is the sofa-against-the-wall bug again, one quarter turn along, and squash cannot see
it any more than it could see the half turn.

`armchair` had shipped that way and nobody had caught it: yaw 270 on a 0.85 x 0.85 target, where
swapping the footprint could not improve the fit at all, so the quarter turn bought literally nothing
and simply turned the chair to face the side wall. It is 180 now. Six of this pass's own rows had the
same fault and were re-aimed, each re-picking its best donor at the unturned footprint and accepting
the worse squash, because facing beats fit.

The rule that came out of it: a seat, a bed, an appliance or a case good carries 180 unless the
**donor** is authored rotated, which the Mega Pack's kitchen cabinets are. `base_cabinet` has shipped
at 270 since the first pass and its door does face the room, which is why the kitchen rows keep theirs.

The check that settles it is a **Top view against a known-good item**: `sofa` is correct, and in a top
view its backrest sits at the top of the frame. Anything whose back is not at the top is turned wrong.
That reads in one screenshot per batch, where a front view of the same batch does not, because a
perspective camera shows off-center items their own sides.

### The mirror stopped being something a person types

`SampleFurniture` duplicates every id and dimension because `CXRAuthoring` has no references and cannot
read a ScriptableObject living in `Assembly-CSharp`. At 35 rows, hand-transcribing it and letting
`SampleResidenceInstaller.VerifyAgainstCatalog` warn at seed time was survivable. At 107 it is not, and
the failure is silent and severe: `SampleFurniture.Get` never throws, it returns `Unknown` at
0.6 x 0.6 x 0.8, so an id missing from the mirror renders and *measures* at a size nobody chose. That
is a wrong answer to the only question the catalog exists to answer. Worse, the seed-time check walks
the mirror and looks each id up in the catalog, so a catalog id absent from the mirror, which is the
common direction of the mistake, was the one case it could not see.

So the mirror joined the wrapper prefabs as a derived artifact of the same source, generated by the
same window, between sentinels that leave the hand-written header, the `Item` struct, the `Floor` and
`Wall` helpers, `Unknown` and `FootprintXZ` untouched. `FurnitureCatalogMirrorTests` then replaces the
warning with a real gate, and checks the **bijection in both directions** plus every mirrored number,
including `decorWidthFrac` and `decorHeightFrac`, which nothing had ever compared and which
`PlanBuilder` writes straight into every `WallMountDef` it authors.

The test reads the asset as **text**. `EditModeTests` cannot name `FurnitureCatalog`, because no asmdef
can reference `Assembly-CSharp`, but it does not have to: the asset is plain YAML with a flat,
fixed-shape entry list and `File.ReadAllText` needs no reference at all. That one observation is what
made the gate possible with no new machinery.

Two things the gate caught immediately, both pre-existing and both correct as they stand: `grab_bar_24`
is 0.04 m thick and `threshold_ramp` 0.03 m, which is under `MIN_ITEM_SIZE`. The bars are wall mounts,
which get no resize gizmo at all, and a threshold ramp exists to be 30 mm. Both are exempted by name in
the test rather than rounded up, because rounding them up would put a lie in the one number this
catalog exists to get right.

### Three smaller decisions

**`MountType.Counter` was avoided rather than implemented.** It exists in the enum and nothing reads
it: `IsWallMounted` is `mount == Wall` alone, so a `Counter` entry silently behaves as a floor item and
a microwave marked `Counter` sits on the floor. Making it real means a host relation between items, a
new placement path, a field on `ObjectInstance`, a migration, and new cases in `VariantDiff`,
`VariantRevert` and the sketch schema. That is a feature, not a catalog expansion. The Wall path
already does everything a counter-top item needs, because `mountHeightM` is the item's **center**: a
0.30 m microwave at 1.06 sits exactly on a 0.91 m counter. A counter-mounted sink and a cooktop were
dropped instead, being duplicates of `sink_base` and `range`.

**The clearance fields were filled on all 107 rows.** `ClearanceRules.Registry` is still empty by
decision, so nothing reads them. They were filled anyway because a zero is indistinguishable from an
unauthored row, which is precisely the state all 35 original entries had been sitting in, and because
the numbers are a property of the object rather than of any rule that might later consume them.

**The role sets stayed hand-written, and gained an eighth member nobody had listed.** `OccupancyModel`,
`SensorPackages` and `SensorFit` keep eight sets keyed on catalog ids, and deriving them from
`category` would be wrong in both directions: a `dining_chair` is `dining` and is sat on, a
`wall_shelf` is `storage` and is not. Two real faults surfaced while extending them:
`SensorFit.Surfaces` had never included `vanity`, and `SensorPackages` reached for the stove with a
bare `"range"` literal, which would have shipped a care package with no stove sensor the moment a plan
used any other cooking appliance. It reads `SensorFit.Cooktops` now, so what counts as a stove is
settled in one place.
