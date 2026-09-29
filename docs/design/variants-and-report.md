# Design notes. Variants: compare, revert, ghost and the report

> `CompareTool`, `VariantRevert` (the exact inverse of `VariantDiff`), the two things the diff was
> not reporting, the sticky two-way ghost, and the before/after HTML report. The rules are summarised
> in [`.claude/rules/variants-and-report.md`](../../.claude/rules/variants-and-report.md); the reasoning lives here.

## The change list is a place, not a paragraph: `CompareTool`

`VariantDiff.Change` has always carried `kind`, `id` and a `worldPos` anchor. The old panel rendered
each one as `"• " + c`, which calls `ToString()` and throws all three away, so the list told you a
bathroom door had been widened and gave you no way to find that door, and no way to change your mind
about it short of hunting it down by hand or undoing back past every good change made since.

A **tool** rather than a foldout, because comparing needs all three of `IResidenceTool`'s surfaces and a
foldout has none: a rail for the list, `DrawOverlay` for the markers that put each change where it
happens, and `HandleInput` for clicking those markers. Rows are grouped by the room the change falls
in: the unit a resident thinks in, and the unit the report is sectioned by: with a trailing
"Elsewhere" for changes that have no position (occupants, the outdoor layer).

- **Any two variants**, not just baseline-vs-active. `VariantDiff.Compare` always took arbitrary
  variants; the old panel simply never asked.
- **Clicking a row selects and focuses, with `reveal: false`.** The default carries you to the Select
  tab, which is right when you click a chair while tracing a sketch and wrong here. It would eject
  you from Compare on every row you clicked.
- **`VariantDef.description` is editable here.** The field existed and was only ever auto-set to
  `"Based on X"`. It is the one piece of prose in the rail and it is the *user's*, like a person's
  note or the change list itself, and it heads the report.

### Taking one change back out: `VariantRevert`

`VariantDiff` answers "what is different"; `VariantRevert` makes one difference go away. **It is the exact inverse and must stay that way**: revert every change in a diff and the diff must come
back empty, for any edit and on all six samples. That property is the specification, and it is what
catches the realistic failure (a kind nobody handled) which no per-field check would see.

It follows the `OpeningFit` convention. Slide where possible, refuse only where nothing is legal,
return a `reason` written to be shown verbatim. There is exactly one refusal and it is real:
restoring an opening or a mount onto a wall the proposal has since removed would write a `wallId`
that resolves to nothing, which `WallLayout` clamps and `ResidenceRenderer` skips *silently*. Reverting an
**added** wall cascades to its openings and mounts, mirroring `SelectTool.DeleteSelected` so the two
cannot disagree about what removing a wall means.

**Ids are preserved on every path.** A revert that minted a fresh id would leave this comparison empty
and report "door removed, door added" on the next one. That is also why the deep copies are written
out by hand: `CXRAuthoring` cannot reach `ResidenceStore.Clone` (`Assembly-CSharp`, which asmdefs cannot
reference), and "keep the id, copy everything else" should be readable rather than implied by a
serializer. A shared `float[]` is the failure mode: two variants pointing at one array, so moving a
wall in the proposal moves it in the baseline too, and there is a test for exactly that.

It must also address the **level** the change was reported from: on a two-storey residence the same shape of
change on each floor reverts on its own floor, never on `levels[0]`.

### Two things `VariantDiff` was quietly not reporting

Both found by the revert property, because a change it cannot see is a change it cannot revert:

- **`DetailWriter` is a struct and was passed to `CompareDay` by value.** Its `StringBuilder` is
  allocated lazily, so when the *only* thing that changed about a person was their day,
  `_sb ??= new StringBuilder()` assigned to the copy and the caller still saw `Any == false`. Moving
  someone to a different bedroom: the example `VariantDiff`'s own header leads with, and the one
  `OccupantDef` hangs off `VariantDef` to make possible. Reported **nothing at all**, unless it
  happened to come with a rename. Now `ref`.
- **`boxSizeMeters` was not compared.** The transform handles exist to resize furniture in real units,
  so a proposal could widen a bed and say nothing.

### The ghost is sticky now, and works both ways

