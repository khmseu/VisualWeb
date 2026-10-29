using System.Security.Cryptography;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Text;
using Xunit;

namespace VisualWeb.Browser.Tests;

[Collection(nameof(RendererProcessCollection))]
public sealed class FontTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string Font(string name = "") => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans" + name + ".ttf");
    private static string Renderer => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private const string Styles = "<style>*{margin:0}body{font-size:16px;line-height:40px}</style>";
    private static LoadedPage Document(string markup, string extraStyles = "") =>
        new(BrowserUrl.Parse("https://example.com/"), "<!doctype html>" + Styles + extraStyles + markup, 200, []);
    private static StaticPageRenderer Local() => new(Font(), 100000,
        boldFontPath: Font("-Bold"), italicFontPath: Font("-Italic"), boldItalicFontPath: Font("-BoldItalic"));

    [Theory]
    [InlineData("-Bold", "C976E4B1B99EDC88775377FCC21692CA4BFA46B6D6CA6522BFDA505B28FF9D6A")]
    [InlineData("-Italic", "36CFF144DF01309DAB648BEA71BAFF9BB074026914AFE63AEACC8BC90B67A28B")]
    [InlineData("-BoldItalic", "6EDF4227EF0FA846ACA70E86A307804CA4401741830F5B3AF0F2554ABE2B8466")]
    public void ActualStyleFacesHavePinnedDistinctNativeIdentities(string suffix, string digest)
    {
        Assert.Equal(digest, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Font(suffix)))));
        using var regular = new TextFont(Font());
        using var face = new TextFont(Font(suffix));
        Assert.NotEqual(regular.Identity, face.Identity);
        Assert.NotEqual(regular.Shape("Sample", 24, Cancellation).Width, face.Shape("Sample", 24, Cancellation).Width);
    }

    [Theory]
    [InlineData("<h1>Sample</h1>", "-Bold")]
    [InlineData("<h2>Sample</h2>", "-Bold")]
    [InlineData("<h3>Sample</h3>", "-Bold")]
    [InlineData("<h4>Sample</h4>", "-Bold")]
    [InlineData("<h5>Sample</h5>", "-Bold")]
    [InlineData("<h6>Sample</h6>", "-Bold")]
    [InlineData("<b>Sample</b>", "-Bold")]
    [InlineData("<strong>Sample</strong>", "-Bold")]
    [InlineData("<em>Sample</em>", "-Italic")]
    [InlineData("<i>Sample</i>", "-Italic")]
    [InlineData("<cite>Sample</cite>", "-Italic")]
    [InlineData("<var>Sample</var>", "-Italic")]
    [InlineData("<strong><em>Sample</em></strong>", "-BoldItalic")]
    [InlineData("<em><strong>Sample</strong></em>", "-BoldItalic")]
    public async Task DefaultMarkupUsesExactFaceBytesForShapingAndPainting(string markup, string suffix)
    {
        using var renderer = Local();
        using var expected = new StaticPageRenderer(Font(suffix), 100000);
        using var regular = new StaticPageRenderer(Font(), 100000);
        var viewport = new PageViewport(300, 100, 1);
        var actual = await renderer.RenderAsync(Document(markup), viewport, Cancellation);
        var reset = Document(markup, "<style>*{font-weight:400;font-style:normal}</style>");
        var reference = await expected.RenderAsync(reset, viewport, Cancellation);
        var plain = await regular.RenderAsync(reset, viewport, Cancellation);
        Assert.Equal(reference.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
        Assert.NotEqual(plain.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData("<strong>Sample</strong>")]
    [InlineData("<em>Sample</em>")]
    [InlineData("<strong><em>Sample</em></strong>")]
    [InlineData("<span style='font-weight:500'>Sample</span>")]
    [InlineData("<span style='font-style:oblique'>Sample</span>")]
    public async Task MissingExactFaceStillFailsWithoutSynthesisOrNearestMatching(string markup)
    {
        using var renderer = new StaticPageRenderer(Font(), 100000);
        await Assert.ThrowsAsync<UnsupportedTextException>(() =>
            renderer.RenderAsync(Document(markup), new(300, 100, 1), Cancellation));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AllFacesSurviveWorkerConfinementScriptsAndRetainedResize(bool confined, bool scripts)
    {
        using var renderer = new ProcessPageRenderer(Renderer, Font(), requireSandbox: confined,
            executeInlineScripts: scripts, boldFontPath: Font("-Bold"), italicFontPath: Font("-Italic"),
            boldItalicFontPath: Font("-BoldItalic"));
        var document = Document("<h1>Heading</h1><p>Regular</p><p><strong>Bold</strong></p>"
            + "<p><em>Italic</em></p><p><b><i>Both</i></b></p>"
            + (scripts ? "<script>document.title='Styled'</script>" : ""));
        var viewport = new PageViewport(300, 200, 1);
        var resized = viewport with { Width = 260, ScrollY = 20 };
        byte[] reference, expectedRetained;
        using (var expected = Local())
        {
            reference = expected.Render(document, viewport, Cancellation).Frame.Pixels.ToArray();
            expected.CommitDocument(document.DocumentId);
            expectedRetained = expected.Render(document, resized, Cancellation, reuseDocument: true).Frame.Pixels.ToArray();
        }
        var frame = await renderer.RenderAsync(document, viewport, Cancellation);
        Assert.Equal(reference, frame.Frame.Pixels.ToArray());
        if (scripts) { Assert.Equal("Styled", frame.Title); }
        renderer.CommitDocument(document.DocumentId);
        var pid = renderer.ProcessId;
        var retained = await renderer.RenderRetainedAsync(document, resized, Cancellation);
        Assert.Equal(expectedRetained, retained.Frame.Pixels.ToArray());
        Assert.Equal(pid, renderer.ProcessId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MissingConfiguredFileFailsDuringLocalStartup(int missing)
    {
        var paths = new[] { Font("-Bold"), Font("-Italic"), Font("-BoldItalic") };
        paths[missing] += ".missing";
        Assert.Throws<FileNotFoundException>(() => new StaticPageRenderer(Font(), 100000,
            boldFontPath: paths[0], italicFontPath: paths[1], boldItalicFontPath: paths[2]));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task MissingConfiguredFileFailsWorkerStartupWithoutUnconfinedRetry(bool confined, int missing)
    {
        var paths = new[] { Font("-Bold"), Font("-Italic"), Font("-BoldItalic") };
        paths[missing] += ".missing";
        using var renderer = new ProcessPageRenderer(Renderer, Font(), requireSandbox: confined,
            boldFontPath: paths[0], italicFontPath: paths[1], boldItalicFontPath: paths[2]);
        await Assert.ThrowsAsync<RendererProcessException>(() =>
            renderer.RenderAsync(Document("<p>Regular</p>"), new(300, 100, 1), Cancellation));
        Assert.Null(renderer.ProcessId);
    }

    [Theory]
    [InlineData(0, "<strong>Sample</strong>")]
    [InlineData(1, "<em>Sample</em>")]
    [InlineData(2, "<span style='font-weight:700;font-style:italic'>Sample</span>")]
    public async Task EachOptionalSlotWorksIndependentlyAndOtherStylesStillFail(int slot, string markup)
    {
        using var renderer = new StaticPageRenderer(Font(), 100000,
            boldFontPath: slot == 0 ? Font("-Bold") : null,
            italicFontPath: slot == 1 ? Font("-Italic") : null,
            boldItalicFontPath: slot == 2 ? Font("-BoldItalic") : null);
        await renderer.RenderAsync(Document(markup), new(300, 100, 1), Cancellation);
        var missing = slot == 0 ? "<em>Sample</em>" : "<strong>Sample</strong>";
        await Assert.ThrowsAsync<UnsupportedTextException>(() =>
            renderer.RenderAsync(Document(missing), new(300, 100, 1), Cancellation));
    }

    [Fact]
    public async Task ExplicitDescriptorsCanShareBytesWithoutDuplicatePaintRegistration()
    {
        using var renderer = new StaticPageRenderer(Font(), 100000,
            boldFontPath: Font(), italicFontPath: Font(), boldItalicFontPath: Font());
        var actual = await renderer.RenderAsync(Document("<b><i>Sample</i></b>"), new(300, 100, 1), Cancellation);
        using var regular = new StaticPageRenderer(Font(), 100000);
        var expected = await regular.RenderAsync(Document("Sample"), new(300, 100, 1), Cancellation);
        Assert.Equal(expected.Frame.Pixels.ToArray(), actual.Frame.Pixels.ToArray());
    }

    [Theory]
    [InlineData("--font-bold")]
    [InlineData("--font-italic")]
    [InlineData("--font-bold-italic")]
    [InlineData("--unknown", "font.ttf")]
    [InlineData("--font-bold", "a.ttf", "--font-bold", "b.ttf")]
    [InlineData("--font-italic", "--font-bold")]
    public async Task WorkerRejectsInvalidStartupOptionsWithoutProtocolOutput(params string[] options)
    {
        var host = new DirectoryInfo(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()).Parent!.Parent!.Parent!;
        var info = new System.Diagnostics.ProcessStartInfo(Path.Combine(host.FullName, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"))
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { Renderer, "--development-unsandboxed", "--font", Font() }.Concat(options))
        { info.ArgumentList.Add(argument); }
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(Cancellation);
        var error = process.StandardError.ReadToEndAsync(Cancellation);
        await process.WaitForExitAsync(Cancellation);
        Assert.NotEqual(0, process.ExitCode);
        Assert.Equal("", await output);
        Assert.NotEmpty(await error);
    }
}
