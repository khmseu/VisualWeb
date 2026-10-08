using VisualWeb.Engine.Css;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using VisualWeb.Ipc.Contracts;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Browser;

public enum ChromeAction { Back, Forward, Reload, NewTab, CloseTab, NewWindow, MoveTab, PreviousTab, NextTab, ActivateTab, Address, Scrollbar }
public sealed record ChromeTarget(LayoutRect Bounds, ChromeAction Action, TabId? Tab = null);
public sealed record ShellFrame(byte[] Pixels, PixelSize Size, int Stride, IReadOnlyList<ChromeTarget> Targets);

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
    private static double CssPixels(int physical, double density)
    {
        var css = physical / density;
        // A division/multiplication round-trip can round above the integer surface size.
        if (Math.Ceiling(css * density) > physical) { css = Math.BitDecrement(css); }
        if (Math.Ceiling(css * density) != physical) { throw new ArgumentOutOfRangeException(nameof(density), "Pixel density cannot represent the surface viewport."); }
        return css;
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
    public ShellFrame Render(BrowserWindow window, BrowserPage? page, PixelSize size, double density, AddressEditor? editor = null,
        int focusedLink = -1, ChromeTarget? focusedChrome = null, ShellFormState? forms = null,
        IReadOnlyList<PageLinkRect>? selectedText = null, double scrollY = 0)
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
                Fill(box, gray);
                Fill(new(box.X + 1, box.Y + 1, Math.Max(0, box.Width - 2), Math.Max(0, box.Height - 2)),
                    control.Disabled ? new(235, 235, 235) : control.Kind is "submit" or "button" ? new(225, 225, 225)
                    : forms.SelectAll && index == forms.Focused ? new(176, 213, 249) : white);
                var text = control.Kind is "submit" or "button" ? control.Label
                    : index == forms.Focused && !forms.SelectAll && forms.Caret >= 0
                        ? forms.Values[index].Insert(Math.Min(forms.Caret, forms.Values[index].Length), "|")
                    : forms.Values[index];
                if (control.Kind == "textarea")
                {
                    var rows = Math.Max(1, (int)((box.Height - 8) / 15));
                    var value = forms.Values[index];
                    var lines = forms.TextareaLines?[index] is { Count: > 0 } measured
                        ? measured : WrapTextarea(value, Math.Max(0, box.Width - 8));
                    var caret = index == forms.Focused && !forms.SelectAll ? Math.Clamp(forms.Caret, 0, value.Length) : -1;
                    var firstLine = Math.Clamp(forms.TextareaFirstLines?[index] ?? 0, 0, Math.Max(0, lines.Count - rows));
                    for (var row = 0; row < rows && firstLine + row < lines.Count; row++)
                    {
                        var line = lines[firstLine + row];
                        var lineText = value[line.Start..line.End];
                        if (caret >= 0 && AddressEditor.FindVisualLine(lines, caret) == firstLine + row)
                        { lineText = lineText.Insert(Math.Clamp(caret - line.Start, 0, lineText.Length), "|"); }
                        Label(lineText, box.X + 4, box.Y + 15 + row * 15, box.Width - 8, control.Disabled ? gray : ink);
                    }
                }
                else
                {
                    Label(text, box.X + 4, box.Y + Math.Min(box.Height - 3, box.Height / 2 + 5), box.Width - 8,
                        control.Disabled ? gray : ink, tail: index == forms.Focused || control.Kind == "button");
                }
            }
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
        }
        return new(pixels, chrome.Size, chrome.Stride, targets.AsReadOnly());

        void Fill(LayoutRect rect, CssColor color) => commands.Add(new FillRectangle(rect, color));
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
        void Outline(PageLinkRect rect)
        {
            var (left, top, right, bottom) = Device(rect);
            for (var x = left; x <= right; x++) { FocusPixel(x, top); FocusPixel(x, bottom); }
            for (var y = top; y <= bottom; y++) { FocusPixel(left, y); FocusPixel(right, y); }
        }
        void FocusPixel(int x, int y)
        {
            var offset = y * chrome.Stride + x * 4;
            pixels[offset] = 128; pixels[offset + 1] = 96; pixels[offset + 2] = 64; pixels[offset + 3] = 255;
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
    IReadOnlyList<int>? TextareaFirstLines = null, IReadOnlyList<IReadOnlyList<TextareaVisualLine>>? TextareaLines = null);
