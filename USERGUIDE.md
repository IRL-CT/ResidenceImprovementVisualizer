# Residence Improvement Visualizer. User guide

This guide assumes you have never opened the app and have never used design software. Every control
is named by its label and its position in the window before you are asked to click it. Terms are
explained the first time they appear, and all of them are collected again in the
[glossary](#20-glossary) at the end.

## Index

1. [What the app is for](#1-what-the-app-is-for)
2. [The window](#2-the-window)
3. [Saving](#3-saving)
4. [Quick start. A plan from sketch to report](#4-quick-start-a-plan-from-sketch-to-report)
5. [Residences](#5-residences)
6. [Variants and the status band](#6-variants-and-the-status-band)
7. [Undo and redo](#7-undo-and-redo)
8. [Import mode. Floors, plans and scale](#8-import-mode-floors-plans-and-scale)
9. [Structure mode. Walls, openings and rooms](#9-structure-mode-walls-openings-and-rooms)
10. [Select mode. Inspecting and changing what is there](#10-select-mode-inspecting-and-changing-what-is-there)
11. [Furnish mode](#11-furnish-mode)
12. [Smart living mode](#12-smart-living-mode)
13. [People mode](#13-people-mode)
14. [Review mode. Compare, measure, report](#14-review-mode-compare-measure-report)
15. [Moving a residence to another machine](#15-moving-a-residence-to-another-machine)
16. [Archiving, and getting a residence back](#16-archiving-and-getting-a-residence-back)
17. [What the app does not do](#17-what-the-app-does-not-do)
18. [Troubleshooting](#18-troubleshooting)
19. [Keyboard and mouse reference](#19-keyboard-and-mouse-reference)
20. [Glossary](#20-glossary)

---

## 1. What the app is for

The app draws a home the way it is now, and beside it the way somebody is proposing to change it.
Widening a doorway, taking out a threshold, putting up grab bars, moving a bed so a wheelchair can
turn beside it. You build both versions, then hold them next to each other so you can compare them.

It runs on Windows as a single program. No internet connection is needed, nothing is installed
alongside it, and no account is required. One button in the app sends data outward, and only when
you press it. That button is covered in full under [Read this plan](#86-read-this-plan-using-claude).

### Where your work is kept

Everything lives in one folder on your machine. To open it, press the Windows key and R together,
paste this line into the box, and press Enter.

```
%USERPROFILE%\AppData\LocalLow\IRL\Residence Improvement Visualizer\ResidenceImprovementVisualizer
```

Inside, you will find these.

| Folder or file | Holds |
|---|---|
| `residences\` | One file per home you have built |
| `residences\_archive\` | Homes you have archived. Nothing here is deleted |
| `underlays\` | The floor plan sketches you imported |
| `reports\` | The before and after documents you generated |
| `settings.json` | Your units preference and which home was open last |
| `anthropic.key` | Your API key, if you entered one |

You will need this path twice in this guide. Once to
[get an archived home back](#16-archiving-and-getting-a-residence-back), and once to
[find a report](#146-generate-report).

---

## 2. The window

The window is divided into six regions. Learn where they are once and the rest of this guide reads
as directions.

<!-- SCREENSHOT: the whole window with a residence open, every region visible -->

### The left rail

Down the left side is the library, which is the list of every home you have. It also holds the
buttons that act on the home you have open, such as Save and Export. The library never goes away,
so there is no button anywhere that takes you "back" to it.

The small `‹` at the top right of the rail folds it away and gives the plan more room. When folded,
a `☰` button appears in its place and opens it again.

### The command bar

Along the top of the window runs a list of words.

```
Select    Import    Structure    Furnish    Smart living    People    Review
```

Each word is a mode, and picking one changes what appears in the rail down the right side of the
window. Each mode is a stage of the work. The plan stays on show throughout, and you can switch
back and forth freely. Hold Ctrl and press 1 through 7 to jump between
them from the keyboard.

To the right of the modes sit four more controls, covered where they matter.

| Control | What it does | Covered in |
|---|---|---|
| Overview / Walkthrough | Switches between looking down at the plan and walking through it | [Moving the camera](#22-moving-the-camera) |
| Standing / Seated (wheelchair) | Eye height in the walkthrough | [Moving the camera](#22-moving-the-camera) |
| The floor chip | Which storey you are working on. Only appears when there are two or more | [Floors](#81-floors) |
| meters / ft / in | Which units everything is shown in | [Units](#23-units) |

Furthest right are Undo and Redo. See [section 7](#7-undo-and-redo).

### The status band

Directly under the command bar is a colored strip. It tells you which version of the design you are
editing and whether you are allowed to change it. It also carries the buttons for starting a new
version. This guide calls it the status band, and [section 6](#6-variants-and-the-status-band)
explains everything on it.

### The right rail

Down the right side is the rail, which holds the tools and settings for whichever mode you picked.
When a mode has more than one tool, small buttons along the top of the rail choose between them.
Press 1, 2 or 3 to pick a tool without reaching for the mouse.

### The scene

The middle of the window is the home itself, drawn in three dimensions. You click in here to place
things and to select them.

### The timeline bar

Along the bottom is a 24-hour strip with a clock. It runs the day forward and moves the residents
through the home as their schedules say they would. It is always there, and it grows taller when you
open [People mode](#13-people-mode). The `▲` at its right expands it, and `▼` collapses it again.

### 2.1 Status messages

Short messages appear near the bottom of the window when something happens, such as
`Plan imported. Set its scale next.` They fade on their own. If you miss one, the action still
happened; nothing waits on you reading it.

### 2.2 Moving the camera

There are two ways to look at the home, chosen by the Overview and Walkthrough buttons in the
command bar.

Overview is where you do the work. You are floating above the home looking down into it, and the
ceilings are hidden so you can see in.

| Gesture | What happens |
|---|---|
| Hold the right mouse button and move the mouse | Turn and look around |
| Hold the middle mouse button (the wheel) and move | Slide the view sideways and up |
| Roll the mouse wheel | Move closer or further away |
| W, A, S, D | Move forward, left, back, right |
| Q and E | Drop lower and rise higher |
| F | Bring the camera to whatever you have selected |

Tilt the view all the way down and you are looking at a flat plan of the home. There is no separate
plan view because this reaches it.

Walkthrough puts you inside the home at a person's eye height, with the ceilings drawn. Hold either
mouse button and move the mouse to look around, and use W, A, S and D to walk. Hold Shift to walk
faster. If you get wedged in a wall, press R and you are put back in a clear spot.

The Standing and Seated (wheelchair) button in the command bar sets your eye height in the
walkthrough. 

The button is greyed out in Overview because eye height means nothing there.

### 2.3 Units

The last chip in the command bar reads either `meters` or `ft / in`. Click it to swap. Every
measurement in the app changes at once, including the ones already on screen.

The same chip also decides how times are written. Metric gives you a 24-hour clock, so 6 in the
evening reads `18:00`. Feet and inches gives you a 12-hour clock, so the same moment reads
`6:00 PM`. One preference drives both.

Anywhere the app asks you to type a measurement, it will accept most ways of writing one.
`12' 6"`, `12'6"`, `36"`, `6 1/2"`, `3.8m` and `380cm` are all understood. A bare number with no
unit is read in whatever the chip is currently showing.

---

## 3. Saving

Nothing in this app saves itself. There is no autosave and no timer. What is on screen stays in
memory until you write it to disk, and the app tells you which of the two it is. Closing does ask
first, and that is the only prompt you get.

The Save button is in the left rail, under a heading that reads `This residence`. It reads `Save`
when everything is written to disk and `Save *` when it is not. The asterisk is the only warning you
get. Ctrl+S does the same thing from anywhere in the app.

<!-- SCREENSHOT: the left rail's This residence section, with Save * showing -->

Get into the habit of pressing Ctrl+S at the end of each stretch of work. The rest of this guide
will remind you at the end of each mode.

Some actions save for you as a side effect. Creating a home, adding a sample, importing a `.riv`
file, generating a report and exporting all write to disk first. Everything else waits for you.

Beside Save is `Save As`, which writes a separate copy under a new name and switches you to working
on that copy. The original is left exactly as it was. Use it before you try something you are not
sure about.

### 3.1 Closing the app

Press Esc to close the app. A card appears in the middle of the window and everything behind it goes
grey until you answer it.

| Button | What happens |
|---|---|
| `Save and exit` | Writes this home to disk, then closes the app |
| `Exit. Discards unsaved changes` | Closes the app and lets that work go. It reads just `Exit` when there is nothing unsaved |
| `Keep working` | The card goes away and you are back where you were |

<!-- SCREENSHOT: the closing card over a dimmed window, with Save * showing behind it -->

Esc does the same as `Keep working`, and Enter does the same as `Save and exit`, so you can answer
the card from the keyboard alone.

The window's X button and Alt+F4 bring up the same card. If the save fails, the message appears over
the card and the app stays open, so nothing is lost.

Esc has three other jobs before this one. It stops whatever the tool is in the middle of, then
clears your selection, then takes you back to the mode you came from. Only when there is nothing
left to undo does it offer to close. So you can press Esc freely while you work without ever meeting
this card by accident. See [section 19](#19-keyboard-and-mouse-reference).

---

## 4. Quick start. A plan from sketch to report

This is the whole job start to finish. It assumes you have a photo, scan or PDF of the home's floor
plan. If you do not have one, skip to [section 5](#5-residences), open one of the six sample homes
instead, and start reading from [section 6](#6-variants-and-the-status-band).

Allow about an hour the first time.

### Step 1. Make a home to work in

In the left rail, press `New residence`. A home appears in the list with one small square room in
it, and the app puts you in Structure mode. Give it a real name now, in the `Name` field further
down the left rail, because that name becomes the report's title and the exported filename.

### Step 2. Bring in the floor plan

Click `Import` in the command bar. At the top of the right rail is a section headed `Floors`,
listing the one floor you have. Press `Import plan…` on its row.

A file chooser opens, titled `Select a floor plan`. Pick your image or PDF and press `Import`. PNG,
JPG and PDF all work.

The sketch appears on the floor of the scene. The message tells you what to do next.

### Step 3. Set the scale

The app has no idea how big your sketch is until you tell it. Until you do, everything you trace is
meaningless.

Find something on the sketch whose real length you know. A doorway, an exterior wall, a printed
dimension, a gridline on graph paper. Then press `Set scale…` in the right rail and follow the three
steps it walks you through.

1. Click one end of that thing on the sketch.
2. Click the other end.
3. Type the real distance into the `Distance` box and press `Apply`.

The sketch resizes around your two points. The button now reads something like `Scale · 11.4 m`,
which is how wide the whole image is in real life. If that number is wildly wrong, press the button
and measure again.

### Step 4. Trace the walls

Click `Structure` in the command bar. The rail offers three tools, and `Walls` is the first.

Click once at a corner of the home, then again at the next corner, and keep going around. Each
segment appears as you complete it. Press Enter when the run is finished, or Esc to throw away a run
you have started badly.

The corners pull themselves onto sensible positions as you go, squaring up to walls you have already
drawn and meeting the ends of existing ones. That is what you want most of the time. Hold Shift
while clicking to place a corner exactly where the cursor is with no help at all.

When a run of walls closes off an area, that area becomes a room by itself. You do not draw rooms.

### Step 5. Put the doors and windows in

In the same mode, click `Openings`. Pick `Door`, `Window` or `Cased opening` at the top of the rail,
set the width, then hover over a wall and click where it goes.

While you hover, the app shows you the clear passage, which is the width a wheelchair actually has
to fit through once the door and its stop are in the way. It is always narrower than the hole in the
wall. If the preview turns red, the opening will not fit where you are pointing, and the message
says why.

For each door, set `Threshold` to the height of the lip somebody has to cross. Leave it at zero if
there is nothing to cross, and the app marks that doorway `Step-free`.

### Step 6. Say what each room is

Click `Rooms`. Twelve room types are listed down the rail. Click `Bedroom`, then click inside every
bedroom in the plan. Click `Bathroom`, click the bathrooms. Work through the whole home.

The type sets the floor color, so a typed plan reads at a glance. It also groups the change list
and the report later on.

### Step 7. Furnish it

Click `Furnish`. Search the catalog or browse it by category, click an item, then click where it
goes. Items settle against a wall when you place them near one, and tuck into a corner when you
place them near two. Press R to turn an item a quarter turn before you put it down.

Put in the furniture that matters to the question being asked. A bed, a chair, the kitchen counters,
the things somebody has to get past. You do not need to model every ornament.

### Step 8. Save, then propose a change

Press Ctrl+S or the Save button,

What you have built so far is the home as it stands. The app calls this the base environment, and it
is locked so nobody edits the record of the home by accident.

Look at the status band under the command bar. Press `New proposal`. The band turns a different
color and names your new proposal. Everything you change from here is a proposal, and the record of
the home as it stands is safe underneath it.

### Step 9. Make the change

Make the requested change, (e.g., widenning the bathroom door, putting a grab bar beside the toilet, installing a new sensor by the sink).

To widen a door, click `Select` in the command bar and click the wall the door is in. The rail lists
the openings in that wall. Click the door in the list, change its `Width`, and if it has a
threshold, press `Make step-free`.

### Step 10. Look at what you changed

Click `Review`. It opens on Compare.

Every difference between your proposal and the home as it stands is listed, grouped by room. Click
any line and the camera brings you to it. If you made a mistake, the small
`✕` at the end of its line takes that one change back out and leaves the rest alone.

The plan now shows red where things were and green where they are now.

### Step 11. Write the report

Still in Compare, write a sentence or two in the box headed `What this proposal does`. 

Press `Generate report`. The app takes its own before and after photographs, writes an HTML file,
and opens it in your web browser. To turn it into a PDF, press Ctrl+P in the browser and choose
`Save as PDF` as the destination.

Press Ctrl+S one more time.

---

## 5. Residences

A residence is one home. It is the thing that gets saved as a file, listed in the library, exported,
and reported on. Everything else in the app lives inside one.

<!-- SCREENSHOT: the left rail library list with several residences -->

### 5.1 The library

The list down the left rail is every home you have. Each row shows its name, and under that a line
like `v7 · 3 variants`, which is how many times it has been saved and how many versions of the
design it holds. Click a row to open it. There is no separate Open button.

Starred homes sort to the top. Everything else sorts by how recently it was saved.

### 5.2 Starting a new home

`New residence` in the left rail makes an empty home with one plain square room in it, and drops you
into Structure mode. The room is there so you have something to build against; delete its walls or
build over them as you like.

A brand new home is the one case where the base environment is not locked, because there is no
record of an existing home to protect yet. Once you have traced the home as it stands, start a
proposal before you change anything.

### 5.3 The six samples

The first time you run the app it puts six finished homes in your library, ranging from a one-person
studio to a five-bedroom assisted living house. They are fully furnished and occupied, and two of
them come with a smart home proposal already built so you can see Compare and the report working
before you build anything yourself.

Feel free to edit them and modify them for you to understand how the application works. 
Each is a copy, and `Sample residences` in the left rail will give you a fresh one
whenever you want. If you have edited one and want the shipped version back, open it and press
`Reset to the latest sample` in the left rail.

Archiving a sample keeps it archived.

### 5.4 Renaming, starring, duplicating

All three are in the left rail under `This residence`.

The `Name` field renames the home. That name appears in the library, in the report's title and in
the filename when you export.

`Star` pins a home to the top of the library list. `Unstar` releases it.

`Save As` writes a copy under a new name and switches you to it. The original closes untouched.

---

## 6. Variants and the status band

A variant is one version of the design. Every home has at least one, and most have several.

The first is the base environment, named `Existing`. It records the home as it actually stands
today. Every other variant is a proposal, which is one option somebody is putting forward. All of
them describe the same home, so they share its floors, its imported sketches and its custom
furniture. What they differ on is the walls, doors, rooms, furniture, devices and residents.

Switching between variants redraws the scene. Nothing is lost by looking.

### 6.1 Reading the status band

The colored strip under the command bar tells you which variant you are in and what you may do to
it. There are three things it can say.

<!-- SCREENSHOT: the status band in all three states, stacked -->

| The band reads | You are | The buttons offer |
|---|---|---|
| `BASE ENVIRONMENT · READ-ONLY`, slate | Looking at the home as it stands, unable to change it | `Modify base environment`, `New proposal` |
| `EDITING BASE ENVIRONMENT`, amber | Changing the record of the home itself | `Done` |
| `PROPOSAL 09/11/2026 · 11 CHANGES`, accent | Working in a proposal | `Compare`, `Report` |

The amber is deliberately loud. If the band is amber, you are editing the record of how the home
really is, and that is rarely what you want.

### 6.2 Starting a proposal

Press `New proposal` on the status band. The proposal copies whatever variant is showing at that
moment, so start it from the base environment unless you mean to build on another proposal.

It is named for you, as `Proposal` and today's date. A second one on the same day becomes
`Proposal 2`, and so on. You cannot rename a variant; the app has no field for it anywhere. What you
can write is its description, in the Compare rail, and that description is what heads the report.

A proposal is always editable. Only the base environment is ever locked.

### 6.3 Switching between variants

When a home holds more than one variant, the band's title becomes a button with a small `▾` after
it. Click it and a list drops down, one row per variant, each saying whether it is the base
environment or a proposal. Click a row to switch.

The same dropdown has `New proposal` at the bottom left, and when you are standing in a proposal, a
`Delete proposal` button at the bottom right. Deleting a proposal discards it. The base environment
cannot be deleted.

### 6.4 Editing the base environment

Sometimes the record is wrong. You measured a doorway at the wrong width, or the home has a second
bathroom nobody mentioned. Correcting the record is not a proposal, because nothing is being
proposed.

Press `Modify base environment` on the status band. The band turns amber and reads
`EDITING BASE ENVIRONMENT`. Make your correction. Press `Done`, and it locks again.

Leaving it unlocked is harmless in itself, but the amber is there so you notice. Lock it when you
are finished.

One warning about a message you may meet. If you try to read a plan into a locked base environment,
the app tells you to press `Correct the record` or `Propose a change`. Those buttons do not exist.
It means `Modify base environment` and `New proposal`.

---

## 7. Undo and redo

Ctrl+Z undoes. Ctrl+Y redoes. Both are also buttons at the far right of the command bar, and both
grey out when there is nothing to undo or redo.

The app holds the last hundred changes. A drag counts as one change, however far you dragged, and so
does an action that has knock-on effects. Deleting a wall takes its doors, its grab bars and the
sensors on them with it, and one Ctrl+Z brings all of it back.

### 7.1 Three things that wipe the undo history

Undo cannot cross these boundaries. After any of them, the history is empty and the changes before
it cannot be undone.

- Switching to a different variant.
- Switching to a different floor.
- Opening a different home.

This is worth knowing before you switch. If you are unsure about a change you just made, undo it
before you change floors, not after.

### 7.2 What undo does not cover

Undo works on the design. It does not touch anything else.

Not undoable: the units chip, the view mode, the eye height, where the camera is, what is selected,
which mode and tool you are in, the clock, or your API key. Also not undoable are Save, Save As,
Export, Import, Archive, Star and `Reset to the latest sample`. Answering an alert in the Monitor
console changes nothing in the file, so there is nothing to undo.

After an undo, your selection is cleared and whatever tool you were using resets. If you were
halfway through drawing a run of walls, that run is dropped. The mode and tool themselves do not
change.

---

## 8. Import mode. Floors, plans and scale

Import mode does three things. It manages the storeys of the home, it brings in the floor plan
sketch you are going to trace, and it sets the scale of that sketch so everything you trace comes
out at true size.

An imported sketch is called an underlay. It lies flat on the floor of the scene, underneath
everything, and you draw over the top of it. It belongs to the building rather than to any one
version of the design, so every variant traces the same sketch. It is never printed and never
appears in a report.

<!-- SCREENSHOT: Import mode, rail showing Floors, Scale and the read buttons -->

### 8.1 Floors

A floor, or storey, is one level of the home. Only one is drawn and edited at a time. Everything
follows the floor you are on, including the tools, the camera and the click plane.

The `Floors` section at the top of the rail lists them. Each row has the floor's name in an editable
box, the filename of its plan, a round `↻` to swap that plan for a different one, and a `✕` to
remove the floor.

- Click a row's name box to switch to that floor.
- Type in it to rename the floor. Renaming applies to every variant at once.
- `Import plan…` appears in place of the filename when a floor has no plan yet.
- `↻` replaces the plan. The scale is set again from scratch.
- `✕` removes the floor and its plan, from every variant. It needs two clicks, and the second click
  tells you exactly what is being discarded, such as `Remove Floor 2. Discards its plan and 14
  walls, 6 rooms, 9 items`. One undo brings it all back.

On a home with only one floor, `✕` removes only the plan and leaves anything you traced standing. A
home must always have at least one floor.

At the bottom of the section, `+ Add floor` adds a storey above the top one, with a name already
filled in for you. The first floor is called `Ground floor` and the rest are numbered. A new storey
is added to every variant at once and starts empty. Because an empty storey asserts nothing, adding
one does not require unlocking the base environment, and it shows up as no change in Compare.

### The floor chip

Once a home has two or more floors, a chip appears in the command bar reading something like
`Ground floor  (1/2)`. Click it to go to the next floor. It cycles round, and there is no dropdown.

Switching floors clears the undo history. See [section 7.1](#71-three-things-that-wipe-the-undo-history).

### 8.2 Bringing in a plan

Press `Import plan…` on a floor's row, or `↻` to replace one. A file chooser opens titled
`Select a floor plan`, with a confirm button reading `Import`.

PNG, JPG and PDF are accepted. BMP is not, so convert one to PNG first if that is what you have. A
photograph of a printed plan taken square-on works perfectly well. It does not need to be a scan.

The sketch appears on the floor of the scene at an arbitrary size, and the message reads
`Plan imported. Set its scale next.`

### A PDF with more than one page

A single-page PDF imports like an image. A PDF with several pages shows you a grid of thumbnails
instead, with the page count at the top. Click a page to select it, then choose one of two buttons.

`Use only page N` brings in that one page as the plan for the floor you pressed Import on.

`Use all N pages as floors` brings in every page, one floor per page, stacked from the ground up.
Page 1 lands on the floor you were already on, and each page after that adds a new floor above. A
two-page PDF into a fresh home leaves you with exactly two floors.

The whole multi-page import is one undo step. Because every page is rendered at the same resolution,
calibrating any one of them sets the scale for all of them.

### 8.3 Setting the scale

This is the step that makes everything else true. Until you do it, the sketch has no real-world size
and nothing traced over it measures correctly.

Find something on the sketch whose real length you know. An exterior wall you measured, a standard
doorway, a printed dimension line, a gridline on graph paper. Longer is better, because an error in
your two clicks matters less across a longer distance.

Press `Set scale…` in the rail. The rail replaces itself with a three-step wizard.

1. `1 / 3` asks you to click the first end of that thing. Click it on the sketch. A yellow dot marks
   the spot.
2. `2 / 3` asks for the other end. A line follows your cursor. Click it.
3. `3 / 3` gives you a `Distance` box. Type the real length and press `Apply`.

The sketch rescales around your two points. The button now reads something like `Scale · 11.4 m`,
telling you how wide the entire image is in real life. That number is a good sanity check. If your
plan is of a one-bedroom apartment and it reads 60 m, you mistyped something.

Press Esc or `Cancel` at any point to abandon the wizard.

Two messages mean you need to try again. `Could not read that measurement.` means the app could not
parse what you typed. `Those two points are in the same place.` means your two clicks landed on top
of each other.

If several floors came from the same PDF, calibrating one of them carries the scale to the others,
but only to those that have never been calibrated or that still carry the value you just replaced. A
page somebody measured by hand is left alone.

To re-measure later, press the `Scale · …` button again.

### 8.4 Adjusting how the sketch looks

The `Display` foldout in the rail holds three controls for the sketch itself. None of them affects
anything you trace.

| Control | What it does |
|---|---|
| `Opacity` | How strongly the sketch shows through. Turn it down when the traced walls get hard to read against it |
| `Angle` | Turns the sketch, to square up a photo taken at a slight angle |
| `Lock in place` | Stops the sketch moving while you trace over it |

These are drag-or-type boxes, which appear throughout the app. Click into one and type a number, or
drag sideways on it to scrub the value up and down. Hold Shift while dragging for finer steps, Ctrl
for coarser ones. The arrow keys nudge. Enter commits and Esc cancels.

### 8.5 Tracing by hand, or having the plan read for you

You now have three ways to turn the sketch into walls and rooms.

Tracing by hand is the accurate one. You draw what you see, you decide what counts as a wall, and
nothing is guessed. For a plan of any importance this is the route to take, and it is covered in
[section 9](#9-structure-mode-walls-openings-and-rooms).

The other two read the sketch for you. Both are fast, both are approximate, and both replace
everything already on that floor. Each is a single undo step, so Ctrl+Z takes the whole thing back
if you do not like the result.

### Read on device (optional)

This runs entirely on your device.

It finds rooms, doorways and windows. It does not place furniture.

It also works without a calibrated scale. If the plan has not been calibrated, it estimates one from
the doorways it finds, assuming a standard 0.813 m (32 in) interior door, and saves that estimate as
the plan's scale. Calibrate properly if the measurements matter.

Press `Read on device` in the rail. The result line reads something like
`6 rooms · 18 walls · 11 doors and windows  (on device, 0.4 s · scale from 5 doorways)`.

Messages you may see instead:

- `Could not find line work in this image.` The sketch is too faint, too noisy or not a line drawing.
- `No rooms could be found in this image.` Nothing closed enough to be a room was found.
- `No scale is set and no doorways were found to estimate one from.` Set the scale first.
- A warning that the walls came out implausibly thick means the estimated scale is wrong. Calibrate
  by hand before trusting anything.

### 8.6 Read this plan, using Claude (optional)

This is the one part of the app that sends anything off your machine.

Press `Read this plan` and the sketch image goes to Anthropic, where Claude reads it and sends back
the rooms, doors, windows and furniture it can see. It reads furniture, which the on-device reader
does not, and it copes better with a messy hand-drawn sketch.

What is sent is the sketch image and a set of instructions. Nothing else about the home goes with
it. Not its name, not its address, not the residents, not any other floor. The image goes only when
you press the button. It is not sent on load, on save, or in the background.

It takes a few minutes, usually. The rail shows what it is doing as it goes, with a `Cancel` button
beside it. Leaving Import mode cancels it too.

Two things must be in place before the button appears. The plan must be calibrated, because a
generated plan is measured against the calibration and can only ever be as accurate as it is. And an
API key must be entered.

### Getting and entering an API key

An API key is a password that lets the app use Anthropic's service. You get one by making an account
at console.anthropic.com and creating a key there. The service charges for use, typically a few
cents per plan read.

In the Import rail, find the `API key` box, paste the key in, and press `Save key`. The eye button
on the right hides the text while you paste, in case somebody is looking over your shoulder.

The key is stored as plain text in a file called `anthropic.key` in the app's folder. It is not
encrypted. It is never included in an exported home, never in a report, and never in a saved
residence. Anyone with access to your user account on this machine can read it, so treat it the way
you would treat a password written on paper in a locked drawer.

`Forget key` deletes it.

If your organization sets the `ANTHROPIC_API_KEY` environment variable on the machine, the app uses
that instead and stores nothing. The rail then says `Key from environment` and offers no Forget
button.

### What comes back

The result replaces everything on that floor. The walls, openings, rooms, furniture and wall-mounted
items are all swapped out.

If the floor already has anything on it, the button turns red and a `⚠` beside it spells out what
will go, such as `Replaces 22 walls, 8 rooms, 14 items already on this floor.`

Afterwards, a `Last read` line reports what was installed. If anything could not be resolved, a
`Read with N problems` line lists what, and a `Notes` row carries Claude's own comments on hover.

### When the result is wrong

If something is wrong, press undo (Ctrl+Z) to revert the last change.

If the result looks incorrect, it is recommended to trace the sketch by hand. 

Messages worth recognizing:

- `Could not reach the API. Check the network connection.`
- `That API key was rejected. Check it in the Sketch panel.` The key is wrong or has been revoked.
- `That API key is not allowed to use this model.` The key is valid but lacks access to the model.
- `Rate limited after 3 attempts. Wait a minute and try again.`
- `Claude is overloaded. Try again shortly.`
- `The plan was cut off before it finished. This sketch may have too many rooms to read in one pass.`
  Split the plan across floors, or trace it.
- `That plan was read for a different floor.` You changed floors mid-run. Go back and read again.

Press Ctrl+S when you are happy with the result.

---

## 9. Structure mode. Walls, openings and rooms

Structure mode builds the shell of the home. It holds three tools, named along the top of the rail.
Press 1, 2 or 3 to switch between them.

| Tool | What it does |
|---|---|
| `Walls` | Draw the walls |
| `Openings` | Put doors, windows and cased openings into them |
| `Rooms` | Say what each enclosed area is |

There is no tool for drawing a room. Walls that close off an area make a room by themselves, the
moment the shape closes.

### 9.1 Walls

<!-- SCREENSHOT: mid-run wall drawing, showing the live length and angle readout -->

Click at a corner, then at the next corner, and keep clicking your way round. Each segment appears
the moment you complete it, so a long trace is never lost to one bad click. Press Enter to finish
the run, or use the `Finish run` button in the rail.

| Key | What it does |
|---|---|
| Enter | Finish the run |
| Esc | Throw away the run you are drawing |
| Backspace | Remove the last corner you placed |
| Shift, held | Draw free, with no snapping |

A readout follows your cursor showing the length and angle of the segment you are about to place,
like `3.66 m   90°`. Before the first click it reads `Click to start a wall`.

### Typing an exact length

Mid-run, just type a number. There is no box to click into. The digits appear in the readout after a
small keyboard glyph, and pressing Enter puts the next corner exactly that far along the direction
your cursor is pointing.

The keys that feed this are the digits, the full stop, the apostrophe, the slash and the space. So
`12' 6` then Enter gives you twelve foot six. A bare number follows whatever the units chip is
showing.

This is the fastest way to trace a plan with dimensions printed on it. Point roughly in the right
direction, type the printed number, press Enter.

### How corners pull into place

As you move the cursor, the app pulls the corner onto a sensible position and names what it did in
the readout. From most specific to least, it will snap to the end of an existing wall, to the point
where your run crosses one, to a point along a wall's centre line, level with a parallel wall's end,
onto a 45 degree axis from your last corner, and finally onto a grid.

Two of these draw something so you can see why the cursor stopped. A ring around the cursor means it
has grabbed an existing wall. A dashed line running off to a distant wall's end means it has lined
you up with that end.

Hold Shift to turn all of it off. Shift means free, which is one thing and not two. It disables the
snapping and it also stops the new wall dividing the walls it crosses. Two walls that cross without
a shared corner enclose nothing, so a room will not form there.

When walls do divide each other, a small hollow ring marks each junction that will be made, and the
readout counts them after a scissors glyph. If you draw along a wall that already exists, the
message reads `A wall already runs along that line.` and nothing is added.

### Thickness, height and the snapping options

Four controls sit in the rail. They apply to walls you are about to draw, not to ones already there.

| Control | Meaning | Starts at |
|---|---|---|
| `Thickness` | How thick the wall is | 0.114 m (4.5 in) |
| `Height` | How tall it is | 2.44 m (8 ft) |
| `Square to 45°` | Hold each run to the nearest 45 degree axis | On |
| `Grid` | The grid corners snap to. Set it to zero to turn the grid off | 0.05 m (2 in) |

To change a wall you have already drawn, use [Select mode](#10-select-mode-inspecting-and-changing-what-is-there).

### 9.2 Openings

An opening is a hole in a wall. There are three kinds, chosen by the chips at the top of the rail
under the heading `Add`.

| Kind | What it is |
|---|---|
| `Door` | A doorway with a door in it |
| `Window` | A window, set above a sill |
| `Cased opening` | A hole in the wall with no door in it |

Hover over a wall and click where the opening goes. The preview appears on the wall as you hover.
Switching kind resets the measurements to that kind's usual ones.

The opening you place comes up selected, which matters because you cannot click an opening in the
plan afterwards. See [reaching an opening again](#93-reaching-an-opening-again).

### The fields

| Field | Applies to | Meaning |
|---|---|---|
| `Width` | All | The rough opening, meaning the hole in the wall |
| `Height` | All | How tall the opening is |
| `Sill` | Windows | How high above the floor the window starts |
| `Threshold` | Doors | The height of the lip somebody has to cross |

A door starts at 0.813 m (32 in) wide and 2.032 m (80 in) tall. A window starts at 0.914 m (36 in)
wide, 1.219 m (48 in) tall, with a sill 0.914 m (36 in) up.

### Clear passage

For a door, the rail shows a live `Clear passage` figure while you work. This is the width somebody
actually goes through once the door leaf and its stop are in the way, and it is always less than the
rough opening above it. It is the number that matters for a wheelchair, and it is the number the
report carries.

A window or a cased opening has no leaf, so its clear passage equals its width and no separate
figure is shown.

### The red preview

The preview bar on the wall is green when the opening fits and red when it does not. When it is red,
the message at the cursor says why. Usually the opening is too close to a corner, overlapping
another opening, or wider than the free span of that wall.

The width box deliberately lets you type a width too big for the wall you are hovering over. The
wall changes as you move the mouse, so rather than clamping the number, the app turns the preview
red and tells you.

### Step-free

Under the `Threshold` field, a badge reads either `Step-free` or `Has a threshold`. Step-free means
there is nothing to cross at that doorway, which is what you want for a wheelchair, a walker, or
anyone who catches their feet.

Set `Threshold` to the real height of the lip when you are recording the home as it stands. Set it
to zero in a proposal when the proposal is to remove it, or press `Make step-free` in
[Select mode](#10-select-mode-inspecting-and-changing-what-is-there).

### 9.3 Reaching an opening again

You cannot click a door or a window in the plan. Clicking one selects the wall it sits in. An
opening is a hole in something, and the something is what you get.

To get at one, click its wall in [Select mode](#10-select-mode-inspecting-and-changing-what-is-there).
The rail lists the openings in that wall under a heading like `Openings (3)`, in the order they run
along the wall. Hover a row and that opening lights up in the plan, which answers the question of
which one is which. Click the row to inspect and change it.

### 9.4 Rooms

A room is an area closed off by walls. The app works them out for you, and the Rooms tool is where
you say what each one is.

<!-- SCREENSHOT: Rooms tool, twelve type rows in the rail, a typed plan behind -->

Twelve types are listed down the rail, always visible.

```
Room    Bedroom    Bathroom    Kitchen    Living    Dining
Hall    Entry      Laundry     Storage    Office    Other
```

`Room` at the top means untyped, which is where every room starts.

Click a type to arm it. The row lights up. Now every click inside a closed area in the plan makes
that area that type, and the type stays armed so you can work through the whole floor in one pass.
Click the armed row again to disarm it.

Clicking somewhere not enclosed by walls gives you `That area is not closed off by walls yet.`

### Why the type matters

The type sets the floor finish, so a bedroom gets carpet, a bathroom gets tile and a kitchen gets
vinyl. 

An untyped room is drawn with a dashed outline. Three dashed rooms on a plan says three areas still
need naming, without anybody writing a sentence about it.

The type also groups the change list in Compare and sections the report.

### Naming a room

Below the type rows is a dropdown listing the rooms on this floor. It names the room you have picked,
or reads `Rooms (7)` when none is picked. Open it, click a room, and the list closes.

With a room picked, three things appear under the dropdown. A `Name` box, a foldout reading
something like `Type: Bedroom` holding the twelve type chips, and the room's floor area.

Setting a type renames the room to match, but only if the name was one the app generated. A name you
typed yourself is never overwritten. Clear the `Name` box to get the generated name back.

### Detect rooms

A `Detect rooms` button appears at the bottom of the rail only when the rooms on file no longer
match the walls. On a healthy plan it is not there at all.

Press it and the rooms are rebuilt from the walls. The message afterwards is one of
`The rooms already match the walls.` or `3 rooms updated from the walls.`

You should rarely need it. It exists for the case where something has drifted.

### Rooms cannot be deleted

There is no delete for a room, in this tool or in Select. A room is the space your walls close off,
so to remove one you remove a wall. The app says as much when you look for the button.

Press Ctrl+S.

---

## 10. Select mode. Inspecting and changing what is there

Select mode is the pointer. Click something in the plan and the rail tells you what it is and lets
you change it.

It is the first mode in the command bar and it has one tool. Esc is its exit. Press Esc once to
deselect, and again to go back to the mode you were working in before.

<!-- SCREENSHOT: Select mode with a furniture item selected and its handles showing -->

### 10.1 Getting into it without leaving what you were doing

You do not always have to press Select. Clicking a piece of furniture, a grab bar, a device or a
resident while you are in another mode carries you into Select with that thing picked.

This only happens for things you can only ever inspect. Walls, rooms, floors and openings are left
alone, because clicking a wall is what the Openings tool is for and clicking a floor is where a wall
corner goes. It also does not happen in Furnish, Smart living or People, because in those modes a
click is already doing something.

Press Esc with nothing selected and you are handed back to the mode you came from. Press it again with nowhere left to go and the app offers to close. See [section 3.1](#31-closing-the-app).

### 10.2 What the rail shows

The rail always reads the same way from the top down. The thing's name, then the controls, then one
quiet line of figures, then Delete.

If you are looking at a locked base environment, the controls are gone and an amber `Read-only`
badge sits where they were. You can still browse. To change anything, press `Modify base environment`
on the status band or work in a proposal.

### A wall

`Thickness` and `Height`, then an `Openings (N)` foldout listing every door and window in it, then a
line reading something like `3.66 m long · 114 mm thick · 2.44 m high`.

### A door or window

Reached through its wall's `Openings` list, never by clicking it.

At the top is a badge reading `Step-free` or `Has a threshold`. Under that, `Width`, `Height`, `Sill`
for a window, `Position` along the wall, and `Threshold` for a door. When a door has a threshold, a
`Make step-free` button appears, which sets it to zero in one press.

Unlike in the Openings tool, these fields are bounded by what the wall will actually take, because
here the app knows which wall you mean.

The figures line leads with the clear width, because that is the number that matters, and the hover
text tells you whether that clear width was measured on site or worked out from the rough opening.

### A room

The room's name, a row of the twelve type chips, and its floor area. There is no Delete, and a small
house glyph sits where it would be. Remove a wall to remove the room.

Turning circles are not reported here. They are in [Measure](#145-measure).

### A piece of furniture

The room it stands in, then a `Handles` control with three settings, then its true dimensions.

`Move`, `Rotate` and `Scale` choose what the handles in the plan do and what appears in the rail
beneath. It is one choice, not two.

- `Move` gives you arrows and a centre pad in the plan and nothing in the rail. Drag it about.
- `Rotate` gives you a ring in the plan and a `Facing` box in the rail, with four preset cells for
  `0°`, `90°`, `180°` and `270°`.
- `Scale` gives you a cube in the plan and `Width`, `Depth` and `Height` boxes in the rail, plus a
  `Lock aspect` toggle and a `Reset to catalog size` button.

`Lock aspect` keeps the proportions, so a bed widened by a fifth also gets a fifth longer instead of
turning into a different bed. These are the item's real dimensions, which is what gets drawn and
what clearances are measured against.

### A wall-mounted item, such as a grab bar

`Height` above the floor, `Along wall` for how far along it sits, and chips for `Left face` and
`Right face`. There are no handles in the plan, but you can drag the item itself, and dragging it
re-hosts it onto whatever wall you drop it nearest.

### A smart living device

A badge saying what the device can notice, a `Reports to staff` toggle, and its alert thresholds.
Everyday aids stop after the badge, because a sock aid raises nothing. Covered in
[section 12](#12-smart-living-mode).

### A resident

Read-only here on purpose. It shows the time, what they are doing right now and where, when the
current block runs, whether they use a wheelchair, and your note about them. A button reading
`Edit this person's day` takes you to People mode.

### 10.3 Moving furniture

With `Move` chosen, drag the arrows or the centre pad.

An item settles flush against a wall when you bring it within about 0.15 m (6 in) of one, and tucks
into a corner when it is near two. Hold Shift to move it free with no help. Hold Ctrl to pull it to
the nearest wall from much further away, about 1.2 m (4 ft). Shift wins if you hold both.

An item that is already flush against a wall stays flush when you turn it or resize it. It re-seats
itself against the same wall rather than leaving a gap.

The arrow keys nudge a selected item by 0.05 m (2 in), and Shift makes that 0.01 m. Nudges do not
snap, so this is how you place something a precise small distance off a wall.

### 10.4 Turning furniture

Drag the ring, or use the keys.

| Key | What it does |
|---|---|
| R | A quarter turn clockwise |
| Shift+R | A quarter turn the other way |
| Z | A quarter turn counter-clockwise |
| X | A quarter turn clockwise |

The ring turns in 15 degree steps. Hold Shift while dragging it to turn freely. The `Facing` box
accepts any angle you type and does not round it.

R does nothing in the walkthrough, where R is the key that puts you back in a clear spot.

### 10.5 Deleting

Press Delete, or Backspace, or the `Delete` button at the bottom of the rail.

Deleting cascades, and the whole cascade is one undo.

| Deleting | Also removes |
|---|---|
| A wall | Its doors and windows, its wall-mounted items, and the devices on any of them. The rooms are then rebuilt, so removing the wall between two bedrooms merges them |
| A door or window | Any device installed on it |
| A piece of furniture | Any device installed on it |
| A resident | Any pendant they were wearing |
| A room | Nothing. Rooms cannot be deleted |

Press Ctrl+S.

---

## 11. Furnish mode

Furnish mode puts the contents of the home in. Furniture matters here for a practical reason. It is
what somebody has to get round, reach over and past, and it is what makes a plan legible to a
resident who does not read plans.

<!-- SCREENSHOT: Furnish mode, the catalog grid with category chips above it -->

### 11.1 Finding an item

The rail has a `Search` box at the top, a row of category chips under it, and the catalog grid below.

The catalog holds a little over a hundred items in ten categories.

```
All   Mobility   Bedroom   Bathroom   Kitchen   Dining
Living   Office   Laundry   Storage   Fixtures   Make your own
```

Searching ignores the categories and looks through everything, matching on the item's name.

Each tile draws the item's footprint to scale against one fixed reference, so a double bed reads as
a big rectangle and a grab bar reads as a sliver. Tiles are not scaled to fill themselves, which is
the point. Hovering a tile tells you its size.

### 11.2 Placing an item

Click a tile and the item is armed, meaning it now follows your cursor as a ghost outline. Click in
the plan to put it down.

The ghost is drawn at the size and position the item will actually end up in, after any snapping. So
what you see before the click is what you get after it.

What you place comes up selected with its handles on it, and you stay in Furnish so you can keep
going.

Press Esc to put an armed item back on the shelf. A right click without dragging does the same.

### 11.3 Snapping to walls and corners

An item settles flush against a wall when you place it within about 0.15 m (6 in) of one. Place it
near two walls and it tucks into the corner against both.

| Modifier | Effect |
|---|---|
| Shift, held | Place it free, exactly where the cursor is |
| Ctrl, held | Pull it to the nearest wall from about 1.2 m (4 ft) away |

Shift wins if you hold both. Ctrl widens the reach to a single wall but not to a corner tuck.

After snapping, the app slides the item clear of any doorway it is tall enough to block. If it had
to move it, the message says so, such as `Placed Armchair: moved clear of the doorway`. It never
refuses a click outright, because a click that appears to do nothing reads as a broken tool.

### 11.4 Turning an item before you place it

| Gesture | Effect |
|---|---|
| R | Quarter turn clockwise |
| Shift+R | Quarter turn the other way |
| Z and X | Quarter turn, counter-clockwise and clockwise |
| Shift and the scroll wheel | 15 degrees per notch |

Q and E are not rotation keys. They lower and raise the camera.

### 11.5 Wall-mounted items

Grab bars, wall cabinets, towel bars, handrails, switches and thermostats mount on a wall rather
than standing on the floor. They behave differently while you place them.

Hover near a wall and the ghost lands on the wall face under your cursor, as a dot with a readout
giving the item and its height above the floor. The side of the wall you hover on is the side it
mounts to. If no wall is within reach the message reads `Move closer to a wall to mount this.`

A wall-mounted item has no facing, so there is no rotation to set. To change its height or slide it
along its wall after placing, select it and use the rail. See
[section 10.2](#a-wall-mounted-item-such-as-a-grab-bar).

Grab bars are also offered in [Smart living mode](#12-smart-living-mode), under a `Fixtures` chip,
because in practice they get specified alongside the sensing equipment.

### 11.6 Make your own

The last chip in the category row is `Make your own`. Use it for something the household owns that
the catalog does not have. A particular recliner, a hoist, a piece of equipment.

Picking the chip puts a short form above the grid.

| Field | Meaning |
|---|---|
| `Name` | What it is called, in the plan and in the report |
| `Width` | Across its front |
| `Depth` | Front to back |
| `Height` | How tall it stands |

`Add item` stays greyed out until you have typed a name. Press it and the item is added to the grid,
armed ready to place, and saved with this home. It goes into the `.riv` file when you export, and
every variant of the home can use it.

A custom item is drawn as a labeled box. There is no artwork for it, and a stretched wardrobe
standing in for a hoist would read worse than a box with the right name on it.

### Custom items are made and deleted, never edited

There is no way to change a custom item after you add it. If the size was wrong, delete it and make
another.

`Delete item` appears in the rail when a custom item is selected. Deleting takes it out of the grid
so you cannot place it again. Anything you already placed stays exactly where it is, at the size it
was placed. Nothing moves and nothing loses its name.

Custom items always stand on the floor. There is no way to make a custom wall-mounted item.

Press Ctrl+S.

---

## 12. Smart living mode

Smart living covers two things that usually get discussed together. Sensing equipment, meaning
devices that notice something and can raise an alert, and everyday aids, meaning objects that make a
task possible without watching anybody.

It comes after Furnish because a device installs onto something. A door sensor goes on a doorway, a
stove sensor goes on a range, a bed pad goes under a bed. The thing has to be there first.

The mode holds two tools, `Equipment` and `Monitor`.

<!-- SCREENSHOT: Smart living mode, the equipment catalog with the coverage overlay on -->

### 12.1 The Equipment catalog

The rail opens with a `Coverage` toggle, a `Search` box, a row of category chips and the grid.

```
All   Safety   Mobility   Health   Staff communication
Hub   Emerging   Everyday living   Fixtures
```

There are twenty-five items. Sixteen sense something. Nine are everyday aids that sense nothing at
all, such as a rocker knife, a sock aid, a button hook or a key turner.

The `Fixtures` chip is different from the rest. It offers three items from the furniture catalog, a
24 inch grab bar, a 36 inch grab bar and a handrail, because those get specified in the same
conversation as the sensors. They carry no price here, since a grab bar is a change to the building
rather than a purchase, and it shows up in the plan and in the report's room sections.

Searching crosses everything, including the fixtures, whichever chip is lit.

A tile draws one of three things. Devices that sense at a distance draw their reach as a cone.
Devices that get installed somewhere draw their footprint. Anything worn draws a solid block of
color. Hovering a tile gives its name, its price range, its reach where it has one, and a sentence
telling you what to click.

### 12.2 Installing a device

Click a tile, then click what the device goes on. The sentence in the tile's hover text tells you
what that is.

| Where it hosts | What to click |
|---|---|
| A doorway | A doorway |
| A wall | A wall |
| Furniture | The counter, table or furniture it sits on |
| A point on the floor | The floor near a sink, toilet or bath |
| A room | Inside a room |
| A resident | Assign it in the rail, or put it down near a counter or table |

A device installs onto the element, not onto a position on the floor. Widen that doorway later and
its sensor goes with it. Move the counter and the air quality monitor rides along.

Unlike furniture, a device that will not fit is refused rather than slid somewhere nearby. A stove
sensor on a wardrobe would be a device that reports nothing forever, so the app says why instead.
Messages you may see include `Click on a doorway to install this.`, `A stove sensor goes on a range.
This is not one.`, `A pad goes under a bed or a chair. This is neither.` and `There is already a
door sensor there.`

### 12.3 Assigning something to a resident

Anything worn or personal shows a chip row in the rail headed `Worn by` or `Belongs to`.

`Nobody` leads the row and is the default. Leave it there and you can put the item down on a counter
or a table like any other object, drawn as a labeled box. That is the right answer for a rocker
knife that lives in a kitchen drawer.

Pick a resident instead and the item belongs to that person and travels with them.

### 12.4 Coverage

The `Coverage` toggle at the top of the rail draws what each device can see, over the plan. It comes
on with the tool and goes off when you leave.

Each device's reach is drawn as an outline, an arc with two edges running back to the device. The
one you have selected is drawn brightly and the rest quietly. A device that can see is drawn in
blue, and the one video device in the catalog, the doorbell, is drawn in violet so it is never
mistaken for anything else.

A way out of the home that nothing watches gets an amber ring and the words
`Nothing watches this way out.` That is amber rather than red on purpose. An unwatched back door is
a decision somebody has not made yet, not a mistake.

The gap at the far end of a corridor is a picture. A percentage is not, which is why this is drawn
rather than scored.

### 12.5 Privacy

Every device carries a badge saying what it can and cannot notice. This is the thing families ask
about first and it is worth being able to answer precisely.

| Badge | Meaning |
|---|---|
| `Not connected` | An everyday aid. It notices nothing |
| `Senses a condition` | It knows about heat, water or temperature, not about people |
| `Senses that someone is there` | It knows somebody is present, not who or what they are doing |
| `Can speak and listen` | It has a microphone and a speaker |
| `Sees` | It has a camera |

Beside the badge is a `Reports to staff` toggle. Turn it off and the device still senses, and still
prompts in the home, but raises nothing with a caregiver. That is a decision worth making per
device, and the app treats it that way.

Under that, an `Alerts` section lists what this device would raise and how long it waits first.
`Alert after` is in minutes and can be set from 1 to 120.

### 12.6 Monitor. What a caregiver would see

The second tool in this mode is a console showing the home as somebody supporting it would see it on
their phone. It demonstrates how the package would behave, and nothing you do in it changes the
home. Answering an alert does not dirty the file and cannot be undone because there is nothing to
undo.

If no devices are installed, the whole rail is one button reading `Install some devices first`,
which takes you to Equipment.

`Viewing as` switches between three roles, and the switch really does change what is shown.

| Role | Sees |
|---|---|
| `DSP` | A Direct Support Professional. Every alert, every device, and the buttons to respond |
| `Family` | Trends and wellbeing. Never a camera, and never where anybody is. An alert reads only as something needing attention at a location |
| `Resident` | Their own prompts and their own pendant. Nothing about anyone else in the home |

`Day` switches between `Typical`, the household's ordinary day, and `Incidents`, the same day with
the scenarios acted out. A package that works raises nothing at all on a typical day, which is the
point.

Run the clock in the timeline bar and alerts land in the list as the day plays. Each card gives the
alert, the time, and what to do about it. The four responses are `Prompt` to speak into the room,
`Call` to phone the resident, `Check` to look at the entry camera, and `Dispatch` to send somebody.
Clicking a card finds that device in the plan without taking you out of the console.

Below the alerts sit the resident list, a `Coverage` foldout giving the share of floor a movement
sensor can see and how many ways out are watched, and a `Cost` foldout giving what the package costs
to install, what it costs monthly, and an estimate of the labour it saves. The cost figures are
whole-building, across every floor. The device list underneath is per floor.

The labour figure rests on an assumption about how often somebody would otherwise drive over. The
app says so in the hover text. Treat it as a number to discuss.

Press Ctrl+S.

---

## 13. People mode

People mode records who lives in the home and what their day looks like. The app then puts them in
the plan, in the room their schedule says they would be in at whatever time the clock is showing.

Nobody's position is stored. It is worked out from the schedule and the clock every time, which is
why scrubbing the clock never changes the file.

Entering this mode expands the timeline bar along the bottom. Leaving collapses it again.

<!-- SCREENSHOT: People mode with the timeline expanded, two residents with full days -->

### 13.1 Adding a resident

Press `+ Add person`. They are added as `Person 1`, given a color, and given a starting day so they
are somewhere rather than nowhere. That starting day is sleeping from 22:00 to 07:00 in the first
bedroom and relaxing the rest of the time in the first living room.

The roster lists everyone, each row showing their name and what they are doing right now. Click a
row to open their settings below and bring the camera to them.

### 13.2 One person's details

| Field | Meaning |
|---|---|
| `Name` | What this resident is called |
| `Uses a wheelchair` | Draws them seated, at seated eye height, with a wheelchair-sized footprint to stand clear of |
| `Show in the plan` | Whether their marker appears at all |
| `Color` | Colors their marker and their row on the timeline. Eight to choose from |
| `Note` | Anything worth recording. Shown when they are selected |

`Uses a wheelchair` is not cosmetic. It changes the footprint the app expects to be kept clear
around them, and it is the same eye height the walkthrough's Seated setting uses.

`Remove this person` at the bottom takes them out of the household, along with any pendant they were
wearing.

### 13.3 Building their day

Under `Their day` is a list of activity blocks. Each reads as a time range, what they are doing, and
which room. Click one to open it.

Press `+ Add activity` to add a block. It starts at whatever time the clock is showing and runs an
hour.

An open block gives you four things.

`Doing` is a row of chips naming the activity.

```
Sleeping   Getting ready   Cooking   Eating   Relaxing
Working    Care            Out       At residence
```

The activity colors the block on the timeline and suggests a room for a new block. Changing it
never moves a block you have already placed in a room, except for `Out`, which clears the room
because they are not in the home.

`Starts` and `Ends` are times, in 15 minute steps. Drag them or type them. An end time before the
start wraps past midnight, which is how a night's sleep is written.

`Room` says where they are. Press `Set room`, which changes to `Click a room…`, then click the room
in the plan. Clicking outside any room gives `That is not inside a room.` and leaves it armed. Esc
cancels. The `Out` chip beside it means away from the home, and their marker hides for that block.

`Delete activity` removes the block.

While a person is selected, the room they are currently in is outlined in the plan and a readout
gives their name and what they are doing.

### 13.4 The timeline bar and the clock

The strip along the bottom of the window is the only clock in the app.

<!-- SCREENSHOT: the timeline bar expanded, showing the hour ruler, two people's days and the alert lane -->

Across the top is an hour ruler. Click anywhere on it to jump the clock to that time.

Below it, one row per resident when expanded, or a compressed strip when collapsed. Each person's
day is drawn as colored bars, one per activity. A block that runs past midnight is drawn as two
pieces rather than clipped, because sleep is the block everyone looks for and it always wraps. Hover
any block for its time range, activity and room. Blocks where somebody is out are drawn faint.

Under the rows is a thin lane carrying sensor events and alerts. Click an alert mark and the clock
jumps to it and selects the device that raised it.

A bright vertical line marks the current time, carrying a chip with the time on it.

The controls at the right end are these.

| Control | What it does |
|---|---|
| `▲` and `▼` | Expand and collapse the bar |
| `▶` and `❚❚` | Run the clock and pause it |
| The speed button | How fast the clock runs. Click to cycle through 15 min/s, 30 min/s, 1 hr/s, 2 hr/s and 4 hr/s |
| `Time` | The clock. Drag it to scrub, or click in and type a time |

The clock opens at 07:30.

The clock is written in whichever format the units chip implies. Metric gives 24-hour, so `18:00`.
Feet and inches gives 12-hour, so `6:00 PM`.

Press Ctrl+S.

---

## 14. Review mode. Compare, measure, report

Review mode is what the rest of the work is for. It holds two tools and opens on the first.

| Tool | What it does |
|---|---|
| `Compare` | Lists what a proposal changes, and generates the report |
| `Measure` | Point-to-point distances, and the turning space in each room |

<!-- SCREENSHOT: Review mode, Compare open with a grouped change list and ghost overlay -->

### 14.1 Compare

Compare holds a proposal against the home as it stands and writes out every difference in plain
English.

It runs in one direction only. `Before` is always the base environment and cannot be changed, so it
is shown as a read-only line. `After` is the proposal. When a home has two or more proposals, `After`
becomes a picker, and switching it also switches what is drawn in the scene, because comparing
something means looking at it.

If the home has no proposals yet, the rail says
`No proposals yet. Press New proposal in the band above.`

### 14.2 The change list

Every difference gets one row, grouped under the room it happens in. Room is the unit a resident
thinks in and the unit the report is sectioned by.

Rows read as `Added`, `Removed` or `Changed`, followed by what. Anything with no position in the
plan, such as a change to somebody's schedule, falls into a group called `Elsewhere` at the bottom.

On a home with more than one floor, the group heading carries the floor as well.

Click a row and the camera goes to that item and selects it, without taking you out of Compare.

It works the other way too. Clicking a marker in the plan highlights its row.

### 14.3 Taking one change back out

At the end of each row is a small `✕`. Press it and that one change is reverted, leaving the rest of
the proposal exactly as it is. It is undoable, so Ctrl+Z brings it back.

There is one case where it refuses. You cannot restore a door or a grab bar onto a wall the proposal
removed. Put the wall back first, then restore the thing that was on it. The reason appears in the
rail rather than being logged somewhere you would not look.

At the bottom of the rail, `Take back all N changes` empties the proposal while keeping its name.
That is undoable too.

### 14.4 The before and after overlay

The `Before/after overlay` toggle draws both versions at once, translucent, on top of each other.

Red is how it was. Green is how it is now. Something that moved shows both, red where it was and
green where it is.

It comes on by default when you enter Review and goes off when you leave. Switching between Compare
and Measure keeps it on. The toggle overrides either way.

Doors and windows are deliberately left out of the overlay. A doorway drawn twice in two colors in
the same wall is unreadable.

### 14.5 Measure

Click in the plan to drop a point, then click again, and again. Each leg is measured and so is the
running total. Points snap to wall corners, and holding Shift turns that off.

Esc or Enter clears the run. Backspace removes the last point. `Clear` in the rail does the same as
Esc.

Below the legs is `Turning space by room`, one line per room giving the diameter of the largest
circle that fits inside it. A wheelchair needs about 1.5 m (5 ft) to turn.

This is the only place in the app that reports a turning circle, and it is not in the report either.
The figure is measured on the bare room with the furniture taken out, so it says what the walls
allow rather than what the room currently permits. That is a useful number as long as you know which
number it is.

### 14.6 Generate report

The report is a before and after document. It is meant to be handed to somebody who does not have
the app.

You must be standing in a proposal. Pressing it from the base environment gives you
`Start a proposal first.`

Before you press it, write a sentence or two in the box headed `What this proposal does`. That
sentence heads the report, and it is the only prose in the document that is yours.

Press `Generate report` in the Compare rail, or `Report` on the status band. The app saves the home,
takes its own photographs of both versions from matched camera positions, writes the file and opens
it in your web browser.

### What is in it

A cover page with the home's name, the proposal's name, the date, the change count and your
description. Then sections in this order.

1. `The plan`. A before and after image pair, the full change list, and two whole-home figures. How
   many doorways are step-free, and how many have 32 inches of clear width or more.
2. `The residence`. A before and after overview pair.
3. One section per changed room, each with its own image pair, its own changes, its floor area, the
   narrowest way into it and that doorway's threshold.
4. `Smart living`, last and with no photographs. Devices installed, everyday aids, the share of floor
   watched, the ways out that are watched, the cost to install, the monthly cost, and the labour
   offset. It lists what the package would raise, and then what it would not catch, because a
   proposal that lists only its strengths is an advertisement.

Turning circles are not in the report.

### Where the file goes

```
%USERPROFILE%\AppData\LocalLow\IRL\Residence Improvement Visualizer\ResidenceImprovementVisualizer\reports\
```

The filename is the home's name, the proposal's name and the date, like
`Maple Street apartment - Proposal 09-11-2026 - 2026-09-11.html`.

It is one self-contained HTML file with the images inside it, so it opens on any machine and emails
as a single attachment.

### Making a PDF of it

Open the report in a browser, press Ctrl+P, and pick `Save as PDF` as the destination. The report is
laid out for A4 and knows not to split an image pair or a section across a page break.

Press Ctrl+S.

---

## 15. Moving a residence to another machine

There are two ways to give a home to somebody else, and which you use depends on whether they have
the app.

If they do not, send them the PDF of the report. It is the whole argument in a form anybody can
open.

If they do, send them a `.riv` file, which is this home complete.

### 15.1 Exporting

Press `Export` in the left rail under `This residence`. The home is saved first, then a file chooser
opens titled `Export residence` with a confirm button reading `Export`. The filename is filled in
with the home's name.

The `.riv` file holds the home itself, every variant, every floor, the residents, your custom
furniture items, and every imported sketch. It does not hold your API key or your settings.

### 15.2 Importing

Press `Import` in the left rail, near the top beside `New residence`. A chooser opens titled
`Import residence`.

The imported home arrives as a new, separate home. It is given a fresh identity, so importing a copy
of one of your own homes never overwrites the original. If the name is already taken, it gets a
number after it, such as `Maple Street apartment (2)`.

Files with the older `.homeviz` extension still import.

Messages you may see: `Not a Residence Improvement Visualizer residence file.` means the file is not
one of these. `The residence file could not be read.` means it is damaged.

One quirk to know about. If a file chooser is already open and you press Export or Import, nothing
happens at all. Close the open one first.

---

## 16. Archiving, and getting a residence back

Archiving takes a home out of the library without destroying it.

### 16.1 Archiving

Press `Archive` in the left rail under `This residence`. It happens immediately, with no
confirmation, and the message reads `Archived.` The home closes and disappears from the library.

The file is moved, not deleted. It goes to a folder called `_archive` and stays there.

Archiving one of the six samples keeps it archived.

### 16.2 Getting an archived residence back

Recovering is done manually via the File Explorer. 

1. Close the app.
2. Press the Windows key and R together to open the Run box.
3. Paste this in and press Enter.

   ```
   %USERPROFILE%\AppData\LocalLow\IRL\Residence Improvement Visualizer\ResidenceImprovementVisualizer\residences
   ```

4. File Explorer opens on your residences folder. Open the `_archive` folder inside it.
5. Inside are files named with long strings of letters and numbers, ending in `.json`. Each is one
   archived home.
6. If you cannot tell which is which, open one in Notepad. The home's name is near the top, after
   `"name":`.
7. Cut the file you want. Go back up one level into the `residences` folder, and paste it there.
8. Start the app. The home is back in the library.

The sketches were never moved, so an archived home comes back with its floor plans intact.

Nothing in `_archive` is ever deleted by the app. If you want a home genuinely gone, delete the file
yourself from that folder, and delete its folder under `underlays` too.

---

## 17. What the app does not do

Knowing the edges saves you hunting for a feature that is not there.

Saving and recovery

- Nothing saves automatically. Closing asks first, and you choose what happens to unsaved work. See [section 3](#3-saving).
- An archived home cannot be restored from inside the app. See
  [section 16.2](#162-getting-an-archived-residence-back).

Design

- Variants cannot be renamed. A proposal keeps the name it was generated with.
- Rooms cannot be deleted on their own. Remove a wall instead.
- Compare only ever measures a proposal against the base environment, not one proposal against
  another.
- There is no automatic check that a doorway is wide enough or a turning circle big enough. The app
  reports the figures and leaves the judgement to you.
- The app models one floor at a time. There are no stairs, no lifts and no vertical circulation.
- Outdoor features exist in the data, so ramps and railings in a sample still draw, but there is
  currently no way to create or edit them.

Drawing

- There is no PDF writer. Reports are HTML that print to PDF through your browser.
- Some items are drawn as labeled boxes rather than as objects. Medical equipment, rails and small
  plates have no honest artwork available, and a stretched wardrobe standing in for a hoist reads
  worse than a box with the right name on it. A box is a finished state, not a missing one.

Smart living

- Coverage does not account for furniture blocking a sensor's view within a room.
- The cost and labour figures rest on stated assumptions. They are a basis for discussion.

---

## 18. Troubleshooting

Find the symptom, follow the link.

### Nothing I draw is the right size

The plan's scale was never set, or was set wrongly. See
[section 8.3](#83-setting-the-scale). Press the Scale button in the Import rail and measure
something you know the length of again. If you used `Read on device` without calibrating, the scale
is an estimate from the doorways it found.

### My walls do not make a room

Two things cause this. The shape is not actually closed, or it was drawn with Shift held.

Holding Shift means draw free, which turns off both the snapping and the dividing. Two walls that
cross without a shared corner enclose nothing. Redraw the offending segment without Shift, or use
`Detect rooms` in the Rooms tool if it appears. See [section 9.4](#94-rooms).

### I cannot click a door to change it

You cannot. Clicking a door selects the wall it is in. Open that wall's `Openings` list in the rail
and click the door there. See [section 9.3](#93-reaching-an-opening-again).

### The opening preview is red and will not place

It does not fit where you are pointing. Usually it is too close to a corner, overlapping another
opening, or wider than the free span of that wall. The message at the cursor says which. Make it
narrower or move along the wall. See [section 9.2](#92-openings).

### The clear width is smaller than the width I typed

That is correct. The width you typed is the hole in the wall. The clear passage is what is left once
the door leaf and its stop are in the way, and that is the figure that matters for a wheelchair. See
[clear passage](#clear-passage).

### Ctrl+Z will not undo far enough

The undo history is cleared whenever you switch variant, switch floor, or open a different home. See
[section 7.1](#71-three-things-that-wipe-the-undo-history). There is no way to recover past one of
those boundaries.

### I lost my work

The app has no autosave. Closing asks first, so check what you answered: `Exit` on the closing card
lets unsaved work go, and so does closing while the Save button reads `Save *` if you chose it. See
[section 3.1](#31-closing-the-app).

### I cannot change anything, and the rail says Read-only

You are in the base environment, which is locked. Either press `New proposal` on the status band to
work in a proposal, or press `Modify base environment` to correct the record itself. See
[section 6.4](#64-editing-the-base-environment).

### A message told me to press Correct the record or Propose a change

Those buttons do not exist. The message is out of date. It means `Modify base environment` and
`New proposal`, both on the status band.

### Read this plan is not there

Two things gate it. The plan must have its scale set, and an API key must be entered. Without a
scale, the rail explains this where the button would be. See
[section 8.6](#86-read-this-plan-using-claude).

`Read on device` needs neither and is always available.

### The plan reader produced nonsense

Press Ctrl+Z once. One undo takes the whole generated plan back out. Then check the scale, read the
problems list in the rail, and try once more. If it fails twice, trace it by hand. See
[section 8.5](#85-tracing-by-hand-or-having-the-plan-read-for-you).

### The report came out with nothing in it

The proposal has no changes in it yet, so there was nothing to photograph. Make a change first.

If you pressed Report from the base environment you would have seen `Start a proposal first.`

### I am stuck inside a wall in the walkthrough

Press R. You are put back in a clear spot in the nearest room.

### The furniture item I placed drifted from where I clicked

It snapped to a wall or slid clear of a doorway it would have blocked. The message says which. Hold
Shift while placing to put it exactly where the cursor is. See [section 11.3](#113-snapping-to-walls-and-corners).

### A device refused to install

Devices are refused rather than moved, because a device on the wrong thing reports nothing forever.
The message names what it needs. See [section 12.2](#122-installing-a-device).

### R rotates furniture everywhere except in the walkthrough

That is deliberate. In the walkthrough R is the key that gets you unstuck, which matters more.

### Pressing Export or Import does nothing

A file chooser is already open somewhere. Close it and try again.

### Times are in the wrong format

Times follow the units chip. Metric gives a 24-hour clock and feet and inches gives a 12-hour clock.
One preference drives both. See [section 2.3](#23-units).

---

## 19. Keyboard and mouse reference

### Everywhere

| Key | What it does |
|---|---|
| Ctrl+S | Save |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+1 to Ctrl+7 | Switch mode |
| 1 to 3 | Pick a tool within the current mode |
| Esc | Cancel what the tool is doing, then deselect, then go back to the previous mode, then offer to close the app |
| Enter | While the closing card is up, take the first button |
| F | Bring the camera to what is selected |
| Delete or Backspace | Delete the selection |

None of these fire while you are typing in a box.

### Overview camera

| Input | What it does |
|---|---|
| Right mouse drag | Look around |
| Middle mouse drag | Pan |
| Scroll wheel | Move closer or further |
| W, A, S, D | Move around |
| Q and E | Lower and raise |

### Walkthrough camera

| Input | What it does |
|---|---|
| Any mouse button held, then move | Look around |
| W, A, S, D | Walk |
| Shift, held | Walk faster |
| R | Return to a clear spot |

### Drawing walls

| Key | What it does |
|---|---|
| Enter | Finish the run |
| Esc | Abandon the run |
| Backspace | Remove the last corner |
| Shift, held | Draw free, no snapping and no dividing |
| Digits, `.`, `'`, `/`, space | Type an exact length, then Enter |

### Furniture

| Input | What it does |
|---|---|
| R | Quarter turn clockwise |
| Shift+R | Quarter turn counter-clockwise |
| Z and X | Quarter turn, each way |
| Shift and scroll | Turn 15 degrees per notch |
| Shift, held while placing or moving | Place free, no snapping |
| Ctrl, held while placing or moving | Pull to the nearest wall from further away |
| Arrow keys | Nudge a selected item |
| Shift and arrow keys | Nudge more finely |
| Esc, or right click without dragging | Put an armed item back |

### Measuring

| Key | What it does |
|---|---|
| Esc or Enter | Clear the run |
| Backspace | Remove the last point |
| Shift, held | Do not snap to corners |

### Any number box

| Input | What it does |
|---|---|
| Drag sideways | Scrub the value |
| Shift while dragging | Finer steps |
| Ctrl while dragging | Coarser steps |
| Up and down arrows | Nudge |
| Enter | Commit |
| Esc | Cancel |

---

## 20. Glossary

Base environment. The variant recording the home as it actually stands today, named `Existing`. It
is locked by default so nobody edits the record by accident. See
[section 6](#6-variants-and-the-status-band).

Cased opening. A hole in a wall with no door in it. See [section 9.2](#92-openings).

Clear passage, or clear width. The width somebody actually goes through a doorway, once the door
leaf and its stop are in the way. Always narrower than the hole in the wall, and the figure that
matters for a wheelchair. See [clear passage](#clear-passage).

Command bar. The row of mode names along the top of the window. See [section 2](#2-the-window).

Custom item. A piece of furniture you defined yourself, by name and three dimensions. Saved with the
home and shared by all its variants. See [section 11.6](#116-make-your-own).

DSP. Direct Support Professional. The person who would answer an alert. One of the three roles in
the Monitor console. See [section 12.6](#126-monitor-what-a-caregiver-would-see).

Floor, or storey. One level of the home. Only one is drawn and edited at a time. See
[section 8.1](#81-floors).

Floor chip. The button in the command bar naming the floor you are on. Only appears when there is
more than one, and cycles when clicked. See [the floor chip](#the-floor-chip).

Ghost, or before and after overlay. Both versions of the design drawn translucent on top of each
other, red for how it was and green for how it is now. See
[section 14.4](#144-the-before-and-after-overlay).

Mode. One of the words in the command bar, each a stage of the work with its own tools in the right
rail. See [section 2](#2-the-window).

Opening. A door, window or cased opening in a wall. Cannot be clicked in the plan; reached through
its wall. See [section 9.3](#93-reaching-an-opening-again).

Proposal. A variant that is not the base environment. One design option. Always editable, never
renameable. See [section 6.2](#62-starting-a-proposal).

Rail. The panel down either side of the window. The left rail is the library, the right rail holds
the current mode's tools. See [section 2](#2-the-window).

Residence. One home. The thing that is saved, listed, exported and reported on. See
[section 5](#5-residences).

`.riv` file. One home packaged as a single file for sending to somebody else, complete with its
sketches. See [section 15](#15-moving-a-residence-to-another-machine).

Scale, or calibration. Telling the app how big the imported sketch is in real life, by clicking two
points and typing the distance between them. Nothing measures correctly until it is done. See
[section 8.3](#83-setting-the-scale).

Status band. The colored strip under the command bar, saying which variant you are in and whether
you may change it. See [section 6.1](#61-reading-the-status-band).

Step-free. A doorway with no lip to cross. See [step-free](#step-free).

Threshold. The height of the lip at a doorway. Zero means step-free. See
[section 9.2](#92-openings).

Turning circle, or turning space. The diameter of the largest circle that fits inside a room,
measured on the bare room with the furniture taken out. A wheelchair needs about 1.5 m (5 ft).
Reported only in Measure. See [section 14.5](#145-measure).

Underlay. The imported floor plan sketch, lying flat under the scene for you to trace over. One per
floor, shared by every variant. See [section 8](#8-import-mode-floors-plans-and-scale).

Variant. One version of the design within a home. Either the base environment or a proposal. See
[section 6](#6-variants-and-the-status-band).
