using UnityEngine;

// The one modal in the app: the question asked before work can be thrown away.
//
// WHY A MODAL, WHEN NOTHING ELSE HERE IS ONE. DrawResetToSample says it plainly: "Two clicks rather
// than a modal: the rail is IMGUI, and a dialog here would be more machinery than the risk." That
// reasoning holds wherever the control and its confirmation share a panel, because the second click
// lands where the first one did and the rail is still on screen to go back to. Quitting has neither.
// There is no rail to confirm in, no panel to return to, and the press that starts it (Esc, the
// window's X, Alt+F4) does not come from a control at all. So this one is a modal, deliberately, and
// it is the only one. A second one is a smell.
//
// WHAT IT PROTECTS. Until this file the app had no exit at all: no Application.Quit anywhere in
// Assets, shipped as a borderless fullscreen window with no title bar. The only way out was Alt+F4,
// and Alt+F4 discarded whatever was unsaved without a word. Both halves of that are fixed here and
// in ProjectSettings.
//
// ONE PROMPT, FOUR INTENTS. Quitting is not the only act that drops unsaved work: opening another
// residence, starting a new one and importing one all swap ResidenceDoc and then clear Dirty in
// AfterOpen. The question is the same question in all four cases, so it is asked by one prompt with
// one set of answers and the intent only changes the words.
//
// A plain class like ModeBand, TimelineBar and SelectionOverlay: the controller owns one and calls
// Draw from its OnGUI. Everything is re-derived per frame and nothing is cached, because undo
// restores the whole ResidenceDoc without notifying anyone.
//
// Nothing here acts. Draw records an Answer and the controller applies it from Update, for the
// reason every other request in this UI is deferred: applying it between a frame's layout and
// repaint passes is what throws Mismatched LayoutGroup, and Application.Quit mid-layout tears the
// IMGUI pass down under itself.
public class ExitPrompt
{
    /// <summary>What the answer is about. Only the wording changes; the three answers do not.</summary>
    public enum Intent
    {
        /// <summary>Close the app.</summary>
        Quit,
        /// <summary>Open a different residence, dropping this one.</summary>
        OpenResidence,
        /// <summary>Start a residence from scratch, dropping this one.</summary>
        NewResidence,
        /// <summary>Read a .riv archive in, dropping this one.</summary>
        ImportResidence,
    }

    public enum Answer
    {
        /// <summary>Write the open residence to disk, then go.</summary>
        SaveAndGo,
        /// <summary>Go, and let the unsaved work drop.</summary>
        GoAnyway,
        /// <summary>Stay exactly where we were.</summary>
        KeepWorking,
    }

    // The card is a fixed size rather than a fitted one. Its content varies by two lines at most, and
    // a dialog that changes shape depending on what it is asking reads as a different dialog.
    private const float CardW = 380f;
    private const float CardH = 208f;

    /// <summary>
    /// The answer this frame's click produced, read by the controller after Draw has returned.
    /// Null on every frame nothing was pressed.
    /// </summary>
    public Answer? Picked { get; private set; }

    /// <summary>
    /// The rect the card occupies. The controller does not actually need it for the pointer test
    /// (the whole window is claimed while the prompt is up) but it keeps the "every panel rect is
    /// knowable" convention intact and is useful to anything that wants to avoid drawing under it.
    /// </summary>
    public Rect CardRect { get; private set; }

    /// <summary>
    /// Re-arms the focus grab. The controller calls this as the card goes up, so a card raised a
    /// second time takes the keyboard again.
    /// </summary>
    public void Reset() => _grabFocus = true;

    // Focus belongs to whatever was on screen a frame ago, and a rail field that still holds it
    // keeps TypingInUI true behind a card that has already taken the window. Cleared on the card's
    // first Layout pass rather than when it opens, because keyboardControl is only reliably the game
    // view's focus state inside an OnGUI pass: the same reason LatchTypingInUI exists.
    private bool _grabFocus = true;

