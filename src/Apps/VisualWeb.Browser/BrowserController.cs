using System.Text;
using System.Text.RegularExpressions;
using VisualWeb.Core.Encoding;
using VisualWeb.Core.Url;
using VisualWeb.Ipc.Contracts;

namespace VisualWeb.Browser;

public readonly record struct TextareaVisualLine(int Start, int End);

/// <summary>UI-thread transactional navigation over local or asynchronous process renderers.</summary>
/// <remarks>Process results are published only by the UI pump; process separation is not OS confinement.
/// <see cref="BrowserTab.Origin"/> is taken from the browser-side <see cref="LoadedPage"/> and changes only when that
/// document is rendered, accepted by <see cref="IPageRenderer.CommitDocument"/> and published.
/// With <see cref="IsolatesOrigins"/>, top-level navigation keeps each tab renderer strictly per origin
/// (<see href="https://html.spec.whatwg.org/multipage/browsers.html#same-origin">same origin</see>, spec ID html):
/// the final <see cref="LoadedPage.Origin"/> is compared with the committed document origin; same-origin documents
/// reuse the committed renderer, while cross-origin and every new opaque document render in a fresh factory
/// candidate that replaces (and disposes) the committed renderer only after render, commit and history succeed.
/// This is navigation renderer rotation, not site isolation, frame isolation or same-origin policy enforcement.</remarks>
public sealed class BrowserController : IDisposable
{
    private sealed class Content(IPageSource source, IPageRenderer renderer)
    {
        internal IPageSource Source { get; } = source;
        internal IPageRenderer Renderer { get; set; } = renderer;
        /// <summary>Whether any document content has been sent to <see cref="Renderer"/>.</summary>
        internal bool Used { get; set; }
        internal LoadedPage? Document { get; set; }
        internal BrowserPage? Page { get; set; }
        internal PageViewport? Viewport { get; set; }
        internal double ScrollY { get; set; }
        internal bool ResizeFailed { get; set; }
        internal bool PageFocused { get; set; }
        internal int FocusedLink { get; set; } = -1;
        /// <summary>Tree-order index into the page's form controls; mutually exclusive with <see cref="FocusedLink"/>.</summary>
        internal int FocusedControl { get; set; } = -1;
        /// <summary>Browser-owned edited values of the committed document's fields, keyed by control index.</summary>
        internal Dictionary<int, AddressEditor> Fields { get; } = [];
        internal Dictionary<int, int> TextareaFirstLines { get; } = [];
        internal Dictionary<int, IReadOnlyList<TextareaVisualLine>> TextareaLines { get; } = [];
        internal HashSet<int> Dirty { get; } = [];
        internal Dictionary<int, bool> CheckedStates { get; } = [];
        internal Dictionary<int, int> SelectedOptions { get; } = [];
        internal int TextSelectionStart { get; set; } = -1;
        internal int TextSelectionEnd { get; set; } = -1;
        internal bool SelectingText { get; set; }
    }
    private sealed class Operation(TabId tab, long generation, Task<LoadedPage> load, CancellationTokenSource cancellation,
        int? traversal, bool replace, bool resize = false, bool scroll = false)
    {
        internal TabId Tab { get; } = tab;
        internal long Generation { get; } = generation;
        internal Task<LoadedPage> Load { get; } = load;
        internal CancellationTokenSource Cancellation { get; } = cancellation;
        internal int? Traversal { get; } = traversal;
        internal bool Replace { get; } = replace;
        internal bool Resize { get; } = resize;
        internal bool Scroll { get; } = scroll;
        internal LoadedPage? Document { get; set; }
        internal Task<BrowserPage>? Render { get; set; }
        internal PageViewport? Viewport { get; set; }
        /// <summary>The renderer that actually received this operation's document.</summary>
        internal IPageRenderer? Renderer { get; set; }
        /// <summary>An unpromoted origin-isolation candidate owned (and disposed) by this operation.</summary>
        internal IPageRenderer? Candidate { get; set; }
        internal void ReleaseCandidate()
        {
            var candidate = Candidate;
            Candidate = null;
            candidate?.Dispose();
        }
    }
    private readonly Dictionary<TabId, Content> content = [];
    private readonly List<Operation> operations = [];
    private readonly Func<IPageSource> sourceFactory;
    private readonly Func<IPageRenderer> rendererFactory;
    private readonly int thread = Environment.CurrentManagedThreadId;
    private bool disposed;
    public BrowserSession Session { get; }
    /// <summary>Whether top-level navigations rotate to a fresh renderer for each new cross-origin or opaque document.</summary>
    public bool IsolatesOrigins { get; }
    public event Action<TabId>? Changed;
    public event Action<TabId, string>? Failed;