`SetGhostVariant` ghosted **only removed walls**; `addedIds` was computed and never used and the
serialized `ghostAdded` colour was dead. Worse, `ClearRendered` clears `_ghostRoot` and every
`Rebuild` calls `ClearRendered`, so any edit, undo or variant switch silently dropped the overlay,
which is why the UI had to offer "Show ghost" and "Hide ghost" as two separate buttons rather than one
toggle that would have gone on lying about its own state within a click.

The state is `_ghostVariantId` / `_ghostOn` on the renderer and `Rebuild` re-applies it last. Walls,
rooms, furniture and mounts ghost red from the *other* variant's level where they **were**, and green
from this one's where they **are**. The green half is ghosted rather than tinted in place because
tinting means holding every touched renderer's original materials and restoring them, and getting that
wrong leaves a residence permanently green. Building both halves into `_ghostRoot` keeps
`ClearGroup(_ghostRoot)` the one and only teardown. Ghosts get **no colliders**: a picture of something
that is not there must never be walked into, nor win a raycast in front of the real element it
describes. `MountPose` was lifted out of `PoseMount` so a removed grab bar can be placed from the level
that still has its wall.

#### …and it draws `Modified`, which is the only kind a proposal usually has

The first version collected `Added` and `Removed` and dropped everything else, which meant that on a
realistic proposal the overlay drew **nothing at all**, chip lit, no warning. `NewProposalFrom` deep
copies *preserving every element id*, on purpose, precisely so that moving a wall or a dresser reports
as one modification rather than a delete plus an add; so `Added`/`Removed` between a baseline and its
proposal describe almost none of what that proposal does. `Modified` now feeds **both** halves.

Three things this needs that the added/removed-only version did not:

- **`GeometryDiffers` gates it.** A rename, a type change or a note is a modification with
  identical geometry, and ghosting one stacks a red and a green copy of the same floor on the same
  plane. It compares only what the four ghost builders actually draw from.
- **The material is translucent, and it was not.** `ghostMaterial` is unassigned in `ResidenceViz.unity`, so
  the fallback runs, and URP/Lit defaults to **opaque**, so the `a: 0.35` in `ghostAdded` /
  `ghostRemoved` was simply ignored and every ghost rendered as a solid box. It is now configured the
  way `UnderlayTool` and `TileBuildingEditor` already do it here: `_Surface`, the blend pair, and
  **`ZWrite` off** in the transparent queue: that last one because a green ghost is the *same mesh at
  the same transform* as the element it tints, and with depth writes on the two fight for the pixel.
- **Two materials, not one per element.** `GhostMaterial` used to `new Material(...)` per ghost and
  destroy none of them, so every rebuild while the overlay was on leaked one per wall, room and item.
  Ghost meshes get their own `_ghostMeshes` list for the same reason: `BuildGhost` runs without a full
  teardown, so `_ownedMeshes` only ever freed them on the next `Rebuild`.

**The diff is against the variant being RENDERED**, not against whatever pair the rail is describing,
which is why `CompareTool` switches the view to its After when the chip goes on. Without that, opening
Review from the base environment (the common case: the After picker is not even drawn until there are
two proposals) lit the chip while `BuildGhost` returned on `other == _variant`. The targeted rebuilds
end in `BuildGhost` for the matching reason: the overlay is a picture of a diff, so an edit that skips
it leaves the ghost describing the residence as it was before that edit.

**Openings are still deliberately absent, and the cost is real.** The old note claimed a changed door
"reads as the changed wall ghost around it". It does not. Widening a door leaves its host wall's own
fields untouched, so `VariantDiff` reports no wall change and there is no ghost to read it in. A
proposal that only widens doorways and drops thresholds therefore draws nothing here; the change list,
the markers and the report all still carry it.

## The before/after report: `Assets/Scripts/ResidenceViz/Report/`

The output of a meeting is a decision, and there was no artifact to carry it: `Export` writes a
`.riv` zip only this app can open. One button now produces a shareable document: the plan, a 3/4
overview, and every changed room, each photographed twice from one camera pose.

**Self-contained HTML with a print stylesheet.** There is no PDF library here and adding one is a real
dependency for something that runs once per meeting; a single `.html` with the images base64'd into it
needs nothing, opens anywhere, emails as one attachment, and prints to a real PDF through the
browser's own Save-as-PDF. The `@page` block is therefore not a nicety, it is the PDF half of the
deliverable. `ReportDoc` exists so that a real PDF writer, when it comes, is a second renderer over
the model rather than a second pass over the `ResidenceDoc`.

