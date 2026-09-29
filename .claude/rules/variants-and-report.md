---
paths:
  - "Assets/Scripts/Authoring/Interior/VariantDiff.cs"
  - "Assets/Scripts/Authoring/Interior/VariantRevert.cs"
  - "Assets/Scripts/Authoring/Interior/VariantSync.cs"
  - "Assets/Scripts/ResidenceViz/Report/**"
  - "Assets/Scripts/ResidenceViz/Tools/CompareTool.cs"
  - "Assets/Tests/EditMode/VariantDiffTests.cs"
  - "Assets/Tests/EditMode/VariantSyncTests.cs"
---

# Variants. Compare, revert, ghost, report

> Loaded when a file under the paths above is read. Rules only: the reasoning is in the design note linked at the end. Edit this file when a rule changes; update CLAUDE.md only if something every session needs moves.

## Variants: compare, revert, ghost, report

- **`VariantDiff.Compare` takes any two variants.** `Change` carries `kind`, `id`, `worldPos`,
  `levelId`/`levelIndex`. `NewProposalFrom` deep-copies **preserving every element id**, so moving
  something reports as one `Modified`, not remove + add. It compares `boxSizeMeters`; `DetailWriter`
  is a struct and must be passed by `ref` (a day-only change was once reported as nothing).
- **`VariantRevert` is the exact inverse of `VariantDiff`: revert every change in a diff and the
  diff comes back empty**, for any edit, on every storey, on all six samples. Ids are preserved on
  every path; deep copies are written by hand (no shared `float[]`); the one refusal is restoring an
  opening or mount onto a wall the proposal removed (restore the wall first); reverting an added wall
  cascades to its openings, mounts and the sensors on those openings, mirroring
  `SelectTool.DeleteSelected`.
- **`VariantSync` carries every baseline edit into every proposal**, from
  `ResidenceEditController.MarkDirty` (synchronous, never a deferred flag: `SetActiveVariant`,
  `RecordBefore` and `SaveResidence` all read the proposals right after an edit). The controller holds
  a **shadow** of the baseline (`VariantSync.Snapshot`, hand copies via `RevertAll`);
  `Compare(shadow, baseline)` is what the edit did and each change goes through
  `VariantRevert.Revert(baseline, proposal, change)` with the **new** baseline as the reference. A
  proposal's own change to the same element (a key of `Compare(shadow, proposal)`) **wins** and stays
  in its list; dependents follow a removed host; a refusal is skipped and shows honestly. **Storeys pair
  by id only** (a storey missing from the shadow reads as empty). `RoomRegions.Sync` runs on a
  proposal storey only after a wall was added, removed or moved, never on a thickness or height edit.
  **The sync sees exactly what the diff sees**: a field `VariantDiff` ignores neither travels nor
  reports. The shadow is reset in `AfterOpen` and undo's `Restore`, refreshed whenever the baseline
  moved, and null while the residence has no proposal.
- **`CompareTool`** rows are grouped by room; click selects + focuses with `reveal: false`; ✕ reverts
  undoably; `VariantDef.description` is edited here and heads the report.
- **The ghost** (`ResidenceRenderer._ghostVariantId`/`_ghostOn`) is re-applied last in every `Rebuild`
  and after the targeted rebuilds. Red from the *other* variant where things **were**, green from
  this one where they **are**; `Modified` feeds both halves, gated by `GeometryDiffers`. **Two**
  translucent materials (`_Surface`, blend pair, **ZWrite off**), ghost meshes in `_ghostMeshes`,
  **no colliders**. The diff is against the variant being **rendered** (Compare switches the view to
  its After when the chip goes on). **On by default in Review**: `CompareTool.Enter` brings the view
  to After and turns it on; `ResidenceEditController.SetStage` turns it off on leaving the Review stage
  (Compare ↔ Measure keeps it); the toggle in the rail still overrides either way. Openings are
  deliberately absent from the ghost.
- **The report** (`Assets/Scripts/ResidenceViz/Report/`): self-contained HTML with a print stylesheet
  (`@page` is the PDF half); `ReportDoc` is the model. `ReportCapture`: a **hidden camera, never
  `ScreenCapture`**; **never from `OnGUI`**; **one rebuild per (variant, storey)**, every framing taken
  from it; **framed over the union of both variants' bounds**; occupants, ghost, selection and sensor
  states hidden/frozen and restored afterwards; **JPEG**. `ReportBuilder` supplies description,
  counted summary and before/after metrics (no turning circles). The **Technology section carries no
  photographs** and must stay last: `ReportCapture.Framings` is shorter than `report.sections` and
  the pairing loop stops at the shorter.

→ [`docs/design/variants-and-report.md`](../../docs/design/variants-and-report.md)