    public BrowserController(Func<IPageSource> sourceFactory, Func<IPageRenderer> rendererFactory, BrowserOptions? options = null,
        bool isolateOrigins = false)
    {
        IsolatesOrigins = isolateOrigins;
        ArgumentNullException.ThrowIfNull(sourceFactory);
        ArgumentNullException.ThrowIfNull(rendererFactory);
        this.sourceFactory = sourceFactory; this.rendererFactory = rendererFactory;
        Session = new(options);
    }
    public BrowserTab CreateTab(BrowserWindowId window)
    {
        Check();
        var tab = Session.CreateTab(window);
        IPageSource? source = null;
        try
        {
            source = sourceFactory();
            content.Add(tab.Id, new(source, rendererFactory()));
            return tab;
        }
        catch { source?.Dispose(); Session.CloseTab(tab.Id); throw; }
    }
    public BrowserPage? Page(TabId tab) { Check(); return content[tab].Page; }
    public double ScrollY(TabId tab) { Check(); return content[tab].ScrollY; }
    public bool PageHasFocus(TabId tab) { Check(); return content[tab].PageFocused; }
    public int FocusedLinkIndex(TabId tab) { Check(); return content[tab].FocusedLink; }
    public void FocusPage(TabId id, bool focused = true)
    {
        Check();
        content[id].PageFocused = focused && content[id].Page is not null;
        Changed?.Invoke(id);
    }
    public void FocusLink(TabId id, int index)
    {
        Check();
        var owner = content[id];
        var count = owner.Page?.LinkTargets.Count ?? 0;
        owner.PageFocused = owner.Page is not null;
        owner.FocusedLink = count == 0 ? -1 : Math.Clamp(index, -1, count - 1);
        owner.FocusedControl = -1;
        Changed?.Invoke(id);
    }
    /// <summary>Moves page focus through the merged tree order of focusable form controls and links, wrapping.</summary>
    /// <returns>The focused link index, or -1 when a form control (or nothing) is focused.</returns>
    public int MoveLinkFocus(TabId id, bool backwards = false)
    {
        Check();
        var owner = content[id];
        var targets = Targets(owner.Page);
        var position = Position(owner, targets);
        var count = targets.Count;
        SetPosition(owner, targets, count == 0 ? -1
            : position < 0 ? backwards ? count - 1 : 0
            : (position + (backwards ? count - 1 : 1)) % count);
        Changed?.Invoke(id);
        return owner.FocusedLink;
    }
    public int FocusedControlIndex(TabId tab) { Check(); return content[tab].FocusedControl; }
    /// <summary>Number of keyboard focus targets on the page: visible enabled non-hidden controls plus links.</summary>
    public int PageFocusCount(TabId tab) { Check(); return Targets(content[tab].Page).Count; }
    public int PageFocusPosition(TabId tab) { Check(); var owner = content[tab]; return Position(owner, Targets(owner.Page)); }
    public void FocusPagePosition(TabId id, int position)
    {
        Check();
        var owner = content[id];
        var targets = Targets(owner.Page);
        SetPosition(owner, targets, targets.Count == 0 ? -1 : Math.Clamp(position, -1, targets.Count - 1));
        Changed?.Invoke(id);
    }
    /// <summary>Begins a browser-owned selection on the visible shaped text fragment under the pointer.</summary>
    public bool StartTextSelection(TabId id, double x, double y, PageViewport? displayedViewport = null)
    {
        Check();
        var owner = content[id];
        if (!double.IsFinite(x) || !double.IsFinite(y))
        { throw new PageNavigationException("Text selection coordinates must be finite."); }
        if (owner.Page is null || owner.Viewport is not { } viewport || !MatchesViewport(owner, displayedViewport)
            || x < 0 || y < 0 || x >= viewport.Width || y >= viewport.Height)
        {
            var changed = owner.TextSelectionStart >= 0;
            ClearTextSelection(owner);
            if (changed) { Changed?.Invoke(id); }
            return false;
        }
        var index = HitText(owner.Page, x, y);
        if (index < 0)
        {
            var changed = owner.TextSelectionStart >= 0;
            ClearTextSelection(owner);
            if (changed) { Changed?.Invoke(id); }
            return false;
        }
        owner.PageFocused = true;
        owner.FocusedLink = -1;
        owner.FocusedControl = -1;
        owner.TextSelectionStart = index;
        owner.TextSelectionEnd = index;
        owner.SelectingText = true;
        Changed?.Invoke(id);
        return true;
    }
    /// <summary>Extends a pointer selection to a visible shaped text fragment.</summary>
    public bool ExtendTextSelection(TabId id, double x, double y, PageViewport? displayedViewport = null)
    {
        Check();
        var owner = content[id];
        if (!double.IsFinite(x) || !double.IsFinite(y))
        { throw new PageNavigationException("Text selection coordinates must be finite."); }
        if (!owner.SelectingText || owner.Page is null || owner.Viewport is not { } viewport
            || !MatchesViewport(owner, displayedViewport) || x < 0 || y < 0 || x >= viewport.Width || y >= viewport.Height)
        { return false; }
        var index = HitText(owner.Page, x, y);
        if (index < 0 || index == owner.TextSelectionEnd) { return false; }
        owner.TextSelectionEnd = index;
        Changed?.Invoke(id);
        return true;
    }
    public void EndTextSelection(TabId id)
    {
        Check();
        content[id].SelectingText = false;
    }
    /// <summary>Selected fragment text in paint order, with line breaks between visibly distinct lines.</summary>
    public string SelectedText(TabId id)
    {
        Check();
        var owner = content[id];
        if (owner.Page is null || owner.TextSelectionStart < 0 || owner.TextSelectionEnd < 0) { return ""; }
        var first = Math.Min(owner.TextSelectionStart, owner.TextSelectionEnd);
        var last = Math.Max(owner.TextSelectionStart, owner.TextSelectionEnd);
        var output = new System.Text.StringBuilder();
        var previousY = owner.Page.TextTargets[first].Rect.Y;
        for (var index = first; index <= last; index++)
        {
            var target = owner.Page.TextTargets[index];
            if (index > first && target.Rect.Y > previousY + 0.5) { output.Append('\n'); }
            output.Append(target.Text);
            previousY = target.Rect.Y;
        }
        return output.ToString();
    }
    public IReadOnlyList<PageLinkRect> SelectedTextRects(TabId id)
    {
        Check();
        var owner = content[id];
        if (owner.Page is null || owner.TextSelectionStart < 0 || owner.TextSelectionEnd < 0)
        { return Array.Empty<PageLinkRect>(); }
        var first = Math.Min(owner.TextSelectionStart, owner.TextSelectionEnd);
        var last = Math.Max(owner.TextSelectionStart, owner.TextSelectionEnd);
        return owner.Page.TextTargets.Skip(first).Take(last - first + 1).Select(target => target.Rect).ToArray();
    }
    private static int HitText(BrowserPage page, double x, double y)
    {
        for (var index = page.TextTargets.Count - 1; index >= 0; index--)
        {
            if (page.TextTargets[index].Rect.Contains(x, y)) { return index; }
        }
        return -1;
    }
    private static void ClearTextSelection(Content owner)
    {
        owner.TextSelectionStart = -1;
        owner.TextSelectionEnd = -1;
        owner.SelectingText = false;
    }
    /// <summary>Focuses a visible, enabled, non-hidden control; returns false (leaving focus unchanged) otherwise.</summary>
    public bool FocusControl(TabId id, int index)
    {
        Check();
        var owner = content[id];
        if (owner.Page is not { } page || index < 0 || index >= page.FormControls.Count || !Focusable(page.FormControls[index]))
        { return false; }
        owner.PageFocused = true;
        owner.FocusedLink = -1;
        owner.FocusedControl = index;
        Changed?.Invoke(id);
        return true;
    }
    /// <summary>Whether committed text should be routed to the focused editable form field.</summary>
    public bool EditingFormControl(TabId id)
    {
        Check();
        var owner = content[id];
        return owner.PageFocused && owner.Page is { } page && owner.FocusedControl >= 0
            && page.FormControls[owner.FocusedControl] is { Kind: "text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "month" or "week" or "number" or "textarea", ReadOnly: false, Disabled: false };
    }
    public bool IsMultilineFormControl(TabId id)
    {
        Check();
        var owner = content[id];
        return EditingFormControl(id) && owner.Page!.FormControls[owner.FocusedControl].Kind == "textarea";
    }
    /// <summary>Current value: the browser-owned edit when present, otherwise the renderer-reported initial value.</summary>
    public bool FormControlChecked(TabId id, int index)
    {
        Check();
        var owner = content[id];
        if (owner.Page is not { } page || index < 0 || index >= page.FormControls.Count
            || page.FormControls[index].Kind is not ("checkbox" or "radio"))
        { throw new ArgumentOutOfRangeException(nameof(index)); }
        return owner.CheckedStates.GetValueOrDefault(index, page.FormControls[index].Checked);
    }
    /// <summary>Toggles the focused enabled checkbox, returning false for other focus targets.</summary>
    public bool ToggleFocusedCheckable(TabId id)
    {
        Check();
        var owner = content[id];
        return owner.PageFocused && owner.FocusedControl >= 0 && owner.Page is { } page
            && ToggleCheckable(id, owner, owner.FocusedControl, page.FormControls[owner.FocusedControl]);
    }
    /// <summary>Activates the focused submit/reset button, or consumes activation of an inert button.</summary>
    public bool ActivateFocusedButton(TabId id)
    {
        Check();
        var owner = content[id];
        if (!owner.PageFocused || owner.FocusedControl < 0 || owner.Page is not { } page) { return false; }
        var control = page.FormControls[owner.FocusedControl];
        if (control.Kind is not ("submit" or "button" or "reset" or "inert")) { return false; }
        if (control.Kind != "inert") { _ = ActivateControl(id, owner, owner.FocusedControl); }
        return true;
    }
    public string FormControlValue(TabId id, int index)
    {
        Check();
        var owner = content[id];
        if (owner.Page is not { } page || index < 0 || index >= page.FormControls.Count)
        { throw new ArgumentOutOfRangeException(nameof(index)); }
        return Value(owner, index);
    }
    public int SelectedOptionIndex(TabId id, int index)
    {
        Check();
        var owner = content[id];
        if (owner.Page is not { } page || index < 0 || index >= page.FormControls.Count
            || page.FormControls[index].Kind != "select")
        { throw new ArgumentOutOfRangeException(nameof(index)); }
        return SelectedOption(owner, index);
    }

    /// <summary>Sets a single-select option from browser-owned popup interaction.</summary>
    public bool SelectOptionFromPointer(TabId id, int controlIndex, int optionIndex)
    {
        Check();
        var owner = content[id];
        if (!owner.PageFocused || owner.FocusedControl != controlIndex || owner.Page is not { } page
            || controlIndex < 0 || controlIndex >= page.FormControls.Count
            || page.FormControls[controlIndex] is not { Kind: "select", Disabled: false } control
            || optionIndex < 0 || optionIndex >= control.Options.Length)
        { return false; }
        if (control.Options[optionIndex].Disabled) { return false; }
        if (SelectedOption(owner, controlIndex) != optionIndex)
        {
            owner.SelectedOptions[controlIndex] = optionIndex;
            Changed?.Invoke(id);
        }
        return true;
    }

    /// <summary>Selects an enabled option matching a bounded browser-owned text prefix.</summary>
    public bool SelectFocusedOptionByPrefix(TabId id, string prefix, bool cycleFromCurrent = false)
    {
        Check();
        ArgumentNullException.ThrowIfNull(prefix);
        if (prefix.Length is 0 or > 64 || prefix.EnumerateRunes().Count() > 32)
        { throw new ArgumentOutOfRangeException(nameof(prefix)); }
        var owner = content[id];
        if (!owner.PageFocused || owner.Page is not { } page || owner.FocusedControl < 0
            || page.FormControls[owner.FocusedControl] is not { Kind: "select", Disabled: false } control)
        { return false; }
        if (control.Options.Length == 0) { return true; }
        var selected = SelectedOption(owner, owner.FocusedControl);
        if (!cycleFromCurrent && selected >= 0 && !control.Options[selected].Disabled
            && control.Options[selected].Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        { return true; }
        var start = selected < 0 ? 0 : (selected + 1) % control.Options.Length;
        for (var offset = 0; offset < control.Options.Length; offset++)
        {
            var candidate = (start + offset) % control.Options.Length;
            if (control.Options[candidate].Disabled
                || !control.Options[candidate].Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { continue; }
            owner.SelectedOptions[owner.FocusedControl] = candidate;
            if (candidate != selected) { Changed?.Invoke(id); }
            return true;
        }
        return true;
    }

    public bool MoveFocusedSelect(TabId id, int direction)
    {
        Check();
        if (direction is not (-1 or 1)) { throw new ArgumentOutOfRangeException(nameof(direction)); }
        var owner = content[id];
        if (!owner.PageFocused || owner.Page is not { } page || owner.FocusedControl < 0
            || page.FormControls[owner.FocusedControl] is not { Kind: "select", Disabled: false } control)
        { return false; }
        var selected = SelectedOption(owner, owner.FocusedControl);
        var candidate = selected < 0 ? (direction > 0 ? 0 : control.Options.Length - 1) : selected + direction;
        while (candidate >= 0 && candidate < control.Options.Length && control.Options[candidate].Disabled)
        { candidate += direction; }
        if (candidate >= 0 && candidate < control.Options.Length && candidate != selected)
        {
            owner.SelectedOptions[owner.FocusedControl] = candidate;
            Changed?.Invoke(id);
        }
        return true;
    }

    public bool MoveFocusedSelectPage(TabId id, int direction, int optionCount)
    {
        Check();
        if (direction is not (-1 or 1)) { throw new ArgumentOutOfRangeException(nameof(direction)); }
        if (optionCount is < 1 or > 12) { throw new ArgumentOutOfRangeException(nameof(optionCount)); }
        var owner = content[id];
        if (!owner.PageFocused || owner.Page is not { } page || owner.FocusedControl < 0
            || page.FormControls[owner.FocusedControl] is not { Kind: "select", Disabled: false } control)
        { return false; }
        var selected = SelectedOption(owner, owner.FocusedControl);
        var candidate = selected < 0 ? (direction > 0 ? 0 : control.Options.Length - 1) : selected + direction;
        var moved = 0;
        while (candidate >= 0 && candidate < control.Options.Length && moved < optionCount)
        {
            if (!control.Options[candidate].Disabled) { moved++; }
            if (moved < optionCount) { candidate += direction; }
        }
        while (candidate >= 0 && candidate < control.Options.Length && control.Options[candidate].Disabled)
        { candidate += direction; }
        if (candidate >= 0 && candidate < control.Options.Length && candidate != selected)
        {
            owner.SelectedOptions[owner.FocusedControl] = candidate;
            Changed?.Invoke(id);
        }
        return true;
    }

    public bool SetFocusedSelectEndpoint(TabId id, bool last)
    {
        Check();
        var owner = content[id];
        if (!owner.PageFocused || owner.Page is not { } page || owner.FocusedControl < 0
            || page.FormControls[owner.FocusedControl] is not { Kind: "select", Disabled: false } control) { return false; }
        var candidate = last ? control.Options.Length - 1 : 0;
        var direction = last ? -1 : 1;
        while (candidate >= 0 && candidate < control.Options.Length && control.Options[candidate].Disabled)
        { candidate += direction; }
        if (candidate >= 0 && candidate < control.Options.Length && candidate != SelectedOption(owner, owner.FocusedControl))
        {
            owner.SelectedOptions[owner.FocusedControl] = candidate;
            Changed?.Invoke(id);
        }
        return true;
    }

    public bool AdjustFocusedRange(TabId id, int direction)
    {
        Check();
        if (direction is not (-1 or 1)) { throw new ArgumentOutOfRangeException(nameof(direction)); }
        var owner = content[id];
        if (!owner.PageFocused || owner.Page is not { } page || owner.FocusedControl < 0
            || page.FormControls[owner.FocusedControl] is not { Kind: "range", Disabled: false } control) { return false; }
        var value = FormNumber.TryParse(Value(owner, owner.FocusedControl), out var parsed) ? parsed : control.Minimum!.Value;
        var step = control.StepAny ? (control.Maximum!.Value - control.Minimum!.Value) / 100 : control.Step!.Value;
        if (!double.IsFinite(step) || step <= 0) { step = 1; }
        SetRangeValue(owner, owner.FocusedControl, Math.Clamp(value + direction * step, control.Minimum!.Value, control.Maximum!.Value));
        Changed?.Invoke(id);
        return true;
    }
    public bool SetFocusedRangeEndpoint(TabId id, bool maximum)
    {
        Check();
        var owner = content[id];
        if (!owner.PageFocused || owner.Page is not { } page || owner.FocusedControl < 0
            || page.FormControls[owner.FocusedControl] is not { Kind: "range", Disabled: false } control) { return false; }
        SetRangeValue(owner, owner.FocusedControl, maximum ? control.Maximum!.Value : control.Minimum!.Value);
        Changed?.Invoke(id);
        return true;
    }
    public bool SetRangeFromPointer(TabId id, int index, double x)
    {
        Check();
        if (!double.IsFinite(x)) { throw new PageNavigationException("Range pointer coordinate must be finite."); }
        var owner = content[id];
        if (owner.Page is not { } page || index < 0 || index >= page.FormControls.Count
            || page.FormControls[index] is not { Kind: "range", Disabled: false, Rect: { } rect } control) { return false; }
        var inset = Math.Min(7, rect.Width / 2);
        var usable = Math.Max(0, rect.Width - inset * 2);
        var fraction = usable == 0 ? 0.5 : Math.Clamp((x - rect.X - inset) / usable, 0, 1);
        var value = control.Minimum!.Value * (1 - fraction) + control.Maximum!.Value * fraction;
        SetRangeValue(owner, index, value);
        Changed?.Invoke(id);
        return true;
    }
    private static void SetRangeValue(Content owner, int index, double value)
    {
        var control = owner.Page!.FormControls[index];
        var minimum = control.Minimum!.Value;
        var maximum = control.Maximum!.Value;
        value = Math.Clamp(value, minimum, maximum);
        if (!control.StepAny && control.Step is { } step)
        {
            var quotient = (value - minimum) / step;
            if (double.IsFinite(quotient) && Math.Abs(quotient) <= 1_000_000_000_000d)
            {
                var snapped = minimum + Math.Floor(quotient + 0.5) * step;
                if (snapped > maximum) { snapped = minimum + Math.Floor(quotient) * step; }
                value = Math.Clamp(snapped, minimum, maximum);
            }
        }
        var editor = Field(owner, index);
        editor.Reset(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        owner.Dirty.Add(index);
    }
    /// <summary>UTF-16 caret of the focused editable field, or -1.</summary>
    public int FormControlCaret(TabId id)
    {
        Check();
        var owner = content[id];
        return owner.FocusedControl >= 0 && owner.Page?.FormControls[owner.FocusedControl].Kind is "text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "month" or "week" or "number" or "textarea"
            ? Field(owner, owner.FocusedControl).Caret : -1;
    }
    public int TextareaFirstLine(TabId id, int index)
    {
        Check();
        var owner = content[id];
        if (owner.Page is not { } page || index < 0 || index >= page.FormControls.Count
            || page.FormControls[index].Kind != "textarea")
        { throw new ArgumentOutOfRangeException(nameof(index)); }
        return owner.TextareaFirstLines.GetValueOrDefault(index);
    }
    internal void UpdateTextareaVisualLines(TabId id, int index, IReadOnlyList<TextareaVisualLine> lines)
    {
        Check();
        ArgumentNullException.ThrowIfNull(lines);
        var owner = content[id];
        if (owner.Page is not { } page || index < 0 || index >= page.FormControls.Count
            || page.FormControls[index].Kind != "textarea")
        { throw new ArgumentOutOfRangeException(nameof(index)); }
        var value = Value(owner, index);
        if (lines.Count == 0 || lines[0].Start != 0 || lines[^1].End != value.Length
            || lines.Any(line => line.Start < 0 || line.End < line.Start || line.End > value.Length)
            || lines.Zip(lines.Skip(1)).Any(pair => pair.First.End > pair.Second.Start
                || pair.Second.Start - pair.First.End > 1
                || pair.Second.Start > pair.First.End && value[pair.First.End] != '\n'))
        { throw new ArgumentException("Textarea visual lines must cover the value in order.", nameof(lines)); }
        owner.TextareaLines[index] = lines.ToArray();
        owner.TextareaFirstLines[index] = Math.Clamp(owner.TextareaFirstLines.GetValueOrDefault(index),
            0, Math.Max(0, lines.Count - TextareaVisibleRows(page.FormControls[index])));
        if (owner.Fields.TryGetValue(index, out var editor)) { EnsureTextareaCaretVisible(owner, index, editor); }
    }
    public bool ScrollTextareaAt(TabId id, double x, double y, int lines)
    {
        Check();
        if (!double.IsFinite(x) || !double.IsFinite(y)) { throw new PageNavigationException("Textarea scroll coordinates must be finite."); }
        var owner = content[id];
        if (owner.Page is not { } page || owner.Viewport is not { } viewport || x < 0 || y < 0
            || x >= viewport.Width || y >= viewport.Height) { return false; }
        for (var index = page.FormControls.Count - 1; index >= 0; index--)
        {
            var control = page.FormControls[index];
            if (control is not { Kind: "textarea", Disabled: false, Rect: { } rect } || !rect.Contains(x, y)) { continue; }
            var rows = TextareaVisibleRows(control);
            var lineCount = CurrentTextareaLines(owner, index, Value(owner, index)).Count;
            var maximum = Math.Max(0, lineCount - rows);
            var current = owner.TextareaFirstLines.GetValueOrDefault(index);
            var next = (int)Math.Clamp((long)current + lines, 0, maximum);
            if (next != current)
            {
                owner.TextareaFirstLines[index] = next;
                Changed?.Invoke(id);
            }
            return true;
        }
        return false;
    }
    /// <summary>Whether the focused editable text field has its full value selected.</summary>
    public bool FormControlSelectAll(TabId tab)
    {
        Check();
        var owner = content[tab];
        return EditingFormControl(tab) && Field(owner, owner.FocusedControl).SelectAll;
    }
    /// <summary>Selects all text in the focused editable text field.</summary>
    public void SelectAllFormControl(TabId tab)
    {
        Check();
        if (!EditingFormControl(tab)) { throw new PageNavigationException("No editable form field is focused."); }
        var owner = content[tab];
        Field(owner, owner.FocusedControl).SelectAll = true;
        Changed?.Invoke(tab);
    }
    /// <summary>Inserts committed text at the caret, truncating to maxlength like user input and bounding total length.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fe-maxlength">maxlength</see>
    /// (UTF-16 code units). Inserted CR/LF and other control characters are rejected rather than stripped.</remarks>
    public string InsertFormText(TabId id, string text)
    {
        Check();
        ArgumentNullException.ThrowIfNull(text);
        if (!EditingFormControl(id)) { throw new PageNavigationException("No editable form field is focused."); }
        var owner = content[id];
        var index = owner.FocusedControl;
        var control = owner.Page!.FormControls[index];
        if (control.Kind == "textarea")
        {
            text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            if (text.Any(c => char.IsControl(c) && c != '\n'))
            { throw new PageNavigationException("Only line feeds are accepted as control characters in textareas."); }
        }
        else if (text.Any(char.IsControl)) { throw new PageNavigationException("Control characters are not accepted in form fields."); }
        var editor = Field(owner, index);
        var existingLength = editor.SelectAll ? 0 : editor.Text.Length;
        if (control.MaxLength >= 0)
        {
            var room = Math.Max(0, control.MaxLength - existingLength);
            if (text.Length > room) { text = text[..room]; }
            if (text.Length > 0 && char.IsHighSurrogate(text[^1])) { text = text[..^1]; }
        }
        if ((long)existingLength + text.Length > RendererProtocol.MaxTextCharacters)
        { throw new BrowserLimitException("Form field length limit exceeded."); }
        editor.Insert(text, RendererProtocol.MaxTextCharacters, allowLineFeed: control.Kind == "textarea");
        if (control.Kind == "textarea") { owner.TextareaLines.Remove(index); }
        owner.Dirty.Add(index);
        if (control.Kind == "textarea") { EnsureTextareaCaretVisible(owner, index, editor); }
        Changed?.Invoke(id);
        return editor.Text;
    }
    public string EditFormControl(TabId id, FormEdit edit)
    {
        Check();
        if (!EditingFormControl(id)) { throw new PageNavigationException("No editable form field is focused."); }
        var owner = content[id];
        var editor = Field(owner, owner.FocusedControl);
        switch (edit)
        {
            case FormEdit.Backspace:
                editor.Backspace(); owner.TextareaLines.Remove(owner.FocusedControl); owner.Dirty.Add(owner.FocusedControl); break;
            case FormEdit.Delete:
                editor.Delete(); owner.TextareaLines.Remove(owner.FocusedControl); owner.Dirty.Add(owner.FocusedControl); break;
            case FormEdit.Left: editor.Left(); break;
            case FormEdit.Right: editor.Right(); break;
            case FormEdit.Up when owner.Page!.FormControls[owner.FocusedControl].Kind == "textarea":
                editor.Up(CurrentTextareaLines(owner, owner.FocusedControl, editor.Text)); break;
            case FormEdit.Down when owner.Page!.FormControls[owner.FocusedControl].Kind == "textarea":
                var visualLines = CurrentTextareaLines(owner, owner.FocusedControl, editor.Text);
                editor.Down(visualLines); break;
            case FormEdit.Home when owner.Page!.FormControls[owner.FocusedControl].Kind == "textarea":
                editor.HomeLine(CurrentTextareaLines(owner, owner.FocusedControl, editor.Text)); break;
            case FormEdit.End when owner.Page!.FormControls[owner.FocusedControl].Kind == "textarea":
                editor.EndLine(CurrentTextareaLines(owner, owner.FocusedControl, editor.Text)); break;
            case FormEdit.Home: editor.Home(); break;
            case FormEdit.End: editor.End(); break;
            default: throw new ArgumentOutOfRangeException(nameof(edit));
        }
        if (owner.Page!.FormControls[owner.FocusedControl].Kind == "textarea")
        { EnsureTextareaCaretVisible(owner, owner.FocusedControl, editor); }
        Changed?.Invoke(id);
        return editor.Text;
    }
    private static void EnsureTextareaCaretVisible(Content owner, int index, AddressEditor editor)
    {
        var control = owner.Page!.FormControls[index];
        if (control.Rect is not { } rect) { return; }
        var rows = TextareaVisibleRows(control);
        var lines = CurrentTextareaLines(owner, index, editor.Text);
        var caret = Math.Clamp(editor.Caret, 0, editor.Text.Length);
        var caretLine = AddressEditor.FindVisualLine(lines, caret);
        var first = owner.TextareaFirstLines.GetValueOrDefault(index);
        if (caretLine < first) { first = caretLine; }
        else if (caretLine >= first + rows) { first = caretLine - rows + 1; }
        owner.TextareaFirstLines[index] = Math.Clamp(first, 0, Math.Max(0, lines.Count - rows));
    }
    private static int TextareaVisibleRows(PageFormControl control) =>
        Math.Max(1, (int)((control.Rect?.Height ?? 23) - 8) / 15);
    private static IReadOnlyList<TextareaVisualLine> CurrentTextareaLines(Content owner, int index, string value)
    {
        if (owner.TextareaLines.TryGetValue(index, out var lines) && lines.Count > 0 && lines[^1].End == value.Length)
        { return lines; }
        var result = new List<TextareaVisualLine>();
        var start = 0;
        for (var offset = 0; offset < value.Length; offset++)
        {
            if (value[offset] != '\n') { continue; }
            result.Add(new(start, offset));
            start = offset + 1;
        }
        result.Add(new(start, value.Length));
        return result;
    }
    private static bool Focusable(PageFormControl control) => control.Kind != "hidden" && !control.Disabled && control.Rect is not null;
    private static List<(bool Control, int Index)> Targets(BrowserPage? page)
    {
        var targets = new List<(bool, int)>();
        if (page is null) { return targets; }
        var control = 0;
        for (var link = 0; link <= page.LinkTargets.Count; link++)
        {
            for (; control < page.FormControls.Count && page.FormControls[control].BeforeLink <= link; control++)
            {
                if (Focusable(page.FormControls[control])) { targets.Add((true, control)); }
            }
            if (link < page.LinkTargets.Count) { targets.Add((false, link)); }
        }
        return targets;
    }
    private static int Position(Content owner, List<(bool Control, int Index)> targets) =>
        owner.FocusedControl >= 0 ? targets.IndexOf((true, owner.FocusedControl))
        : owner.FocusedLink >= 0 ? targets.IndexOf((false, owner.FocusedLink)) : -1;
    private static void SetPosition(Content owner, List<(bool Control, int Index)> targets, int position)
    {
        owner.PageFocused = owner.Page is not null;
        var target = position < 0 ? ((bool, int)?)null : targets[position];
        owner.FocusedLink = target is (false, var link) ? link : -1;
        owner.FocusedControl = target is (true, var control) ? control : -1;
    }
    private static AddressEditor Field(Content owner, int index)
    {
        if (!owner.Fields.TryGetValue(index, out var editor))
        {
            editor = new AddressEditor();
            editor.Reset(owner.Page!.FormControls[index].Value);
            owner.Fields.Add(index, editor);
        }
        return editor;
    }
    private static string Value(Content owner, int index)
    {
        var control = owner.Page!.FormControls[index];
        if (control.Kind == "select")
        {
            var selected = SelectedOption(owner, index);
            return selected >= 0 && !control.Options[selected].Disabled ? control.Options[selected].Value : "";
        }
        return owner.Fields.TryGetValue(index, out var editor) ? editor.Text : control.Value;
    }

    private static int SelectedOption(Content owner, int index)
    {
        var control = owner.Page!.FormControls[index];
        if (owner.SelectedOptions.TryGetValue(index, out var selected)) { return selected; }
        return Array.FindIndex(control.Options, option => option.Selected);
    }
    public bool ActivateFocusedLink(TabId id, PageViewport? displayedViewport = null)
    {
        Check();
        var owner = content[id];
        if (!owner.PageFocused || owner.Page is null || !MatchesViewport(owner, displayedViewport)) { return false; }
        if (owner.FocusedControl >= 0) { return ActivateControl(id, owner, owner.FocusedControl); }
        if (owner.FocusedLink < 0 || owner.FocusedLink >= owner.Page.LinkTargets.Count) { return false; }
        var link = owner.Page.LinkTargets[owner.FocusedLink];
        return NavigateLink(id, link.Url, openInNewTab: link.OpenInNewTab);
    }
    /// <summary>Enter activates submit/reset controls; text fields perform implicit submission. Checkable controls use Space or pointer activation.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#implicit-submission">implicit
    /// submission</see>. The default button is the first submit button of the form in tree order.</remarks>
    private bool ToggleCheckable(TabId id, Content owner, int index, PageFormControl control)
    {
        if (control.Kind is not ("checkbox" or "radio") || control.Disabled) { return false; }
        if (control.Kind == "checkbox")
        { owner.CheckedStates[index] = !owner.CheckedStates.GetValueOrDefault(index, control.Checked); }
        else { SelectRadio(owner, index); }
        Changed?.Invoke(id);
        return true;
    }
    private static void SelectRadio(Content owner, int selected)
    {
        var controls = owner.Page!.FormControls;
        var control = controls[selected];
        for (var index = 0; index < controls.Count; index++)
        {
            if (index != selected && RadioGroupMember(controls[index], control)) { owner.CheckedStates[index] = false; }
        }
        owner.CheckedStates[selected] = true;
    }
    private static bool RadioGroupMember(PageFormControl candidate, PageFormControl selected) =>
        candidate.Kind == "radio" && candidate.Form == selected.Form && selected.Name.Length > 0
        && candidate.Name == selected.Name;
    public bool MoveFocusedRadio(TabId id, int direction)
    {
        Check();
        var owner = content[id];
        if (direction is not (-1 or 1) || !owner.PageFocused || owner.Page is not { } page
            || owner.FocusedControl < 0 || page.FormControls[owner.FocusedControl].Kind != "radio") { return false; }
        var current = owner.FocusedControl;
        var selected = page.FormControls[current];
        if (selected.Name.Length == 0) { return false; }
        var group = Enumerable.Range(0, page.FormControls.Count)
            .Where(index => RadioGroupMember(page.FormControls[index], selected)
                && !page.FormControls[index].Disabled).ToArray();
        if (group.Length < 2) { return false; }
        var position = Array.IndexOf(group, current);
        var next = group[(position + direction + group.Length) % group.Length];
        SelectRadio(owner, next);
        owner.FocusedControl = next;
        Changed?.Invoke(id);
        return true;
    }
    private static bool RadioGroupChecked(Content owner, int selectedIndex)
    {
        var controls = owner.Page!.FormControls;
        var selected = controls[selectedIndex];
        for (var index = 0; index < controls.Count; index++)
        {
            if ((index == selectedIndex || RadioGroupMember(controls[index], selected))
                && owner.CheckedStates.GetValueOrDefault(index, controls[index].Checked)) { return true; }
        }
        return false;
    }
    private bool ActivateControl(TabId id, Content owner, int index)
    {
        var controls = owner.Page!.FormControls;
        var control = controls[index];
        if (control.Disabled) { return false; }
        if (control.Form < 0) { return false; }
        if (control.Kind == "reset")
        {
            ResetForm(id, owner, control.Form);
            return true;
        }
        if (control.Kind is "submit" or "button") { return Submit(id, owner, control.Form, index); }
        if (control.Kind == "select") { return true; }
        if (control.Kind is not ("text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "month" or "week" or "number")) { return false; }
        for (var candidate = 0; candidate < controls.Count; candidate++)
        {
            if (controls[candidate].Form == control.Form && controls[candidate].Kind is "submit" or "button")
            { return !controls[candidate].Disabled && Submit(id, owner, control.Form, candidate); }
        }
        if (controls.Count(c => c.Form == control.Form && c.Kind is "text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "month" or "week" or "number") > 1) { return false; }
        return Submit(id, owner, control.Form, -1);
    }
    private void ResetForm(TabId id, Content owner, int formIndex)
    {
        var controls = owner.Page!.FormControls;
        for (var index = 0; index < controls.Count; index++)
        {
            if (controls[index].Form != formIndex) { continue; }
            owner.Fields.Remove(index);
            owner.Dirty.Remove(index);
            owner.CheckedStates.Remove(index);
            owner.SelectedOptions.Remove(index);
            owner.TextareaFirstLines.Remove(index);
            owner.TextareaLines.Remove(index);
        }
        Changed?.Invoke(id);
    }
    /// <summary>Validates and submits one form as a bounded GET through <see cref="Navigate"/> (HSTS, redirects, origin commit).</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-algorithm">form
    /// submission algorithm</see> and <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#interactively-validate-the-constraints">interactive
    /// validation</see> (required, URL, pattern, minlength, and maxlength checks). Failures throw before any request.</remarks>
    private bool Submit(TabId id, Content owner, int formIndex, int submitter)
    {
        var page = owner.Page!;
        var form = page.Forms[formIndex];
        if (form.Error is { } error) { throw new PageNavigationException(error); }
        var document = owner.Document!;
        var submitterControl = submitter >= 0 ? page.FormControls[submitter] : null;
        if (submitterControl?.FormTargetError is { } submitterTargetError)
        { throw new PageNavigationException(submitterTargetError); }
        if (submitterControl?.FormTargetOpenInNewTab is null && form.TargetError is { } targetError)
        { throw new PageNavigationException(targetError); }
        var openInNewTab = submitterControl?.FormTargetOpenInNewTab ?? form.OpenInNewTab;
        if (submitterControl?.FormActionError is { } actionError) { throw new PageNavigationException(actionError); }
        var action = BrowserUrl.Parse(submitterControl?.FormAction ?? form.Action);
        if (action.Protocol is "http:" or "https:")
        {
            if (document.Url.Protocol == "data:")
            { throw new PageNavigationException("Network form submissions from opaque documents are blocked."); }
            if (document.Url.Protocol is "http:" or "https:"
                && !document.Origin.IsSameOrigin(action.Origin)
                && !CanUpgradeSameHostFormAction(document.Origin, action.Origin))
            { throw new PageNavigationException("Cross-origin form submissions are blocked; forms must target the document's origin."); }
        }
        var encoding = document.CharacterEncoding;
        if (encoding is not ("UTF-8" or "UTF-16BE" or "UTF-16LE" or "replacement"))
        { throw new PageNavigationException($"Form submission encoding {encoding} is unsupported; only UTF-8 is implemented."); }
        var normalizedEmailValues = new Dictionary<int, string>();
        var skipValidation = form.NoValidate || submitter >= 0 && page.FormControls[submitter].FormNoValidate;
        for (var index = 0; index < page.FormControls.Count; index++)
        {
            var control = page.FormControls[index];
            if (control.Form != formIndex || control.Kind is not ("text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "month" or "week" or "number" or "range" or "textarea" or "checkbox" or "radio" or "select") || control.Disabled || control.ReadOnly) { continue; }
            var value = Value(owner, index);
            if (control.Kind == "email")
            {
                value = FormEmail.Sanitize(value, control.Multiple);
                normalizedEmailValues[index] = value;
            }
            if (skipValidation) { continue; }
            var isChecked = owner.CheckedStates.GetValueOrDefault(index, control.Checked);
            if (control.Required && (control.Kind == "checkbox" ? !isChecked
                : control.Kind == "radio" ? !RadioGroupChecked(owner, index) : value.Length == 0))
            { throw new PageNavigationException($"Form field '{control.Name}' is required; submission blocked."); }
            if (control.Kind == "date" && value.Length > 0 && !FormDate.IsValid(value))
            { throw new PageNavigationException($"Form field '{control.Name}' must contain a valid date; submission blocked."); }
            if (control.Kind == "time" && value.Length > 0 && !FormTime.IsValid(value))
            { throw new PageNavigationException($"Form field '{control.Name}' must contain a valid time; submission blocked."); }
            if (control.Kind == "month" && value.Length > 0 && !FormMonth.IsValid(value))
            { throw new PageNavigationException($"Form field '{control.Name}' must contain a valid month; submission blocked."); }
            if (control.Kind == "week" && value.Length > 0 && !FormWeek.IsValid(value))
            { throw new PageNavigationException($"Form field '{control.Name}' must contain a valid week; submission blocked."); }
            if (control.Kind == "url" && value.Length > 0 && (value != value.Trim() || !BrowserUrl.ParseResult(value).Success))
            { throw new PageNavigationException($"Form field '{control.Name}' must contain a valid absolute URL; submission blocked."); }
            if (control.Kind == "email" && value.Length > 0 && !FormEmail.IsValid(value, control.Multiple))
            {
                var requiredFormat = control.Multiple ? "valid email address list" : "valid email address";
                throw new PageNavigationException($"Form field '{control.Name}' must contain a {requiredFormat}; submission blocked.");
            }
            if ((control.Kind is "number" or "range") && value.Length > 0)
            {
                if (!FormNumber.TryParse(value, out var number))
                { throw new PageNavigationException($"Form field '{control.Name}' must contain a valid number; submission blocked."); }
                if (control.Minimum is { } minimum && number < minimum)
                { throw new PageNavigationException($"Form field '{control.Name}' is below its minimum; submission blocked."); }
                if (control.Maximum is { } maximum && number > maximum)
                { throw new PageNavigationException($"Form field '{control.Name}' exceeds its maximum; submission blocked."); }
                if (!control.StepAny)
                {
                    var stepBase = control.Minimum ?? (FormNumber.TryParse(control.Value, out var initialValue) ? initialValue : 0);
                    if (!FormNumber.IsStepAligned(number, stepBase, control.Step!.Value))
                    { throw new PageNavigationException($"Form field '{control.Name}' does not match its step; submission blocked."); }
                }
            }
            if (control.Pattern is { } pattern && value.Length > 0)
            {
                try
                {
                    if (!FormPattern.Matches(pattern, value))
                    { throw new PageNavigationException($"Form field '{control.Name}' does not match its pattern; submission blocked."); }
                }
                catch (RegexMatchTimeoutException)
                { throw new PageNavigationException($"Form field '{control.Name}' pattern matching exceeded the time limit; submission blocked."); }
                catch (ArgumentException)
                { throw new PageNavigationException($"Form field '{control.Name}' has an unsupported pattern; submission blocked."); }
                catch (NotSupportedException)
                { throw new PageNavigationException($"Form field '{control.Name}' has an unsupported pattern; submission blocked."); }
            }
            if (control.MinLength > 0 && owner.Dirty.Contains(index) && value.Length > 0 && value.Length < control.MinLength)
            { throw new PageNavigationException($"Form field '{control.Name}' is shorter than minlength {control.MinLength}; submission blocked."); }
            if (control.MaxLength >= 0 && owner.Dirty.Contains(index) && value.Length > control.MaxLength)
            { throw new PageNavigationException($"Form field '{control.Name}' exceeds maxlength {control.MaxLength}; submission blocked."); }
        }
        var limit = Session.Options.MaxAddressCharacters;
        var preventHttpsDowngrade = document.Url.Protocol is "https:" or "data:";
        var sameOriginRedirectOrigin = document.Url.Protocol is "http:" or "https:" ? document.Origin : null;
        if (openInNewTab) { owner.Source.ValidateNavigationTarget(action, preventHttpsDowngrade, sameOriginRedirectOrigin); }
        var query = FormSubmission.Serialize(FormSubmission.Entries(page.FormControls, formIndex, submitter,
            i => normalizedEmailValues.TryGetValue(i, out var email) ? email
                : page.FormControls[i] is { Kind: "textarea", TextareaWrapHard: true } textarea
                    ? FormSubmission.HardWrapTextarea(Value(owner, i), textarea.TextareaWrapColumns)
                    : Value(owner, i),
            i => owner.CheckedStates.GetValueOrDefault(i, page.FormControls[i].Checked), i =>
            {
                var control = page.FormControls[i];
                if (control.Kind != "select") { return true; }
                var selected = SelectedOption(owner, i);
                return selected >= 0 && !control.Options[selected].Disabled;
            }), limit);
        return NavigateLink(id, FormSubmission.ApplyQuery(action, query, limit).Href, preventHttpsDowngrade,
            sameOriginRedirectOrigin, openInNewTab);
    }
    private static bool CanUpgradeSameHostFormAction(SecurityOrigin documentOrigin, SecurityOrigin actionOrigin) =>
        documentOrigin.Scheme == "https" && actionOrigin.Scheme == "http"
        && documentOrigin.Host == actionOrigin.Host
        && (actionOrigin.Port == 80 ? documentOrigin.Port == 443 : documentOrigin.Port == actionOrigin.Port);
    private static bool MatchesViewport(Content owner, PageViewport? displayedViewport) =>
        owner.Viewport is { } viewport && (displayedViewport is not { } visible
            || (viewport.Width == visible.Width && viewport.Height == visible.Height && viewport.Scale == visible.Scale));
    private bool NavigateLink(TabId id, string destination, bool preventHttpsDowngrade = false,
        SecurityOrigin? sameOriginRedirectOrigin = null, bool openInNewTab = false)
    {
        var sourceDocument = content[id].Document;
        preventHttpsDowngrade = preventHttpsDowngrade
            || (sourceDocument?.Url.Protocol is "https:" or "data:");
        var url = BrowserUrl.Parse(destination);
        if (url.Protocol is not ("http:" or "https:" or "file:" or "data:"))
        { throw new PageNavigationException("Unsupported link URL scheme: " + url.Protocol); }
        if (!openInNewTab && sameOriginRedirectOrigin is null && TryNavigateFragment(id, url)) { return true; }
        if (url.Protocol == "file:")
        { throw new PageNavigationException("Page-initiated file navigation is blocked; enter local file URLs in the address bar."); }
        if (url.Protocol == "data:")
        { throw new PageNavigationException("Page-initiated data URL navigation is blocked; enter data URLs in the address bar."); }
        if (sourceDocument?.Url.Protocol == "file:" && url.Protocol is "http:" or "https:")
        {
            throw new PageNavigationException("Page-initiated network navigation is blocked from local file documents; use the address bar.");
        }
        var target = id;
        if (openInNewTab)
        {
            var window = Session.Windows.Single(candidate => candidate.Tabs.Any(tab => tab.Id == id));
            target = CreateTab(window.Id).Id;
        }
        try { Start(Session.Tab(target), url, null, false, preventHttpsDowngrade, sameOriginRedirectOrigin); }
        catch
        {
            if (target != id)
            {
                CloseTab(target);
                Changed?.Invoke(id);
            }
            throw;
        }
        return true;
    }
    private bool TryNavigateFragment(TabId id, BrowserUrl destination)
    {
        var tab = Session.Tab(id);
        var owner = content[id];
        if (tab.IsLoading || owner.Document is not { } document || owner.Page is not { } page
            || FragmentBase(document.Url) != FragmentBase(destination)) { return false; }
        ScrollToFragment(id, destination, page);
        if (tab.History.Current?.Href != destination.Href) { tab.History.CommitSameDocument(destination); }
        tab.AddressText = destination.Href;
        tab.Error = null;
        Changed?.Invoke(id);
        return true;
    }

    private bool TryTraverseSameDocument(TabId id, int index)
    {
        var tab = Session.Tab(id);
        var owner = content[id];
        if (tab.IsLoading || owner.Page is not { } page || !tab.History.IsSameDocumentAsCurrent(index)) { return false; }

        var destination = tab.History.Entries[index];
        tab.History.Commit(destination, traversalIndex: index);
        tab.AddressText = destination.Href;
        tab.Error = null;
        ScrollToFragment(id, destination, page);
        Changed?.Invoke(id);
        return true;
    }

    private void ScrollToFragment(TabId id, BrowserUrl destination, BrowserPage page)
    {
        var fragmentIndex = destination.Href.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? Uri.UnescapeDataString(destination.Href[(fragmentIndex + 1)..]) : "";
        if (fragment.Length == 0) { Scroll(id, -content[id].ScrollY); }
        else if (page.FragmentTargets.FirstOrDefault(target => target.Id == fragment) is { } target)
        { Scroll(id, target.Y - content[id].ScrollY); }
    }

    private static string FragmentBase(BrowserUrl url)
    {
        var fragment = url.Href.IndexOf('#');
        return fragment < 0 ? url.Href : url.Href[..fragment];
    }

    public bool ActivateLink(TabId id, double x, double y, PageViewport? displayedViewport = null,
        bool forceNewTab = false)
    {
        Check();
        var owner = content[id];
        if (!double.IsFinite(x) || !double.IsFinite(y))
        { throw new PageNavigationException("Link coordinates must be finite."); }
        if (owner.Page is null || owner.Viewport is not { } viewport
            || x < 0 || y < 0 || x >= viewport.Width || y >= viewport.Height) { return false; }
        if (!MatchesViewport(owner, displayedViewport))
        { return false; }
        for (var index = owner.Page.FormControls.Count - 1; index >= 0; index--)
        {
            var control = owner.Page.FormControls[index];
            if (control.Kind == "hidden" || control.Rect?.Contains(x, y) != true) { continue; }
            if (forceNewTab || control.Disabled) { return false; }
            if (owner.PageFocused) { FocusControl(id, index); }
            if (control.Kind is "submit" or "button" or "reset") { return ActivateControl(id, owner, index); }
            if (control.Kind is "checkbox" or "radio") { return ToggleCheckable(id, owner, index, control); }
            if (control.Kind == "range") { SetRangeFromPointer(id, index, x); return true; }
            return control.Kind is "text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "month" or "week" or "number" or "textarea" or "select";
        }
        for (var index = owner.Page.LinkTargets.Count - 1; index >= 0; index--)
        {
            var link = owner.Page.LinkTargets[index];
            if (!link.Contains(x, y)) { continue; }
            if (owner.PageFocused) { FocusLink(id, index); }
            return NavigateLink(id, link.Url, openInNewTab: forceNewTab || link.OpenInNewTab);
        }
        return false;
    }
    public void SetAddress(TabId id, string value)
    {
        Check();
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > Session.Options.MaxAddressCharacters) { throw new BrowserLimitException("Address length limit exceeded."); }
        Session.Tab(id).AddressText = value;
        Changed?.Invoke(id);
    }
    public void Navigate(TabId id, string address)
    {
        Check();
        ArgumentNullException.ThrowIfNull(address);
        var tab = Session.Tab(id);
        if (address.Length > Session.Options.MaxAddressCharacters) { throw new BrowserLimitException("Address length limit exceeded."); }
        Start(tab, BrowserUrl.Parse(address), null, false);
    }
    public void Back(TabId id)
    {
        Check();
        var tab = Session.Tab(id);
        if (!tab.History.CanGoBack) { throw new InvalidOperationException("No back history entry."); }
        var index = tab.History.Index - 1;
        if (TryTraverseSameDocument(id, index)) { return; }
        Start(tab, tab.History.Entries[index], index, false);
    }
    public void Forward(TabId id)
    {
        Check();
        var tab = Session.Tab(id);
        if (!tab.History.CanGoForward) { throw new InvalidOperationException("No forward history entry."); }
        var index = tab.History.Index + 1;
        if (TryTraverseSameDocument(id, index)) { return; }
        Start(tab, tab.History.Entries[index], index, false);
    }
    public void Reload(TabId id)
    {
        Check();
        var tab = Session.Tab(id);
        Start(tab, tab.History.Current ?? throw new InvalidOperationException("No committed page to reload."), null, true);
    }
    private void Start(BrowserTab tab, BrowserUrl url, int? traversal, bool replace, bool preventHttpsDowngrade = false,
        SecurityOrigin? sameOriginRedirectOrigin = null)
    {
        Limit();
        Cancel(tab.Id);
        var owner = content[tab.Id];
        owner.ScrollY = owner.Viewport?.ScrollY ?? 0;
        var cancellation = new CancellationTokenSource();
        Task<LoadedPage> task;
        try { task = content[tab.Id].Source.LoadAsync(url, cancellation.Token, preventHttpsDowngrade, sameOriginRedirectOrigin); }
        catch { cancellation.Dispose(); throw; }
        operations.Add(new(tab.Id, checked(++tab.Generation), task, cancellation, traversal, replace));
        tab.AddressText = url.Href; tab.Error = null; tab.IsLoading = true; tab.Status = "Loading " + url.Href;
        Changed?.Invoke(tab.Id);
    }
    /// <summary>Observe completed loading/rendering without awaiting on the UI thread.</summary>
    public void Pump(Func<TabId, PageViewport?> viewport)
    {
        Check();
        ArgumentNullException.ThrowIfNull(viewport);
        foreach (var (id, owner) in content)
        {
            if (owner.Renderer.TakeFailure() is { } failure) { Report(id, failure, finishNavigation: false); }
        }
        foreach (var operation in operations.ToArray())
        {
            Advance(operation, () => viewport(operation.Tab));
        }
    }
    private void Advance(Operation operation, Func<PageViewport?> viewport)
    {
        var current = Session.Contains(operation.Tab) && Session.Tab(operation.Tab).Generation == operation.Generation;
        if (!operation.Load.IsCompleted || operation.Render is { IsCompleted: false }) { return; }
        var finished = true;
        try
        {
            var document = operation.Load.GetAwaiter().GetResult();
            if (!current)
            {
                if (operation.Render is { } stale) { _ = stale.GetAwaiter().GetResult(); }
                return;
            }
            if (operation.Render is null)
            {
                var size = operation.Viewport ?? viewport();
                if (size is null) { finished = false; return; }
                operation.Document = document;
                operation.Viewport = operation.Resize ? size : size.Value with { ScrollY = 0 };
                var renderer = operation.Renderer = Select(content[operation.Tab], operation, document);
                operation.Render = operation.Resize
                    ? renderer.RenderRetainedAsync(document, operation.Viewport.Value, operation.Cancellation.Token)
                    : renderer.RenderAsync(document, operation.Viewport.Value, operation.Cancellation.Token);
                if (!operation.Render.IsCompleted) { finished = false; return; }
            }
            var rendered = operation.Render.GetAwaiter().GetResult();
            if (!operation.Resize)
            {
                var latest = viewport();
                if (latest is null) { finished = false; return; }
                var geometry = latest.Value with { ScrollY = 0 };
                if (geometry != operation.Viewport)
                {
                    operation.Viewport = geometry;
                    operation.Render = operation.Renderer!.RenderRetainedAsync(document, geometry, operation.Cancellation.Token);
                    finished = false;
                    return;
                }
            }
            var owner = content[operation.Tab];
            var tab = Session.Tab(operation.Tab);
            // Throwing steps precede publication so a rejected commit leaves renderer, document, origin and history intact.
            operation.Renderer!.CommitDocument(document.DocumentId);
            if (!operation.Resize) { tab.History.Commit(document.Url, operation.Traversal, operation.Replace); }
            var previous = owner.Renderer;
            if (operation.Candidate is { } candidate)
            {
                operation.Candidate = null;
                owner.Renderer = candidate; owner.Used = true;
            }
            owner.ScrollY = Math.Min(operation.Viewport!.Value.ScrollY,
                Math.Max(0, rendered.ScrollHeight - operation.Viewport.Value.Height));
            var sameControls = operation.Resize && owner.Document?.DocumentId == document.DocumentId
                && owner.Page is { } old && SameControls(old, rendered);
            owner.Document = document; owner.Page = rendered;
            // Frame-local groups have no DOM identity: never retarget a focused anchor after repaint.
            owner.FocusedLink = -1;
            ClearTextSelection(owner);
            // Controls of a retained (script-free repaint) document keep tree-order identity; anything else resets field state.
            if (!sameControls)
            { owner.Fields.Clear(); owner.Dirty.Clear(); owner.CheckedStates.Clear(); owner.SelectedOptions.Clear(); owner.FocusedControl = -1; }
            else if (owner.FocusedControl >= 0 && !Focusable(rendered.FormControls[owner.FocusedControl])) { owner.FocusedControl = -1; }
            owner.Viewport = operation.Viewport.Value with { ScrollY = owner.ScrollY };
            tab.Origin = document.Origin;
            if (operation.Resize)
            {
                if (owner.ResizeFailed) { tab.Error = null; }
                if (tab.Error is null) { tab.Status = rendered.Status; }
            }
            else
            {
                tab.AddressText = document.Url.Href; tab.Title = rendered.Title; tab.Status = rendered.Status;
                tab.Error = null; tab.IsLoading = false;
            }
            owner.ResizeFailed = false;
            if (!ReferenceEquals(previous, owner.Renderer)) { previous.Dispose(); }
            Changed?.Invoke(operation.Tab);
        }
        catch (OperationCanceledException)
        {
            if (current)
            {
                if (operation.Resize) { content[operation.Tab].ScrollY = content[operation.Tab].Viewport?.ScrollY ?? 0; }
                Report(operation.Tab, "Navigation/rendering canceled.");
            }
        }
        catch (Exception exception) when (IsPageFailure(exception))
        {
            if (current)
            {
                if (operation.Resize)
                {
                    var owner = content[operation.Tab];
                    owner.ScrollY = owner.Viewport?.ScrollY ?? 0;
                    if (!operation.Scroll)
                    { owner.Page = null; owner.Viewport = operation.Viewport; owner.ResizeFailed = true; }
                }
                Report(operation.Tab, (operation.Scroll ? "Scroll rendering failed: " : operation.Resize ? "Resize rendering failed: " : "") + exception.Message);
            }
        }
        finally
        {
            if (finished)
            {
                operations.Remove(operation); operation.Cancellation.Dispose();
                operation.ReleaseCandidate();
            }
        }
    }
    private static bool SameControls(BrowserPage old, BrowserPage rendered) =>
        old.Forms.SequenceEqual(rendered.Forms) && old.FormControls.Count == rendered.FormControls.Count
        && old.FormControls.Zip(rendered.FormControls).All(pair =>
            pair.First with { Rect = null, BeforeLink = 0, Options = Array.Empty<PageFormOption>() }
                == pair.Second with { Rect = null, BeforeLink = 0, Options = Array.Empty<PageFormOption>() }
                && pair.First.Options.SequenceEqual(pair.Second.Options));
    /// <summary>Choose the renderer for a loaded document: the committed one or a fresh origin-isolation candidate.</summary>
    private IPageRenderer Select(Content owner, Operation operation, LoadedPage document)
    {
        var reuse = !IsolatesOrigins || operation.Resize || (owner.Document is { } committed
            ? !document.Origin.IsOpaque && committed.Origin.IsSameOrigin(document.Origin)
            : !owner.Used);
        if (reuse)
        {
            owner.Used = true;
            return owner.Renderer;
        }
        IPageRenderer candidate;
        try { candidate = rendererFactory() ?? throw new InvalidOperationException("Renderer factory returned null."); }
        catch (Exception exception) when (IsPageFailure(exception)
            || exception is ArgumentException or PlatformNotSupportedException or InvalidOperationException)
        {
            throw new RendererProcessException("Origin-isolated renderer creation failed: " + exception.Message);
        }
        operation.Candidate = candidate;
        return candidate;
    }
    public void Resize(TabId id, PageViewport viewport)
    {
        Check();
        var owner = content[id];
        var tab = Session.Tab(id);
        if (tab.IsLoading || owner.Document is null) { return; }
        viewport = viewport with { ScrollY = owner.ScrollY };
        var pending = operations.FirstOrDefault(op => op.Tab == id && op.Generation == tab.Generation);
        if (pending?.Viewport == viewport) { return; }
        if (owner.Viewport == viewport)
        {
            if (pending is not null) { Cancel(id); tab.Generation = checked(tab.Generation + 1); }
            return;
        }
        Limit();
        Cancel(id);
        var operation = new Operation(id, checked(++tab.Generation), Task.FromResult(owner.Document),
            new(), null, false, resize: true)
        { Viewport = viewport };
        operations.Add(operation);
        Advance(operation, () => viewport);
    }
    /// <summary>Scroll the active retained document in CSS pixels; finite deltas saturate at its extent.</summary>
    public void Scroll(TabId id, double delta)
    {
        Check();
        if (!double.IsFinite(delta)) { throw new ArgumentOutOfRangeException(nameof(delta), "Scroll delta must be finite."); }
        var owner = content[id];
        var tab = Session.Tab(id);
        if (tab.IsLoading || owner.Document is null || owner.Page is null || owner.Viewport is null || delta == 0) { return; }
        var pending = operations.FirstOrDefault(op => op.Tab == id && op.Generation == tab.Generation);
        var viewport = pending?.Viewport ?? owner.Viewport.Value;
        var maximum = Math.Max(0, owner.Page.ScrollHeight - viewport.Height);
        var offset = Math.Clamp(owner.ScrollY + delta, 0, maximum);
        if (offset == owner.ScrollY) { return; }
        Limit();
        Cancel(id);
        owner.ScrollY = offset;
        viewport = viewport with { ScrollY = offset };
        var operation = new Operation(id, checked(++tab.Generation), Task.FromResult(owner.Document),
            new(), null, false, resize: true, scroll: true)
        { Viewport = viewport };
        operations.Add(operation);
        Advance(operation, () => viewport);
    }
    public void Report(TabId id, string error) => Report(id, error, finishNavigation: true);
    private void Report(TabId id, string error, bool finishNavigation)
    {
        Check();
        var tab = Session.Tab(id);
        if (finishNavigation) { tab.IsLoading = false; }
        tab.Error = error; tab.Status = error;
        Failed?.Invoke(id, error); Changed?.Invoke(id);
    }
    public void CloseTab(TabId id)
    {
        Check();
        Cancel(id);
        foreach (var operation in operations.Where(op => op.Tab == id)) { operation.ReleaseCandidate(); }
        var owner = content[id];
        content.Remove(id);
        owner.Source.Dispose(); owner.Renderer.Dispose(); Session.CloseTab(id);
    }
    public void CloseWindow(BrowserWindowId id)
    {
        Check();
        foreach (var tab in Session.Window(id).Tabs.ToArray()) { CloseTab(tab.Id); }
        if (Session.Windows.Any(w => w.Id == id)) { Session.CloseWindow(id); }
    }
    private void Limit()
    {
        if (operations.Count >= Session.Options.MaxPendingLoads) { throw new BrowserLimitException("Pending load/render limit exceeded; wait for canceled operations to finish."); }
    }
    private void Cancel(TabId id)
    {
        foreach (var operation in operations.Where(op => op.Tab == id)) { operation.Cancellation.Cancel(); }
    }
    internal static bool IsPageFailure(Exception exception) => StaticPageRenderer.IsRenderFailure(exception)
        || exception is IOException or UnauthorizedAccessException or UrlParseException or WebDecodingException or BrowserLimitException;
    private void Check()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (thread != Environment.CurrentManagedThreadId) { throw new InvalidOperationException("Browser controller must stay on its creating thread."); }
    }
    public void Dispose()
    {
        if (disposed) { return; }
        Check();
        foreach (var tab in content.Keys.ToArray()) { CloseTab(tab); }
        foreach (var operation in operations)
        {
            Task task = operation.Render is { } render ? render : operation.Load;
            _ = task.ContinueWith(done =>
            {
                if (done.IsFaulted) { _ = done.Exception; }
                operation.Cancellation.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        operations.Clear(); disposed = true;
    }
}