`ReportCapture` is the delicate file, and each of its four rules has already caught this codebase out:

- **A hidden camera, not `ScreenCapture`.** The whole UI is IMGUI, so a backbuffer grab photographs
  the rails, the timeline, the selection halos and the readout chips.
- **Never from `OnGUI`.** Rendering a camera swaps the active render target and blanks the entire
  IMGUI pass: the warning `ThumbnailCache` carries at its head and the reason its jobs are queued.
- **Two rebuilds, not two per shot.** Each variant is rendered once and every framing taken from it.
  Beyond being far faster, it is what guarantees a pair shares a camera pose *exactly*.
- **Framed over the union of both variants' bounds.** A shot framed on the proposal alone crops
  whatever the baseline had and the proposal removed: precisely what the reader is looking for.

Occupants are hidden for the duration (`SetOccupantsVisible`, the same shape as `SetCeilingsVisible`
and for the same reason the markers have their own root): a capsule standing in the bathroom in the
"before" and beside the bed in the "after" is a difference a reader will try to read as part of the
proposal, and it is only the clock having moved. The ghost is switched off too, for the same reason.
Everything (variant, ceilings, occupants, ghost, selection) is restored afterwards; a report is a
read, and a read must not leave the editor somewhere else.

**JPEG, not PNG**, and that is load-bearing rather than cosmetic: a 1600×1000 PNG of shaded geometry
runs 300-800 KB and a six-room report holds sixteen images, so a PNG report is a ~15 MB file nobody
can email. It is also what a PDF writer wants. PDF embeds JPEG bytes verbatim through `DCTDecode`.

`ReportBuilder` supplies the two halves of the writeup: the author's `description` and a counted
summary ("Three doorways widened. One threshold removed."). Plus the **metrics**, which are the part
that existed nowhere before. Every number was already computable and already computed, one element at
a time, in the inspector, for whatever happened to be selected. A report is the first thing that asks
all of them at once and puts before beside after, which is the form the accessibility argument
actually takes: not "this door is 36 inches" but "this door was 32 and is now 36".

**The Technology section carries no photographs, and that is deliberate.** A proposal that installs
a smart home package touches every room, and a pair of shots differing by a 70 mm grey box on a
ceiling is sixteen images nobody can read a difference in, while making the report slower to produce
and far larger to email. So sensor changes are excluded from `ChangedRooms` and from the plan
section's change list, and the last section carries them in the form that answers something instead:
the device list grouped and counted, the total, coverage before and after, ways out watched before and
after, and **the scenarios this package would have caught**, taken from `SensorSim`'s demonstration
day as distinct *kinds* rather than a count. It also lists what the package still does **not** cover,
because a proposal that reports only its strengths is an advertisement, and the one thing a care team
must not discover later is the back door nobody watched.

`ReportCapture.Framings` therefore returns a shorter list than `report.sections`, and the pairing loop
stops at whichever runs out first, so **any future unphotographed section must also go at the end**,
or the two lists slip by one and every caption lies.

**Turning circles are not among them.** `RoomMetrics` had a "Largest turning circle" row and
`WholeResidenceMetrics` a "Rooms with a 5' turning circle" count; both came out with the rest of the
turning system, along with the `TurningCircle` const and `RoomsThatTurn`. The reason is not only
consistency: `LargestInscribedCircle` is computed on the **bare** room, so every figure those rows
printed described a room emptied of its furniture: a claim a reader takes at face value and is wrong
about. What remains is floor area, the narrowest way into each room, its threshold, and the two
whole-residence counts.

## The ghost is on when Review opens

The overlay is the picture of what the change list says, and Review is where you go to see it, so
`CompareTool.Enter` runs the same two lines the toggle runs (view to After, ghost on) and the rail
opens already showing red and green. It stays sticky within the stage (Compare ↔ Measure), and
`ResidenceEditController.SetStage` turns it off on the way out, because drawing walls under a red-and-green
overlay of where they used to be is not editing, it is guessing. The toggle still wins either way.
Proposals are named `Proposal MM/DD/YYYY` now, with the residence's proposal ordinal in front of the date
from the second onwards. Letters walked past Z, and deleting "B" minted a second "B".

## Base edits reach every proposal: `VariantSync`

