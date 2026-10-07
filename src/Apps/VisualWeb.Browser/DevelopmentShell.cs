using SDL3;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Browser;

/// <summary>SDL-backed main-thread UI with local or optionally confined per-tab rendering.</summary>
public sealed class DevelopmentShell : IDisposable
{
    public const string HomeHtml = """
        <!doctype html><html><head><title>VisualWeb development</title>
        <style>
        * { margin-top:0; margin-bottom:0 }
        h1,b,strong { font-weight:400 }
        i,em { font-style:normal }
        body { background-color:#f4f7fa; padding:20px; font-family:serif }
        h1 { color:#204060 }
        p { line-height:28px }
        </style></head><body><h1>VisualWeb</h1>
        <p>Static HTML and CSS with native text and CPU painting.</p>
        <p>Development only. No sandbox or origin isolation.</p>
        <p>Use Ctrl L for an absolute HTTP, HTTPS, file or HTML data URL.</p>
        <p>Ctrl T creates a tab. Ctrl W closes it. Ctrl N opens a window.</p>
        <p>Ctrl M moves the active tab to another window.</p>
        <p>Alt Left and Alt Right navigate history. F5 reloads.</p>
        <p>Wheel, Page Up, Page Down, Home and End scroll outside address editing. The page scrollbar can be clicked or dragged.</p>
        <p>Basic links, GET forms and visible text selection are available; general page events remain deferred.</p>
        </body></html>
        """;
    public static string HomeUrl => "data:text/html;charset=utf-8," + Uri.EscapeDataString(HomeHtml);
    private sealed class View(IPlatformWindow native)
    {
        internal IPlatformWindow Native { get; } = native;
        internal AddressEditor Editor { get; } = new();
        internal bool Editing { get; set; }
        internal bool TextInput { get; set; }
        internal bool SelectingText { get; set; }
        internal bool DraggingScrollbar { get; set; }
        internal bool Dirty { get; set; } = true;
        internal IReadOnlyList<ChromeTarget> Targets { get; set; } = [];
        internal PixelSize LastSize { get; set; }
        internal float LastDensity { get; set; }
        internal TabId? LastTab { get; set; }
        internal bool SizeLimitReported { get; set; }
        internal double? PointerY { get; set; }
        internal ChromeTarget? KeyboardTarget { get; set; }
    }
    private readonly IWindowSystem system;
    private readonly ShellChrome chrome;
    private readonly Dictionary<BrowserWindowId, View> views = [];
    private readonly bool hidden;
    private readonly bool textInput;
    private readonly bool requireSandbox;
    // One store per shell session, shared by all tab-local loaders in every rendering mode.
    private readonly Engine.Net.HstsPolicyStore hstsPolicyStore = new();
    private bool quit;
    private bool disposed;
    public BrowserController Controller { get; }
    public int PresentedFrames { get; private set; }
    public IReadOnlyList<(BrowserWindowId Browser, WindowId Native)> Windows =>
        views.Select(pair => (pair.Key, pair.Value.Native.Id)).ToArray();

