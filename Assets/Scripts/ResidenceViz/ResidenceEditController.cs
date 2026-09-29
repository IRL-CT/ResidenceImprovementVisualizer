using System.Collections.Generic;
using ResidenceViz.Report;
using Newtonsoft.Json;
using SimpleFileBrowser;
using UnityEngine;
using UnityEngine.InputSystem;

// The ResidenceViz application shell: holds the open document, owns the tool registry, draws the rails,
// and hosts undo.
//
// This is deliberately THIN. EditController is 5,424 lines because every tool's input handling,
// preview, commit logic, and panel live inline in one file behind a 13-case enum, so adding a tool
// means editing four places in a monolith. Here a tool is a file that implements IResidenceTool plus one
// line in Register(), and this class never needs to know what any of them do.
//
// Layout follows the existing docked-rails idiom (UIShell / Redesign.html): library on the left,
// command bar across the top, inspector on the right, scene in the middle.
//
// The command bar drives a WORKFLOW STAGE (ResidenceWorkflow), not a mode in the Site tool's sense. Site
// is a tool whose operator ranges freely over terrain, buildings and generation; ResidenceViz is used
// in a meeting and its work has an order. Import a plan, trace it, furnish it, then compare options.
// Showing one stage's tools at a time is what keeps the rail short enough to read at a table, and it
// is what lets outdoor work be present for the residences that need it and absent for the ones that do not.
public class ResidenceEditController : MonoBehaviour, EditHistory.IHost
{
    // USER WIRES THIS IN INSPECTOR:
    [SerializeField] private ResidenceRenderer residenceRenderer;
    // USER WIRES THIS IN INSPECTOR:
    [SerializeField] private ViewController viewController;
    // USER WIRES THIS IN INSPECTOR: falls back to Camera.main.
    [SerializeField] private Camera cam;

    [Header("Layout")]
    [SerializeField] private float leftRailWidth = 250f;
    [SerializeField] private float rightRailWidth = 310f;
    // Tall enough for a full-height UITheme.CommandBar: PrimaryH (44) plus the Pad (14) that
    // UITheme.Inset takes off each side, PLUS the button style's vertical margin (3 + 3): the
    // toolbar is laid out at y = margin.top inside the inset, so a bar sized without the margin drew
    // the tabs at 3..47 in a 44 px area and BeginArea clipped their bottom edge. Computed from the
    // theme rather than serialized, so it cannot drift from the styles it has to contain (the scene
    // still carries an orphaned topBarHeight: 72 that Unity ignores).
    private static float TopBarHeight
        => UITheme.PrimaryH + UITheme.Pad * 2f + UITheme.ButtonStyle.margin.vertical;

    // ---------------------------------------------------------------------------------------

    public ResidenceDoc Doc { get; private set; }
    public VariantDef Variant => ResidenceStore.ActiveVariant(Doc);
    /// <summary>
    /// The story being edited. Every tool sees it through ResidenceToolContext.Level, and the ground-plane
    /// raycast that turns a click into world XZ reads its elevation, so this one property is what
    /// puts a drawing tool on the right floor.
    ///
    /// It was hardcoded to levels[0]. ResidenceRenderer has taken a level index since it was written and
    /// clamps it exactly like this, but nothing ever passed one, so the app had two notions of "the
    /// current level" that agreed only because one of them was always zero.
    /// </summary>
    public LevelDef Level
    {
        get
        {
            var v = Variant;
            if (v?.levels == null || v.levels.Count == 0) return null;
            return v.levels[Mathf.Clamp(_levelIndex, 0, v.levels.Count - 1)];
        }
    }

    /// <summary>Which story, clamped to what the active variant actually has.</summary>
    public int LevelIndex
    {
        get
        {
            var v = Variant;
            int count = v?.levels?.Count ?? 0;
            return count == 0 ? 0 : Mathf.Clamp(_levelIndex, 0, count - 1);
        }
    }

    public int LevelCount => Variant?.levels?.Count ?? 0;

    private int _levelIndex;

    public bool Dirty { get; private set; }
    public bool PointerOverUI { get; private set; }

    // Shared selection. Tools read it; SelectTool writes it. Kept here rather than in a tool so the
    // inspector rail can describe the selection no matter which tool is active.
    public ResidenceElementMarker.Kind SelectedKind { get; set; }
    public string SelectedId { get; set; }
    public void ClearSelection() => SelectedId = null;

    /// <summary>
    /// The opening whose row in the wall inspector's list is under the cursor, highlighted in the plan
    /// so a list of "Bathroom door / Bathroom door" is still telling you which one you are about to
    /// pick. Set by SelectTool while it draws that list, and cleared at the top of every OnGUI,
    /// clearing there rather than in the list itself is what stops a highlight surviving a change of
    /// selection to something that draws no list at all.
    /// </summary>
    public string HoverOpeningId { get; set; }

    /// <summary>
    /// Selects an element and, unless told otherwise, carries the UI to the Select tab so the inspector
    /// describing it is on screen.
    ///
    /// An explicit method rather than logic in the setters: the two properties are written as separate
    /// statements everywhere, so a setter-side hook would fire on a half-updated pair. The stage change
    /// is queued, so this is safe to call from HandleInput and from OnGUI alike.
    /// </summary>
    /// <param name="reveal">
    /// False where selecting is a side effect rather than the point. Placing furniture selects what you
    /// placed (switching tabs after every placement would make the tool unusable) and the People
    /// stage is already inspector-shaped, so being thrown out of it mid-edit helps nobody.
    /// </param>
    public void Select(ResidenceElementMarker.Kind kind, string id, bool reveal = true)
    {
        SelectedKind = kind;
        SelectedId = id;
        if (reveal && _stage != ResidenceStage.Select && !string.IsNullOrEmpty(id))
            RequestStage(ResidenceStage.Select);
    }

    public EditHistory History { get; private set; }
    public ResidenceRenderer Renderer => residenceRenderer;

    private readonly List<IResidenceTool> _tools = new List<IResidenceTool>();
    private IResidenceTool _active;
    private ResidenceToolContext _ctx;

    // The active workflow stage, and the stages currently on offer (Outdoors is absent unless the
    // residence has switched its exterior layer on).
    private ResidenceStage _stage = ResidenceStage.Import;
    private List<ResidenceStage> _stages = ResidenceWorkflow.VisibleStages(null);

    // A stage change rewrites the command bar AND the tool list, so applying one from inside OnGUI
    // would leave IMGUI's layout pass and repaint pass disagreeing about how many controls exist.
    // UI-initiated changes queue here and land at the top of the next Update instead.
    private ResidenceStage? _pendingStage;
    private bool _stagesIncludeOutdoors;

    // Where Esc goes back to. Select is a destination you arrive at mid-task: from a click on a chair
    // while tracing a sketch, so leaving it has to return you to the work, not to a fixed default.
    private ResidenceStage? _stageBefore;

    // The library rail folded down to a strip. Two rails cost 560 px of a 1280-wide window before the
    // plan gets any, and the library is consulted between residences rather than during one. Transient by
    // design: not persisted, not a setting.
    private bool _leftCollapsed;

    // Both of these change the control COUNT of a panel, so they queue exactly like _pendingStage,
    // flipping either mid-OnGUI leaves the layout pass and the repaint pass disagreeing.
    private bool _pendingLeftToggle;
    private ViewController.Mode? _pendingViewMode;

    /// <summary>The left rail's width right now: the strip when collapsed. Used by every rect that
    /// touches it, so the panel, the top bar and the pointer test cannot drift apart.</summary>
    // 52, not 44: UITheme.Inset takes Pad (14) off BOTH sides, so 44 would leave 16 px of content for
    // a 24 px button. 52 leaves exactly 24.
    private float LeftWidth => _leftCollapsed ? 52f : leftRailWidth;

    private List<ResidenceSummary> _library = new List<ResidenceSummary>();
    private Vector2 _libScroll, _railScroll;
    // Armed by "Reset to the latest sample" and cleared on every open, so a confirmation can never
    // carry over from the residence it was meant for to the next one.
    private bool _confirmReset;
    private string _status;
    private float _statusUntil;

    private bool _showOutdoors;
    private bool _showSamples;

    // The mode band: which of the two jobs you are doing, said permanently under the command bar.
    // See ModeBand for why this replaced the "Design options" foldout rather than joining it.
    private readonly ModeBand _modeBand = new ModeBand();

    /// <summary>Whether the band is expanded into its variant list.</summary>
    private bool _variantMenuOpen;

    // Opening the menu changes the band's height AND its control count, so it queues exactly like
    // _pendingStage. Every deferred toggle in this file exists for that one reason.
    private bool _pendingVariantMenu;

    // How many changes the active proposal makes, recomputed once per Update rather than per OnGUI
    // pass. The band shows it continuously, and VariantDiff.Compare builds a dictionary per element
    // kind. Cheap, but OnGUI runs layout and repaint separately, so drawing it directly would run
    // the whole diff twice a frame.
    private int _changeCount;

    /// <summary>
    /// Whether the timeline bar is showing the full per-person gantt rather than its collapsed strip.
    /// Transient like the clock it reads: expanding it changes no data and dirties nothing.
    /// </summary>
    public bool TimelineExpanded { get; set; }

    // Changing this rewrites the bar's height AND its control count, so it queues like _pendingStage.
    private bool _pendingTimelineToggle;

    private readonly TimelineBar _timeline = new TimelineBar();

    // The one modal in the app. See ExitPrompt for why quitting gets one when nothing else here does.
    private readonly ExitPrompt _exitPrompt = new ExitPrompt();

    /// <summary>Whether the exit prompt is up. While it is, it owns the window and every key.</summary>
    public bool ExitPromptOpen { get; private set; }

    private ExitPrompt.Intent _exitIntent;

    // Whether there was unsaved work when the card went up. See RequestExitPrompt for why this is
    // latched rather than read live.
    private bool _exitDirty;

    // Which residence an OpenResidence intent lands on once the question is answered. Deliberately
    // NOT cleared by AfterOpen the way _confirmReset is: this one has to survive the document swap
    // it is the authorization for.
    private string _exitTargetId;

    // The click lands here and Update applies it, like every other request in this file. Quitting
    // from inside OnGUI would tear the IMGUI pass down under itself.
    private ExitPrompt.Answer? _pendingExitAnswer;

    // Set by the wantsToQuit handler, which fires on whatever frame the window's X was pressed.
    private bool _pendingExitPrompt;

    // Set immediately before Application.Quit so the wantsToQuit handler lets the second pass
    // through instead of asking the question it has already had answered.
    private bool _quitConfirmed;

    // Swapping the document rebuilds both rails, so an open requested from a library row queues here
    // like every other control-count change. These carry the request whether or not the prompt was
    // involved: with nothing unsaved there is no question to ask and the row still cannot act inline.
    private string _pendingOpenId;
    private bool _pendingNewResidence;

    // A stage to land on after the open, for the one caller that has to beat NewResidence's own
    // choice of Structure. See RequestNewResidence.
    private ResidenceStage? _afterOpenStage;

    // Draws the current selection in the scene. A plain class like TimelineBar and UITheme, and
    // owned here rather than by SelectTool because the highlight follows the SELECTION, which every
    // tool can set, not the active tool. See SelectionOverlay for why it cannot live in a tool.
    private readonly SelectionOverlay _selectionView = new SelectionOverlay();

    // What the sensing layer can see, drawn over the plan. Owned here rather than by SensorTool for
    // the reason SelectionOverlay is: a tool's DrawOverlay is gated on the pointer being off the
    // rails, and the coverage picture is what someone looks at WHILE reading the coverage figure in
    // the rail. It is also useful from Compare and Review, which are not the Sensors tool at all.
    private readonly SensorOverlay _sensorView = new SensorOverlay();

    public bool SensorCoverageVisible
    {
        get => _sensorView.Enabled;
        set => _sensorView.Enabled = value;
    }

    // Drag handles on the selected item, and which of them are showing.
    //
    // This is the SITE tool's gizmo, reused as-is (Assets/Scripts/TransformGizmo.cs) rather than
    // re-expressed the way WallLinker re-expresses FenceLinker: it takes GameObjects and a Camera and
    // emits deltas, with nothing site-shaped anywhere in it, so literal reuse is what makes the two
    // editors feel the same instead of merely similar. It joins WorldRenderer, PrefabRegistry and
    // EnvironmentScale as shared-by-design; the two knobs ResidenceViz needed (minHandleSize, Tick's
    // acceptInput) were added there with defaults that leave Site's behaviour untouched.
    private TransformGizmo _gizmo;

    /// <summary>Which handles the gizmo shows. The Select rail's segmented control writes it.</summary>
    public TransformGizmo.Mode TransformMode { get; set; } = TransformGizmo.Mode.Move;

    /// <summary>True while the cursor is on a gizmo handle, or dragging one.</summary>
    /// <remarks>
    /// SelectTool checks this before picking: without it the click that grabs a handle also runs a
    /// selection raycast, which lands on whatever is behind the handle and swaps the selection out
    /// from under the drag that is starting.
    /// </remarks>
    public bool GizmoBusy => _gizmo != null && _gizmo.enabled && _gizmo.IsInteracting;

    // Opened lazily on the first delta of a drag and closed centrally on mouse-up, so a whole gesture
    // is one undo entry. Cleared on DragEnded.
    private bool _gizmoGesture;

    // The GameObject the gizmo is currently framing, so it is only re-targeted when it changes.
    private GameObject _gizmoTarget;

    private Rect _leftRect, _rightRect, _topRect, _timelineRect, _modeRect;

