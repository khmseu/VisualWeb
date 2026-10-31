using VisualWeb.Engine.Css;
using VisualWeb.Engine.Layout;
using VisualWeb.Engine.Paint;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Engine.Content.Tests;

public sealed class HiddenRenderingTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");

    [Fact]
    public void HiddenNativePixelsAndGeometryMatchIndependentDisplayNoneReference()
    {
        using var font = new TextFont(FontPath);
        var text = new FontSet();
        text.Register("serif", font);
        using var fonts = new PaintFontRegistry();
        fonts.Register(font);
        CssStyleSource[] styles = [new("*{margin:0}body{font-size:20px;line-height:1}")];
        const string actualHtml = """
            <!doctype html><p id=shown>Before <a hidden href=/secret>Secret</a> After</p>
            <div hidden><p>Unpainted</p><input name=secret value=inside></div>
            """;
        const string referenceHtml = """
            <!doctype html><p id=shown>Before <a style="display:none" href=/secret>Secret</a> After</p>
            <div style="display:none"><p>Unpainted</p><input name=secret value=inside></div>
            """;
        var actual = OfflinePageRenderer.Render(actualHtml, styles, text, fonts, 220, 80, cancellationToken: Cancellation);
        var expected = OfflinePageRenderer.Render(referenceHtml, styles, text, fonts, 220, 80, cancellationToken: Cancellation);
        var overridden = OfflinePageRenderer.Render(actualHtml, [.. styles, new("a{display:inline}")],
            text, fonts, 220, 80, cancellationToken: Cancellation);
        var explicitOverride = OfflinePageRenderer.Render(
            actualHtml.Replace("<a hidden", "<a hidden style='display:inline'", StringComparison.Ordinal),
            styles, text, fonts, 220, 80, cancellationToken: Cancellation);

        Assert.Empty(actual.Styles.Diagnostics);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.Equal(Find(expected.Layout.Root!, "shown")!.Content, Find(actual.Layout.Root!, "shown")!.Content);
        Assert.DoesNotContain(Find(actual.Layout.Root!, "shown")!.Lines.SelectMany(line => line.Fragments),
            fragment => fragment.Run.Text.Contains("Secret", StringComparison.Ordinal));
        Assert.NotEqual(actual.Frame.Pixels.ToArray(), overridden.Frame.Pixels.ToArray());
        Assert.Equal(explicitOverride.Frame.Pixels.ToArray(), overridden.Frame.Pixels.ToArray());
        Assert.Contains(Find(overridden.Layout.Root!, "shown")!.Lines.SelectMany(line => line.Fragments),
            fragment => fragment.Run.Text == "Secret");
    }

    private static LayoutBox? Find(LayoutBox root, string id) => root.Element.GetAttribute("id") == id ? root
        : root.Children.Select(child => Find(child, id)).FirstOrDefault(found => found is not null);
}