A proposal is a full copy of the baseline, ids preserved, and nothing ever revisited that copy. So
the day a proposal existed, every later edit to the base environment (a chair added, a wall drawn, a
door removed) landed in the base alone, and `Compare(baseline, proposal)` said the proposal had
**removed** it. True of the data, false of what happened. The report said the same. The base
environment is the record of how the residence *is*; a change to that record is a fact every
proposal inherits, silently, because the proposal did not do it.

**A shadow diff, not per-tool hooks.** Eleven tools mutate the level and each knows what it changed,
so the obvious fix was a "propagate" call in each. The one already-shared fact is better: every
mutation ends in `MarkDirty`. The controller keeps a private copy of the baseline as it last stood
(`VariantSync.Snapshot`), and `Compare(shadow, baseline)` after an edit is precisely the list of what
that edit did, in the same terms the change list itself uses.

**`VariantRevert` is the primitive.** `Revert(reference, proposal, change)` makes the proposal match
the reference for one element: present in the reference, copy it in (or replace); absent, remove it,
with the same cascades `SelectTool.DeleteSelected` runs (wall → openings, mounts, sensors; furniture →
sensors; room → `RoomRegions.RemoveRoom`; person → worn devices); and refuse where the result would
be a `wallId` or `hostId` resolving to nothing. Called with the *new* baseline as the reference, that
is propagation, case for case, including the refusals, which are exactly the cases where carrying
the change would leave a door that exists in the data and nowhere on screen. No new copy code, no
new cascade, and the "exact inverse" property that specifies `VariantRevert` now also guarantees
that after a base edit `Compare(baseline, proposal)` is what it was before it.

**The proposal's own edit wins**, per element. The keys of `Compare(shadow, proposal)` are what the
proposal has done on its own; a base change on one of those is left alone, and that difference stays
in the list where it belongs. The unit is the element, not the field: the proposal moved the chair,
the base resized it, the proposal keeps its chair. Dependents follow a removed host (the proposal's
grab bar on a wall the base removed goes with the wall), because that is what removing a wall means
everywhere else in this codebase.

**Synchronous, in `MarkDirty`.** A `_pending` flag drained next frame is the house pattern for
anything touching IMGUI's control count; this touches none, and the flag has three real holes.
`SetActiveVariant` is called straight from `OnGUI` and renders at once, so a proposal came up one
edit behind. `RecordBefore` snapshots the whole document before the *next* edit, so a pending
propagation would be captured un-applied and never applied after an undo. `SaveResidence` writes
whatever is there. Propagation is pure data work and safe from anywhere, so it runs in the one place
every edit already passes through. The undo argument comes free: the snapshot precedes the edit,
propagation happens inside the edit's own `MarkDirty`, so Ctrl+Z takes both back together, and
`Restore` resets the shadow to the restored baseline.

**Storeys pair by id only.** `VariantDiff.MatchLevel` falls back to position, which is right for a
diff between two whole variants and wrong here: after `Stories.Remove` of a middle storey, position
pairs two different floors and the "changes" it reports would be applied to the wrong one. The sync
therefore diffs one storey at a time, by the id `Stories.Add` gives a storey in every variant. A
storey missing from the shadow reads as empty, so a shadow one `Stories.Add` stale simply delivers
the first thing drawn there as an addition.

**Rooms travel by id, then `Sync` runs once.** The base's own `Sync` reshaped rooms and minted any new
`Untyped` one; those arrive in the proposal under the same ids first, so the change list stays empty.
Then, only on a proposal storey where a wall was added, removed or moved (never a thickness or height
edit, per the rooms rules), `RoomRegions.Sync` puts the polygons right for *that proposal's* walls.
Where they agree with the base it is a no-op; where the proposal had its own wall edits, whatever it
changes is the proposal's own honest difference.

**The sync sees exactly what the diff sees.** A field `VariantDiff` does not compare (a note, a marker
colour, a device's privacy class) neither travels nor shows in any list. That is what makes "a base
edit is absent from every proposal's report" true by construction rather than by effort, and it is
the line to hold: teach the diff a field and the sync learns it in the same commit.

**Residences saved before this** carry the drift already; there is no "before" to diff against and
nothing is repaired retroactively. The route is the one that was always there: each "Removed" row's
✕ in Compare copies the base's element into the proposal.