    // ---------------------------------------------------------------------------------------

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (residenceRenderer == null) residenceRenderer = FindFirstObjectByType<ResidenceRenderer>();
        if (viewController == null) viewController = FindFirstObjectByType<ViewController>();

        History = new EditHistory(this);
        _ctx = new ResidenceToolContext
        {
            Controller = this, Renderer = residenceRenderer, View = viewController,
            History = History, Cam = cam,
        };

        Register(new SelectTool());
        Register(new WallTool());
        Register(new OpeningTool());
        Register(new RoomTool());
        Register(new FurnitureTool());
        Register(new SensorTool());
        Register(new MonitorTool());
        Register(new CompareTool());
        Register(new MeasureTool());
        Register(new UnderlayTool());
        Register(new OutdoorTool());
        Register(new PeopleTool());

        _gizmo = gameObject.AddComponent<TransformGizmo>();
        // A room is not a site. The stock 2 m floor on the handle radius would draw a 0.51 m toilet's
        // gizmo four times the size of the toilet and clean across the bathroom.
        _gizmo.minHandleSize = 0.35f;
        // Yaw is the only rotation this app applies (OnGizmoRotate), so the X and Z rings were two
        // handles that did nothing when dragged.
        _gizmo.yawOnly = true;
        _gizmo.enabled = false;
        _gizmo.MoveDelta   += OnGizmoMove;
        _gizmo.RotateDelta += OnGizmoRotate;
        _gizmo.ScaleDelta  += OnGizmoResize;
        _gizmo.DragEnded   += OnGizmoDragEnded;
    }

    // Every way out goes through one question. Esc is the one people are told about, but the window's
    // X button and Alt+F4 are what somebody who does not know the app will actually reach for, and
    // those two are precisely the ones that used to discard unsaved work without a word.
    private void OnEnable()  => Application.wantsToQuit += OnWantsToQuit;
    private void OnDisable() => Application.wantsToQuit -= OnWantsToQuit;

    private bool OnWantsToQuit()
    {
#if UNITY_EDITOR
        // THE EDITOR IS NOT A WINDOW ANYBODY IS CLOSING. Unity raises this when play mode ends, so a
        // veto here would leave the Stop button doing nothing at all and the Editor needing to be
        // killed to get out. Let it straight through: in the Editor, Esc is how the card is reached,
        // and the X-button route is a build-only path by construction.
        return true;
#else
        if (_quitConfirmed) return true;   // already asked, already answered

        // Raised from Update, like every other deferred change: this can fire on any frame, and
        // opening the card rewrites what the whole window draws.
        _pendingExitPrompt = true;
        return false;                      // veto this one, and ask
#endif
    }

    private void Register(IResidenceTool tool) => _tools.Add(tool);

    private IResidenceTool FindTool(string id)
    {
        foreach (var t in _tools) if (t.Id == id) return t;
        return null;
    }

    private void Start()
    {
        _ = ResidenceStore.Settings;   // applies the unit preference before anything is formatted

        // First run only. Landing on an empty library means the first thing anyone has to do is the
        // hardest step of the workflow (import a plan and calibrate it) so the samples go in before
        // the list is read. Archiving one keeps it archived; see ResidenceSettings.samplesSeeded.
        SampleResidenceInstaller.SeedIfNeeded();
        // For installs that predate occupants: the samples are already on disk and the seeder will
        // never run again, so their households are filled in here instead.
        SampleResidenceInstaller.BackfillOccupants();
        // And for installs that predate any later fix to the plans themselves. Same reason again,
        // the seeder is one-shot, but generalised, so this one does not need rewriting next time.
        // Only samples nobody has started working on are touched; see SampleRefresh.
        SampleResidenceInstaller.RefreshStaleSamples();
        SampleResidenceInstaller.VerifyAgainstCatalog(residenceRenderer?.Catalog);
        SampleResidenceInstaller.VerifyAgainstCatalog(residenceRenderer?.Sensors);
        SampleResidenceInstaller.VerifyFloorFinishes(residenceRenderer?.MaterialPalette);

        RefreshLibrary();
        SetStage(ResidenceStage.Import);

        // Reopen whatever was last worked on: this is a tool people return to across sessions,
        // and landing on an empty library every time is needless friction.
        string last = ResidenceStore.Settings.lastOpenedResidenceId;
        if (!string.IsNullOrEmpty(last) && ResidenceStore.Exists(last)) OpenResidence(last);
    }

    private void Update()
    {
        SyncStages();

        if (_pendingStage.HasValue)
        {
            var next = _pendingStage.Value;
            _pendingStage = null;
            SetStage(next);
        }

        if (!string.IsNullOrEmpty(_pendingToolId))
        {
            var next = _pendingToolId;
            _pendingToolId = null;
            SetTool(FindTool(next));
        }

        if (_pendingLeftToggle) { _pendingLeftToggle = false; _leftCollapsed = !_leftCollapsed; }

        if (_pendingTimelineToggle) { _pendingTimelineToggle = false; TimelineExpanded = !TimelineExpanded; }

        if (_pendingVariantMenu) { _pendingVariantMenu = false; _variantMenuOpen = !_variantMenuOpen; }

        if (_pendingReport) { _pendingReport = false; StartReport(); }

        if (_pendingAlert.HasValue)
        {
            var alert = _pendingAlert.Value;
            _pendingAlert = null;
            GoToAlert(alert);
        }

        if (!string.IsNullOrEmpty(_pendingVariantId))
        {
            var next = _pendingVariantId;
            _pendingVariantId = null;
            if (Doc != null && next != Doc.activeVariantId) SetActiveVariant(next);
        }

        if (_pendingLevelIndex >= 0)
        {
            int next = _pendingLevelIndex;
            _pendingLevelIndex = -1;
            SetActiveLevel(next);
        }

        if (_pendingViewMode.HasValue)
        {
            var mode = _pendingViewMode.Value;
            _pendingViewMode = null;
            viewController?.SetMode(mode);
        }

        if (_pendingExitPrompt)
        {
            _pendingExitPrompt = false;
            RequestExitPrompt(ExitPrompt.Intent.Quit);
        }

        if (_pendingExitAnswer.HasValue)
        {
            var answer = _pendingExitAnswer.Value;
            _pendingExitAnswer = null;
            ApplyExitAnswer(answer);
        }

        if (!string.IsNullOrEmpty(_pendingOpenId) || _pendingNewResidence)
        {
            string id = _pendingOpenId;
            bool fresh = _pendingNewResidence;
            var then = _afterOpenStage;
            _pendingOpenId = null;
            _pendingNewResidence = false;
            _afterOpenStage = null;

            if (fresh) NewResidence(); else OpenResidence(id);

            // After the open, so it beats the stage the open picked for itself. NewResidence asks for
            // Structure through the same queue, and that request is still sitting in _pendingStage
            // waiting for the next frame, so it has to be dropped rather than merely outrun.
            if (then.HasValue)
            {
                _pendingStage = null;
                SetStage(then.Value);
            }
        }

        // BEFORE the Doc == null return below, and that placement is the point. An empty library is
        // the one state in which the Esc ladder has no rung to offer, and it is also the state
        // somebody is most likely to be in when they want out of the app. Esc did nothing at all
        // there until now.
        if (HandleExitKeys()) return;

        if (Doc == null) return;

        RefreshChangeCount();

        HandleGlobalKeys();

        // Before the tool, so a click that lands on a handle is claimed by the gizmo and SelectTool's
        // GizmoBusy check sees it in the same frame.
        TickGizmo();

        TryAutoSelect();

        // Ungated, and BEFORE the gated call: this is where a tool applies what its own rail queued,
        // and the cursor is over that rail at the moment the button was clicked.
        _active?.Tick();

        if (!PointerOverUI) _active?.HandleInput();

        // Central gesture close, matching EditController's convention: a whole drag collapses into
        // one undo entry regardless of which tool ran it.
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            History.EndGesture();

            // …and the same release closes a continuous furniture edit, whether it came from the
            // gizmo or from a slider in the rail.
            if (_furniturePending)
            {
                _furniturePending = false;
                CommitFurnitureEdit(SelectedFurniture());
            }

            // Belt and braces: DragEnded is the gizmo's own close, but a gesture that never came from
            // the gizmo must not leave this latched, or the next real handle drag skips its undo entry.
            _gizmoGesture = false;
        }
    }

    /// <summary>
    /// Clicking a chair, a grab bar or a resident selects it and carries the UI to the Select tab, from
    /// whatever tab you were on. Runs BEFORE the active tool, and claims the click so the tool does not
    /// also act on it.
    ///
    /// The whitelist is the load-bearing part. RoomMeshBuilder gives every floor a ResidenceElementMarker,
    /// so admitting Floor/Room/Ceiling would mean every click inside an existing room selected the
    /// floor instead of placing a wall corner: the feature would read as "the wall tool is broken".
    /// Walls are excluded too, because clicking a wall is precisely what the doors and windows tool is
    /// for. Openings are excluded for a second reason that now holds everywhere: an opening is a hole,
    /// not a thing you point at, so even in the Select stage a click on one resolves to its host wall,
    /// whose rail lists the openings in it. What is left is the set of things you can only inspect,
    /// never draw.
    /// </summary>
    private void TryAutoSelect()
    {
        _ctx.ClickConsumed = false;

        if (PointerOverUI || _stage == ResidenceStage.Select) return;
        if (_active != null && _active.ClaimsClicks) return;
        if (GizmoBusy) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        if (!_ctx.PickElement(out ResidenceElementMarker marker, out _)) return;
        if (!IsInspectable(marker.kind)) return;

        _ctx.ClickConsumed = true;
        Select(marker.kind, marker.id);   // queues the stage change for the next frame
    }

    private static bool IsInspectable(ResidenceElementMarker.Kind kind)
        => kind == ResidenceElementMarker.Kind.Furniture
        || kind == ResidenceElementMarker.Kind.WallMount
        // A smart home device joins the list for the same reason a grab bar is on it: it is a thing
        // you inspect, never a thing you draw. No tool places one by clicking empty floor. SensorTool
        // claims its own clicks, so admitting it cannot steal a gesture from anything.
        || kind == ResidenceElementMarker.Kind.Sensor
        || kind == ResidenceElementMarker.Kind.Occupant;

    /// <summary>Someone is in a text field, so letters and digits are text rather than shortcuts.</summary>
    // ViewController and the legacy EditController have each declared this for their own keys since
    // they were written; ResidenceEditController never did, and the gap was survivable only because the
    // app had almost nothing to type into. It does not survive the drag-number fields: every wall
    // thickness and mount height in the rail now takes keyboard focus, so typing "3" would also
    // switch stage and typing "z" would spin the selected chair a quarter turn.
    //
    // LATCHED IN OnGUI, read from Update: the same arrangement as PointerOverUI, and for the same
    // reason. GUIUtility.keyboardControl is only reliably the game view's focus state inside an OnGUI
    // pass; read from Update in the Editor it can answer for whichever IMGUI context last ran, which is
    // how typing a name in the rail also walked the camera. ViewController and every tool read this
    // property rather than GUIUtility directly.
    public static bool TypingInUI { get; private set; }

    private static void LatchTypingInUI()
        => TypingInUI = GUIUtility.keyboardControl != 0
                        || !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl());

    private void HandleGlobalKeys()
    {
        var kb = Keyboard.current;
        if (kb == null || TypingInUI) return;

        if ((kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed))
        {
            if (kb.zKey.wasPressedThisFrame) { History.Undo(); AfterHistoryJump(); }
            if (kb.yKey.wasPressedThisFrame) { History.Redo(); AfterHistoryJump(); }
            if (kb.sKey.wasPressedThisFrame) SaveResidence();
        }

        // Four rungs, in that order. Whatever the tool is holding but has not committed goes first (the
        // furniture tool's armed catalog item: with one armed, every click places, and until now
        // nothing put it down). Then deselect, which Esc has always meant and which is used constantly;
        // only once there is nothing selected does a press leave the Select tab and hand you back the
        // stage you were working in. Not gated on the pointer: Esc over the rail cancels too.
        //
        // The fourth rung asks to close the app, and it is reached only once the other three have
        // nothing left to do. That ordering is the whole safety of it: a press that had work to
        // cancel still cancels the work, so nobody meets this prompt mid-wall.
        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (_active != null && _active.Cancel()) { }
            else if (!string.IsNullOrEmpty(SelectedId)) ClearSelection();
            else if (_stage == ResidenceStage.Select && _stageBefore.HasValue) RequestStage(_stageBefore.Value);
            else RequestExitPrompt(ExitPrompt.Intent.Quit);
        }

        // F frames whatever is selected: a person, a wall, a room, an item. Same call the People
        // view's roster click makes, so the keyboard and the pointer answer "where is that" alike.
        if (kb.fKey.wasPressedThisFrame && !string.IsNullOrEmpty(SelectedId))
            FocusElement(SelectedId, OccupantName(SelectedId));

        // Digits pick a tool WITHIN the active stage, so the count is small, the numbers match the
        // chips you can see, and every tool has a key: the flat 1-6 this replaced left the seventh
        // tool (Import) unreachable from the keyboard. Ctrl+digit moves between stages.
        //
        // Eight digits, not seven. The count is "however many stages VisibleStages can return", and
        // it grew again when Smart living landed: seven core stages, eight with the exterior layer on. A
        // ladder one rung short leaves the last stage unreachable from the keyboard, which is the
        // exact gap this scheme was built to close and would silently reopen.
        int digit = -1;
        if (kb.digit1Key.wasPressedThisFrame) digit = 0;
        else if (kb.digit2Key.wasPressedThisFrame) digit = 1;
        else if (kb.digit3Key.wasPressedThisFrame) digit = 2;
        else if (kb.digit4Key.wasPressedThisFrame) digit = 3;
        else if (kb.digit5Key.wasPressedThisFrame) digit = 4;
        else if (kb.digit6Key.wasPressedThisFrame) digit = 5;
        else if (kb.digit7Key.wasPressedThisFrame) digit = 6;
        else if (kb.digit8Key.wasPressedThisFrame) digit = 7;
        if (digit < 0) return;

        if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)
        {
            if (digit < _stages.Count) SetStage(_stages[digit]);
        }
        else
        {
            var ids = ResidenceWorkflow.ToolIdsFor(_stage);
            if (digit < ids.Length) SetTool(FindTool(ids[digit]));
        }
    }

    // ---------------------------------------------------------------------------------------
    // Transform handles on the selected item
    // ---------------------------------------------------------------------------------------

    private void TickGizmo()
    {
        if (_gizmo == null) return;

        var item = SelectedFurniture();

        // Only while the pointer tool is active. Handles that stayed live under the wall tool would
        // swallow the click that starts a wall wherever they happened to overlap it.
        bool wanted = item != null
                      && _active != null && _active.Id == "select"
                      && !(Variant?.locked ?? true)
                      && viewController != null
                      && viewController.Current != ViewController.Mode.Walkthrough;

        if (!wanted)
        {
            if (_gizmo.enabled) { _gizmo.enabled = false; _gizmo.Clear(); _gizmoTarget = null; }
            return;
        }

        // Re-acquired every frame and never cached, for the reason SelectionOverlay states in its
        // header: Rebuild() destroys and respawns every GameObject, and undo swaps the whole ResidenceDoc
        // without telling anyone, so a held reference is a stale object pointing at nothing.
        var go = residenceRenderer != null ? residenceRenderer.GetGO(item.instanceId) : null;
        if (go == null)
        {
            if (_gizmo.enabled) { _gizmo.enabled = false; _gizmo.Clear(); _gizmoTarget = null; }
            return;
        }

        _gizmo.enabled = true;
        // Re-targeted only when the object actually changes: SetTarget re-runs
        // GetComponentsInChildren, which would allocate a fresh array every frame otherwise.
        if (!ReferenceEquals(go, _gizmoTarget)) { _gizmoTarget = go; _gizmo.SetTarget(go, cam); }
        _gizmo.SetMode(TransformMode);

        // Shift means FREE, here as everywhere else in ResidenceViz. The ring used to invert that (the
        // Site tool's convention: free by default, Shift to snap), which left the app with two rules
        // for one key. Now the ring steps like the Facing field and Shift releases both.
        _gizmo.rotationSnap = _ctx.ShiftHeld ? 0f : ROTATION_SNAP_DEG;

        // Not gated outright on PointerOverUI: ViewController gates only the START of a camera drag,
        // so a look or a pan already in flight carries on across a rail, and the handles have to keep
        // up with it rather than freeze in a stale pose. (While a look holds the cursor captured this
        // reads as false anyway; the pointer sits at the center of the screen.)
        _gizmo.Tick(acceptInput: !PointerOverUI);
    }

    private const float ROTATION_SNAP_DEG = ResidenceConventions.FACING_STEP_DEG;

    /// <summary>The selected item, when the selection is a piece of floor furniture.</summary>
    private ObjectInstance SelectedFurniture()
    {
        if (SelectedKind != ResidenceElementMarker.Kind.Furniture || string.IsNullOrEmpty(SelectedId)) return null;

        var list = Level?.furniture;
        if (list == null) return null;

        foreach (var f in list) if (f != null && f.instanceId == SelectedId) return f;
        return null;
    }

    // The three deltas all follow the same shape, and it is the shape EditController has used for site
    // instances all along: write the def AND the live GameObject, and do NOT re-render. Ctx.Changed()
    // means a full Rebuild(), which would allocate a material per item per frame and destroy the very
    // object the gizmo is dragging. The rebuild happens once, on release.

    // Where the cursor has actually dragged the item to, before snapping. Accumulated raw for the
    // reason TransformGizmo accumulates _rotRaw: snapping the stored position and then adding the
    // next delta to it would make the wall sticky, the item unable to leave until the cursor had
    // travelled the whole snap range again.
    private Vector2 _moveRaw;

    private void OnGizmoMove(Vector3 delta)
    {
        var item = SelectedFurniture();
        if (item?.position == null || item.position.Length < 3) return;

        if (!_gizmoGesture) _moveRaw = new Vector2(item.position[0], item.position[2]);
        BeginGizmoGesture("Move furniture");

        // Furniture stands on the floor. The gizmo offers a Y arrow because a site object can sit on a
        // slope; here the story's elevation is the only legal height, so the vertical is dropped
        // rather than hidden: an item half-sunk into its own floor is not a placement.
        _moveRaw.x += delta.x;
        _moveRaw.y += delta.z;

        Vector2 at = SnapToWall(_moveRaw, item, item.rotationY);
        item.position[0] = at.x;
        item.position[2] = at.y;

        residenceRenderer?.PoseFurnitureGO(item);
    }

    /// <summary>
    /// The placement rule shared by the ghost and the move handle: flush against a wall face within
    /// <see cref="ResidenceConventions.FURNITURE_SNAP_RANGE"/>, Shift for free, Ctrl to pull from as
    /// far as <see cref="ResidenceConventions.MOUNT_REACH"/>. Shift wins when both are held.
    /// </summary>
    public Vector2 SnapToWall(Vector2 raw, ObjectInstance item, float yaw)
    {
        Vector3 size = FurnitureSize(item);
        return SnapToWall(raw, size.x, size.z, yaw);
    }

    public Vector2 SnapToWall(Vector2 raw, float widthM, float depthM, float yaw)
    {
        if (Level == null || _ctx.ShiftHeld) return raw;
        float range = _ctx.CtrlHeld ? ResidenceConventions.MOUNT_REACH
                                    : ResidenceConventions.FURNITURE_SNAP_RANGE;
        return FurnitureSnap.ToWall(raw, widthM, depthM, yaw, Level, range).position;
    }

    private void OnGizmoRotate(Vector3 eulerDelta)
    {
        var item = SelectedFurniture();
        if (item == null) return;

        // Yaw only. A tipped-over bed is not a proposal, and every footprint in this app. FurnitureFit,
        // ResidenceMetrics, SelectionOverlay, the occupancy checks. Is computed from rotationY alone, so an
        // X or Z tilt would show on screen and in none of the numbers.
        BeginGizmoGesture("Rotate furniture");
        RotateFurniture(item, item.rotationY + eulerDelta.y, discrete: false);
    }

    // ---- turning, and staying against the wall -------------------------------------------------

    // Which wall, or which corner, the item was flush with when the current edit began, read ONCE
    // before the first write. A bed against a wall that turns a quarter about its center has swapped
    // width and depth, and the gap that opens (or the overlap that closes) is half their difference,
    // far more than any snap range; the only way to keep it flush is to remember the wall and snap
    // back to it on commit. A bed in a CORNER needs both walls remembered, or the turn seats it on the
    // one and drives it through the other. `_flushChecked` is its own field because Against
    // legitimately answers nothing and "checked, not flush" has to be told apart from "not checked
    // yet".
    private bool _flushChecked;
    private string _flushWallId;
    private string _flushWallId2;

    /// <summary>
    /// Remembers the wall, or corner, <paramref name="item"/> sits flush against, once per edit. Every
    /// path that changes an item's shape (turn, resize) calls this before its first write; the commit
    /// reads it.
    /// </summary>
    // Not called from OnGizmoMove on purpose: a move is snapped every frame already, so the commit
    // sees nothing captured and just refits.
    public void CaptureFlush(ObjectInstance item)
    {
        if (_flushChecked || item?.position == null || item.position.Length < 3 || Level == null) return;
        _flushChecked = true;

        Vector3 size = FurnitureSize(item);
        var flush = FurnitureSnap.Against(new Vector2(item.position[0], item.position[2]),
                                          size.x, size.z, item.rotationY, Level,
                                          ResidenceConventions.FURNITURE_FLUSH_TOL);
        _flushWallId = flush.wall?.id;
        _flushWallId2 = flush.wall2?.id;
    }

    /// <summary>
    /// The one place a furniture item's yaw is written: the keys, the quarter buttons, the Facing
    /// field and the ring all come here, so all four keep the item against its wall.
    /// </summary>
    /// <param name="discrete">True for a key or a button (one undo entry, committed now); false for a
    /// drag (a gesture already opened by the caller, committed on release).</param>
    public void RotateFurniture(ObjectInstance item, float newYaw, bool discrete)
    {
        if (item == null) return;

        CaptureFlush(item);
        if (discrete) RecordDocEdit("Rotate furniture");

        item.rotationY = Mathf.Repeat(newYaw, 360f);

        if (discrete)
        {
            CommitFurnitureEdit(item);
        }
        else
        {
            residenceRenderer?.PoseFurnitureGO(item);
            NoteFurnitureEdit();
        }
    }

    private void OnGizmoResize(float delta)
    {
        var item = SelectedFurniture();
        if (item == null) return;

        CaptureFlush(item);
        BeginGizmoGesture("Resize furniture");

        // The gizmo emits an ADDITIVE scale delta, because a site instance carries a uniform `scale`
        // multiplier. ResidenceViz has no such field in play. boxSizeMeters is the item's true size and is
        // what everything downstream measures, so the delta is applied as a proportional factor and
        // the real dimensions are what change.
        Vector3 size = FurnitureSize(item) * Mathf.Max(0.05f, 1f + delta);
        SetFurnitureSize(item, size);

        residenceRenderer?.PoseFurnitureGO(item);
    }

    private void OnGizmoDragEnded()
    {
        _gizmoGesture = false;
        NoteFurnitureEdit();
    }

    /// <summary>
    /// Flags that a continuous edit is in flight on the selected item, so releasing the mouse
    /// re-fits and rebuilds it once.
    /// </summary>
    /// <remarks>
    /// Both continuous paths end here (the gizmo drag and the rail's sliders) because the rail is
    /// drawn from OnGUI with the cursor over it, where a tool's HandleInput never runs and a slider
    /// therefore has no release event of its own to hang the commit on. Update owns the release.
    /// </remarks>
    public void NoteFurnitureEdit() => _furniturePending = true;

    private bool _furniturePending;

    /// <summary>
    /// Ends one edit to a furniture item: slide it clear of anything it now blocks, mark the
    /// document dirty, and rebuild the furniture: not the whole residence, which did not change.
    /// </summary>
    public void CommitFurnitureEdit(ObjectInstance item)
    {
        if (item == null) { _flushChecked = false; _flushWallId = null; _flushWallId2 = null; return; }

        // Back against the wall, or the corner, it started on, BEFORE the fit: the fit slides along
        // the wall, so flushness survives it, while the other order would let the fit undo the snap.
        // Shift held at the release means free, the same as it does mid-drag.
        if (_flushWallId != null && !_ctx.ShiftHeld && Level != null
            && item.position != null && item.position.Length >= 3)
        {
            Vector3 size = FurnitureSize(item);
            var snap = FurnitureSnap.ToWall(new Vector2(item.position[0], item.position[2]),
                                            size.x, size.z, item.rotationY, Level,
                                            float.PositiveInfinity, _flushWallId, _flushWallId2);
            item.position[0] = snap.position.x;
            item.position[2] = snap.position.y;
        }
        _flushChecked = false;
        _flushWallId = null;
        _flushWallId2 = null;

        RefitFurniture(item);
        MarkDirty();
        residenceRenderer?.RebuildFurniture();
    }

    private void BeginGizmoGesture(string label)
    {
        if (_gizmoGesture) return;
        _gizmoGesture = true;
        // Idempotent until EndGesture, which Update already calls on every left-button release, so the
        // whole drag collapses into one undo entry.
        History?.BeginGesture(EditHistory.Scope.Environment, label);
    }

    /// <summary>
    /// Slides an item clear of any opening it is tall enough to reach, after it has been moved,
    /// turned or resized. Reports in the status line when it had to move.
    /// </summary>
    /// <remarks>
    /// Deliberately run on RELEASE, not per frame. FurnitureFit slides rather than refuses, and an
    /// item that jumped aside mid-drag would be fighting the cursor that is still holding it. Growing
    /// or turning an item is just as capable of reaching into a doorway as moving it, which is why all
    /// three paths end here.
    /// </remarks>
    public void RefitFurniture(ObjectInstance item)
    {
        if (item?.position == null || item.position.Length < 3 || Level == null) return;

        Vector3 size = FurnitureSize(item);
        var fit = FurnitureFit.Fit(new Vector2(item.position[0], item.position[2]),
                                   FurnitureFit.Footprint(size.x, size.z, item.rotationY),
                                   size.y,
                                   Level);

        item.position[0] = fit.position.x;
        item.position[2] = fit.position.y;

        var entry = residenceRenderer?.EntryFor(item.prefabType);
        string label = entry != null ? entry.Label : "Item";

        if (!fit.ok) Status($"{label} does not fit cleanly: {fit.reason}");
        else if (fit.moved) Status(fit.reason);
    }

    /// <summary>The item's true size in meters, from the instance or the catalog behind it.</summary>
    public Vector3 FurnitureSize(ObjectInstance item)
    {
        if (item?.boxSizeMeters != null && item.boxSizeMeters.Length >= 3)
            return new Vector3(item.boxSizeMeters[0], item.boxSizeMeters[1], item.boxSizeMeters[2]);

        var entry = residenceRenderer?.EntryFor(item?.prefabType);
        return entry != null ? entry.SizeMeters : new Vector3(0.6f, 0.8f, 0.6f);
    }

    /// <summary>Writes a true size back, floored so an item can never be scaled out of existence.</summary>
    public void SetFurnitureSize(ObjectInstance item, Vector3 size)
    {
        if (item == null) return;
        item.boxSizeMeters = new[]
        {
            Mathf.Max(MIN_ITEM_SIZE, size.x),
            Mathf.Max(MIN_ITEM_SIZE, size.y),
            Mathf.Max(MIN_ITEM_SIZE, size.z),
        };
    }

    public const float MIN_ITEM_SIZE = 0.05f;

    /// <summary>The far end of the same range, for a field that types a size in from nothing.</summary>
    /// <remarks>
    /// A resize drag needs no ceiling because it starts from a real item and moves by proportion.
    /// Typing does: 4 m is longer than anything a dwelling holds loose (the longest catalog item is a
    /// 2.13 m hospital bed) and short enough that a slipped decimal point is caught at the field
    /// rather than discovered as an object covering the floor plan.
    /// </remarks>
    public const float MAX_ITEM_SIZE = 4f;

    // ---------------------------------------------------------------------------------------
    // Document lifecycle
    // ---------------------------------------------------------------------------------------

    public void RefreshLibrary() => _library = ResidenceStore.List();

    // ---------------------------------------------------------------------------------------
    // Leaving: the exit prompt
    //
    // Four acts drop unsaved work: quitting, opening another residence, starting a new one and
    // importing one. The last three all end in AfterOpen, which clears Dirty. They ask one question
    // through one prompt, because they ARE one question.
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Raise the prompt. Safe to call from anywhere including OnGUI: it only sets flags, and the
    /// prompt itself is drawn from OnGUI and applied from Update.
    /// </summary>
    private void RequestExitPrompt(ExitPrompt.Intent intent, string targetId = null)
    {
        if (ExitPromptOpen) return;

        _exitIntent = intent;
        _exitTargetId = targetId;
        ExitPromptOpen = true;

        // Latched, and never re-read while the card is up. The muted line naming the residence is
        // drawn only when there is something to lose, so a Dirty that changed between a frame's
        // layout pass and its repaint pass would change the card's control count: the exact fault
        // every _pending* flag in this file exists to avoid. Nothing can dirty the document behind
        // the card anyway, which is what makes latching free.
        _exitDirty = Doc != null && Dirty;

        // One armed confirmation at a time, and the rail it lives in is about to go grey.
        _confirmReset = false;

        // The card takes the keyboard. See ExitPrompt.Reset.
        _exitPrompt.Reset();
    }

    /// <summary>
    /// The prompt's own keys, and the one Esc case the three-rung ladder cannot reach. Returns true
    /// when this frame belongs to the prompt and the rest of Update should not run.
    ///
    /// Called BEFORE Update's Doc == null return. See the call site.
    /// </summary>
    private bool HandleExitKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return ExitPromptOpen;

        // The file browser is a uGUI canvas from SimpleFileBrowser and the only other blocking UI in
        // the app. It is not in PointerOverUI and it owns Esc while it is up, so stacking a second
        // modal behind it would leave two dialogs fighting over one key.
        if (FileBrowser.IsOpen) return ExitPromptOpen;

        if (ExitPromptOpen)
        {
            // Esc closes what Esc opened, which is what Esc means everywhere else in this app.
            if (kb.escapeKey.wasPressedThisFrame)
                _pendingExitAnswer = ExitPrompt.Answer.KeepWorking;
            // Enter takes the answer that loses nothing. A blind confirm should land on the safe one.
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                _pendingExitAnswer = ExitPrompt.Answer.SaveAndGo;

            return true;
        }

        // With no residence open the ladder in HandleGlobalKeys never runs at all, because Update
        // returns above it. This is that gap: an empty library still answers Esc.
        if (Doc == null && !TypingInUI && kb.escapeKey.wasPressedThisFrame)
        {
            RequestExitPrompt(ExitPrompt.Intent.Quit);
            return true;
        }

        return false;
    }

    private void ApplyExitAnswer(ExitPrompt.Answer answer)
    {
        if (answer == ExitPrompt.Answer.KeepWorking)
        {
            // The follow-up stage belonged to the open that was just declined. Left set, it would
            // land on whichever open happened next.
            _afterOpenStage = null;
            CloseExitPrompt();
            return;
        }

        // Gated on Dirty, not just on the answer. ResidenceStore.Save bumps doc.version and rewrites
        // the file, so saving a document that is already on disk would quietly advance the number the
        // library row prints for no change at all. The button is still offered either way, because it
        // is the same three answers every time and a button that appears and vanishes is worse.
        if (answer == ExitPrompt.Answer.SaveAndGo && _exitDirty && !SaveResidence())
        {
            // The write failed and Status is already saying why, over the scrim. Leave the card up:
            // the three answers are still the right three, and going on from here would destroy
            // exactly the work this answer promised to keep.
            return;
        }

        var intent = _exitIntent;
        string target = _exitTargetId;
        CloseExitPrompt();

        switch (intent)
        {
            case ExitPrompt.Intent.OpenResidence:   _pendingOpenId = target; break;
            case ExitPrompt.Intent.NewResidence:    _pendingNewResidence = true; break;
            case ExitPrompt.Intent.ImportResidence: ImportResidence(); break;
            default:                                Quit(); break;
        }
    }

    private void CloseExitPrompt()
    {
        ExitPromptOpen = false;
        _exitTargetId = null;
        _exitDirty = false;
    }

    private void Quit()
    {
        // Read by the wantsToQuit handler, which Application.Quit is about to call. Without it that
        // handler would veto our own quit and put the card straight back up.
        _quitConfirmed = true;

#if UNITY_EDITOR
        // Application.Quit does nothing under the Editor, so the button would read as broken in the
        // one place this gets developed.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// A library row, or "New residence". Both swap the document, so both ask first when there is
    /// unsaved work, and both queue rather than act: they rebuild the rail they were clicked in.
    /// </summary>
    private void RequestOpenResidence(string id)
    {
        if (Dirty) RequestExitPrompt(ExitPrompt.Intent.OpenResidence, id);
        else _pendingOpenId = id;
    }

    /// <param name="then">
    /// A stage to land on once the residence exists, overriding the Structure that NewResidence
    /// chooses. The empty library's "Start from a floor plan" is the one caller: it has to win over
    /// NewResidence's own choice, and it can no longer do that by simply running second.
    /// </param>
    private void RequestNewResidence(ResidenceStage? then = null)
    {
        _afterOpenStage = then;
        if (Dirty) RequestExitPrompt(ExitPrompt.Intent.NewResidence);
        else _pendingNewResidence = true;
    }

    /// <summary>
    /// Import asks BEFORE the file browser opens. Asking afterwards would mean putting this prompt
    /// up over a document already chosen, and two decisions stacked on one action.
    /// </summary>
    private void RequestImportResidence()
    {
        if (Dirty) RequestExitPrompt(ExitPrompt.Intent.ImportResidence);
        else ImportResidence();
    }

    public void NewResidence()
    {
        Doc = ResidenceStore.Create("Untitled residence");
        AfterOpen();
        // The starter room is already there, so the next move is the wall, opening and room tools.
        // The empty-library button asks for Import after this and wins by running second.
        RequestStage(ResidenceStage.Structure);
        Status("Created " + Doc.name);
    }

    public void OpenResidence(string id)
    {
        var doc = ResidenceStore.Load(id);
        if (doc == null) { Status("Could not open that residence."); return; }
        Doc = doc;
        AfterOpen();
        Status("Opened " + Doc.name);
    }

    private void AfterOpen()
    {
        Dirty = false;
        History.Clear();
        ClearSelection();
        _confirmReset = false;
        _levelIndex = 0;   // a residence always opens on its ground floor
        RefreshLibrary();
        SyncStages();   // this residence may or may not have an exterior; the command bar follows it

        residenceRenderer?.RenderResidence(Doc, Doc.activeVariantId, LevelIndex);
        SyncUnderlayQuad();
        viewController?.FrameContent();

        ResidenceStore.Settings.lastOpenedResidenceId = Doc.id;
        ResidenceStore.SaveSettings();

        ResetShadow();   // a different document: the old baseline's picture describes nothing here
    }

    /// <summary>
    /// The sketch quad belongs to UnderlayTool and outlives that tool's Exit so plans can be traced
    /// over from every stage, but the tool only refreshes it while active. Anything that swaps the
    /// document or the story out from under an inactive tool must call this, or the previous
    /// floorplan stays on the ground.
    /// </summary>
    private void SyncUnderlayQuad() => (FindTool("underlay") as UnderlayTool)?.SyncQuad();

    /// <summary>
    /// Writes the open residence. Returns whether it reached disk.
    ///
    /// The return value exists for save-and-exit. A failed write used to vanish into a four second
    /// toast, which is survivable while the app stays open and is not survivable at all if the next
    /// thing that happens is Application.Quit.
    /// </summary>
    public bool SaveResidence()
    {
        if (Doc == null) return true;   // nothing to write is not a failure
        if (ResidenceStore.Save(Doc, out string err))
        {
            Dirty = false;
            RefreshLibrary();
            Status("Saved " + Doc.name);
            return true;
        }

        Status("Save failed: " + err);
        return false;
    }

    /// <summary>
    /// Every mutation ends here, which is what makes it the one place a base environment edit can be
    /// carried into the proposals. Synchronous on purpose: SetActiveVariant is called straight from
    /// OnGUI and renders at once, RecordBefore snapshots the whole document before the NEXT edit, and
    /// SaveResidence writes whatever is there. A flag drained next frame would let all three see
    /// proposals the base had already moved past.
    /// </summary>
    public void MarkDirty()
    {
        Dirty = true;
        SyncProposals();
    }

    // The baseline as it stood after the last edit anyone saw. VariantSync diffs the live baseline
    // against it and carries the difference into every proposal. Null while the residence has no
    // proposal to carry anything into.
    private VariantDef _baselineShadow;

    private void SyncProposals()
    {
        if (Doc == null) { _baselineShadow = null; return; }

        var baseline = ResidenceStore.Baseline(Doc);
        if (baseline == null || !HasProposal(Doc)) { _baselineShadow = null; return; }

        // Nothing to measure against yet: take the picture and wait for the next edit.
        if (_baselineShadow == null || _baselineShadow.id != baseline.id)
        {
            _baselineShadow = VariantSync.Snapshot(baseline);
            return;
        }

        // Re-snapshot whenever the baseline moved, whether or not a proposal took the change (a
        // proposal that kept its own version of an element must not see that base edit again).
        if (VariantSync.PropagateAll(_baselineShadow, baseline, Doc.variants) > 0)
            _baselineShadow = VariantSync.Snapshot(baseline);
    }

    /// <summary>Forget the shadow and take a fresh one: the document under it was swapped wholesale.</summary>
    private void ResetShadow()
    {
        _baselineShadow = null;
        SyncProposals();
    }

    private static bool HasProposal(ResidenceDoc doc)
    {
        if (doc?.variants == null) return false;
        foreach (var v in doc.variants) if (v != null && !v.isBaseline) return true;
        return false;
    }

    public void SetActiveVariant(string variantId)
    {
        if (Doc == null) return;
        Doc.activeVariantId = variantId;
        ClearSelection();
        History.Clear();   // undo does not span variants: each is its own editing context
        SyncProposals();   // before the render, so a proposal comes up already carrying the base's edits
        residenceRenderer?.RenderResidence(Doc, variantId, LevelIndex);
        MarkDirty();
    }

    /// <summary>Queues a variant switch for the next frame. Use this from anything drawn in OnGUI.</summary>
    // Switching tears down and rebuilds every GameObject in the residence and clears the undo stack, so
    // doing it mid-OnGUI leaves the frame's repaint pass drawing a rail that describes a document the
    // layout pass never saw.
    public void RequestVariant(string variantId) => _pendingVariantId = variantId;

    private string _pendingVariantId;

    /// <summary>
    /// Switches story. The exact shape of SetActiveVariant, for the same reasons: a different floor
    /// is a different set of GameObjects, the selection cannot survive it, and undo does not span it.
    /// </summary>
    public void SetActiveLevel(int index)
    {
        if (Doc == null) return;
        int count = LevelCount;
        if (count == 0) return;

        index = Mathf.Clamp(index, 0, count - 1);
        if (index == _levelIndex) return;

        _levelIndex = index;
        ClearSelection();
        History.Clear();
        residenceRenderer?.RenderResidence(Doc, Doc.activeVariantId, index);
        SyncUnderlayQuad();   // each story has its own sketch, and the floor chip works from any stage
        viewController?.FrameContent();
        Status("Now on " + LevelName(index) + ".");
    }

    /// <summary>Deferred, like every other request drawn in OnGUI: this rebuilds the whole residence.</summary>
    public void RequestLevel(int index) => _pendingLevelIndex = index;

    private int _pendingLevelIndex = -1;

    /// <summary>A story's name, or a generated one when it has none.</summary>
    public string LevelName(int index)
    {
        var levels = Variant?.levels;
        if (levels == null || index < 0 || index >= levels.Count) return ResidenceStore.DefaultLevelName(index);
        var l = levels[index];
        return string.IsNullOrEmpty(l?.name) ? ResidenceStore.DefaultLevelName(index) : l.name;
    }

    /// <summary>
    /// Adds a story to every variant and switches to it. Deliberately NOT gated on the baseline being
    /// unlocked. See ResidenceStore.AddLevel for why an empty story asserts nothing about the residence. What
    /// you then draw on it is gated, exactly as it is on the ground floor.
    /// </summary>
    public void AddLevel(string name = null)
    {
        if (Doc == null) return;
        RecordDocEdit("Add a floor");
        int index = ResidenceStore.AddLevel(Doc, string.IsNullOrWhiteSpace(name) ? null : name.Trim());
        MarkDirty();
        _levelIndex = index;
        ClearSelection();
        residenceRenderer?.RenderResidence(Doc, Doc.activeVariantId, index);
        Status("Added " + LevelName(index) + ".");
    }

    public void RemoveLevel(int index)
    {
        if (Doc == null) return;
        string name = LevelName(index);

        RecordDocEdit("Remove a floor");
        if (!ResidenceStore.RemoveLevel(Doc, index, out string error)) { Status(error); return; }

        MarkDirty();
        _levelIndex = Mathf.Clamp(_levelIndex, 0, Mathf.Max(0, LevelCount - 1));
        ClearSelection();
        residenceRenderer?.RenderResidence(Doc, Doc.activeVariantId, LevelIndex);
        Status("Removed " + name + ".");
    }

    // ---------------------------------------------------------------------------------------
    // Tools
    // ---------------------------------------------------------------------------------------

    public void SetTool(IResidenceTool tool)
    {
        if (tool == null || _active == tool) return;
        _active?.Exit();
        _active = tool;
        _active?.Enter(_ctx);

        // Picking a tool by any route (hotkey, another panel) carries the rail to the stage it lives
        // in, so the chips never disagree with what is actually active.
        _stage = ResidenceWorkflow.StageOf(tool.Id, _stage);
    }

    public IResidenceTool ActiveTool => _active;

    public void SetStage(ResidenceStage stage)
    {
        // Remember where we came from, but only on the way IN to Select. Arriving at Select from
        // Select (re-clicking the tab) must not overwrite the way back with Select itself.
        if (stage == ResidenceStage.Select) { if (_stage != ResidenceStage.Select) _stageBefore = _stage; }
        else _stageBefore = null;

        bool leavingPeople = _stage == ResidenceStage.People && stage != ResidenceStage.People;
        bool leavingReview = _stage == ResidenceStage.Review && stage != ResidenceStage.Review;
        _stage = stage;
        // Review used to open the "Design options" foldout here. It no longer needs to: comparing is
        // CompareTool now, and Review opens on it because it is first in the stage's tool array.
        // Same argument for People: the household at a glance is what the stage is for, so the timeline
        // opens to its full gantt, and folds back to the strip when you leave, because a 300 px gantt
        // over the bottom of every other stage was a panel you had to remember to put away. The
        // chevron still opens and closes it from anywhere.
        if (stage == ResidenceStage.People) TimelineExpanded = true;
        else if (leavingPeople) TimelineExpanded = false;
        // The before/after ghost is Review's: CompareTool turns it on as it enters, and leaving the
        // stage turns it off, so proposals are not edited under a red-and-green overlay. Here rather
        // than in CompareTool.Exit so Compare ↔ Measure within Review keeps it.
        if (leavingReview) residenceRenderer?.SetGhostVariant(null, false);
        SetTool(FindTool(ResidenceWorkflow.PrimaryToolId(stage)));
    }

    /// <summary>Queues a stage change for the next frame. Use this from anything drawn in OnGUI.</summary>
    public void RequestStage(ResidenceStage stage) => _pendingStage = stage;

    /// <summary>
    /// Queues a TOOL change for the next frame, by id. Deferred for the reason every other pending
    /// flag in this file is: switching the tool swaps the whole rail, so doing it mid-OnGUI changes
    /// the control count between the layout pass and the repaint pass.
    /// </summary>
    public void RequestTool(string toolId) => _pendingToolId = toolId;

    private string _pendingToolId;

    /// <summary>
    /// Keeps the stage list in step with the document. Outdoors appears and disappears with
    /// ResidenceDoc.exteriorEnabled. Runs once per frame from Update, never from OnGUI, so the command bar
    /// has the same number of buttons in a frame's layout pass and its repaint pass.
    /// </summary>
    private void SyncStages()
    {
        bool wantOutdoors = ResidenceWorkflow.OutdoorsUI && Doc != null && Doc.exteriorEnabled;
        if (_stages != null && wantOutdoors == _stagesIncludeOutdoors) return;

        _stages = ResidenceWorkflow.VisibleStages(Doc);
        _stagesIncludeOutdoors = wantOutdoors;
        if (!_stages.Contains(_stage)) RequestStage(ResidenceStage.Structure);
    }

    // ---------------------------------------------------------------------------------------
    // EditHistory.IHost: the whole ResidenceDoc is the undo unit
    // ---------------------------------------------------------------------------------------

    public string ActiveContextId(EditHistory.Scope scope)
        => scope == EditHistory.Scope.Environment ? Doc?.id : null;

    public string Serialize(EditHistory.Scope scope, string contextId)
    {
        if (scope != EditHistory.Scope.Environment || Doc == null || Doc.id != contextId) return null;
        return JsonConvert.SerializeObject(Doc);
    }

    public void Restore(EditHistory.Scope scope, string contextId, string json)
    {
        if (scope != EditHistory.Scope.Environment || json == null) return;

        var restored = JsonConvert.DeserializeObject<ResidenceDoc>(json);
        if (restored == null) return;

        ResidenceStore.Migrate(restored);
        Doc = restored;
        // The snapshot was taken before the edit, so it already holds the proposals as they were
        // before that edit was carried into them: undo takes both back in one step, and the shadow
        // must describe the restored baseline before AfterHistoryJump's MarkDirty diffs against it.
        ResetShadow();
        ClearSelection();
        residenceRenderer?.RenderResidence(Doc, Doc.activeVariantId, LevelIndex);
    }

    // ---------------------------------------------------------------------------------------
    // UI
    // ---------------------------------------------------------------------------------------

    private void OnGUI()
    {
        UITooltip.BeginFrame();
        LatchTypingInUI();
        // An exception thrown mid-layout last frame would otherwise leave a width pushed and mis-size
        // every panel from here on. Cheap, and it makes the stack self-healing.
        UITheme.ResetWidths();

        float w = Screen.width, h = Screen.height;
        float leftW = LeftWidth;

        // The timeline runs the full width along the bottom. Full width, not the gap between the rails:
        // a 24-hour chart in the 720 px between them gets 30 px an hour, and across the whole window it
        // gets 53. The chart is the content. Give it the pixels.
        //
        // Sized here so the rect is known before the pointer test. A panel the pointer test does not
        // know about is a panel every click falls straight through, and for this one that would mean
        // scrubbing the clock also orbited the camera.
        // Expanded, the bar is exactly as tall as its roster. Ruler, one row per person, the alert
        // lane and the transport: up to just over half the window, past which the rows scroll. It
        // used to have a 260 px floor, so one resident got a gantt with a third of it empty.
        float barH = Doc == null ? 0f
                   : TimelineExpanded
                       ? Mathf.Min(TimelineBar.ExpandedHeight(RosterCount()), h * 0.55f)
                       : TimelineBar.CollapsedHeight;
        _timelineRect = barH > 0f ? new Rect(0f, h - barH, w, barH) : Rect.zero;

        // The rails always stop at the COLLAPSED height, whatever the bar is doing. Expanding then only
        // draws over their lower ends rather than re-laying-out everything else on screen.
        float railBottom = Doc == null ? 0f : TimelineBar.CollapsedHeight;

        // THE COMMAND BAR SPANS THE WHOLE WINDOW, and the rails start beneath it. It used to be the
        // gap between them, which at 1280 is 720 px and 692 once Inset has taken its padding, while
        // the seven stage tabs want 448 and what sits beside them (the view modes, the eye-height
        // chip, Undo and Redo) wants about 500. BeginArea clips rather than scrolls, so the right-hand
        // end simply vanished. Full width gives the same bar 1252 px at the same window size.
        //
        // This is also the shared convention rather than a new one: UITheme.RailTop exists for exactly
        // this shape and the Site tool has always used it.
        _topRect = new Rect(0f, 0f, w, TopBarHeight);
        _leftRect = new Rect(0f, TopBarHeight, leftW, h - TopBarHeight - railBottom);
        _rightRect = new Rect(w - rightRailWidth, TopBarHeight,
                              rightRailWidth, h - TopBarHeight - railBottom);

        // Directly under the command bar and between the rails, so it reads as one piece of chrome
        // with the tabs rather than as a panel over the scene. Sized here, before the pointer test:
        // a panel that test does not know about is a panel every click falls straight through, which
        // for this one would mean pressing "Propose a change" also placed a wall.
        float bandH = ModeBand.HeightFor(Doc, _variantMenuOpen);
        _modeRect = bandH > 0f
            ? new Rect(leftW, TopBarHeight, w - leftW - rightRailWidth, bandH)
            : Rect.zero;

        Vector2 m = Event.current.mousePosition;
        // The prompt claims the WHOLE window, which is the same rule every other panel here follows,
        // applied to a panel that happens to cover everything: a rect the pointer test does not know
        // about is a rect every click falls straight through. Without this, pressing Exit would also
        // place a wall under the button, and a right-drag beside the card would orbit the camera.
        PointerOverUI = ExitPromptOpen
                        || _leftRect.Contains(m) || _rightRect.Contains(m) || _topRect.Contains(m)
                        || (bandH > 0f && _modeRect.Contains(m))
                        || (barH > 0f && _timelineRect.Contains(m));

        // Re-established by whichever rail is hovering an opening row, every pass. Cleared here rather
        // than by the list that sets it, so a selection change to something with no list cannot leave
        // a highlight burning in the plan.
        HoverOpeningId = null;

        // DISABLED, not merely covered, and this line is what makes the exit prompt a modal.
        //
        // IMGUI hands a mouse event to the FIRST control that claims it, and these rails are drawn
        // before the card is. A card painted on top of them would look modal and answer to nothing:
        // the click on "Save and exit" would be eaten by whichever rail button happened to sit under
        // it. GUI.enabled = false makes these controls claim nothing, while still allocating their
        // control IDs, so the layout pass and the repaint pass go on agreeing.
        GUI.enabled = !ExitPromptOpen;

        DrawLeftRail();
        DrawTopBar();
        DrawModeBand();
        DrawRightRail();
        DrawTimeline();

        // Sensor coverage, under the selection highlight: it is a wash across whole rooms and the
        // highlight is a line, so drawing it first keeps the line readable on top of it. Outside the
        // PointerOverUI guard for the same reason the highlight is: the cursor is over the rail
        // precisely while someone reads the coverage figure this is illustrating.
        if (Doc != null) _sensorView.Draw(cam, Level, Variant, SelectedId);

        // The selection highlight is drawn OUTSIDE the PointerOverUI guard below, and before the tool's
        // own overlay so a live preview always sits on top of it. Gating it the way tool overlays are
        // gated would blank the highlight the moment the cursor reached the rail, which is exactly
        // where the cursor goes to read the inspector describing what was just selected.
        // Under the selection, so hovering one row while another opening is selected reads as two
        // different states rather than two equals.
        if (Doc != null && !string.IsNullOrEmpty(HoverOpeningId) && HoverOpeningId != SelectedId)
            _selectionView.DrawHoverOpening(cam, Level, HoverOpeningId);

        if (Doc != null && !string.IsNullOrEmpty(SelectedId))
            _selectionView.Draw(cam, Level, Variant, SelectedKind, SelectedId, residenceRenderer);

        if (Doc != null && !PointerOverUI) _active?.DrawOverlay();

        GUI.enabled = true;

        // Over everything: the rails, the mode band, the timeline and every scene overlay.
        DrawExitPrompt(w, h);

        // AFTER the prompt, deliberately. Status is how a failed save reports itself, and answering
        // "Save and exit" on a disk that refuses the write is exactly the moment the message has to
        // be readable. Drawn before the card it would be behind the scrim.
        DrawStatus();

        // Last, after every EndArea: a tip drawn inside a layout area would be clipped to it, and one
        // drawn before the rails would be painted over by them. Ungated by PointerOverUI for the same
        // reason the selection highlight is: the cursor being over a rail is precisely when a tooltip
        // is wanted.
        UITooltip.Draw();
    }

    private SensorAlert? _pendingAlert;

    /// <summary>
    /// Jumps the clock to the minute an alert fired and selects the device that raised it, so the
    /// timeline answers "when", the plan answers "where", and the two are one click apart.
    /// </summary>
    private void GoToAlert(SensorAlert alert)
    {
        if (residenceRenderer == null) return;

        residenceRenderer.Occupancy.Playing = false;
        residenceRenderer.Occupancy.ScrubTo(alert.minute);
        residenceRenderer.UpdateOccupantPoses();
        residenceRenderer.UpdateSensorStates();

        // reveal: false: an alert clicked from the timeline should not eject you from the console
        // that is describing it, exactly as CompareTool's rows do not eject you from Compare.
        Select(ResidenceElementMarker.Kind.Sensor, alert.sensorId, reveal: false);
        FocusElement(alert.sensorId);
        Status($"{Clock.Format(alert.minute)}: {alert.Title}. {alert.body}");
    }

    private int RosterCount() => Variant?.occupants?.Count ?? 0;

    private void DrawTimeline()
    {
        if (Doc == null || residenceRenderer == null || _timelineRect.height <= 0f) return;

        _timeline.Draw(_timelineRect, TimelineExpanded, Variant, Level, residenceRenderer.Occupancy,
                       residenceRenderer.SensorDay);

        // Queued, not applied: this changes the bar's height and its control count, and doing that
        // between a frame's layout and repaint passes is what throws Mismatched LayoutGroup.
        if (_timeline.ToggleRequested) _pendingTimelineToggle = true;
        if (_timeline.Scrubbed)
        {
            residenceRenderer.UpdateOccupantPoses();
            residenceRenderer.UpdateSensorStates();
        }

        // An alert mark carries you to the moment AND to the device: the clock jumps to when it fired,
        // and the sensor that raised it is selected so the plan says where. Deferred through
        // _pendingAlert, because scrubbing re-poses every marker and re-tints every device, which is
        // not something to do halfway through drawing the bar that asked for it.
        if (_timeline.ClickedAlert.HasValue) _pendingAlert = _timeline.ClickedAlert;

        string clicked = _timeline.ClickedOccupantId;
        if (string.IsNullOrEmpty(clicked)) return;

        Select(ResidenceElementMarker.Kind.Occupant, clicked);
        FocusElement(clicked, OccupantName(clicked));
    }

    /// <summary>
    /// Points the camera at a rendered element and closes in on it. Shared by the People view's roster
    /// click, the People tool's plan click, and the F key, so all three answer "where is that" the
    /// same way.
    ///
    /// It reports rather than silently doing nothing, which is what the first version did in both of
    /// the cases that actually come up: a resident who is out of the house has no marker to point at,
    /// and the walkthrough camera is a body standing in a room, not a viewpoint that may be teleported.
    /// </summary>
    public void FocusElement(string id, string label = null)
    {
        if (viewController == null || residenceRenderer == null || string.IsNullOrEmpty(id)) return;

        if (viewController.Current == ViewController.Mode.Walkthrough)
        {
            Status("Focus only works in the overview.");
            return;
        }

        var go = residenceRenderer.GetGO(id);
        if (go == null || !go.activeInHierarchy)
        {
            Status(string.IsNullOrEmpty(label) ? "Nothing to focus on." : label + " is out of the residence right now.");
            return;
        }

        viewController.FocusOn(FocusPoint(go), closeUp: true);
    }

    // The transform is not the answer: a floor or ceiling GameObject sits at the render root because
    // RoomMeshBuilder bakes world coordinates into the mesh, so its position is the origin. Renderer
    // bounds give the middle of the thing you actually see, which for an occupant marker is chest
    // height, exactly where you want a person centered on screen.
    private static Vector3 FocusPoint(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return go.transform.position;

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        // An opening handle's MeshRenderer has no mesh, so its bounds are a degenerate point. Fall
        // back to the transform, which openings do set explicitly.
        return b.size.sqrMagnitude > 0.0001f ? b.center : go.transform.position;
    }

    /// <summary>The name behind an occupant id, or null when the id is not an occupant.</summary>
    public string OccupantName(string id)
    {
        if (Variant?.occupants == null || string.IsNullOrEmpty(id)) return null;
        foreach (var person in Variant.occupants)
            if (person != null && person.id == id) return person.name;
        return null;
    }

    private void DrawLeftRail()
    {
        UITheme.BeginPanel(_leftRect);

        // Folded to a strip: one control and nothing else, so the panel's control count is consistent
        // for the whole frame either way.
        if (_leftCollapsed)
        {
            if (UITheme.GhostButton("☰", GUILayout.Width(UITheme.GlyphW))) _pendingLeftToggle = true;
            UITheme.Tip($"Open the residence library ({_library.Count})");
            UITheme.EndPanel();
            return;
        }

        GUILayout.BeginHorizontal();
        UITheme.Title("Residence Improvement Visualizer");
        GUILayout.FlexibleSpace();
        if (UITheme.GhostButton("‹", GUILayout.Width(UITheme.GlyphW))) _pendingLeftToggle = true;
        UITheme.Tip("Fold the library away and give the plan the space");
        GUILayout.EndHorizontal();
        UITheme.Gap();

        GUILayout.BeginHorizontal();
        if (UITheme.PrimaryButton("New residence")) RequestNewResidence();
        UITheme.Tip("Start a dwelling with one plain room to build from. Import a floor plan next if "
                    + "you have one.");
        // Same height as the primary beside it: a 44 px button next to a 30 px one reads as a mistake.
        if (UITheme.SecondaryButton("Import", GUILayout.Height(UITheme.PrimaryH))) RequestImportResidence();
        UITheme.Tip("Open a .riv archive someone sent you");
        GUILayout.EndHorizontal();

        // Its own row: three buttons will not fit the rail's width.
        if (UITheme.SecondaryButton(_showSamples ? "Sample residences ▾" : "Sample residences ▸"))
            _showSamples = !_showSamples;
        UITheme.Tip("Six finished, furnished plans to look around. Each adds a fresh copy to the "
                    + "library, so a sample can be pulled again after it has been edited.");

        if (_showSamples) DrawSamplePicker();

        UITheme.Header($"Residences ({_library.Count})");

        // Nothing on disk yet. Everything here is local files, so the first move is always the same
        // one: get a floor plan in and calibrate it. The button IS the empty state. It takes them
        // there, and its tooltip is what the paragraph here used to say. Reachable after archiving
        // everything, since the samples are only seeded once.
        if (_library.Count == 0)
        {
            // Import rather than the Structure that NewResidence lands on. It cannot be a RequestStage
            // here any more: the open is deferred now, and _pendingStage drains BEFORE it, so a stage
            // asked for here would be spent before NewResidence overwrote it with its own. The
            // follow-up rides along with the request instead, and is applied after it.
            if (UITheme.SecondaryButton("Start from a floor plan"))
                RequestNewResidence(ResidenceStage.Import);
            UITheme.Tip("No residences yet. This starts one and takes you straight to importing a plan "
                        + "sketch and setting its scale, so everything traced afterwards is at true "
                        + "size. Or open a sample above to look around a finished plan first.");
        }

        _libScroll = UITheme.BeginScroll(_libScroll, GUILayout.MaxHeight(Screen.height * 0.45f));
        foreach (var row in _library)
        {
            bool isOpen = Doc != null && Doc.id == row.id;
            string label = (row.favorite ? "★ " : "") + row.name;
            // The version and option count are the row's own content: what tells two saved residences
            // apart, so they stay printed.
            bool open = UITheme.StateRow(label,
                $"v{row.version} · {row.variantCount} variant{(row.variantCount == 1 ? "" : "s")}", isOpen);
            UITheme.Tip(isOpen ? "The residence you have open" : $"Open {row.name}");
            if (open) RequestOpenResidence(row.id);
        }
        UITheme.EndScroll();

        if (Doc != null)
        {
            UITheme.Header("This residence");
            Doc.name = UITheme.TextRow("Name", Doc.name, "What this residence is called in the library");

            GUILayout.BeginHorizontal();
            if (UITheme.PrimaryButton(Dirty ? "Save *" : "Save")) SaveResidence();
            UITheme.Tip(Dirty ? "Unsaved changes. Write them to disk  (Ctrl+S)" : "Saved  (Ctrl+S)");
            if (UITheme.SecondaryButton("Save As", GUILayout.Height(UITheme.PrimaryH))) SaveAs();
            UITheme.Tip("Save a separate copy and switch to working on it");
            GUILayout.EndHorizontal();

            UITheme.Gap();
            GUILayout.BeginHorizontal();
            if (UITheme.GhostButton("Export")) ExportResidence();
            UITheme.Tip("Write a .riv archive holding this residence and its sketch, to send to "
                        + "another machine");
            bool star = UITheme.GhostButton(Doc.favorite ? "Unstar" : "Star");
            UITheme.Tip(Doc.favorite ? "Stop starring this residence" : "Star this residence in the library");
            if (star)
            {
                ResidenceStore.ToggleFavorite(Doc.id);
                Doc.favorite = !Doc.favorite;
                RefreshLibrary();
            }
            GUILayout.EndHorizontal();

            DrawResetToSample();   // puts its own gap above itself, because it is often not drawn

            UITheme.Gap();
            if (UITheme.DangerButton("Archive")) ArchiveResidence();
            UITheme.Tip("Move this residence out of the library. Nothing is destroyed. It goes to the "
                        + "archive folder on disk.");
        }

        UITheme.EndPanel();
    }

    // Rebuilds this residence from the sample it came from. Startup already refreshes every sample nobody
    // has touched (see SampleRefresh), so the only residences that ever reach this button are ones with
    // work in them, which is exactly why it confirms and says what it costs first. Two clicks rather
    // than a modal: the rail is IMGUI, and a dialog here would be more machinery than the risk.
    private void DrawResetToSample()
    {
        if (Doc == null || !SampleRefresh.IsSample(Doc)) return;
        if (SampleResidenceInstaller.ResolveKey(Doc) == null) return;

        int proposals = Doc.variants != null ? Mathf.Max(0, Doc.variants.Count - 1) : 0;

        UITheme.Gap();
        if (!_confirmReset)
        {
            if (UITheme.GhostButton("Reset to the latest sample")) _confirmReset = true;
            UITheme.Tip("Rebuild this residence from the shipped plan, picking up any improvement made to "
                        + "the sample since it was installed.");
            return;
        }

        // The cost goes in the button's own label, not in a sentence above it. A count is not a
        // subtitle, and a destructive button that states its own price is better than one that does
        // not: this is the one place where the number has to be on screen, not on hover.
        //
        // Stacked, not side by side. This label runs to 28 characters and grows with the count, and
        // sharing a 222 px rail with Cancel left it about 160 px, so the price it exists to state was
        // the part that got cut off. It is already a two-click confirmation; two lines is consistent.
        if (UITheme.DangerButton(proposals > 0
                ? $"Reset. Discards {proposals} proposal{(proposals == 1 ? "" : "s")}"
                : "Reset. Discards your edits"))
            ResetToSample();
        UITheme.Tip("Rebuild this residence from the shipped plan. Every proposal, and any edit to the "
                    + "baseline, is thrown away. This cannot be undone.");
        UITheme.GapTight();
        if (UITheme.GhostButton("Cancel")) _confirmReset = false;
        UITheme.Tip("Leave this residence as it is");
    }

    private void ResetToSample()
    {
        _confirmReset = false;
        if (Doc == null) return;

        if (!SampleResidenceInstaller.ResetToSample(Doc, out string error))
        {
            Status("Could not reset: " + error);
            return;
        }

        // Reload rather than reusing the in-memory doc: ResetToSample replaced its variant list, so
        // the selection, the active variant and the undo stack all point at elements that are gone.
        string id = Doc.id;
        Doc = ResidenceStore.Load(id);
        AfterOpen();
        RefreshLibrary();
        Status("Reset to the shipped plan.");
    }

    // The built-in samples. Each click writes a fresh COPY into the library rather than opening a
    // shared original, so a sample can be pulled again after it has been edited or archived.
    private void DrawSamplePicker()
    {
        foreach (var spec in SampleResidences.All)
        {
            // spec.blurb is the row's own description: the thing that tells one sample from another,
            // and therefore content rather than a caption.
            bool pick = UITheme.StateRow(spec.displayName, spec.blurb, false);
            UITheme.Tip($"Add a copy of {spec.displayName} to the library. Its base environment is "
                        + "read-only; start a proposal to change anything.");
            if (!pick) continue;

            var doc = SampleResidenceInstaller.Install(spec.key);
            if (doc == null) { Status("Could not add that sample."); continue; }

            Doc = doc;
            AfterOpen();
            Status("Added " + doc.name);
        }
    }

    private static readonly ViewController.Mode[] ViewModes =
        (ViewController.Mode[])System.Enum.GetValues(typeof(ViewController.Mode));

    private static readonly string[] ViewModeLabels = { "Overview", "Walkthrough" };

    private static readonly string[] ViewModeTips =
    {
        "Perspective free-look with the ceilings hidden. The view for drawing, measuring and showing "
            + "the layout. Right-drag to turn, middle-drag to pan, scroll to zoom, WASD to move, "
            + "Q/E to lower and raise. Look straight down for a plan.",
        "Walk the plan in first person, at standing or seated eye height. R returns you to a clear spot.",
    };

    // The two labels the eye-height chip alternates between. Pinned to the wider of the two so that
    // toggling it does not shove everything to its right sideways.
    //
    // SeatedShort is what it says in a window too narrow to hold the bar at full width. It is the
    // widest thing in the right-hand cluster and the only one of them with a shorter honest form, so
    // it is what gives way first, and its tooltip carries the whole sentence either way, which is
    // what makes the trim cost nothing. See DrawTopBar.
    private const string SeatedLabel   = "Seated (wheelchair)";
    private const string SeatedShort   = "Seated";
    private const string StandingLabel = "Standing";

    // Same treatment for the units chip: it shows the system currently in force, so it alternates too.
    private const string ImperialLabel = "ft / in";
    private const string MetricLabel   = "meters";

    /// <summary>
    /// The longest label the floor chip can ever show, so its width is pinned. An unpinned chip that
    /// changes label pushes Undo and Redo sideways every time it is clicked: the same reason the
    /// eye-height and units chips beside it are pinned to their widest state.
    /// </summary>
    private string WidestFloorLabel()
    {
        int count = LevelCount;
        string widest = "";
        for (int i = 0; i < count; i++)
        {
            string label = $"{LevelName(i)}  ({i + 1}/{count})";
            if (label.Length > widest.Length) widest = label;
        }
        return widest;
    }

    /// <summary>
    /// The width a stage tab is drawn at when every label fits inside it. Tabs shrink below this in a
    /// narrow window; they grow past it only for a label that genuinely needs more room. See
    /// <see cref="TabBreath"/> and the ceiling computed in <see cref="DrawTopBar"/>.
    /// </summary>
    private const float TabIdeal = 92f;
    // The one gap on the command bar. Between the tabs and every control after them. ONE constant,
    // because the reserve arithmetic counts these gaps and the draw code spends them, and the two used
    // to be literals (18, 14, 14, 14) kept in step by hand.
    private const float BarGap = UITheme.SectionH;

    /// <summary>
    /// Slack over a label's own measured width, so a tab that had to grow is not drawn with its text
    /// flush against the cell edge. <see cref="UITheme.Measure"/> already includes the style's padding,
    /// so this is breathing room rather than a correction.
    /// </summary>
    private const float TabBreath = 8f;

    // TabTight (56 px) is GONE. It was the width below which the right-hand cluster started giving up
    // label text, and it was a guess standing in for the question actually being asked: "would the
    // tabs otherwise have to trim a name?", which DrawTopBar can now measure directly against the
    // widest label. Deleted rather than left declared, on the WallDef.structural precedent: a constant
    // nothing reads is a threshold somebody will later assume is enforced.

    private void DrawTopBar()
    {
        var inner = UITheme.Inset(_topRect);
        UITheme.BeginPanel(_topRect);
        GUILayout.BeginHorizontal();

        // What sits to the RIGHT of the stage tabs, measured rather than guessed. The old code
        // reserved a flat 330 px for this cluster while it actually wants about 500, and it took that
        // 330 off _topRect.width rather than off the inset width. Double-spending the padding
        // BeginArea had already removed. Both mistakes are why Undo and Redo fell off the bar.
        var labels = Doc != null ? ResidenceWorkflow.LabelsFor(_stages) : null;
        int tabCount = labels?.Length ?? 0;

        float modesW = UITheme.MeasureBar(ViewModeLabels, UITheme.SegmentStyle);
        float undoW  = Doc == null ? 0f
                     : UITheme.Measure("Undo", UITheme.ButtonStyle)
                     + UITheme.Measure("Redo", UITheme.ButtonStyle)
                     + UITheme.MarginW(UITheme.ButtonStyle) * 2f;
        float unitsW = Mathf.Max(UITheme.Measure(ImperialLabel, UITheme.ChipStyle),
                                 UITheme.Measure(MetricLabel,   UITheme.ChipStyle));

        // The floor chip appears ONLY on a residence with more than one story: the same
        // conditional-visibility rule the Outdoors stage follows, and the reason every existing
        // single-story residence's command bar is untouched by this. Its width joins the MEASURED reserve
        // rather than being hoped for: the bar is already tight at 1280, and a control that is not in
        // the reserve is a control that silently pushes another one off the end, which is exactly how
        // Undo and Redo disappeared once before.
        bool manyStories = LevelCount > 1;
        float floorW = manyStories ? Mathf.Max(96f, UITheme.Measure(WidestFloorLabel(), UITheme.ChipStyle)) : 0f;

        // MARGINS ARE PART OF THE RESERVE. Measure reports a style's padding but not its margin, and
        // every control here is drawn at an explicit width with its margin outside that: six pixels
        // a chip, six for the stage toolbar. The old reserve counted a flat 8 px of slack to cover
        // all of them together and was short by twenty to thirty, which is a bar that overruns its
        // panel and has its right-hand end silently clipped away by BeginArea.
        float margins = UITheme.MarginW(UITheme.CommandStyle)
                      + UITheme.MarginW(UITheme.SegmentStyle)
                      + UITheme.MarginW(UITheme.ChipStyle) * (manyStories ? 3f : 2f);

        float spaces = BarGap * (manyStories ? 5f : 4f);

        float fixedW = modesW + unitsW + undoW + floorW + margins + spaces;

        // The eye-height chip is the widest single thing on the right and the only one with a shorter
        // honest form, so in a window that cannot hold the bar at full width it is what gives way,
        // before the tabs give up any of theirs, and never at the cost of a control vanishing. Its
        // tooltip is unchanged, which is what makes the shorter label free.
        //
        // IT NOW GIVES WAY WHEN IT WOULD OTHERWISE COST A LABEL, which is what that paragraph always
        // claimed and the old trigger did not do. That trigger fired only once the tabs were under
        // 56 px (long past the point where they had started ellipsising) so at 1280 the bar spent
        // ~80 px printing "(wheelchair)", a word already on the chip's own tooltip, and paid for it by
        // cutting "Smart living" to "Smart li…". The comparison is against the widest label rather
        // than against a threshold, so the chip shortens exactly when it buys a whole name and stays
        // long whenever it does not: at 1440 and above nothing changes.
        float seatedFull = Mathf.Max(UITheme.Measure(SeatedLabel,   UITheme.ChipStyle),
                                     UITheme.Measure(StandingLabel, UITheme.ChipStyle));
        float widestTab = tabCount > 0 ? UITheme.MeasureBar(labels, UITheme.CommandStyle) / tabCount : 0f;
        bool shortSeated = tabCount > 0
                        && (inner.width - fixedW - seatedFull) / tabCount < widestTab;

        string seatedText = shortSeated ? SeatedShort : SeatedLabel;
        float chipW = shortSeated
                    ? Mathf.Max(UITheme.Measure(SeatedShort,   UITheme.ChipStyle),
                                UITheme.Measure(StandingLabel, UITheme.ChipStyle))
                    : seatedFull;

        float reserve = fixedW + chipW;

        // The stage bar. Same widget as the Site tool's command bar, so the two apps read as one family
        // even though what they command is different.
        if (Doc != null)
        {
            int current = Mathf.Max(0, _stages.IndexOf(_stage));
            // Adaptive, not a fixed 92 px a stage. Select made the list seven long, and seven fixed
            // tabs overran a 1280-wide window: the same clipping the fixed width was itself introduced
            // to fix, one stage later. With the bar now full width this lands on the 92 px ceiling at
            // 1280.
            //
            // There is NO FLOOR under it, and that is the correction. A floor is a promise that the
            // tabs will be at least that wide, and the only way to keep that promise in a window too
            // narrow to hold them is to push the right-hand cluster off the end, which is precisely
            // the failure this reserve exists to prevent. What the tabs give up instead is label
            // text, through FitAll, with StageTips putting the full name back on hover.
            //
            // THE CEILING IS MEASURED, NOT 92. A flat 92 px was a guess made when every stage name was
            // one short word, and it is the same guess this file has already had to correct twice,
            // once for the right-hand reserve, once for the view-mode picker ("sized from its own
            // labels, not from a hardcoded width"). "Smart living" needs more than 92, so FitAll
            // ellipsised it at EVERY window size, including one with a hundred px of bar standing
            // empty to the right of Redo: a label trimmed to fit a box that did not have to be that
            // small. The ceiling is now the widest label's own width, and 92 becomes the size tabs
            // settle at when nothing needs more, so a bar of short names looks exactly as it did.
            //
            // This can never eat the reserve. It only raises the CAP; the Min below still takes
            // whatever the reserve leaves, so the extra width comes out of space that was going to be
            // blank, which is the space the trim was being paid for out of.
            float ceiling = Mathf.Max(TabIdeal, widestTab + TabBreath);

            float per = Mathf.Min(ceiling, Mathf.Max(1f, (inner.width - reserve) / labels.Length));

            // Insurance again: below about 64 px a tab cannot hold "Outdoors" at _command's padding,
            // so shorten the label rather than let the Toolbar cut it mid-glyph. StageTips then leads
            // that tab's tooltip with the full name, so nothing is ever actually hidden.
            var shown = UITheme.FitAll(labels, UITheme.CommandStyle, per);
            int picked = UITheme.CommandBar(current, shown, StageTips(labels, shown),
                                            GUILayout.Width(per * labels.Length));
            if (picked != current && picked >= 0 && picked < _stages.Count) RequestStage(_stages[picked]);

            GUILayout.Space(BarGap);
        }

        // View modes. A segmented control, like the stage bar beside it: both pick exactly one of a
        // short fixed list, and drawing one as a toolbar and the other as three loose chips made two
        // identical choices look like different kinds of thing.
        //
        // Sized from its own labels, not from a hardcoded width: a Toolbar makes every cell the width
        // of the widest, and a guessed total clipped "Walkthrough", which needs about 75 px.
        int curMode = viewController != null ? System.Array.IndexOf(ViewModes, viewController.Current) : 0;
        int pickMode = UITheme.Segmented(Mathf.Max(0, curMode), ViewModeLabels, ViewModeTips,
                                         GUILayout.Width(modesW));
        if (pickMode != curMode && pickMode >= 0 && pickMode < ViewModes.Length)
            _pendingViewMode = ViewModes[pickMode];   // deferred: see below

        GUILayout.Space(BarGap);

        // The eye-height toggle: the cheapest meaningful accessibility feature in the tool.
        //
        // Always drawn, greyed out when it does not apply, and the mode change above is deferred. Both
        // halves matter: this chip existing only in Walkthrough meant switching mode from inside OnGUI
        // changed the control count between the layout and repaint passes of one frame, and it also
        // made the whole right-hand end of the bar jump sideways every time the view changed.
        {
            bool walking = viewController != null && viewController.Current == ViewController.Mode.Walkthrough;
            GUI.enabled = walking;
            bool seated = viewController != null && viewController.Seated;
            if (UITheme.Chip(seated ? seatedText : StandingLabel, seated, GUILayout.Width(chipW))
                && walking)
                viewController.ToggleSeated();
            UITheme.Tip("Eye height in the walkthrough: standing 1.60 m, or a wheelchair user's 1.19 m. "
                        + "The seated setting shows the real sightline over counters and through windows.");
            GUI.enabled = true;
            GUILayout.Space(BarGap);
        }

        // Which story. Only when there is more than one: a chip reading "Ground floor 1 of 1" on
        // every residence that has ever existed would be a permanent control that can do nothing.
        //
        // Deferred like the stage picker, and for the stronger version of the same reason: switching
        // story tears down and rebuilds every GameObject in the residence and clears the selection, so
        // doing it mid-OnGUI leaves the repaint pass drawing a rail describing a floor the layout pass
        // never saw.
        if (manyStories)
        {
            int count = LevelCount;
            int index = LevelIndex;
            // It CYCLES rather than opening a menu. A dropdown would need a rect of its own in the
            // PointerOverUI test and a deferred open/close for its control count: the machinery
            // ModeBand's variant list needs: to choose between two or three items. The Import rail's
            // Floors section is where a specific story is picked, named and removed.
            if (UITheme.Chip($"{LevelName(index)}  ({index + 1}/{count})", false, GUILayout.Width(floorW)))
                RequestLevel((index + 1) % count);
            UITheme.Tip("Which floor you are working on. Click for the next one. Only one floor is "
                        + "drawn at a time, and every tool, the click plane and the camera follow it. "
                        + "Import → Floors names and removes them.");
            GUILayout.Space(BarGap);
        }

        // Feet and inches, or metres: for the whole app, at once.
        //
        // ResidenceSettings.metricUnits has existed, been persisted, and been wired to Units.Display through
        // ResidenceStore.ApplySettings the entire time; it simply had no control, so the only way to switch
        // was to hand-edit settings.json. Nothing else is needed here: SaveSettings calls ApplySettings,
        // and every readout in the app re-derives its text per frame.
        //
        // Pinned to the wider of the two labels, for the reason the eye-height chip beside it is: an
        // unpinned chip that changes label shoves Undo and Redo sideways every time it is clicked. No
        // deferral, though. Unlike the stage and view-mode pickers this changes neither the bar's
        // height nor its control count, so it cannot produce a Mismatched LayoutGroup.
        {
            bool metric = ResidenceStore.Settings.metricUnits;
            if (UITheme.Chip(metric ? MetricLabel : ImperialLabel, false, GUILayout.Width(unitsW)))
            {
                ResidenceStore.Settings.metricUnits = !metric;
                ResidenceStore.SaveSettings();
            }
            UITheme.Tip("Show every measurement in feet and inches, or in meters. This also switches "
                        + "times between 12- and 24-hour, which is the same preference.");
            GUILayout.Space(BarGap);
        }

        // No "People view" chip any more. The timeline is permanent along the bottom and carries its
        // own expand chevron, so a second control up here would be a third way to do one thing, and
        // the command bar needs the width.

        GUILayout.FlexibleSpace();

        if (Doc != null)
        {
            GUI.enabled = History.CanUndo;
            if (UITheme.GhostButton("Undo")) { History.Undo(); AfterHistoryJump(); }
            UITheme.Tip("Undo the last change  (Ctrl+Z)");
            GUI.enabled = History.CanRedo;
            if (UITheme.GhostButton("Redo")) { History.Redo(); AfterHistoryJump(); }
            UITheme.Tip("Redo the change you just undid  (Ctrl+Y)");
            GUI.enabled = true;
        }

        GUILayout.EndHorizontal();
        UITheme.EndPanel();
    }

    /// <summary>
    /// The hover text for each tab. The stage you are IN reports its active tool's own hint instead of
    /// the generic stage blurb: that tab is the only place a tool's instructions live now that no rail
    /// prints them, so hovering where you are working says how to work there.
    ///
    /// A tab whose label had to be shortened to fit leads with its full name, so the ellipsis costs
    /// nothing: the same rule that lets every caption in this app live on hover.
    /// </summary>
    private string[] StageTips(string[] full, string[] shown)
    {
        var tips = new string[full.Length];
        for (int i = 0; i < full.Length && i < _stages.Count; i++)
        {
            var stage = _stages[i];
            string body = stage == _stage && !string.IsNullOrEmpty(_active?.Hint)
                ? _active.Hint
                : ResidenceWorkflow.Tip(stage);
            bool trimmed = shown != null && i < shown.Length && shown[i] != full[i];
            tips[i] = trimmed ? full[i] + ". " + body : body;
        }
        return tips;
    }

    private void DrawRightRail()
    {
        UITheme.BeginPanel(_rightRect);

        if (Doc == null)
        {
            // A title, not a sentence: the left rail is where the answer is, and it says so on hover.
            UITheme.Title("No residence open");
            UITheme.Tip("Create a new residence, or open one from the library on the left, to begin.");
            UITheme.EndPanel();
            return;
        }

        _railScroll = UITheme.BeginScroll(_railScroll);

        // 1: the tools of this stage, and only where there is a choice to make. The stage header and
        // the tool header that used to sit here both restated what the command bar is already
        // highlighting; for five of the seven stages that was the entire top of the rail saying nothing.
        DrawStageTools();

        // 2: the active tool owns the rail.
        _active?.DrawRail();

        // 3: everything that is not this stage's work, folded away. The variant list left this rail
        // for the mode band: it was answering "which of the two jobs am I doing", which is a question
        // you have while working, not one you go looking for at the bottom of a scroll view.
        // TEMPORARY: the outdoor gate is hidden with the rest of the outdoor UI. The space goes with
        // it: a separator above nothing is a rail that looks like it failed to draw something.
        if (ResidenceWorkflow.OutdoorsUI)
        {
            DrawOutdoorFoldout();
        }

        UITheme.EndScroll();
        UITheme.EndPanel();
    }

    private void DrawStageTools()
    {
        var ids = ResidenceWorkflow.ToolIdsFor(_stage);

        // Nothing to choose between. With Select promoted out of every stage's array, five of the seven
        // hold one tool, and a lone permanently-active chip is a button that cannot do anything.
        if (ids.Length <= 1) return;

        // A SEGMENTED control, not a chip row. The tools used to be accent chips: the same control,
        // in the same style, eight pixels above the accent chips that pick what the tool places
        // (Door / Window / Opening, the room types), so "which tool" and "what am I adding" read as
        // one undifferentiated row of lit pills. The segmented track is the sub-tab look (Move / Rotate
        // / Scale, Viewing as), it fits itself to the rail, and each cell carries its tool's Hint.
        //
        // NO ROW LABEL. "Tool" was the one place the naming rule was applied to something that was
        // already nothing but names. Deferred through RequestTool: switching the tool swaps the whole
        // rail, which must not happen between a frame's layout and repaint passes.
        var names = new string[ids.Length];
        var tips = new string[ids.Length];
        int current = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            var tool = FindTool(ids[i]);
            names[i] = tool?.DisplayName ?? ids[i];
            tips[i] = tool?.Hint;
            if (tool != null && tool == _active) current = i;
        }
        int picked = UITheme.Segmented(current, names, tips);
        if (picked != current && picked >= 0 && picked < ids.Length) RequestTool(ids[picked]);
        UITheme.Gap();
    }

    /// <summary>
    /// Recomputes what the band reports. Once per Update, never from OnGUI. OnGUI runs a layout pass
    /// and a repaint pass, so a diff computed there runs twice a frame.
    /// </summary>
    private void RefreshChangeCount()
    {
        var cur = Variant;
        var baseline = ResidenceStore.Baseline(Doc);
        _changeCount = cur == null || baseline == null || baseline == cur
            ? 0
            : VariantDiff.Compare(baseline, cur).Count;
    }

    /// <summary>
    /// The band, plus everything it asks for. Every request is applied HERE rather than inside
    /// ModeBand, because three of them change the band's own control count and one changes the
    /// stage: both of which have to land outside the OnGUI pass that raised them.
    /// </summary>
    private void DrawModeBand()
    {
        if (Doc == null || _modeRect.height <= 0f) return;

        var baseline = ResidenceStore.Baseline(Doc);
        _modeBand.Draw(_modeRect, Doc, Variant, _changeCount, baseline?.name, _variantMenuOpen);

        if (_modeBand.ToggleMenuRequested) _pendingVariantMenu = true;

        // Both guarded to the baseline. A proposal has no locked state any more, and letting one
        // acquire it here would resurrect a mode ModeBand can no longer draw its way out of.
        if (_modeBand.EditBaseRequested && Variant != null && Variant.isBaseline)
        {
            Variant.locked = false;
            MarkDirty();
            Status("Editing the base environment.");
        }

        if (_modeBand.DoneRequested && Variant != null && Variant.isBaseline)
        {
            Variant.locked = true;
            MarkDirty();
            Status("Base environment is read-only again.");
        }

        if (_modeBand.NewProposalRequested && Variant != null)
        {
            _pendingVariantMenu = _variantMenuOpen;   // close the menu behind the new proposal
            NewProposalFrom(Variant);
        }

        if (_modeBand.CompareRequested) RequestStage(ResidenceStage.Review);

        if (_modeBand.ReportRequested) RequestReport();

        if (!string.IsNullOrEmpty(_modeBand.PickedVariantId))
        {
            _pendingVariantMenu = true;               // picking closes the menu
            SetActiveVariant(_modeBand.PickedVariantId);
        }

        if (!string.IsNullOrEmpty(_modeBand.DeleteVariantId))
        {
            var doomed = ResidenceStore.FindVariant(Doc, _modeBand.DeleteVariantId);
            if (doomed != null && !doomed.isBaseline)
            {
                RecordDocEdit("Delete proposal");
                Doc.variants.Remove(doomed);
                SetActiveVariant(ResidenceStore.Baseline(Doc)?.id);
                MarkDirty();
                Status("Deleted " + doomed.name + ".");
            }
        }
    }

    // The exterior gate, and nothing else. Collapsed, at the bottom, below the work, which is the
    // right weight for a feature most residences in this tool never use. Turning it on adds the Outdoors
    // stage to the command bar; turning it off takes every outdoor control away again.
    private void DrawOutdoorFoldout()
    {
        // "(optional)" dropped from the label: being collapsed at the very bottom of the rail already
        // says that, and the tooltip on the toggle inside says the rest.
        _showOutdoors = UITheme.Foldout(_showOutdoors, "Outdoors");
        if (_showOutdoors) DrawExteriorToggle();
    }

    private void NewProposalFrom(VariantDef source)
    {
        // Deep copy PRESERVING every element id: that is what lets VariantDiff report a widened door
        // as a modification instead of a delete plus an add.
        var copy = ResidenceStore.Clone(source);
        copy.id = System.Guid.NewGuid().ToString();
        copy.name = ResidenceStore.NewProposalName(Doc, System.DateTime.Now);
        copy.description = "Based on " + source.name;
        copy.basedOnVariantId = source.id;
        copy.isBaseline = false;
        copy.locked = false;

        RecordDocEdit("New proposal");
        Doc.variants.Add(copy);
        SetActiveVariant(copy.id);
        Status("Created " + copy.name);
    }

    private void DrawExteriorToggle()
    {
        bool on = UITheme.Toggle("Outdoor additions", Doc.exteriorEnabled,
            "Turn on for a residence where an outdoor change is part of the proposal: an entry ramp, "
            + "a path to the door, a porch railing. It adds the Outdoors tab; turning it "
            + "off takes every outdoor control away again.");
        if (on != Doc.exteriorEnabled)
        {
            RecordDocEdit("Toggle exterior");
            Doc.exteriorEnabled = on;

            var v = Variant;
            if (on && v != null && v.exterior == null) v.exterior = ExteriorBridge.NewExterior();

            MarkDirty();
            residenceRenderer?.Rebuild();

            // The Outdoors stage exists only while this is on. SyncStages picks the change up next
            // frame; turning it on lands you there so the tools are one click away, not a hunt.
            if (on) RequestStage(ResidenceStage.Outdoors);
        }
    }

    public void RecordDocEdit(string label) => History.RecordBefore(EditHistory.Scope.Environment, label);

    // ---------------------------------------------------------------------------------------

    private void SaveAs()
    {
        if (Doc == null) return;
        SaveResidence();
        var copy = ResidenceStore.Duplicate(Doc.id, Doc.name + " copy");
        if (copy != null) { Doc = copy; AfterOpen(); Status("Saved as " + copy.name); }
    }

    private void ArchiveResidence()
    {
        if (Doc == null) return;
        ResidenceStore.Archive(Doc.id);
        Doc = null;
        _baselineShadow = null;
        residenceRenderer?.RenderResidence(null);
        SyncUnderlayQuad();
        RefreshLibrary();
        Status("Archived.");
    }

    /// <summary>
    /// Queues a before/after report for the active proposal. Anything drawn in OnGUI must go through
    /// here rather than starting the capture directly: the capture renders a camera, and rendering a
    /// camera during IMGUI's repaint swaps the active render target and blanks the entire UI.
    /// </summary>
    public void RequestReport() => _pendingReport = true;

    private bool _pendingReport;

    private void StartReport()
    {
        if (Doc == null || residenceRenderer == null) return;

        var cur = Variant;
        var baseline = ResidenceStore.Baseline(Doc);
        if (cur == null || baseline == null || baseline == cur)
        {
            Status("Start a proposal first.");
            return;
        }

        SaveResidence();
        ReportCapture.Run(this, residenceRenderer, Doc, baseline.id, cur.id, Status);
    }

    // Export/import is the whole sharing story now that there is no server: one self-contained file
    // holding the residence plus its traced sketch, which you can email to a colleague.
    private void ExportResidence()
    {
        if (Doc == null) return;
        SaveResidence();

        FileBrowser.SetFilters(true, new FileBrowser.Filter("Residence archive", ResidenceStore.EXPORT_EXT));
        FileBrowser.SetDefaultFilter(ResidenceStore.EXPORT_EXT);
        FileBrowser.ShowSaveDialog(
            paths =>
            {
                if (paths == null || paths.Length == 0) return;
                Status(ResidenceStore.ExportResidence(Doc.id, paths[0], out string err)
                    ? "Exported to " + System.IO.Path.GetFileName(paths[0])
                    : "Export failed: " + err);
            },
            () => Status("Export canceled."),
            FileBrowser.PickMode.Files, false, null,
            Doc.name + ResidenceStore.EXPORT_EXT, "Export residence", "Export");
    }

    private void ImportResidence()
    {
        // The pre-rename extension is offered as well, so an archive sent before the rename is
        // still visible rather than hidden behind "All Files". The reader ignores the extension.
        FileBrowser.SetFilters(true, new FileBrowser.Filter(
            "Residence archive", ResidenceStore.EXPORT_EXT, ResidenceStore.LEGACY_EXPORT_EXT));
        FileBrowser.SetDefaultFilter(ResidenceStore.EXPORT_EXT);
        FileBrowser.ShowLoadDialog(
            paths =>
            {
                if (paths == null || paths.Length == 0) return;

                var doc = ResidenceStore.ImportResidence(paths[0], out string err);
                if (doc == null) { Status("Import failed: " + err); return; }

                Doc = doc;
                AfterOpen();
                Status("Imported " + doc.name);
            },
            () => Status("Import canceled."),
            FileBrowser.PickMode.Files, false, null, null, "Import residence", "Import");
    }

    // Undo and redo restore the whole ResidenceDoc, which can delete the very wall the active tool is
    // rubber-banding from. WallTool's chain would go on drawing a line to a corner that no longer
    // exists. Re-entering the tool IS "forget your transient state", and every tool already implements
    // both halves, so doing it here fixes all of them at once instead of each one guarding its own
    // cache. Selection is cleared by History itself for the same reason.
    private void AfterHistoryJump()
    {
        residenceRenderer?.Rebuild();
        MarkDirty();
        if (_active == null) return;
        _active.Exit();
        _active.Enter(_ctx);
    }

    public void Status(string text)
    {
        _status = text;
        _statusUntil = Time.realtimeSinceStartup + 4f;
    }

    // Lifted clear of the timeline bar, and drawn as a chip rather than a bare label.
    //
    // It used to be a plain GUI.Label painted straight onto the 3D scene, which meant it inherited
    // UITheme's near-black Ink (the colour every rail uses because every rail is light paper) and was
    // therefore invisible against a wall. Routing it through the same renderer as the tooltips gives it
    // its own contrast and one visual language for everything transient that floats over the scene.
    private void DrawExitPrompt(float w, float h)
    {
        if (!ExitPromptOpen) return;

        // Drop whatever the dimmed rails claimed on the way past: UITooltip is a hover tracker over
        // rects, and GUI.enabled does not stop a rect containing the cursor. A tooltip describing a
        // control that cannot be pressed is noise.
        UITooltip.BeginFrame();

        _exitPrompt.Draw(new Rect(0f, 0f, w, h), _exitIntent, _exitDirty, Doc?.name);

        // Queued, never applied here. Quitting or swapping the document mid-layout is the whole
        // reason every request in this file is deferred.
        if (_exitPrompt.Picked.HasValue) _pendingExitAnswer = _exitPrompt.Picked;
    }

    private void DrawStatus()
    {
        if (string.IsNullOrEmpty(_status) || Time.realtimeSinceStartup > _statusUntil) return;

        float y = Screen.height - _timelineRect.height - 46f;
        OverlayDraw.Tip(new Vector2(_topRect.center.x - 150f, y), _status, 420f);
    }
}
