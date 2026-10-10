using System.Text;
using System.Diagnostics.CodeAnalysis;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Browser;

public enum ChromeAction { Back, Forward, Reload, NewTab, CloseTab, NewWindow, MoveTab, PreviousTab, NextTab, ActivateTab, Address, Scrollbar, SelectOption, SelectListboxOption }
public sealed record ChromeTarget(LayoutRect Bounds, ChromeAction Action, TabId? Tab = null, int ControlIndex = -1, int OptionIndex = -1);
public sealed record ShellFrame(byte[] Pixels, PixelSize Size, int Stride, IReadOnlyList<ChromeTarget> Targets);
internal sealed record SelectPopupLayout(LayoutRect Bounds, int FirstOption, int VisibleOptions, double RowHeight);

/// <summary>Development chrome drawn independently of untrusted page styles.</summary>
/// <remarks>References: skia-canvas and sdl-window-surface. Chrome is not HTML content.</remarks>
public sealed class ShellChrome : IDisposable
{
    public const double Height = 120;
    private readonly TextFont font;
    private readonly PaintFontRegistry fonts = new();
    private readonly PaintOptions options;
    private readonly bool multiprocess;
    private readonly bool requireSandbox;
    public ShellChrome(string fontPath, int maxPixels, bool multiprocess = false, bool requireSandbox = false)
    {
        this.multiprocess = multiprocess;
        this.requireSandbox = requireSandbox;
        options = new() { MaxPixels = maxPixels };
        font = new(fontPath);
        try { fonts.Register(font); }
        catch { fonts.Dispose(); font.Dispose(); throw; }
    }
    internal static double ScrollbarThumbHeight(double viewportHeight, double scrollHeight)
    {
        if (!double.IsFinite(viewportHeight) || viewportHeight <= 0 || !double.IsFinite(scrollHeight) || scrollHeight <= viewportHeight)
        { throw new ArgumentOutOfRangeException(nameof(scrollHeight)); }
        return Math.Min(viewportHeight, Math.Max(Math.Min(24, viewportHeight), viewportHeight * viewportHeight / scrollHeight));
    }
    public static PageViewport? Viewport(PixelSize size, double density)
    {
        if (!double.IsFinite(density) || density <= 0) { throw new ArgumentOutOfRangeException(nameof(density)); }
        var header = Math.Ceiling(Height * density);
        if (size.Width <= 0 || size.Height <= header) { return null; }
        return new(CssPixels(size.Width, density), CssPixels(size.Height - (int)header, density), density);
    }
    internal static SelectPopupLayout? PopupLayout(BrowserPage page, int controlIndex, PageViewport viewport,
        int firstOption)
    {
        if (controlIndex < 0 || controlIndex >= page.FormControls.Count
            || page.FormControls[controlIndex] is not { Kind: "select", Disabled: false, Rect: { } rect } control
            || control.Options.Length == 0) { return null; }
        const double rowHeight = 20;
        var availableBelow = Math.Max(0, viewport.Height - rect.Y - rect.Height);
        var availableAbove = Math.Max(0, rect.Y);
        var available = Math.Max(availableAbove, availableBelow);
        var visible = Math.Min(control.Options.Length, Math.Min(12, (int)(available / rowHeight)));
        if (visible <= 0) { return null; }
        var height = visible * rowHeight;
        var openAbove = availableBelow < height && availableAbove > availableBelow;
        var top = openAbove ? rect.Y - height : rect.Y + rect.Height;
        top = Math.Clamp(top, 0, Math.Max(0, viewport.Height - height));
        var width = Math.Min(rect.Width, viewport.Width);
        var left = Math.Clamp(rect.X, 0, Math.Max(0, viewport.Width - width));
        var first = Math.Clamp(firstOption, 0, control.Options.Length - visible);
        return new(new(left, Height + top, width, height), first, visible, rowHeight);
    }
    private static double CssPixels(int physical, double density)
    {
        var css = physical / density;
        // A division/multiplication round-trip can round above the integer surface size.
        if (Math.Ceiling(css * density) > physical) { css = Math.BitDecrement(css); }
        if (Math.Ceiling(css * density) != physical) { throw new ArgumentOutOfRangeException(nameof(density), "Pixel density cannot represent the surface viewport."); }
        return css;
    }
    private static string PasswordText(string value, int caret)
    {
        var length = ScalarCount(value);
        var masked = new string('*', length);
        if (caret < 0) { return masked; }
        caret = Math.Clamp(caret, 0, value.Length);
        return masked.Insert(ScalarCount(value.AsSpan(0, caret)), "|");
    }

