using System.Text;
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
        <p>Wheel, Arrow Up and Down, Page Up and Page Down, Home and End scroll outside editing fields. The page scrollbar can be clicked or dragged.</p>
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
        internal bool ControlModifier { get; set; }
        internal bool ShiftModifier { get; set; }
        internal bool SelectingText { get; set; }
        internal bool PendingLinkActivation { get; set; }
        internal bool PendingLinkDragged { get; set; }
        internal int PendingLinkIndex { get; set; } = -1;
        internal TabId? PendingLinkTab { get; set; }
        internal double PendingLinkX { get; set; }
        internal double PendingLinkY { get; set; }
        internal PageViewport? PendingLinkViewport { get; set; }
        internal bool DraggingScrollbar { get; set; }
        internal int DraggingRangeControl { get; set; } = -1;
        internal int HoveredLink { get; set; } = -1;
        internal int OpenSelectControl { get; set; } = -1;
        internal int SelectPopupFirstOption { get; set; }
        internal int SelectPopupHoverOption { get; set; } = -1;
        internal string SelectTypeaheadText { get; set; } = "";
        internal long SelectTypeaheadTimestamp { get; set; }
        internal int SelectTypeaheadControl { get; set; } = -1;
        internal BrowserPage? SelectTypeaheadPage { get; set; }
        internal BrowserPage? SelectPopupPage { get; set; }
        internal bool Dirty { get; set; } = true;
        internal IReadOnlyList<ChromeTarget> Targets { get; set; } = [];
        internal PixelSize LastSize { get; set; }
        internal float LastDensity { get; set; }
        internal TabId? LastTab { get; set; }
        internal bool SizeLimitReported { get; set; }
        internal double? PointerX { get; set; }
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
                view.PendingLinkActivation = false;
                view.PendingLinkDragged = false;
                view.PendingLinkIndex = -1;
                view.PendingLinkTab = null;
                view.PendingLinkViewport = null;
                view.HoveredLink = -1;
                CloseSelectPopup(view);
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
            if (view.OpenSelectControl >= 0
                && (window.ActiveTab is not { } popupTab || !ReferenceEquals(view.SelectPopupPage, Controller.Page(popupTab.Id))))
            { CloseSelectPopup(view); }
            if (window.ActiveTab is { } tab && ShellChrome.Viewport(size, density) is { } viewport)
            {
                try { Controller.Resize(tab.Id, viewport); }
                catch (BrowserLimitException exception) { Controller.Report(tab.Id, exception.Message); }
            }
            UpdateHoveredLink(view, window);
            SyncTextInput(view, window);
            if (!view.Dirty) { continue; }
            var frame = chrome.Render(window, window.ActiveTabId is { } active ? Controller.Page(active) : null,
                size, density, view.Editing ? view.Editor : null,
                window.ActiveTabId is { } focused && Controller.PageHasFocus(focused)
                    ? Controller.FocusedLinkIndex(focused) : -1,
                window.ActiveTabId is { } pageFocus && Controller.PageHasFocus(pageFocus) ? null : view.KeyboardTarget,
                Forms(window, view), window.ActiveTabId is { } selectedTab ? Controller.SelectedTextRects(selectedTab) : null,
                window.ActiveTab is { } scrollingTab ? Controller.ScrollY(scrollingTab.Id) : 0, view.HoveredLink);
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
                    view.ControlModifier = false;
                    view.ShiftModifier = false;
                    view.PendingLinkActivation = false;
                    view.PendingLinkDragged = false;
                    view.PendingLinkIndex = -1;
                    view.PendingLinkTab = null;
                    view.PendingLinkViewport = null;
                    view.HoveredLink = -1;
                    Edit(view, window, false);
                    CloseSelectPopup(view);
                    view.SelectingText = false;
                    view.DraggingScrollbar = false;
                    if (window.ActiveTab is { } blurredTab) { Controller.EndTextSelection(blurredTab.Id); }
                    break;
                case PointerMoved moved:
                    view.PointerX = moved.X;
                    view.PointerY = moved.Y;
                    if (view.PendingLinkActivation
                        && Math.Pow(moved.X - view.PendingLinkX, 2) + Math.Pow(moved.Y - view.PendingLinkY, 2) >= 16)
                    { view.PendingLinkDragged = true; }
                    UpdateSelectPopupHover(view, window, moved.X, moved.Y);
                    UpdateHoveredLink(view, window);
                    if (view.DraggingScrollbar) { ScrollScrollbar(window, view, moved.Y); }
                    if (view.DraggingRangeControl >= 0 && window.ActiveTab is { } rangeTab)
                    { Controller.SetRangeFromPointer(rangeTab.Id, view.DraggingRangeControl, moved.X); }
                    if (view.SelectingText && window.ActiveTab is { } selectingTab
                        && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } selectionViewport
                        && moved.Y >= ShellChrome.Height && moved.Y < ShellChrome.Height + selectionViewport.Height)
                    { Controller.ExtendTextSelection(selectingTab.Id, moved.X, moved.Y - ShellChrome.Height, selectionViewport); }
                    break;
                case PointerScrolled wheel when !view.Editing && window.ActiveTab is { } active:
                    if (!float.IsFinite(wheel.X) || !float.IsFinite(wheel.Y))
                    { Controller.Report(active.Id, "Wheel delta must be finite."); break; }
                    if (view.OpenSelectControl >= 0 && view.PointerX is { } menuX && view.PointerY is { } menuY
                        && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } menuViewport
                        && Controller.Page(active.Id) is { } menuPage
                        && ShellChrome.PopupLayout(menuPage, view.OpenSelectControl, menuViewport, view.SelectPopupFirstOption) is { } menu
                        && menuX >= menu.Bounds.X && menuX < menu.Bounds.X + menu.Bounds.Width
                        && menuY >= menu.Bounds.Y && menuY < menu.Bounds.Y + menu.Bounds.Height)
                    {
                        var delta = (int)Math.Clamp(Math.Round(-wheel.Y * 3), -300, 300);
                        var maxFirst = menuPage.FormControls[view.OpenSelectControl].Options.Length - menu.VisibleOptions;
                        view.SelectPopupFirstOption = (int)Math.Clamp((long)view.SelectPopupFirstOption + delta, 0, maxFirst);
                        UpdateSelectPopupHover(view, window, menuX, menuY);
                        view.Dirty = true;
                        break;
                    }
                    if (view.PointerY is < ShellChrome.Height) { break; }
                    var overTextarea = view.PointerX is { } pointerX && view.PointerY is { } pointerY
                        && Controller.ScrollTextareaAt(active.Id, pointerX, pointerY - ShellChrome.Height,
                            (int)Math.Clamp(Math.Round(wheel.Y * 3), -300, 300));
                    if (!overTextarea) { Controller.Scroll(active.Id, Math.Clamp((double)wheel.Y, -100, 100) * 48); }
                    break;
                case PointerButtonChanged { Pressed: true, Button: 2 } middle:
                    view.PointerX = middle.X;
                    view.PointerY = middle.Y;
                    if (window.ActiveTab is { } middleTab
                        && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } middleViewport
                        && view.OpenSelectControl >= 0
                        && Controller.Page(middleTab.Id) is { } middlePage
                        && ShellChrome.PopupLayout(middlePage, view.OpenSelectControl,
                            middleViewport, view.SelectPopupFirstOption) is { } openPopup
                        && middle.X >= openPopup.Bounds.X && middle.X < openPopup.Bounds.X + openPopup.Bounds.Width
                        && middle.Y >= openPopup.Bounds.Y && middle.Y < openPopup.Bounds.Y + openPopup.Bounds.Height)
                    { break; }
                    if (view.OpenSelectControl >= 0) { CloseSelectPopup(view); }
                    Edit(view, window, false);
                    view.KeyboardTarget = null;
                    if (middle.Y < ShellChrome.Height
                        && ShellChrome.Hit(view.Targets, middle.X, middle.Y) is
                        { Action: ChromeAction.ActivateTab or ChromeAction.CloseTab, Tab: { } closingTab }
                        && window.Tabs.Any(tab => tab.Id == closingTab))
                    {
                        Controller.CloseTab(closingTab);
                        view.Dirty = true;
                        break;
                    }
                    if (window.ActiveTab is { } linkTab && middle.Y >= ShellChrome.Height
                        && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } linkViewport
                        && middle.X >= 0 && middle.X < linkViewport.Width
                        && middle.Y < ShellChrome.Height + linkViewport.Height)
                    {
                        Controller.FocusPage(linkTab.Id);
                        Controller.ClearSelectedText(linkTab.Id);
                        Controller.ActivateLink(linkTab.Id, middle.X, middle.Y - ShellChrome.Height,
                            linkViewport, forceNewTab: true);
                    }
                    break;
                case PointerButtonChanged { Pressed: true, Button: 1 } pointer:
                    view.PointerX = pointer.X;
                    view.PointerY = pointer.Y;
                    view.PendingLinkActivation = false;
                    view.PendingLinkDragged = false;
                    view.PendingLinkTab = null;
                    view.PendingLinkIndex = -1;
                    view.PendingLinkViewport = null;
                    view.SelectingText = false;
                    view.DraggingScrollbar = false;
                    view.DraggingRangeControl = -1;
                    if (view.ControlModifier)
                    {
                        if (view.OpenSelectControl >= 0) { CloseSelectPopup(view); }
                        Edit(view, window, false);
                        view.KeyboardTarget = null;
                        if (window.ActiveTab is { } modifiedLinkTab && pointer.Y >= ShellChrome.Height
                            && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } modifiedLinkViewport
                            && pointer.X >= 0 && pointer.X < modifiedLinkViewport.Width
                            && pointer.Y < ShellChrome.Height + modifiedLinkViewport.Height)
                        {
                            Controller.FocusPage(modifiedLinkTab.Id);
                            Controller.ClearSelectedText(modifiedLinkTab.Id);
                            Controller.ActivateLink(modifiedLinkTab.Id, pointer.X, pointer.Y - ShellChrome.Height,
                                modifiedLinkViewport, forceNewTab: true, activateNewTab: false);
                        }
                        break;
                    }
                    var target = ShellChrome.Hit(view.Targets, pointer.X, pointer.Y);
                    if (target?.Action == ChromeAction.SelectOption)
                    {
                        if (window.ActiveTabId is { } optionTab && view.OpenSelectControl == target.ControlIndex
                            && target.Tab == optionTab && ReferenceEquals(view.SelectPopupPage, Controller.Page(optionTab))
                            && Controller.SelectOptionFromPointer(optionTab, target.ControlIndex, target.OptionIndex))
                        { CloseSelectPopup(view); }
                        break;
                    }
                    var dismissingSelect = view.OpenSelectControl >= 0;
                    var dismissedControl = view.OpenSelectControl;
                    if (dismissingSelect) { CloseSelectPopup(view); }
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
                            var pageY = pointer.Y - ShellChrome.Height;
                            var page = Controller.Page(pageTab.Id);
                            var overControl = page?.FormControls.Any(control => control.Rect?.Contains(pointer.X, pageY) == true) == true;
                            if (view.ShiftModifier && !overControl
                                && Controller.ExtendSelectedText(pageTab.Id, pointer.X, pageY, pageViewport))
                            { break; }
                            Controller.ClearSelectedText(pageTab.Id);
                            var linkIndex = -1;
                            if (page is not null)
                            {
                                for (var index = 0; index < page.LinkTargets.Count; index++)
                                {
                                    if (page.LinkTargets[index].Contains(pointer.X, pageY))
                                    { linkIndex = index; }
                                }
                            }
                            if (linkIndex >= 0)
                            {
                                view.SelectingText = Controller.StartTextSelection(pageTab.Id, pointer.X, pageY, pageViewport);
                                view.PendingLinkActivation = true;
                                view.PendingLinkIndex = linkIndex;
                                view.PendingLinkTab = pageTab.Id;
                                view.PendingLinkX = pointer.X;
                                view.PendingLinkY = pointer.Y;
                                view.PendingLinkViewport = pageViewport;
                                break;
                            }
                            var activated = Controller.ActivateLink(pageTab.Id, pointer.X, pageY, pageViewport);
                            var focusedControl = Controller.FocusedControlIndex(pageTab.Id);
                            if (activated && focusedControl >= 0
                                && Controller.Page(pageTab.Id)?.FormControls[focusedControl].Kind == "range")
                            { view.DraggingRangeControl = focusedControl; }
                            if (activated && focusedControl >= 0
                                && Controller.Page(pageTab.Id)?.FormControls[focusedControl].Kind == "select"
                                && !(dismissingSelect && dismissedControl == focusedControl))
                            { OpenSelectPopup(view, pageTab, focusedControl); }
                            if (!activated)
                            {
                                view.SelectingText = Controller.StartTextSelection(pageTab.Id, pointer.X,
                                    pointer.Y - ShellChrome.Height, pageViewport);
                            }
                        }
                        else if (window.ActiveTabId is { } unfocused) { Controller.FocusPage(unfocused, false); }
                    }
                    break;
                case PointerButtonChanged { Pressed: false, Button: 1 } released when window.ActiveTab is { } releasedTab:
                    view.DraggingScrollbar = false;
                    view.DraggingRangeControl = -1;
                    if (view.PendingLinkActivation)
                    {
                        var pendingLinkIndex = view.PendingLinkIndex;
                        var pendingViewport = view.PendingLinkViewport;
                        var pageY = released.Y - ShellChrome.Height;
                        var releaseIsOnLink = pendingViewport is { } viewport
                            && released.X >= 0 && released.X < viewport.Width
                            && pageY >= 0 && pageY < viewport.Height
                            && Controller.Page(releasedTab.Id) is { } page
                            && pendingLinkIndex >= 0 && pendingLinkIndex < page.LinkTargets.Count
                            && page.LinkTargets[pendingLinkIndex].Contains(released.X, pageY);
                        var activate = !view.PendingLinkDragged && view.PendingLinkTab == releasedTab.Id
                            && releaseIsOnLink;
                        var linkX = released.X;
                        var linkY = pageY;
                        var pendingTab = view.PendingLinkTab;
                        view.PendingLinkActivation = false;
                        view.PendingLinkDragged = false;
                        view.PendingLinkTab = null;
                        view.PendingLinkIndex = -1;
                        view.PendingLinkViewport = null;
                        if (view.SelectingText && pendingTab is { } selectedTab && Controller.Session.Contains(selectedTab))
                        { Controller.EndTextSelection(selectedTab); }
                        if (activate)
                        {
                            Controller.ClearSelectedText(releasedTab.Id);
                            Controller.ActivateLink(releasedTab.Id, linkX, linkY, pendingViewport);
                        }
                    }
                    view.SelectingText = false;
                    Controller.EndTextSelection(releasedTab.Id);
                    break;
                case KeyChanged key:
                    view.ControlModifier = (((SDL.Keymod)key.Modifiers) & SDL.Keymod.Ctrl) != 0;
                    view.ShiftModifier = (((SDL.Keymod)key.Modifiers) & SDL.Keymod.Shift) != 0;
                    if (key.Pressed) { Key(window, view, key); }
                    break;
                case TextEntered text when view.Editing && window.ActiveTab is { } tab:
                    Controller.SetAddress(tab.Id, view.Editor.Insert(text.Text, Controller.Session.Options.MaxAddressCharacters));
                    view.Dirty = true;
                    break;
                case TextEntered text when window.ActiveTab is { } fieldTab && Controller.EditingFormControl(fieldTab.Id):
                    Controller.InsertFormText(fieldTab.Id, text.Text);
                    RefreshTextareaLayouts(fieldTab.Id);
                    view.Dirty = true;
                    break;
                case TextEntered text when window.ActiveTab is { } selectTab:
                    if (HandleSelectTypeahead(view, selectTab.Id, text.Text)) { view.Dirty = true; }
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
        if (!view.Editing && control && !alt && !shift && !key.Repeat && code == SDL.Scancode.Return
            && Controller.PageHasFocus(tab.Id) && Controller.FocusedLinkIndex(tab.Id) >= 0)
        {
            Controller.ActivateFocusedLink(tab.Id, ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity),
                forceNewTab: true, activateNewTab: false);
            return;
        }
        if (!view.Editing && Controller.IsMultilineFormControl(tab.Id)) { RefreshTextareaLayouts(tab.Id); }
        if (!control && !alt && code == SDL.Scancode.Tab)
        {
            CloseSelectPopup(view);
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
            if (control && !alt && !shift && code is SDL.Scancode.Home or SDL.Scancode.End
                && !Controller.EditingFormControl(tab.Id))
            {
                Controller.Scroll(tab.Id, code == SDL.Scancode.Home ? -double.MaxValue : double.MaxValue);
                return;
            }
            if (!control && !alt && !key.Repeat && code == SDL.Scancode.Escape)
            {
                if (view.OpenSelectControl >= 0) { CloseSelectPopup(view); return; }
                if (Controller.ClearSelectedText(tab.Id)) { return; }
            }
            if (!control && !alt && code == SDL.Scancode.Tab && view.OpenSelectControl >= 0)
            { CloseSelectPopup(view); }
            if (control && !alt && !key.Repeat && code == SDL.Scancode.A && Controller.EditingFormControl(tab.Id))
            { Controller.SelectAllFormControl(tab.Id); return; }
            if (control && !alt && !key.Repeat && code == SDL.Scancode.A
                && Controller.PageHasFocus(tab.Id) && Controller.FocusedControlIndex(tab.Id) < 0
                && Controller.SelectAllVisibleText(tab.Id)) { return; }
            if (control && !alt && !key.Repeat && code == SDL.Scancode.V && Controller.EditingFormControl(tab.Id))
            {
                var clipboard = view.Native.GetClipboardText();
                if (clipboard.Length > 0)
                {
                    Controller.InsertFormText(tab.Id, clipboard);
                    RefreshTextareaLayouts(tab.Id);
                }
                return;
            }
            if (control && !alt && !key.Repeat && code == SDL.Scancode.C && Controller.SelectedText(tab.Id) is { Length: > 0 } selected)
            { view.Native.SetClipboardText(selected); return; }
            if (shift && !control && !alt && code is (SDL.Scancode.Left or SDL.Scancode.Right)
                && !Controller.EditingFormControl(tab.Id)
                && Controller.ExtendSelectedTextByKey(tab.Id, code == SDL.Scancode.Right)) { return; }
            if (!control && !alt && code is SDL.Scancode.Left or SDL.Scancode.Down
                && Controller.AdjustFocusedRange(tab.Id, -1)) { return; }
            if (!control && !alt && code is SDL.Scancode.Right or SDL.Scancode.Up
                && Controller.AdjustFocusedRange(tab.Id, 1)) { return; }
            if (!control && !alt && code == SDL.Scancode.Home && Controller.SetFocusedRangeEndpoint(tab.Id, false)) { return; }
            if (!control && !alt && code == SDL.Scancode.End && Controller.SetFocusedRangeEndpoint(tab.Id, true)) { return; }
            if (!control && !alt && code == SDL.Scancode.Space && !key.Repeat
                && ToggleFocusedSelectPopup(window, view, tab.Id)) { return; }
            if (!control && !alt && code is SDL.Scancode.Pageup or SDL.Scancode.Pagedown
                && MoveFocusedSelectPage(view, tab.Id, code == SDL.Scancode.Pageup ? -1 : 1)) { return; }
            if (!control && !alt && code is SDL.Scancode.Up or SDL.Scancode.Down
                && Controller.MoveFocusedSelect(tab.Id, code == SDL.Scancode.Up ? -1 : 1))
            { KeepPopupSelectionVisible(view, tab.Id); return; }
            if (!control && !alt && code == SDL.Scancode.Home && Controller.SetFocusedSelectEndpoint(tab.Id, false))
            { KeepPopupSelectionVisible(view, tab.Id); return; }
            if (!control && !alt && code == SDL.Scancode.End && Controller.SetFocusedSelectEndpoint(tab.Id, true))
            { KeepPopupSelectionVisible(view, tab.Id); return; }
            if (!control && !alt && Controller.EditingFormControl(tab.Id) && code switch
            {
                SDL.Scancode.Backspace => FormEdit.Backspace,
                SDL.Scancode.Delete => FormEdit.Delete,
                SDL.Scancode.Left => FormEdit.Left,
                SDL.Scancode.Right => FormEdit.Right,
                SDL.Scancode.Up when Controller.IsMultilineFormControl(tab.Id) => FormEdit.Up,
                SDL.Scancode.Down when Controller.IsMultilineFormControl(tab.Id) => FormEdit.Down,
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
            if (!control && !alt && code == SDL.Scancode.Space && !key.Repeat
                && Controller.ToggleFocusedCheckable(tab.Id)) { return; }
            if (!control && !alt && code == SDL.Scancode.Space && !key.Repeat
                && Controller.ActivateFocusedButton(tab.Id)) { return; }
            if (!control && !alt && code is SDL.Scancode.Left or SDL.Scancode.Up or SDL.Scancode.Right or SDL.Scancode.Down)
            {
                var direction = code is SDL.Scancode.Left or SDL.Scancode.Up ? -1 : 1;
                if (Controller.MoveFocusedRadio(tab.Id, direction)) { return; }
            }
            if (!control && !alt && code == SDL.Scancode.Return && !key.Repeat)
            {
                if (ToggleFocusedSelectPopup(window, view, tab.Id)) { return; }
                if (Controller.IsMultilineFormControl(tab.Id))
                {
                    Controller.InsertFormText(tab.Id, "\n");
                    RefreshTextareaLayouts(tab.Id);
                    return;
                }
                if (view.KeyboardTarget is { } target) { Action(window, view, target.Action, target.Tab); }
                else if (Controller.PageHasFocus(tab.Id))
                { Controller.ActivateFocusedLink(tab.Id, ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity), forceNewTab: shift); }
                return;
            }
            if (!control && !alt && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } viewport)
            {
                switch (code)
                {
                    case SDL.Scancode.Down when !Controller.EditingFormControl(tab.Id): Controller.Scroll(tab.Id, 48); break;
                    case SDL.Scancode.Up when !Controller.EditingFormControl(tab.Id): Controller.Scroll(tab.Id, -48); break;
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
        if (enabled) { CloseSelectPopup(view); }
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
    private ShellFormState? Forms(BrowserWindow window, View view)
    {
        if (window.ActiveTabId is not { } id || Controller.Page(id) is not { FormControls.Count: > 0 } page) { return null; }
        var values = Enumerable.Range(0, page.FormControls.Count).Select(index => Controller.FormControlValue(id, index)).ToArray();
        var visualLines = RefreshTextareaLayouts(id, page, values);
        var focused = Controller.PageHasFocus(id) ? Controller.FocusedControlIndex(id) : -1;
        var firstLines = Enumerable.Range(0, page.FormControls.Count).Select(index =>
            page.FormControls[index].Kind == "textarea" ? Controller.TextareaFirstLine(id, index) : 0).ToArray();
        var checkedStates = Enumerable.Range(0, page.FormControls.Count).Select(index =>
            page.FormControls[index].Kind == "checkbox" && Controller.FormControlChecked(id, index)).ToArray();
        var selectIndices = Enumerable.Range(0, page.FormControls.Count).Select(index =>
            page.FormControls[index].Kind == "select" ? Controller.SelectedOptionIndex(id, index) : -1).ToArray();
        return new(values, focused, focused >= 0 ? Controller.FormControlCaret(id) : -1,
            focused >= 0 && Controller.FormControlSelectAll(id), firstLines, visualLines, checkedStates, selectIndices,
            view.OpenSelectControl, view.SelectPopupFirstOption, view.SelectPopupHoverOption);
    }
    private bool ToggleFocusedSelectPopup(BrowserWindow window, View view, TabId id)
    {
        var index = Controller.FocusedControlIndex(id);
        if (Controller.Page(id) is not { } page || index < 0 || index >= page.FormControls.Count
            || page.FormControls[index] is not { Kind: "select", Disabled: false }) { return false; }
        if (view.OpenSelectControl == index) { CloseSelectPopup(view); }
        else if (window.ActiveTab is { } tab) { OpenSelectPopup(view, tab, index); }
        return true;
    }
    private bool HandleSelectTypeahead(View view, TabId id, string text)
    {
        var index = Controller.FocusedControlIndex(id);
        if (Controller.Page(id) is not { } page || index < 0 || index >= page.FormControls.Count
            || page.FormControls[index] is not { Kind: "select", Disabled: false }) { return false; }
        if (view.SelectTypeaheadControl != index || !ReferenceEquals(view.SelectTypeaheadPage, page))
        { view.SelectTypeaheadText = ""; }
        view.SelectTypeaheadControl = index;
        view.SelectTypeaheadPage = page;
        var now = Environment.TickCount64;
        if (now - view.SelectTypeaheadTimestamp > 1000) { view.SelectTypeaheadText = ""; }
        foreach (var rune in text.EnumerateRunes().Take(32))
        {
            if (Rune.IsControl(rune)) { continue; }
            var character = rune.ToString();
            var cycle = view.SelectTypeaheadText.Length > 0
                && string.Equals(view.SelectTypeaheadText, character, StringComparison.OrdinalIgnoreCase);
            var prefix = cycle ? character : view.SelectTypeaheadText + character;
            if (prefix.EnumerateRunes().Count() > 32) { prefix = character; cycle = true; }
            view.SelectTypeaheadText = prefix;
            view.SelectTypeaheadTimestamp = now;
            _ = Controller.SelectFocusedOptionByPrefix(id, prefix, cycle);
            KeepPopupSelectionVisible(view, id);
        }
        return true;
    }
    private static void CloseSelectPopup(View view)
    {
        view.SelectPopupHoverOption = -1;
        view.SelectTypeaheadText = "";
        view.SelectTypeaheadTimestamp = 0;
        view.SelectTypeaheadControl = -1;
        view.SelectTypeaheadPage = null;
        if (view.OpenSelectControl < 0) { return; }
        view.OpenSelectControl = -1;
        view.SelectPopupFirstOption = 0;
        view.SelectPopupPage = null;
        view.Dirty = true;
    }
    private void OpenSelectPopup(View view, BrowserTab tab, int controlIndex)
    {
        if (Controller.Page(tab.Id) is not { } page
            || ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is not { } viewport
            || ShellChrome.PopupLayout(page, controlIndex, viewport, 0) is not { } popup)
        { CloseSelectPopup(view); return; }
        view.OpenSelectControl = controlIndex;
        view.SelectPopupFirstOption = Math.Clamp(Controller.SelectedOptionIndex(tab.Id, controlIndex), 0,
            Math.Max(0, page.FormControls[controlIndex].Options.Length - popup.VisibleOptions));
        view.SelectPopupPage = page;
        view.Dirty = true;
    }
    private bool MoveFocusedSelectPage(View view, TabId id, int direction)
    {
        var index = Controller.FocusedControlIndex(id);
        if (Controller.Page(id) is not { } page || index < 0 || index >= page.FormControls.Count
            || page.FormControls[index].Kind != "select") { return false; }
        var pageSize = 10;
        if (view.OpenSelectControl == index
            && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } viewport
            && ShellChrome.PopupLayout(page, index, viewport, view.SelectPopupFirstOption) is { } popup)
        { pageSize = popup.VisibleOptions; }
        _ = Controller.MoveFocusedSelectPage(id, direction, Math.Clamp(pageSize, 1, 12));
        KeepPopupSelectionVisible(view, id);
        return true;
    }
    private void UpdateHoveredLink(View view, BrowserWindow window)
    {
        var hovered = -1;
        if (view.OpenSelectControl < 0 && view.PointerX is { } x && view.PointerY is { } y
            && y >= ShellChrome.Height && window.ActiveTab is { } tab
            && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } viewport
            && x >= 0 && x < viewport.Width && y < ShellChrome.Height + viewport.Height
            && Controller.Page(tab.Id) is { } page)
        {
            var pageY = y - ShellChrome.Height;
            for (var index = page.LinkTargets.Count - 1; index >= 0; index--)
            {
                if (page.LinkTargets[index].Contains(x, pageY)) { hovered = index; break; }
            }
        }
        if (view.HoveredLink == hovered) { return; }
        view.HoveredLink = hovered;
        view.Dirty = true;
    }
    private void UpdateSelectPopupHover(View view, BrowserWindow window, double x, double y)
    {
        var hovered = -1;
        if (window.ActiveTabId is { } id && view.OpenSelectControl >= 0
            && ReferenceEquals(view.SelectPopupPage, Controller.Page(id))
            && Controller.Page(id) is { } page
            && ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is { } viewport
            && ShellChrome.PopupLayout(page, view.OpenSelectControl, viewport, view.SelectPopupFirstOption) is { } popup
            && x >= popup.Bounds.X && x < popup.Bounds.X + popup.Bounds.Width
            && y >= popup.Bounds.Y && y < popup.Bounds.Y + popup.Bounds.Height)
        {
            var row = (int)((y - popup.Bounds.Y) / popup.RowHeight);
            if (row >= 0 && row < popup.VisibleOptions) { hovered = popup.FirstOption + row; }
        }
        if (hovered == view.SelectPopupHoverOption) { return; }
        view.SelectPopupHoverOption = hovered;
        view.Dirty = true;
    }
    private void KeepPopupSelectionVisible(View view, TabId id)
    {
        if (view.OpenSelectControl < 0 || Controller.FocusedControlIndex(id) != view.OpenSelectControl
            || Controller.Page(id) is not { } page
            || ShellChrome.Viewport(view.Native.PixelSize, view.Native.PixelDensity) is not { } viewport
            || ShellChrome.PopupLayout(page, view.OpenSelectControl, viewport, view.SelectPopupFirstOption) is not { } popup)
        { return; }
        var selected = Controller.SelectedOptionIndex(id, view.OpenSelectControl);
        if (selected < popup.FirstOption) { view.SelectPopupFirstOption = selected; }
        else if (selected >= popup.FirstOption + popup.VisibleOptions)
        { view.SelectPopupFirstOption = selected - popup.VisibleOptions + 1; }
        view.Dirty = true;
    }
    private void RefreshTextareaLayouts(TabId id)
    {
        if (Controller.Page(id) is not { } page) { return; }
        var values = Enumerable.Range(0, page.FormControls.Count).Select(index => Controller.FormControlValue(id, index)).ToArray();
        RefreshTextareaLayouts(id, page, values);
    }
    private IReadOnlyList<TextareaVisualLine>[] RefreshTextareaLayouts(TabId id, BrowserPage page, IReadOnlyList<string> values)
    {
        var visualLines = Enumerable.Repeat<IReadOnlyList<TextareaVisualLine>>(Array.Empty<TextareaVisualLine>(), page.FormControls.Count).ToArray();
        for (var index = 0; index < page.FormControls.Count; index++)
        {
            if (page.FormControls[index] is not { Kind: "textarea", Rect: { } rect }) { continue; }
            visualLines[index] = chrome.WrapTextarea(values[index], Math.Max(0, rect.Width - 8));
            Controller.UpdateTextareaVisualLines(id, index, visualLines[index]);
        }
        return visualLines;
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
