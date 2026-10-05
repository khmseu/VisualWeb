using VisualWeb.Engine.Css;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Browser;

public enum ChromeAction { Back, Forward, Reload, NewTab, CloseTab, NewWindow, MoveTab, PreviousTab, NextTab, ActivateTab, Address }
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
    public ShellFrame Render(BrowserWindow window, BrowserPage? page, PixelSize size, double density, AddressEditor? editor = null)
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
            + (requireSandbox ? " - LINUX CONFINEMENT REQUIRED" : " - NO SANDBOX"), 8, 20, width - 16, white);
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
        var chrome = CpuRasterizer.Render(new(width, height, commands), fonts, density, options: options);
        var pixels = chrome.Pixels.ToArray();
        var headerPixels = (int)Math.Min(size.Height, Math.Ceiling(Height * density));
        if (chrome.Size != size) { throw new PlatformException("Chrome framebuffer does not match the native surface size."); }
        if (page is not null && page.Frame.Size == new PixelSize(size.Width, size.Height - headerPixels))
        {
            page.Frame.Pixels.Span.CopyTo(pixels.AsSpan(headerPixels * chrome.Stride));
        }
        return new(pixels, chrome.Size, chrome.Stride, targets.AsReadOnly());

        void Fill(LayoutRect rect, CssColor color) => commands.Add(new FillRectangle(rect, color));
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