    private static int ScalarCount(ReadOnlySpan<char> text)
    {
        var count = 0;
        foreach (var _ in text.EnumerateRunes()) { count++; }
        return count;
    }

    internal int CaretAtTextX(string value, int caret, double x, double availableWidth)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!double.IsFinite(x) || !double.IsFinite(availableWidth) || availableWidth < 0)
        { throw new ArgumentOutOfRangeException(nameof(x)); }
        if (caret >= 0)
        {
            caret = Math.Clamp(caret, 0, value.Length);
            if (caret > 0 && caret < value.Length && char.IsHighSurrogate(value[caret - 1]) && char.IsLowSurrogate(value[caret]))
            { caret--; }
        }
        var displayed = caret < 0 ? value : value.Insert(caret, "|");
        var displayStart = Math.Max(0, displayed.Length - 512);
        var safe = new string(displayed.Skip(displayStart).Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray());
        var maximum = Math.Min(safe.Length, Math.Max(0, (int)(availableWidth / 7)));
        displayStart += safe.Length - maximum;
        safe = safe[^maximum..];
        var run = font.Shape(safe, 13);
        while (safe.Length > 0 && run.Width > availableWidth)
        {
            safe = safe[1..];
            displayStart++;
            run = font.Shape(safe, 13);
        }
        if (safe.Length == 0) { return caret < 0 ? 0 : caret; }
        var bestBoundary = 0;
        var bestDistance = Math.Abs(x);
        var penX = 0.0;
        var clusters = run.Glyphs.GroupBy(glyph => glyph.Cluster).OrderBy(group => group.Key).ToArray();
        for (var index = 0; index < clusters.Length; index++)
        {
            var start = clusters[index].Key;
            var end = index + 1 < clusters.Length ? clusters[index + 1].Key : safe.Length;
            if (start < 0 || end <= start || end > safe.Length)
            { throw new PlatformException("Shell font shaping returned invalid caret clusters."); }
            var startDistance = Math.Abs(x - penX);
            if (startDistance < bestDistance) { bestDistance = startDistance; bestBoundary = start; }
            var width = clusters[index].Sum(glyph => glyph.Advance);
            var endDistance = Math.Abs(x - penX - width);
            if (endDistance < bestDistance) { bestDistance = endDistance; bestBoundary = end; }
            penX += width;
        }
        var displayedOffset = displayStart + bestBoundary;
        var result = caret < 0 ? displayedOffset : displayedOffset > caret ? displayedOffset - 1 : displayedOffset;
        result = Math.Clamp(result, 0, value.Length);
        if (result > 0 && result < value.Length && char.IsHighSurrogate(value[result - 1]) && char.IsLowSurrogate(value[result]))
        { result--; }
        return result;
    }
    internal IReadOnlyList<TextareaVisualLine> WrapTextarea(string value, double availableWidth)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!double.IsFinite(availableWidth) || availableWidth < 0) { throw new ArgumentOutOfRangeException(nameof(availableWidth)); }
        var lines = new List<TextareaVisualLine>();
        var logicalStart = 0;
        while (true)
        {
            var newline = value.IndexOf('\n', logicalStart);
            var logicalEnd = newline < 0 ? value.Length : newline;
            AddWrappedLine(logicalStart, value[logicalStart..logicalEnd]);
            if (newline < 0) { break; }
            logicalStart = newline + 1;
        }
        return lines;

        void AddWrappedLine(int sourceStart, string sourceLine)
        {
            if (sourceLine.Length == 0)
            {
                lines.Add(new(sourceStart, sourceStart));
                return;
            }
            var safe = new string(sourceLine.Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray());
            var run = font.Shape(safe, 13);
            var clusters = run.Glyphs.GroupBy(glyph => glyph.Cluster).OrderBy(group => group.Key).ToArray();
            var units = new List<(int Start, int End, double Width)>(clusters.Length);
            for (var index = 0; index < clusters.Length; index++)
            {
                var start = clusters[index].Key;
                var end = index + 1 < clusters.Length ? clusters[index + 1].Key : safe.Length;
                if (start < 0 || end <= start || end > safe.Length)
                { throw new PlatformException("Textarea shaping returned invalid text clusters."); }
                units.Add((start, end, clusters[index].Sum(glyph => glyph.Advance)));
            }
            if (units.Count == 0) { lines.Add(new(sourceStart, sourceStart + sourceLine.Length)); return; }
            var firstUnit = 0;
            while (firstUnit < units.Count)
            {
                var nextUnit = firstUnit;
                var lastBreak = -1;
                var width = 0.0;
                while (nextUnit < units.Count)
                {
                    var candidate = width + units[nextUnit].Width;
                    if (candidate > availableWidth && nextUnit > firstUnit) { break; }
                    width = candidate;
                    nextUnit++;
                    if (char.IsWhiteSpace(sourceLine[units[nextUnit - 1].End - 1])) { lastBreak = nextUnit; }
                }
                if (nextUnit < units.Count && lastBreak > firstUnit) { nextUnit = lastBreak; }
                var lineEnd = units[nextUnit - 1].End;
                lines.Add(new(sourceStart + units[firstUnit].Start, sourceStart + lineEnd));
                firstUnit = nextUnit;
            }
        }
    }
    [SuppressMessage("Sonar", "S107:Methods should not have too many parameters", Justification = "The renderer composes the complete explicit shell and page snapshot without retaining mutable cross-tab presentation state.")]
    [SuppressMessage("Sonar", "S3776:Cognitive Complexity of methods should not be too high", Justification = "This bounded composition method must keep chrome, form overlays, selection, scrolling, and pixel ownership in one ordered render pass.")]
    public ShellFrame Render(BrowserWindow window, BrowserPage? page, PixelSize size, double density, AddressEditor? editor = null,
        int focusedLink = -1, ChromeTarget? focusedChrome = null, ShellFormState? forms = null,
        IReadOnlyList<PageLinkRect>? selectedText = null, double scrollY = 0, int hoveredLink = -1)
    {
        if (size.Width <= 0 || size.Height <= 0) { throw new ArgumentOutOfRangeException(nameof(size)); }
        if (!double.IsFinite(density) || density <= 0) { throw new ArgumentOutOfRangeException(nameof(density)); }
        var width = CssPixels(size.Width, density);
        var height = CssPixels(size.Height, density);
        var commands = new List<PaintCommand>();
        var targets = new List<ChromeTarget>();
        var tab = window.ActiveTab;
        var dark = new CssColor(30, 38, 49);
        var ink = new CssColor(20, 25, 32);
        var white = new CssColor(255, 255, 255);
        Fill(new(0, 0, width, Math.Min(Height, height)), new(228, 232, 238));
        Fill(new(0, 0, width, 28), dark);
        Label("VISUALWEB DEVELOPMENT - " + (multiprocess ? "MULTIPROCESS" : "SINGLE PROCESS")
            + (requireSandbox ? OperatingSystem.IsWindows() ? " - WINDOWS APP CONTAINER REQUIRED" : " - LINUX CONFINEMENT REQUIRED"
                : " - NO SANDBOX"), 8, 20, width - 16, white);
        Button("<", new(0, 28, 32, 32), ChromeAction.PreviousTab);
        Button(">", new(32, 28, 32, 32), ChromeAction.NextTab);
        var slots = Math.Max(0, (int)((width - 64) / 120));
        var active = window.Tabs.ToList().FindIndex(t => t.Id == window.ActiveTabId);
        var start = slots == 0 ? 0 : Math.Max(0, active - slots + 1);
        for (var index = start; index < window.Tabs.Count && index < start + slots; index++)
        {
            var candidate = window.Tabs[index];
            var bounds = new LayoutRect(64 + (index - start) * 120, 28, 118, 32);
            Fill(bounds, candidate.Id == window.ActiveTabId ? white : new(195, 205, 218));
            Label(candidate.Title, bounds.X + 5, 49, 86, ink);
            targets.Add(new(bounds, ChromeAction.ActivateTab, candidate.Id));
            Label("x", bounds.X + 101, 49, 13, ink);
            targets.Add(new(new(bounds.X + 96, 28, 22, 32), ChromeAction.CloseTab, candidate.Id));
        }
        Button("B", new(0, 62, 30, 30), ChromeAction.Back, tab?.History.CanGoBack == true);
        Button("F", new(32, 62, 30, 30), ChromeAction.Forward, tab?.History.CanGoForward == true);
        Button("R", new(64, 62, 30, 30), ChromeAction.Reload, tab?.History.Current is not null);
        Button("+T", new(96, 62, 30, 30), ChromeAction.NewTab);
        Button("+W", new(128, 62, 30, 30), ChromeAction.NewWindow);
        Button("M", new(160, 62, 30, 30), ChromeAction.MoveTab, tab is not null);
        var address = new LayoutRect(196, 62, Math.Max(0, width - 204), 30);
        if (address.Width > 0)
        {
            Fill(address, editor is { SelectAll: true } ? new(176, 213, 249) : white);
            var value = editor is null ? tab?.AddressText ?? "" : editor.Text.Insert(editor.Caret, "|");
            Label(value, address.X + 4, 83, address.Width - 8, ink, tail: true);
            targets.Add(new(address, ChromeAction.Address));
        }
        Label(tab?.Status ?? "No active tab", 8, 111, width - 16,
            tab?.Error is null ? ink : new(170, 0, 0));
        if (focusedChrome is not null && targets.FirstOrDefault(target =>
            target.Action == focusedChrome.Action && target.Tab == focusedChrome.Tab) is { } selected)
        {
            var rect = selected.Bounds;
            var color = new CssColor(64, 96, 128);
            Fill(new(rect.X, rect.Y, rect.Width, 1), color);
            Fill(new(rect.X, rect.Y + rect.Height - 1, rect.Width, 1), color);
            Fill(new(rect.X, rect.Y, 1, rect.Height), color);
            Fill(new(rect.X + rect.Width - 1, rect.Y, 1, rect.Height), color);
        }
        var headerPixels = (int)Math.Min(size.Height, Math.Ceiling(Height * density));
        var pageFits = page is not null && page.Frame.Size == new PixelSize(size.Width, size.Height - headerPixels);
        var pageViewport = pageFits ? Viewport(size, density) : null;
        var scrollRange = pageViewport is { } scrollViewport ? Math.Max(0, page!.ScrollHeight - scrollViewport.Height) : 0;
        SelectPopupLayout? selectPopup = null;
        if (pageFits && forms is not null && forms.Values.Count == page!.FormControls.Count)
        {
            // Shell-owned widget overlay: renderer pixels never contain browser-edited values. Offsetting by the
            // device header keeps chrome rasterization aligned with the copied page rows at fractional densities.
            var offset = headerPixels / density;
            for (var index = 0; index < page.FormControls.Count; index++)
            {
                var control = page.FormControls[index];
                if (control.Rect is not { } rect || control.Kind == "hidden") { continue; }
                var box = new LayoutRect(rect.X, rect.Y + offset, rect.Width, rect.Height);
                var gray = new CssColor(118, 118, 118);
                if (control.Kind == "range")
                {
                    DrawRangeControl(control, index, box, gray);
                    continue;
                }
                if (control.Kind is "checkbox" or "radio")
                {
                    DrawCheckControl(control, index, box, gray);
                    continue;
                }
                Fill(box, gray);
                Fill(new(box.X + 1, box.Y + 1, Math.Max(0, box.Width - 2), Math.Max(0, box.Height - 2)),
                    control.Disabled ? new(235, 235, 235) : control.Kind is "submit" or "button" or "reset" or "inert" ? new(225, 225, 225)
                    : forms.SelectAll && index == forms.Focused ? new(176, 213, 249) : white);
                if (control.Kind == "color" && FormColor.IsValid(forms.Values[index]))
                {
                    var colorValue = forms.Values[index];
                    var color = new CssColor(Convert.ToByte(colorValue[1..3], 16), Convert.ToByte(colorValue[3..5], 16), Convert.ToByte(colorValue[5..7], 16));
                    Fill(new(box.X + 4, box.Y + 4, Math.Max(0, box.Width - 8), Math.Max(0, box.Height - 8)), color);
                    continue;
                }
                var selectedOption = forms.SelectIndices?[index] ?? Array.FindIndex(control.Options, option => option.Selected);
                var selectedCount = forms.SelectSets is { } sets && index < sets.Count ? sets[index].Count
                    : selectedOption >= 0 ? 1 : 0;
                if (control.Kind == "select" && control.SelectRows > 1)
                {
                    DrawSelectListbox(control, index, box, selectedOption, gray);
                    continue;
                }
                var value = forms.Values[index];
                var showingPlaceholder = value.Length == 0 && control.Placeholder.Length > 0;
                var text = control.Kind is "submit" or "button" or "reset" or "inert" ? control.Label
                    : control.Kind == "select" ? control.Multiple ? $"{selectedCount} selected  v"
                        : (selectedOption >= 0 ? control.Options[selectedOption].Label : "") + "  v"
                    : control.Kind == "password" ? PasswordText(value,
                        index == forms.Focused && !forms.SelectAll ? forms.Caret : -1)
                    : showingPlaceholder ? index == forms.Focused && !forms.SelectAll && forms.Caret == 0
                        ? "|" + control.Placeholder : control.Placeholder
                    : index == forms.Focused && !forms.SelectAll && forms.Caret >= 0
                        ? value.Insert(Math.Min(forms.Caret, value.Length), "|")
                    : value;
                if (control.Kind == "textarea")
                {
                    var rows = Math.Max(1, (int)((box.Height - 8) / 15));
                    var textareaValue = forms.Values[index];
                    var displayValue = textareaValue.Length == 0 ? control.Placeholder : textareaValue;
                    var lines = textareaValue.Length == 0 ? WrapTextarea(displayValue, Math.Max(0, box.Width - 8))
                        : forms.TextareaLines?[index] is { Count: > 0 } measured
                            ? measured : WrapTextarea(value, Math.Max(0, box.Width - 8));
                    var caret = textareaValue.Length > 0 && index == forms.Focused && !forms.SelectAll
                        ? Math.Clamp(forms.Caret, 0, textareaValue.Length) : -1;
                    var firstLine = Math.Clamp(forms.TextareaFirstLines?[index] ?? 0, 0, Math.Max(0, lines.Count - rows));
                    for (var row = 0; row < rows && firstLine + row < lines.Count; row++)
                    {
                        var line = lines[firstLine + row];
                        var lineText = displayValue[line.Start..line.End];
                        if (caret >= 0 && AddressEditor.FindVisualLine(lines, caret) == firstLine + row)
                        { lineText = lineText.Insert(Math.Clamp(caret - line.Start, 0, lineText.Length), "|"); }
                        Label(lineText, box.X + 4, box.Y + 15 + row * 15, box.Width - 8,
                            control.Disabled ? gray : textareaValue.Length == 0 && control.Placeholder.Length > 0 ? new(128, 128, 128) : ink);
                    }
                }
                else
                {
                    Label(text, box.X + 4, box.Y + Math.Min(box.Height - 3, box.Height / 2 + 5), box.Width - 8,
                        control.Disabled ? gray : showingPlaceholder ? new(128, 128, 128) : ink,
                        tail: index == forms.Focused || control.Kind is "button" or "reset" or "inert");
                }
            }
            if (forms.OpenSelect >= 0 && pageViewport is { } popupViewport
                && (selectPopup = PopupLayout(page, forms.OpenSelect, popupViewport, forms.SelectPopupFirstOption)) is { } popup)
            { DrawSelectPopup(popup); }
        }
        var chrome = CpuRasterizer.Render(new(width, height, commands), fonts, density, options: options);
        var pixels = chrome.Pixels.ToArray();
        if (chrome.Size != size) { throw new PlatformException("Chrome framebuffer does not match the native surface size."); }
        if (pageFits)
        {
            page!.Frame.Pixels.Span.CopyTo(pixels.AsSpan(headerPixels * chrome.Stride));
            if (selectedText is not null)
            {
                foreach (var rect in selectedText)
                {
                    var (left, top, right, bottom) = Device(rect);
                    for (var y = top; y <= bottom; y++)
                    {
                        for (var x = left; x <= right; x++)
                        {
                            var offset = y * chrome.Stride + x * 4;
                            pixels[offset] = (byte)((pixels[offset] + 245) / 2);
                            pixels[offset + 1] = (byte)((pixels[offset + 1] + 205) / 2);
                            pixels[offset + 2] = (byte)((pixels[offset + 2] + 180) / 2);
                        }
                    }
                }
            }
            if (forms is not null && forms.Values.Count == page.FormControls.Count)
            {
                foreach (var control in page.FormControls)
                {
                    if (control.Rect is not { } rect || control.Kind == "hidden") { continue; }
                    var (left, top, right, bottom) = Device(rect);
                    for (var y = top; y <= bottom; y++)
                    {
                        chrome.Pixels.Span.Slice(y * chrome.Stride + left * 4, (right - left + 1) * 4)
                            .CopyTo(pixels.AsSpan(y * chrome.Stride + left * 4));
                    }
                }
                if (forms.Focused >= 0 && forms.Focused < page.FormControls.Count && page.FormControls[forms.Focused].Rect is { } focus)
                { Outline(focus); }
            }
            if (hoveredLink >= 0 && hoveredLink < page.LinkTargets.Count && hoveredLink != focusedLink)
            {
                foreach (var rect in page.LinkTargets[hoveredLink].Rects) { Outline(rect, 80, 130, 190); }
            }
            if (focusedLink >= 0 && focusedLink < page.LinkTargets.Count)
            {
                foreach (var rect in page.LinkTargets[focusedLink].Rects) { Outline(rect); }
            }
            if (scrollRange > 0 && pageViewport is { } visible)
            {
                var track = new LayoutRect(Math.Max(0, width - 12), Height, Math.Min(width, 12), visible.Height);
                var thumbHeight = ScrollbarThumbHeight(visible.Height, page!.ScrollHeight);
                var thumbRange = visible.Height - thumbHeight;
                var thumbY = Height + (thumbRange <= 0 ? 0 : thumbRange * Math.Clamp(scrollY / scrollRange, 0, 1));
                targets.Add(new(track, ChromeAction.Scrollbar, tab?.Id));
                FillSurface(track, 222, 226, 232);
                FillSurface(new(track.X + 2, thumbY, Math.Max(0, track.Width - 4), thumbHeight), 105, 117, 132);
            }
            if (selectPopup is { } popup && forms is not null)
            {
                var left = Math.Clamp((int)Math.Floor(popup.Bounds.X * density), 0, size.Width - 1);
                var right = Math.Clamp((int)Math.Ceiling((popup.Bounds.X + popup.Bounds.Width) * density) - 1,
                    left, size.Width - 1);
                var top = Math.Clamp((int)Math.Floor(popup.Bounds.Y * density), 0, size.Height - 1);
                var bottom = Math.Clamp((int)Math.Ceiling((popup.Bounds.Y + popup.Bounds.Height) * density) - 1,
                    top, size.Height - 1);
                for (var y = top; y <= bottom; y++)
                {
                    chrome.Pixels.Span.Slice(y * chrome.Stride + left * 4, (right - left + 1) * 4)
                        .CopyTo(pixels.AsSpan(y * chrome.Stride + left * 4));
                }
                for (var row = 0; row < popup.VisibleOptions; row++)
                {
                    targets.Add(new(new(popup.Bounds.X, popup.Bounds.Y + row * popup.RowHeight,
                        popup.Bounds.Width, popup.RowHeight), ChromeAction.SelectOption, tab?.Id,
                        forms.OpenSelect, popup.FirstOption + row));
                }
            }
        }
        return new(pixels, chrome.Size, chrome.Stride, targets.AsReadOnly());

        void Fill(LayoutRect rect, CssColor color) => commands.Add(new FillRectangle(rect, color));
        void DrawRangeControl(PageFormControl control, int index, LayoutRect box, CssColor gray)
        {
            var inset = Math.Min(7, box.Width / 2);
            var trackStart = box.X + inset;
            var end = box.X + box.Width - inset;
            var centerY = box.Y + box.Height / 2;
            Fill(new(trackStart, centerY, Math.Max(1, end - trackStart), 2), gray);
            var rangeValue = double.Parse(forms!.Values[index], System.Globalization.CultureInfo.InvariantCulture);
            var range = control.Maximum!.Value - control.Minimum!.Value;
            var fraction = range == 0 || !double.IsFinite(range) ? 0.5 : (rangeValue - control.Minimum.Value) / range;
            if (!double.IsFinite(fraction)) { fraction = 0.5; }
            var thumbWidth = Math.Min(12, box.Width);
            var thumbHeight = Math.Min(16, box.Height);
            var trackRange = Math.Max(0, end - trackStart);
            var thumbX = Math.Clamp(trackStart + Math.Clamp(fraction, 0, 1) * trackRange,
                box.X + thumbWidth / 2, box.X + box.Width - thumbWidth / 2);
            var thumbColor = control.Disabled ? gray : index == forms.Focused
                ? new CssColor(40, 90, 160) : new CssColor(80, 88, 98);
            Fill(new(thumbX - thumbWidth / 2, centerY - thumbHeight / 2, thumbWidth, thumbHeight), thumbColor);
        }
        void DrawCheckControl(PageFormControl control, int index, LayoutRect box, CssColor gray)
        {
            var side = Math.Min(13, Math.Min(box.Width, box.Height));
            var check = new LayoutRect(box.X + 3, box.Y + (box.Height - side) / 2, side, side);
            if (control.Kind == "checkbox")
            {
                Fill(check, gray);
                Fill(new(check.X + 1, check.Y + 1, Math.Max(0, side - 2), Math.Max(0, side - 2)), control.Disabled ? new(235, 235, 235) : white);
                if (forms!.Checked?[index] == true)
                {
                    var mark = control.Disabled ? gray : ink;
                    Fill(new(check.X + 2, check.Y + 6, 2, 2), mark); Fill(new(check.X + 4, check.Y + 8, 2, 2), mark);
                    Fill(new(check.X + 6, check.Y + 6, 2, 2), mark); Fill(new(check.X + 8, check.Y + 4, 2, 2), mark);
                    Fill(new(check.X + 10, check.Y + 2, 2, 2), mark);
                }
                return;
            }
            var center = side / 2;
            var radius = Math.Max(1, center);
            for (var row = -radius; row <= radius; row++)
            {
                var half = (int)Math.Floor(Math.Sqrt(Math.Max(0, radius * radius - row * row)));
                Fill(new(check.X + center - half, check.Y + center + row, half * 2 + 1, 1), gray);
            }
            if (forms!.Checked?[index] == true)
            {
                var mark = control.Disabled ? gray : ink;
                var dot = Math.Max(1, side / 4);
                var dotOffset = center - dot / 2;
                for (var row = 0; row < dot; row++) { Fill(new(check.X + dotOffset, check.Y + dotOffset + row, dot, 1), mark); }
                return;
            }
            var inner = Math.Max(1, radius - 1);
            for (var row = -inner; row <= inner; row++)
            {
                var half = (int)Math.Floor(Math.Sqrt(Math.Max(0, inner * inner - row * row)));
                Fill(new(check.X + center - half, check.Y + center + row, half * 2 + 1, 1), control.Disabled ? new(235, 235, 235) : white);
            }
        }
        void DrawSelectListbox(PageFormControl control, int index, LayoutRect box, int selectedOption, CssColor gray)
        {
            var rows = Math.Min(control.SelectRows, control.Options.Length);
            for (var row = 0; row < rows; row++)
            {
                var option = control.Options[row];
                var rowBounds = new LayoutRect(box.X + 1, box.Y + 1 + row * 20, Math.Max(0, box.Width - 2), Math.Min(20, Math.Max(0, box.Height - 2 - row * 20)));
                if (row == selectedOption) { Fill(rowBounds, new(176, 213, 249)); }
                Label(option.Label, rowBounds.X + 4, rowBounds.Y + 15, rowBounds.Width - 8, option.Disabled ? gray : ink);
                targets.Add(new(rowBounds, ChromeAction.SelectListboxOption, tab?.Id, index, row));
            }
        }
        void DrawSelectPopup(SelectPopupLayout popup)
        {
            Fill(popup.Bounds, new(118, 118, 118));
            Fill(new(popup.Bounds.X + 1, popup.Bounds.Y + 1, Math.Max(0, popup.Bounds.Width - 2), Math.Max(0, popup.Bounds.Height - 2)), white);
            for (var row = 0; row < popup.VisibleOptions; row++)
            {
                var optionIndex = popup.FirstOption + row;
                var option = page!.FormControls[forms!.OpenSelect].Options[optionIndex];
                var rowBounds = new LayoutRect(popup.Bounds.X + 1, popup.Bounds.Y + 1 + row * popup.RowHeight, Math.Max(0, popup.Bounds.Width - 2), popup.RowHeight);
                if (optionIndex == forms.SelectPopupHoverOption) { Fill(rowBounds, option.Disabled ? new(230, 230, 230) : new(205, 224, 245)); }
                else if (forms.SelectSets?[forms.OpenSelect].Contains(optionIndex) == true || optionIndex == forms.SelectIndices?[forms.OpenSelect]) { Fill(rowBounds, new(176, 213, 249)); }
                Label(option.Label, rowBounds.X + 4, rowBounds.Y + 15, rowBounds.Width - 8, option.Disabled ? new(118, 118, 118) : ink);
            }
        }
        void FillSurface(LayoutRect rect, byte red, byte green, byte blue)
        {
            var left = Math.Clamp((int)Math.Floor(rect.X * density), 0, size.Width - 1);
            var right = Math.Clamp((int)Math.Ceiling((rect.X + rect.Width) * density) - 1, left, size.Width - 1);
            var top = Math.Clamp(headerPixels + (int)Math.Floor((rect.Y - Height) * density), headerPixels, size.Height - 1);
            var bottom = Math.Clamp(headerPixels + (int)Math.Ceiling((rect.Y + rect.Height - Height) * density) - 1, top, size.Height - 1);
            for (var y = top; y <= bottom; y++)
            {
                for (var x = left; x <= right; x++)
                {
                    var offset = y * chrome.Stride + x * 4;
                    pixels[offset] = red; pixels[offset + 1] = green; pixels[offset + 2] = blue; pixels[offset + 3] = 255;
                }
            }
        }
        (int Left, int Top, int Right, int Bottom) Device(PageLinkRect rect)
        {
            var left = Math.Clamp((int)Math.Floor(rect.X * density), 0, size.Width - 1);
            var right = Math.Clamp((int)Math.Ceiling((rect.X + rect.Width) * density) - 1, left, size.Width - 1);
            var top = Math.Clamp(headerPixels + (int)Math.Floor(rect.Y * density), headerPixels, size.Height - 1);
            var bottom = Math.Clamp(headerPixels + (int)Math.Ceiling((rect.Y + rect.Height) * density) - 1, top, size.Height - 1);
            return (left, top, right, bottom);
        }
        void Outline(PageLinkRect rect, byte red = 128, byte green = 96, byte blue = 64)
        {
            var (left, top, right, bottom) = Device(rect);
            for (var x = left; x <= right; x++) { FocusPixel(x, top, red, green, blue); FocusPixel(x, bottom, red, green, blue); }
            for (var y = top; y <= bottom; y++) { FocusPixel(left, y, red, green, blue); FocusPixel(right, y, red, green, blue); }
        }
        void FocusPixel(int x, int y, byte red, byte green, byte blue)
        {
            var offset = y * chrome.Stride + x * 4;
            pixels[offset] = red; pixels[offset + 1] = green; pixels[offset + 2] = blue; pixels[offset + 3] = 255;
        }
        void Button(string label, LayoutRect rect, ChromeAction action, bool enabled = true)
        {
            Fill(rect, enabled ? white : new(209, 214, 220));
            Label(label, rect.X + 5, rect.Y + 21, rect.Width - 10, enabled ? ink : new(115, 120, 128));
            if (enabled) { targets.Add(new(rect, action)); }
        }
        void Label(string value, double x, double baseline, double available, CssColor color, bool tail = false)
        {
            if (available <= 0) { return; }
            // Chrome remains readable even when page titles/URLs need unsupported script shaping.
            var safe = new string(value.Take(512).Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());
            var maximum = Math.Min(safe.Length, Math.Max(0, (int)(available / 7)));
            if (tail && value.Length > 512) { safe = new(value.TakeLast(512).Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray()); }
            safe = tail ? safe[^maximum..] : safe[..maximum];
            var run = font.Shape(safe, 13);
            while (safe.Length > 0 && run.Width > available)
            {
                safe = tail ? safe[1..] : safe[..^1];
                run = font.Shape(safe, 13);
            }
            if (run.Glyphs.Count == 0) { return; }
            var glyphs = new List<PaintGlyph>();
            foreach (var glyph in run.Glyphs)
            {
                glyphs.Add(new((ushort)glyph.GlyphId, x + glyph.OffsetX, baseline + glyph.OffsetY));
                x += glyph.Advance;
            }
            commands.Add(new DrawGlyphRun(run.FontIdentity, run.FontSize, color, glyphs));
        }
    }
    public static ChromeTarget? Hit(IReadOnlyList<ChromeTarget> targets, double x, double y) =>
        targets.LastOrDefault(target => x >= target.Bounds.X && y >= target.Bounds.Y
            && x < target.Bounds.X + target.Bounds.Width && y < target.Bounds.Y + target.Bounds.Height);
    public void Dispose() { fonts.Dispose(); font.Dispose(); }
}

/// <summary>Browser-owned current form values and focused field caret for the shell widget overlay.</summary>
public sealed record ShellFormState(IReadOnlyList<string> Values, int Focused, int Caret, bool SelectAll = false,
    IReadOnlyList<int>? TextareaFirstLines = null, IReadOnlyList<IReadOnlyList<TextareaVisualLine>>? TextareaLines = null,
    IReadOnlyList<bool>? Checked = null, IReadOnlyList<int>? SelectIndices = null,
    IReadOnlyList<IReadOnlySet<int>>? SelectSets = null,
    int OpenSelect = -1, int SelectPopupFirstOption = 0, int SelectPopupHoverOption = -1);