    /// <summary>Creates a browser session with one shared, memory-only HSTS store in every rendering mode.</summary>
    /// <remarks>Spec: rfc6797; <see href="https://www.rfc-editor.org/rfc/rfc6797.html#section-8">UA processing</see>.
    /// An optional transport factory returns a fresh owned handler per tab, subject to ResourceLoader's trusted
    /// handler contract (including certificate authentication). Cookies remain off; independent shells share no policy state.</remarks>
    public DevelopmentShell(IWindowSystem system, string fontPath, BrowserOptions? options = null,
        bool hidden = false, bool textInput = true, string? rendererPath = null, bool requireSandbox = false,
        bool executeInlineScripts = false, Func<HttpMessageHandler>? pageTransportFactory = null)
    {
        this.system = system;
        this.hidden = hidden;
        this.textInput = textInput;
        this.requireSandbox = requireSandbox;
        var settings = options ?? new();
        settings.Validate();
        if (requireSandbox && rendererPath is null) { throw new ArgumentException("Renderer confinement requires a worker process."); }
        var multiprocess = rendererPath is not null;
        chrome = new(fontPath, settings.MaxFramePixels, multiprocess, requireSandbox);
        Controller = new(() => new GetPageSource(pageTransportFactory?.Invoke(), hstsPolicyStore), () => rendererPath is null
            ? new StaticPageRenderer(fontPath, settings.MaxFramePixels, executeInlineScripts)
            : new ProcessPageRenderer(rendererPath, fontPath, requireSandbox: requireSandbox, executeInlineScripts: executeInlineScripts), settings,
            // Every multiprocess mode (confined or explicitly unsandboxed) rotates renderers per origin; there is no opt-out.
            isolateOrigins: multiprocess);
        Controller.Changed += id =>
        {
            foreach (var (key, view) in views)
            {
                if (Controller.Session.Windows.Any(w => w.Id == key && w.Tabs.Any(tab => tab.Id == id)))
                {
                    view.Dirty = true;
                    if (!view.Editing && Controller.Session.Window(key).ActiveTabId == id)
                    {
                        view.Editor.Reset(Controller.Session.Tab(id).AddressText);
                    }
                }
            }
        };
        Controller.Failed += (id, error) => Console.Error.WriteLine($"Tab {id.Value}: {error}");
        system.QuitRequested += Quit;
    }
    public BrowserWindow OpenWindow(string? url = null, bool empty = false)
    {
        var window = Controller.Session.CreateWindow();
        IPlatformWindow? native = null;
        try
        {
            native = system.CreateWindow(new(WindowTitle(null), 1000, 720, hidden));
            var view = new View(native);
            views.Add(window.Id, view);
            native.EventReceived += Dispatch;
            if (!empty)
            {
                var tab = Controller.CreateTab(window.Id);
                Controller.Navigate(tab.Id, url ?? HomeUrl);
            }
            return window;
        }
        catch
        {
            if (Controller.Session.Windows.Any(w => w.Id == window.Id)) { Controller.CloseWindow(window.Id); }
            views.Remove(window.Id);
            native?.Dispose();
            throw;
        }
    }
    public void Run()
    {
        while (!quit && views.Count > 0)
        {
            Tick();
            Thread.Sleep(10);
        }
    }
    public void Tick()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        system.PumpEvents();
        SynchronizeWindows();
        Controller.Pump(id =>
        {
            var window = Controller.Session.Windows.Single(w => w.Tabs.Any(tab => tab.Id == id));
            var view = views[window.Id];
            if ((long)view.Native.PixelSize.Width * view.Native.PixelSize.Height > Controller.Session.Options.MaxFramePixels)
            {
                return null;
            }
            return ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity);
        });
        foreach (var (id, view) in views.ToArray())
        {
            var size = view.Native.PixelSize;
            var density = view.Native.PixelDensity;
            if (size.Width <= 0 || size.Height <= 0) { continue; }
            if ((long)size.Width * size.Height > Controller.Session.Options.MaxFramePixels)
            {
                if (!view.SizeLimitReported)
                {
                    const string error = "Window exceeds framebuffer limit; resize it smaller.";
                    view.Native.SetTitle(WindowTitle(error));
                    Console.Error.WriteLine(error);
                    view.SizeLimitReported = true;
                }
                continue;
            }
            if (view.SizeLimitReported) { view.Dirty = true; view.SizeLimitReported = false; }
            var window = Controller.Session.Window(id);
            if (view.LastTab != window.ActiveTabId)
            {
                view.DraggingScrollbar = false;
                view.KeyboardTarget = null;
                Edit(view, window, false);
                view.Editor.Reset(window.ActiveTab?.AddressText ?? "");
                view.LastTab = window.ActiveTabId;
            }
            if (view.LastSize != size || view.LastDensity != density)
            {
                view.LastSize = size; view.LastDensity = density; view.Dirty = true;
            }
            if (window.ActiveTab is { } tab && ShellChrome.Viewport(size, density) is { } viewport)
            {
                try { Controller.Resize(tab.Id, viewport); }
                catch (BrowserLimitException exception) { Controller.Report(tab.Id, exception.Message); }
            }
            SyncTextInput(view, window);
            if (!view.Dirty) { continue; }
            var frame = chrome.Render(window, window.ActiveTabId is { } active ? Controller.Page(active) : null,
                size, density, view.Editing ? view.Editor : null,
                window.ActiveTabId is { } focused && Controller.PageHasFocus(focused)
                    ? Controller.FocusedLinkIndex(focused) : -1,
                window.ActiveTabId is { } pageFocus && Controller.PageHasFocus(pageFocus) ? null : view.KeyboardTarget,
                Forms(window), window.ActiveTabId is { } selectedTab ? Controller.SelectedTextRects(selectedTab) : null,
                window.ActiveTab is { } scrollingTab ? Controller.ScrollY(scrollingTab.Id) : 0);
            view.Targets = frame.Targets;
            view.Native.Surface.Present(frame.Pixels, frame.Size, frame.Stride);
            view.Native.SetTitle(WindowTitle(window.ActiveTab?.Title));
            view.Dirty = false;
            PresentedFrames++;
        }
    }
    public void Dispatch(WindowEvent input)
    {
        var pair = views.FirstOrDefault(pair => pair.Value.Native.Id == input.Window);
        if (pair.Value is not { } view) { return; }
        var window = Controller.Session.Window(pair.Key);
        try
        {
            switch (input)
            {
                case CloseRequested: Controller.CloseWindow(window.Id); break;
                case WindowResized or WindowExposed or WindowScaleChanged: view.Dirty = true; break;
                case FocusChanged { Focused: false }:
                    Edit(view, window, false);
                    view.SelectingText = false;
                    view.DraggingScrollbar = false;
                    if (window.ActiveTab is { } blurredTab) { Controller.EndTextSelection(blurredTab.Id); }
                    break;
                case PointerMoved moved:
                    view.PointerY = moved.Y;
                    if (view.DraggingScrollbar) { ScrollScrollbar(window, view, moved.Y); }
                    if (view.SelectingText && window.ActiveTab is { } selectingTab
                        && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } selectionViewport
                        && moved.Y >= ShellChrome.Height && moved.Y < ShellChrome.Height + selectionViewport.Height)
                    { Controller.ExtendTextSelection(selectingTab.Id, moved.X, moved.Y - ShellChrome.Height, selectionViewport); }
                    break;
                case PointerScrolled wheel when !view.Editing && view.PointerY is not < ShellChrome.Height
                    && window.ActiveTab is { } active:
                    if (!float.IsFinite(wheel.X) || !float.IsFinite(wheel.Y))
                    { Controller.Report(active.Id, "Wheel delta must be finite."); break; }
                    Controller.Scroll(active.Id, Math.Clamp((double)wheel.Y, -100, 100) * 48);
                    break;
                case PointerButtonChanged { Pressed: true, Button: 1 } pointer:
                    view.PointerY = pointer.Y;
                    view.SelectingText = false;
                    view.DraggingScrollbar = false;
                    var target = ShellChrome.Hit(view.Targets, pointer.X, pointer.Y);
                    if (target?.Action == ChromeAction.Scrollbar)
                    {
                        Action(window, view, target.Action, target.Tab);
                        view.DraggingScrollbar = true;
                        ScrollScrollbar(window, view, pointer.Y);
                    }
                    else if (target is not null) { Action(window, view, target.Action, target.Tab); }
                    else
                    {
                        Edit(view, window, false);
                        view.KeyboardTarget = null;
                        if (pointer.Y >= ShellChrome.Height && window.ActiveTab is { } pageTab
                            && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } pageViewport
                            && pointer.X >= 0 && pointer.X < pageViewport.Width
                            && pointer.Y < ShellChrome.Height + pageViewport.Height)
                        {
                            Controller.FocusPage(pageTab.Id);
                            var activated = Controller.ActivateLink(pageTab.Id, pointer.X, pointer.Y - ShellChrome.Height, pageViewport);
                            if (!activated)
                            {
                                view.SelectingText = Controller.StartTextSelection(pageTab.Id, pointer.X,
                                    pointer.Y - ShellChrome.Height, pageViewport);
                            }
                        }
                        else if (window.ActiveTabId is { } unfocused) { Controller.FocusPage(unfocused, false); }
                    }
                    break;
                case PointerButtonChanged { Pressed: false, Button: 1 } when window.ActiveTab is { } releasedTab:
                    view.DraggingScrollbar = false;
                    view.SelectingText = false;
                    Controller.EndTextSelection(releasedTab.Id);
                    break;
                case KeyChanged { Pressed: true } key: Key(window, view, key); break;
                case TextEntered text when view.Editing && window.ActiveTab is { } tab:
                    Controller.SetAddress(tab.Id, view.Editor.Insert(text.Text, Controller.Session.Options.MaxAddressCharacters));
                    view.Dirty = true;
                    break;
                case TextEntered text when window.ActiveTab is { } fieldTab && Controller.EditingFormControl(fieldTab.Id):
                    Controller.InsertFormText(fieldTab.Id, text.Text);
                    view.Dirty = true;
                    break;
            }
        }
        catch (Exception exception) when (BrowserController.IsPageFailure(exception))
        {
            if (window.ActiveTab is { } tab) { Controller.Report(tab.Id, exception.Message); }
            else { Console.Error.WriteLine(exception.Message); }
        }
        if (views.ContainsKey(pair.Key)) { SyncTextInput(view, window); }
        SynchronizeWindows();
    }
    private void ScrollScrollbar(BrowserWindow window, View view, double pointerY)
    {
        if (window.ActiveTab is not { } tab || Controller.Page(tab.Id) is not { } page
            || ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is not { } viewport) { return; }
        var maximum = Math.Max(0, page.ScrollHeight - viewport.Height);
        if (maximum <= 0) { return; }
        var thumbHeight = ShellChrome.ScrollbarThumbHeight(viewport.Height, page.ScrollHeight);
        var trackRange = viewport.Height - thumbHeight;
        var fraction = trackRange <= 0 ? 0 : Math.Clamp((pointerY - ShellChrome.Height - thumbHeight / 2) / trackRange, 0, 1);
        Controller.Scroll(tab.Id, fraction * maximum - Controller.ScrollY(tab.Id));
    }
    private void Key(BrowserWindow window, View view, KeyChanged key)
    {
        var modifiers = (SDL.Keymod)key.Modifiers;
        var control = (modifiers & SDL.Keymod.Ctrl) != 0;
        var alt = (modifiers & SDL.Keymod.Alt) != 0;
        var shift = (modifiers & SDL.Keymod.Shift) != 0;
        var code = (SDL.Scancode)key.ScanCode;
        if (control && !key.Repeat)
        {
            switch (code)
            {
                case SDL.Scancode.L: Edit(view, window, true); return;
                case SDL.Scancode.T: Action(window, view, ChromeAction.NewTab); return;
                case SDL.Scancode.N: Action(window, view, ChromeAction.NewWindow); return;
                case SDL.Scancode.M: Action(window, view, ChromeAction.MoveTab); return;
                case SDL.Scancode.W:
                    if (window.ActiveTabId is { } close) { Controller.CloseTab(close); }
                    return;
                case SDL.Scancode.Tab: Action(window, view, shift ? ChromeAction.PreviousTab : ChromeAction.NextTab); return;
                case SDL.Scancode.R: Action(window, view, ChromeAction.Reload); return;
                case SDL.Scancode.A when view.Editing: view.Editor.SelectAll = true; view.Dirty = true; return;
            }
        }
        if (alt && !key.Repeat && code is SDL.Scancode.Left or SDL.Scancode.Right)
        {
            Action(window, view, code == SDL.Scancode.Left ? ChromeAction.Back : ChromeAction.Forward);
            return;
        }
        if (code == SDL.Scancode.F5 && !key.Repeat) { Action(window, view, ChromeAction.Reload); return; }
        if (window.ActiveTab is not { } tab) { return; }
        if (!control && !alt && code == SDL.Scancode.Tab)
        {
            TraverseFocus(window, view, shift);
            return;
        }
        if (view.Editing && control && !alt && !key.Repeat && code == SDL.Scancode.V)
        {
            var clipboard = view.Native.GetClipboardText();
            if (clipboard.Length > 0)
            {
                Controller.SetAddress(tab.Id, view.Editor.Insert(clipboard, Controller.Session.Options.MaxAddressCharacters));
                view.Dirty = true;
            }
            return;
        }
        if (!view.Editing)
        {
            if (control && !alt && !key.Repeat && code == SDL.Scancode.A && Controller.EditingFormControl(tab.Id))
            { Controller.SelectAllFormControl(tab.Id); return; }
            if (control && !alt && !key.Repeat && code == SDL.Scancode.V && Controller.EditingFormControl(tab.Id))
            {
                var clipboard = view.Native.GetClipboardText();
                if (clipboard.Length > 0) { Controller.InsertFormText(tab.Id, clipboard); }
                return;
            }
            if (control && !alt && !key.Repeat && code == SDL.Scancode.C && Controller.SelectedText(tab.Id) is { Length: > 0 } selected)
            { view.Native.SetClipboardText(selected); return; }
            if (!control && !alt && Controller.EditingFormControl(tab.Id) && code switch
            {
                SDL.Scancode.Backspace => FormEdit.Backspace,
                SDL.Scancode.Delete => FormEdit.Delete,
                SDL.Scancode.Left => FormEdit.Left,
                SDL.Scancode.Right => FormEdit.Right,
                SDL.Scancode.Home => FormEdit.Home,
                SDL.Scancode.End => FormEdit.End,
                _ => (FormEdit?)null,
            } is { } edit)
            {
                Controller.EditFormControl(tab.Id, edit);
                return;
            }
            if (!control && !alt && Controller.EditingFormControl(tab.Id) && code is SDL.Scancode.Pageup or SDL.Scancode.Pagedown)
            { return; }
            if (!control && !alt && code == SDL.Scancode.Return && !key.Repeat)
            {
                if (view.KeyboardTarget is { } target) { Action(window, view, target.Action, target.Tab); }
                else if (Controller.PageHasFocus(tab.Id))
                { Controller.ActivateFocusedLink(tab.Id, ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity)); }
                return;
            }
            if (!control && !alt && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } viewport)
            {
                switch (code)
                {
                    case SDL.Scancode.Pagedown: Controller.Scroll(tab.Id, viewport.Height); break;
                    case SDL.Scancode.Pageup: Controller.Scroll(tab.Id, -viewport.Height); break;
                    case SDL.Scancode.Home: Controller.Scroll(tab.Id, -double.MaxValue); break;
                    case SDL.Scancode.End: Controller.Scroll(tab.Id, double.MaxValue); break;
                }
            }
            return;
        }
        switch (code)
        {
            case SDL.Scancode.Return:
                var address = view.Editor.Text;
                Edit(view, window, false);
                Controller.Navigate(tab.Id, address);
                break;
            case SDL.Scancode.Escape:
                Controller.SetAddress(tab.Id, tab.History.Current?.Href ?? "");
                Edit(view, window, false);
                break;
            case SDL.Scancode.Backspace: Controller.SetAddress(tab.Id, view.Editor.Backspace()); break;
            case SDL.Scancode.Delete: Controller.SetAddress(tab.Id, view.Editor.Delete()); break;
            case SDL.Scancode.Left: view.Editor.Left(); break;
            case SDL.Scancode.Right: view.Editor.Right(); break;
            case SDL.Scancode.Home: view.Editor.Home(); break;
            case SDL.Scancode.End: view.Editor.End(); break;
        }
        view.Dirty = true;
    }
    private void TraverseFocus(BrowserWindow window, View view, bool backwards)
    {
        if (window.ActiveTab is not { } tab) { return; }
        var targets = view.Targets.Where(target => target.Action != ChromeAction.Scrollbar).ToArray();
        var count = Controller.PageFocusCount(tab.Id);
        if (Controller.PageHasFocus(tab.Id))
        {
            var position = Controller.PageFocusPosition(tab.Id);
            if (count > 0 && (position < 0 || (backwards ? position > 0 : position < count - 1)))
            {
                view.KeyboardTarget = null;
                Controller.MoveLinkFocus(tab.Id, backwards);
                return;
            }
            SelectChrome(backwards ? targets.Length - 1 : 0);
            return;
        }
        var current = view.Editing ? targets.ToList().FindIndex(target => target.Action == ChromeAction.Address)
            : view.KeyboardTarget is { } focused
                ? targets.ToList().FindIndex(target => target.Action == focused.Action && target.Tab == focused.Tab) : -1;
        var next = current < 0 ? backwards ? targets.Length : 0 : current + (backwards ? -1 : 1);
        if (next < 0 || next >= targets.Length)
        {
            if (count > 0)
            {
                Edit(view, window, false);
                view.KeyboardTarget = null;
                Controller.FocusPagePosition(tab.Id, backwards ? count - 1 : 0);
                return;
            }
            next = backwards ? targets.Length - 1 : 0;
        }
        SelectChrome(next);

        void SelectChrome(int index)
        {
            Controller.FocusPage(tab.Id, false);
            var target = index >= 0 && index < targets.Length ? targets[index] : null;
            Edit(view, window, target?.Action == ChromeAction.Address);
            view.KeyboardTarget = target;
            view.Dirty = true;
        }
    }
    private void Action(BrowserWindow window, View view, ChromeAction action, TabId? target = null)
    {
        var tab = window.ActiveTab;
        if (action == ChromeAction.Address) { Edit(view, window, true); return; }
        Edit(view, window, false);
        view.KeyboardTarget = null;
        if (tab is not null && action is not (ChromeAction.ActivateTab or ChromeAction.PreviousTab or ChromeAction.NextTab
            or ChromeAction.MoveTab or ChromeAction.NewTab or ChromeAction.NewWindow))
        { Controller.FocusPage(tab.Id, false); }
        switch (action)
        {
            case ChromeAction.Back when tab?.History.CanGoBack == true: Controller.Back(tab.Id); break;
            case ChromeAction.Forward when tab?.History.CanGoForward == true: Controller.Forward(tab.Id); break;
            case ChromeAction.Reload when tab?.History.Current is not null: Controller.Reload(tab.Id); break;
            case ChromeAction.NewTab:
                var created = Controller.CreateTab(window.Id);
                Controller.Navigate(created.Id, HomeUrl);
                break;
            case ChromeAction.NewWindow: OpenWindow(); break;
            case ChromeAction.CloseTab when target is { } close: Controller.CloseTab(close); break;
            case ChromeAction.MoveTab when tab is not null:
                var destination = Controller.Session.Windows.FirstOrDefault(w => w.Id != window.Id) ?? OpenWindow(empty: true);
                Controller.Session.MoveTab(tab.Id, destination.Id);
                views[destination.Id].Dirty = true;
                break;
            case ChromeAction.ActivateTab when target is { } id: Controller.Session.Activate(window.Id, id); break;
            case ChromeAction.PreviousTab or ChromeAction.NextTab when window.Tabs.Count > 0:
                var index = window.Tabs.ToList().FindIndex(t => t.Id == window.ActiveTabId);
                var offset = action == ChromeAction.NextTab ? 1 : -1;
                Controller.Session.Activate(window.Id, window.Tabs[(index + offset + window.Tabs.Count) % window.Tabs.Count].Id);
                break;
        }
        view.Editor.Reset(window.ActiveTab?.AddressText ?? "");
        view.Dirty = true;
    }
    private void Edit(View view, BrowserWindow window, bool enabled)
    {
        if (enabled && window.ActiveTab is null) { return; }
        view.Editing = enabled;
        SyncTextInput(view, window);
        if (enabled)
        {
            Controller.FocusPage(window.ActiveTab!.Id, false);
            view.KeyboardTarget = view.Targets.LastOrDefault(target => target.Action == ChromeAction.Address);
            view.Editor.Reset(window.ActiveTab.AddressText, selectAll: true);
        }
        view.Dirty = true;
    }
    /// <summary>Enables committed text input only while the address bar or a page text field is being edited.</summary>
    private void SyncTextInput(View view, BrowserWindow window)
    {
        var enabled = view.Editing || window.ActiveTabId is { } id && Controller.EditingFormControl(id);
        if (enabled == view.TextInput) { return; }
        view.TextInput = enabled;
        if (textInput) { view.Native.SetTextInput(enabled); }
    }
    private ShellFormState? Forms(BrowserWindow window)
    {
        if (window.ActiveTabId is not { } id || Controller.Page(id) is not { FormControls.Count: > 0 } page) { return null; }
        var values = Enumerable.Range(0, page.FormControls.Count).Select(index => Controller.FormControlValue(id, index)).ToArray();
        var focused = Controller.PageHasFocus(id) ? Controller.FocusedControlIndex(id) : -1;
        return new(values, focused, focused >= 0 ? Controller.FormControlCaret(id) : -1,
            focused >= 0 && Controller.FormControlSelectAll(id));
    }
    private string WindowTitle(string? title)
    {
        var warning = requireSandbox
            ? OperatingSystem.IsWindows() ? "WINDOWS APP CONTAINER REQUIRED" : "LINUX CONFINEMENT REQUIRED"
            : "NO SANDBOX";
        var value = "VisualWeb DEVELOPMENT - " + warning + " - " + (title ?? "New window");
        return string.Concat(value.EnumerateRunes().Take(120).Select(rune => rune.ToString()));
    }
    private void SynchronizeWindows()
    {
        foreach (var (id, view) in views.ToArray())
        {
            if (Controller.Session.Windows.Any(window => window.Id == id)) { continue; }
            view.Native.EventReceived -= Dispatch;
            view.Native.Dispose();
            views.Remove(id);
        }
    }
    private void Quit() => quit = true;
    public void Dispose()
    {
        if (disposed) { return; }
        system.QuitRequested -= Quit;
        Controller.Dispose();
        foreach (var view in views.Values) { view.Native.EventReceived -= Dispatch; view.Native.Dispose(); }
        views.Clear();
        chrome.Dispose();
        disposed = true;
    }
}