    public void Draw(Rect window, Intent intent, bool dirty, string residenceName)
    {
        Picked = null;

        if (Event.current.type == EventType.Layout && _grabFocus)
        {
            _grabFocus = false;
            GUIUtility.keyboardControl = 0;
            GUI.FocusControl(null);
        }

        // The scrim covers the whole window, including the rails and the timeline. A Button with no
        // style under the card is what actually swallows the clicks: painting a texture leaves every
        // control beneath it live, and the first thing anyone does with a dialog they did not expect
        // is click beside it.
        var prev = GUI.color;
        GUI.color = UITheme.Scrim;
        GUI.DrawTexture(window, UITheme.Pixel);
        GUI.color = prev;
        GUI.Button(window, GUIContent.none, GUIStyle.none);

        CardRect = new Rect(window.x + (window.width - CardW) * 0.5f,
                            window.y + (window.height - CardH) * 0.5f,
                            CardW, CardH);

        UITheme.BeginPanel(CardRect);

        // The title is the question, which is the content this panel exists to show: the one case the
        // no-prose rule exempts. No period, because it is a name for the moment rather than a sentence.
        UITheme.Title(Title(intent, dirty));

        // What is at stake, named rather than described. Only when there is something to lose: with
        // nothing unsaved there is no stake, and a line saying so would be prose.
        if (dirty && !string.IsNullOrEmpty(residenceName))
        {
            UITheme.GapTight();
            UITheme.MutedLine(residenceName + " has unsaved changes",
                              "Everything you have done since the last save");
        }

        UITheme.Gap();

        // Save leads. It is the answer that loses nothing, so it is the one a hurried press should
        // land on, and Enter is bound to it for the same reason.
        if (UITheme.PrimaryButton(SaveLabel(intent), GUILayout.Height(UITheme.PrimaryH)))
            Picked = Answer.SaveAndGo;
        UITheme.Tip(SaveTip(intent));

        UITheme.Gap();

        // DangerButton, and the label carries its own price, which is what every destructive button
        // in this app already does (see DrawResetToSample).
        if (UITheme.DangerButton(GoLabel(intent, dirty), GUILayout.Height(UITheme.PrimaryH)))
            Picked = Answer.GoAnyway;
        UITheme.Tip(GoTip(intent, dirty));

        UITheme.Gap();

        if (UITheme.GhostButton("Keep working", GUILayout.Height(UITheme.PrimaryH)))
            Picked = Answer.KeepWorking;
        UITheme.Tip("Go back to the app with nothing changed  (Esc)");

        UITheme.EndPanel();
    }

    // ---- the words ----
    //
    // US English, no em dash, no en dash, and nothing said by contrast, per the house style. Each
    // label names the act it performs; the sentence about it is on the hover.

    private static string Title(Intent intent, bool dirty) => intent switch
    {
        Intent.OpenResidence   => "Open another residence",
        Intent.NewResidence    => "Start a new residence",
        Intent.ImportResidence => "Import a residence",
        _                      => dirty ? "Close the app with unsaved changes" : "Close the app",
    };

    private static string SaveLabel(Intent intent) =>
        intent == Intent.Quit ? "Save and exit" : "Save and continue";

    private static string SaveTip(Intent intent) =>
        intent == Intent.Quit
            ? "Write this residence to disk, then close the app  (Enter)"
            : "Write this residence to disk, then go on  (Enter)";

    private static string GoLabel(Intent intent, bool dirty)
    {
        if (intent == Intent.Quit) return dirty ? "Exit. Discards unsaved changes" : "Exit";
        return dirty ? "Continue. Discards unsaved changes" : "Continue";
    }

    private static string GoTip(Intent intent, bool dirty)
    {
        if (!dirty)
            return intent == Intent.Quit ? "Close the app" : "Go on. Everything here is already saved";

        return intent == Intent.Quit
            ? "Close the app and let this work go. This cannot be undone."
            : "Go on and let this work go. This cannot be undone.";
    }
}
