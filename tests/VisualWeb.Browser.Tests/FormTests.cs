using System.Net;
using VisualWeb.Core.Url;
using VisualWeb.Engine.Net;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class FormTests
{
    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    private static string RendererPath => Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Style = "<!doctype html><style>*{margin:0} input,button,fieldset{display:block} input,button{height:20px}</style>";
    internal static LoadedPage Document(string body, string url = "https://example.com/final/index.html") =>
        new(BrowserUrl.Parse(url), Style + body, 200, []);
    private static IPageRenderer Renderer(bool process) => process ? new ProcessPageRenderer(RendererPath, FontPath)
        : new StaticPageRenderer(FontPath, 100000);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RendererReportsSupportedControlsInTreeOrderWithGeometry(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("""
            <a href=/a>a</a>
            <form action='search?x=1#frag'>
            <input name=q value="a&#10;b&#13;c" minlength=2 maxlength=5 required pattern="[a-z]+">
            <input type=HIDDEN name=h value=" v&#10; ">
            <input type=search name=s readonly disabled>
            <input type=submit name=go>
            <button name=b value=bv>Go</button>
            <button type=button>inert</button>
            </form>
            <form method=post><input name=p></form>
            <input name=formless value=f>
            <a href=/b>b</a>
            """), new(200, 400, 1), Cancellation);
        Assert.Equal(2, page.LinkTargets.Count);
        Assert.Equal([new PageForm("https://example.com/final/search?x=1#frag", null),
            new PageForm("", "Unsupported form method: post (only GET submission is implemented).")], page.Forms);
        var controls = page.FormControls;
        Assert.Equal(["text", "hidden", "search", "submit", "button", "text", "text"], controls.Select(c => c.Kind));
        Assert.Equal([0, 0, 0, 0, 0, 1, -1], controls.Select(c => c.Form));
        Assert.Equal(["q", "h", "s", "go", "b", "p", "formless"], controls.Select(c => c.Name));
        Assert.Equal(["abc", " v\n ", "", "", "bv", "", "f"], controls.Select(c => c.Value));
        Assert.Equal(["", "", "", "Submit", "Go", "", ""], controls.Select(c => c.Label));
        Assert.True(controls[0].Required);
        Assert.Equal(2, controls[0].MinLength);
        Assert.Equal(5, controls[0].MaxLength);
        Assert.Equal("[a-z]+", controls[0].Pattern);
        Assert.True(controls[2].Disabled && controls[2].ReadOnly);
        Assert.All(controls, c => Assert.Equal(1, c.BeforeLink));
        Assert.Null(controls[1].Rect);
        Assert.All(controls.Where(c => c.Kind != "hidden"), c => Assert.Equal(200, c.Rect!.Width));
        Assert.All(controls.Where(c => c.Kind != "hidden"), c => Assert.Equal(20, c.Rect!.Height));
        Assert.Equal(20, controls[2].Rect!.Y - controls[0].Rect!.Y);
        Assert.Equal(20, controls[4].Rect!.Y - controls[3].Rect!.Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RendererReportsSanitizedPlaceholdersAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input name=q placeholder=\"Search\n here\"><textarea name=message placeholder=\"First line&#10;Second line\"></textarea><input type=checkbox placeholder=ignored></form>"),
            new(200, 400, 1), Cancellation);
        Assert.Equal(["Search here", "First line\nSecond line", ""], page.FormControls.Select(control => control.Placeholder));
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RendererReportsSingleSelectOptionsAndInlineGeometry(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("""
            <form><select name=mode required>
              <option value=fast> Fast   mode </option>
              <option selected value=slow label="Slow choice">Slow mode</option>
            </select>
            <select name=default><option disabled>Unavailable</option><option value=ready>Ready</option></select></form>
            """), new(200, 400, 1), Cancellation);

        Assert.Equal(2, page.FormControls.Count);
        var control = page.FormControls[0];
        Assert.Equal("select", control.Kind);
        Assert.True(control.Required);
        Assert.Equal("slow", control.Value);
        Assert.NotNull(control.Rect);
        Assert.Equal(160, control.Rect!.Width);
        Assert.Equal(20, control.Rect.Height);
        Assert.Equal([new PageFormOption("fast", "Fast mode", false, false),
            new PageFormOption("slow", "Slow choice", false, true)], control.Options);
        Assert.Equal("ready", page.FormControls[1].Value);
        Assert.Equal([new PageFormOption("Unavailable", "Unavailable", true, false),
            new PageFormOption("ready", "Ready", false, true)], page.FormControls[1].Options);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RendererReportsPasswordControlsAndConstraints(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("""<form><input type=password name=secret value=seed minlength=4 maxlength=12 pattern="[a-z]+" required></form>"""),
            new(200, 400, 1), Cancellation);

        var control = Assert.Single(page.FormControls);
        Assert.Equal("password", control.Kind);
        Assert.Equal("seed", control.Value);
        Assert.Equal(4, control.MinLength);
        Assert.Equal(12, control.MaxLength);
        Assert.Equal("[a-z]+", control.Pattern);
        Assert.True(control.Required);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Fact]
    public void PasswordFieldSupportsMaskedEditingAndSameTabGetSubmission()
    {
        using var harness = new Harness("<form action='/login'><input type=password name=secret value=initial maxlength=16><button>Sign in</button></form>");
        var tab = harness.Tab.Id;

        Assert.False(harness.Controller.EditingFormControl(tab));
        Assert.True(harness.Controller.FocusControl(tab, 0));
        Assert.True(harness.Controller.EditingFormControl(tab));
        harness.Controller.SelectAllFormControl(tab);
        Assert.Equal("s3cret", harness.Controller.InsertFormText(tab, "s3cret"));
        Assert.True(harness.Controller.ActivateFocusedLink(tab, harness.Viewport));

        Assert.Equal("https://example.com/login?secret=s3cret", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void PasswordFieldEnforcesEditedLengthAndPatternConstraints()
    {
        using (var length = new Harness("<form><input type=password name=secret value=seed minlength=5></form>"))
        {
            Assert.True(length.Controller.FocusControl(length.Tab.Id, 0));
            length.Controller.SelectAllFormControl(length.Tab.Id);
            length.Controller.InsertFormText(length.Tab.Id, "pass");
            var error = Assert.Throws<PageNavigationException>(() => length.Controller.ActivateFocusedLink(length.Tab.Id));
            Assert.Contains("minlength", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(length.Source.Requests);
        }

        using (var pattern = new Harness("<form><input type=password name=secret value=seed pattern=\"[a-z]+\"></form>"))
        {
            Assert.True(pattern.Controller.FocusControl(pattern.Tab.Id, 0));
            pattern.Controller.SelectAllFormControl(pattern.Tab.Id);
            pattern.Controller.InsertFormText(pattern.Tab.Id, "abc1");
            var error = Assert.Throws<PageNavigationException>(() => pattern.Controller.ActivateFocusedLink(pattern.Tab.Id));
            Assert.Contains("pattern", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(pattern.Source.Requests);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RendererReportsInputAndButtonResetControls(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("""
            <form>
            <input type=reset value=Clear>
            <button type=reset>Reset all</button>
            </form>
            """), new(200, 400, 1), Cancellation);
        Assert.Equal(["reset", "reset"], page.FormControls.Select(control => control.Kind));
        Assert.Equal(["Clear", "Reset all"], page.FormControls.Select(control => control.Label));
        Assert.All(page.FormControls, control => Assert.Equal(0, control.Form));
        Assert.All(page.FormControls, control => Assert.NotNull(control.Rect));
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TextFieldPatternMetadataIsBoundedAndCrossesRendererBoundary(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input name=q pattern=\"[a-z]{2,5}\"></form>"),
            new(200, 400, 1), Cancellation);
        Assert.Equal("[a-z]{2,5}", Assert.Single(page.FormControls).Pattern);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Fact]
    public async Task PatternCountIsBoundedAndExcessIsVisible()
    {
        using var renderer = Renderer(false);
        var controls = string.Concat(Enumerable.Range(0, RendererProtocol.MaxFormPatterns + 1)
            .Select(index => $"<input name=q{index} pattern=a>"));
        var page = await renderer.RenderAsync(Document($"<form>{controls}</form>"), new(200, 400, 1), Cancellation);
        Assert.Contains("pattern", Assert.Single(page.Forms).Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RendererProtocol.MaxFormPatterns, page.FormControls.Count(control => control.Pattern is not null));
    }

    [Theory]
    [InlineData("pattern='['", "invalid pattern")]
    [InlineData("pattern='(?=a)'", "unsupported")]
    [InlineData("pattern='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'", "pattern")]
    [InlineData("<textarea pattern=a></textarea>", "textarea pattern")]
    public async Task UnsupportedPatternSemanticsAreVisible(string markup, string expected)
    {
        using var renderer = Renderer(false);
        var body = markup.StartsWith("<textarea", StringComparison.Ordinal)
            ? $"<form>{markup}</form>" : $"<form><input name=q {markup}></form>";
        var page = await renderer.RenderAsync(Document(body), new(200, 400, 1), Cancellation);
        Assert.Contains(expected, Assert.Single(page.Forms).Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DateInputReportsBoundedIsoValuesAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=date name=leap value=2024-02-29 required><input type=date name=invalid value=2023-02-29></form>"),
            new(200, 400, 1), Cancellation);

        Assert.Equal(["date", "date"], page.FormControls.Select(control => control.Kind));
        Assert.Equal(["2024-02-29", ""], page.FormControls.Select(control => control.Value));
        Assert.True(page.FormControls[0].Required);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Fact]
    public void DateInputSupportsIsoEditingAndRejectsInvalidSubmission()
    {
        using (var valid = new Harness("<form action='/save'><input type=date name=day value=2024-02-29><button>Save</button></form>"))
        {
            var controller = valid.Controller;
            var tab = valid.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            Assert.True(controller.EditingFormControl(tab));
            controller.SelectAllFormControl(tab);
            Assert.Equal("2024-03-01", controller.InsertFormText(tab, "2024-03-01"));
            Assert.True(controller.ActivateFocusedLink(tab, valid.Viewport));
            Assert.Equal("https://example.com/save?day=2024-03-01", valid.Source.Requests[^1].Url.Href);
        }

        using (var invalid = new Harness("<form><input type=date name=day><button>Save</button></form>"))
        {
            var controller = invalid.Controller;
            var tab = invalid.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            controller.SelectAllFormControl(tab);
            controller.InsertFormText(tab, "2023-02-29");
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab, invalid.Viewport));
            Assert.Contains("date", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(invalid.Source.Requests);
        }

        using (var required = new Harness("<form><input type=date name=day required><button>Save</button></form>"))
        {
            var controller = required.Controller;
            var tab = required.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab, required.Viewport));
            Assert.Contains("required", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(required.Source.Requests);
        }
    }

    [Fact]
    public async Task DateInputRejectsUnsupportedConstraintAttributes()
    {
        using var renderer = Renderer(false);
        var page = await renderer.RenderAsync(Document("<form><input type=date name=day min=2024-01-01></form>"),
            new(200, 400, 1), Cancellation);

        Assert.Contains("date min", Assert.Single(page.Forms).Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeInputReportsBoundedValuesAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=time name=meeting value=09:30 required><input type=time name=bad value=24:00></form>"),
            new(200, 400, 1), Cancellation);

        Assert.Equal(["time", "time"], page.FormControls.Select(control => control.Kind));
        Assert.Equal(["09:30", ""], page.FormControls.Select(control => control.Value));
        Assert.True(page.FormControls[0].Required);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Fact]
    public void TimeInputSupportsEditingAndValidatesBeforeSubmission()
    {
        using (var valid = new Harness("<form action='/save'><input type=time name=meeting value=09:30><button>Save</button></form>"))
        {
            var controller = valid.Controller;
            var tab = valid.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            Assert.True(controller.EditingFormControl(tab));
            controller.SelectAllFormControl(tab);
            Assert.Equal("23:59:59.5", controller.InsertFormText(tab, "23:59:59.5"));
            Assert.True(controller.ActivateFocusedLink(tab, valid.Viewport));
            Assert.Equal("https://example.com/save?meeting=23%3A59%3A59.5", valid.Source.Requests[^1].Url.Href);
        }

        using (var invalid = new Harness("<form><input type=time name=meeting><button>Save</button></form>"))
        {
            var controller = invalid.Controller;
            var tab = invalid.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            controller.SelectAllFormControl(tab);
            controller.InsertFormText(tab, "24:00");
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab, invalid.Viewport));
            Assert.Contains("time", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(invalid.Source.Requests);
        }

        using (var required = new Harness("<form><input type=time name=meeting required><button>Save</button></form>"))
        {
            var controller = required.Controller;
            var tab = required.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab, required.Viewport));
            Assert.Contains("required", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(required.Source.Requests);
        }
    }

    [Fact]
    public async Task TimeInputRejectsUnsupportedConstraintAttributes()
    {
        using var renderer = Renderer(false);
        var page = await renderer.RenderAsync(Document("<form><input type=time name=meeting step=900></form>"),
            new(200, 400, 1), Cancellation);

        Assert.Contains("time step", Assert.Single(page.Forms).Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MonthInputReportsBoundedValuesAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=month name=billing value=2024-02 required><input type=month name=invalid value=2024-13></form>"),
            new(200, 400, 1), Cancellation);

        Assert.Equal(["month", "month"], page.FormControls.Select(control => control.Kind));
        Assert.Equal(["2024-02", ""], page.FormControls.Select(control => control.Value));
        Assert.True(page.FormControls[0].Required);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Fact]
    public void MonthInputSupportsEditingAndValidatesBeforeSubmission()
    {
        using (var valid = new Harness("<form action='/save'><input type=month name=billing value=2024-02><button>Save</button></form>"))
        {
            var controller = valid.Controller;
            var tab = valid.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            Assert.True(controller.EditingFormControl(tab));
            controller.SelectAllFormControl(tab);
            Assert.Equal("2025-11", controller.InsertFormText(tab, "2025-11"));
            Assert.True(controller.ActivateFocusedLink(tab, valid.Viewport));
            Assert.Equal("https://example.com/save?billing=2025-11", valid.Source.Requests[^1].Url.Href);
        }

        using (var invalid = new Harness("<form><input type=month name=billing><button>Save</button></form>"))
        {
            var controller = invalid.Controller;
            var tab = invalid.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            controller.SelectAllFormControl(tab);
            controller.InsertFormText(tab, "2025-13");
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab, invalid.Viewport));
            Assert.Contains("month", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(invalid.Source.Requests);
        }

        using (var required = new Harness("<form><input type=month name=billing required><button>Save</button></form>"))
        {
            var controller = required.Controller;
            var tab = required.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab, required.Viewport));
            Assert.Contains("required", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(required.Source.Requests);
        }
    }

    [Fact]
    public async Task MonthInputRejectsUnsupportedConstraintAttributes()
    {
        using var renderer = Renderer(false);
        var page = await renderer.RenderAsync(Document("<form><input type=month name=billing min=2024-01></form>"),
            new(200, 400, 1), Cancellation);

        Assert.Contains("month min", Assert.Single(page.Forms).Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WeekInputReportsBoundedValuesAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=week name=week value=2020-W53 required><input type=week name=invalid value=2021-W53></form>"),
            new(200, 400, 1), Cancellation);

        Assert.Equal(["week", "week"], page.FormControls.Select(control => control.Kind));
        Assert.Equal(["2020-W53", ""], page.FormControls.Select(control => control.Value));
        Assert.True(page.FormControls[0].Required);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Fact]
    public void WeekInputSupportsEditingAndValidatesBeforeSubmission()
    {
        using (var valid = new Harness("<form action='/save'><input type=week name=week value=2020-W53><button>Save</button></form>"))
        {
            var controller = valid.Controller;
            var tab = valid.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            Assert.True(controller.EditingFormControl(tab));
            controller.SelectAllFormControl(tab);
            Assert.Equal("2021-W52", controller.InsertFormText(tab, "2021-W52"));
            Assert.True(controller.ActivateFocusedLink(tab, valid.Viewport));
            Assert.Equal("https://example.com/save?week=2021-W52", valid.Source.Requests[^1].Url.Href);
        }

        using (var invalid = new Harness("<form><input type=week name=week><button>Save</button></form>"))
        {
            var controller = invalid.Controller;
            var tab = invalid.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            controller.SelectAllFormControl(tab);
            controller.InsertFormText(tab, "2021-W53");
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab, invalid.Viewport));
            Assert.Contains("week", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(invalid.Source.Requests);
        }

        using (var required = new Harness("<form><input type=week name=week required><button>Save</button></form>"))
        {
            var controller = required.Controller;
            var tab = required.Tab.Id;
            Assert.True(controller.FocusControl(tab, 0));
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab, required.Viewport));
            Assert.Contains("required", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(required.Source.Requests);
        }
    }

    [Fact]
    public async Task WeekInputRejectsUnsupportedConstraintAttributes()
    {
        using var renderer = Renderer(false);
        var page = await renderer.RenderAsync(Document("<form><input type=week name=week max=2024-W01></form>"),
            new(200, 400, 1), Cancellation);

        Assert.Contains("week max", Assert.Single(page.Forms).Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UrlInputReportsEditableMetadataAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=url name=site value=\"https://example.com/path\" minlength=8 maxlength=80 pattern=\"https://.*\"></form>"),
            new(200, 400, 1), Cancellation);
        var control = Assert.Single(page.FormControls);
        Assert.Equal("url", control.Kind);
        Assert.Equal("https://example.com/path", control.Value);
        Assert.Equal(8, control.MinLength);
        Assert.Equal(80, control.MaxLength);
        Assert.Equal("https://.*", control.Pattern);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NumberInputReportsEditableMetadataAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=number name=count value=5 min=-10 max=10 required></form>"),
            new(200, 400, 1), Cancellation);
        var control = Assert.Single(page.FormControls);
        Assert.Equal("number", control.Kind);
        Assert.Equal("5", control.Value);
        Assert.Equal(-10, control.Minimum);
        Assert.Equal(10, control.Maximum);
        Assert.Equal(1, control.Step);
        Assert.False(control.StepAny);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NumberInputReportsStepMetadataAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=number step=0.25><input type=number step=any><input type=number step=invalid><input type=number step=-0.25></form>"),
            new(200, 400, 1), Cancellation);
        Assert.Equal([0.25, null, 1, 1], page.FormControls.Select(control => control.Step));
        Assert.Equal([false, true, false, false], page.FormControls.Select(control => control.StepAny));
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RangeInputsReportBoundedAndSnappedValuesAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=range name=a min=0 max=10 step=2 value=5><input type=range name=b><input type=range name=c min=-2 max=2 step=any value=99></form>"),
            new(200, 400, 1), Cancellation);
        Assert.Equal(["range", "range", "range"], page.FormControls.Select(control => control.Kind));
        Assert.Equal(["6", "50", "2"], page.FormControls.Select(control => control.Value));
        Assert.Equal([0, 0, -2], page.FormControls.Select(control => control.Minimum));
        Assert.Equal([10, 100, 2], page.FormControls.Select(control => control.Maximum));
        Assert.Equal([2, 1, null], page.FormControls.Select(control => control.Step));
        Assert.Equal([false, false, true], page.FormControls.Select(control => control.StepAny));
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RadioInputsReportOneSelectedValueAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=radio name=mode value=a checked><input type=radio name=mode value=b checked><input type=radio name=mode value=c></form>"),
            new(200, 400, 1), Cancellation);
        Assert.Equal(["radio", "radio", "radio"], page.FormControls.Select(control => control.Kind));
        Assert.Equal([false, true, false], page.FormControls.Select(control => control.Checked));
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckboxReportsCheckedStateAndValueAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=checkbox name=choice value=yes checked><input type=checkbox name=choice value=no></form>"),
            new(200, 400, 1), Cancellation);
        Assert.Equal(["checkbox", "checkbox"], page.FormControls.Select(control => control.Kind));
        Assert.Equal(["yes", "no"], page.FormControls.Select(control => control.Value));
        Assert.Equal([true, false], page.FormControls.Select(control => control.Checked));
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmailInputReportsEditableMetadataAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=email name=contact value=person@example.com minlength=8 maxlength=80 pattern=.+></form>"),
            new(200, 400, 1), Cancellation);
        var control = Assert.Single(page.FormControls);
        Assert.Equal("email", control.Kind);
        Assert.Equal("person@example.com", control.Value);
        Assert.Equal(8, control.MinLength);
        Assert.Equal(80, control.MaxLength);
        Assert.Equal(".+", control.Pattern);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmailMultipleReportsCanonicalValuesAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=email name=contact value=\" first@example.com , second@example.org \" multiple></form>"),
            new(200, 400, 1), Cancellation);
        var control = Assert.Single(page.FormControls);
        Assert.Equal("email", control.Kind);
        Assert.True(control.Multiple);
        Assert.Equal("first@example.com,second@example.org", control.Value);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TextareaReportsInitialMultilineValueAndGeometryAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><textarea name=message rows=3 minlength=3 maxlength=80 required>first\nsecond</textarea></form>"),
            new(200, 400, 1), Cancellation);

        var control = Assert.Single(page.FormControls);
        Assert.Equal("textarea", control.Kind);
        Assert.Equal("first\nsecond", control.Value);
        Assert.Equal(3, control.MinLength);
        Assert.Equal(80, control.MaxLength);
        Assert.True(control.Required);
        Assert.Equal(160, control.Rect!.Width);
        Assert.Equal(60, control.Rect.Height);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData("rows=65")]
    [InlineData("cols=129")]
    public async Task TextareaDimensionsEnforceRendererBounds(string dimensions)
    {
        using var renderer = Renderer(false);
        await Assert.ThrowsAsync<VisualWeb.Engine.Layout.LayoutLimitException>(() =>
            renderer.RenderAsync(Document($"<textarea {dimensions}></textarea>"), new(200, 400, 1), Cancellation));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TelephoneInputUsesTextControlMetadataAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=tel name=phone value='555-0100'></form>"),
            new(200, 400, 1), Cancellation);
        var control = Assert.Single(page.FormControls);
        Assert.Equal("tel", control.Kind);
        Assert.Equal("555-0100", control.Value);
        Assert.NotNull(control.Rect);
        Assert.Null(Assert.Single(page.Forms).Error);
    }

    [Theory]
    [InlineData("<form method=dialog><input name=q></form>", "method")]
    [InlineData("<form method=post><input name=q></form>", "method")]
    [InlineData("<form enctype=multipart/form-data><input name=q></form>", "enctype")]
    [InlineData("<form enctype=text/plain><input name=q></form>", "enctype")]
    [InlineData("<form target=_blank><input name=q></form>", "target")]
    [InlineData("<base target=_top><form><input name=q></form>", "target")]
    [InlineData("<form novalidate><input name=q></form>", "novalidate")]
    [InlineData("<form accept-charset=iso-8859-1><input name=q></form>", "accept-charset")]
    [InlineData("<form action='javascript:alert(1)'><input name=q></form>", "scheme")]
    [InlineData("<form action='ftp://example.com/'><input name=q></form>", "scheme")]
    [InlineData("<form action='http://[bad'><input name=q></form>", "action")]
    [InlineData("<base href=/other/><form><input name=q></form>", "base")]
    [InlineData("<form><input name=q dirname=d></form>", "dirname")]
    [InlineData("<form><input name=q list=l></form>", "list")]
    [InlineData("<form><input type=submit formmethod=post></form>", "formmethod")]
    [InlineData("<form><button formaction=/x>x</button></form>", "formaction")]
    [InlineData("<form><button formtarget=_blank>x</button></form>", "formtarget")]
    [InlineData("<form><input type=submit formnovalidate></form>", "formnovalidate")]
    [InlineData("<form><input type=submit formenctype=text/plain></form>", "formenctype")]
    [InlineData("<form><input type=file name=c style=display:none></form>", "file")]
    [InlineData("<form><textarea name=t dirname=d></textarea></form>", "dirname")]
    [InlineData("<form><textarea name=t wrap=hard></textarea></form>", "wrap=hard")]
    [InlineData("<form><fieldset disabled><input name=q></fieldset></form>", "fieldset")]
    [InlineData("<form id=f></form><input form=f name=q>", "form attribute")]
    [InlineData("<form><select multiple name=q><option>x</option></select></form>", "select multiple")]
    [InlineData("<form><select size=2 name=q><option>x</option></select></form>", "display size")]
    [InlineData("<form><select size=999999999999999999999 name=q><option>x</option></select></form>", "display size")]
    [InlineData("<form><select size=0 name=q><option>x</option></select></form>", "display size")]
    [InlineData("<form><select size=+2 name=q><option>x</option></select></form>", "display size")]
    public async Task UnsupportedFormSemanticsAreVisibleFormErrors(string body, string expected)
    {
        using var renderer = Renderer(false);
        var page = await renderer.RenderAsync(Document(body), new(200, 400, 1), Cancellation);
        var form = Assert.Single(page.Forms);
        Assert.Equal("", form.Action);
        Assert.Contains(expected, form.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0001")]
    [InlineData("-2")]
    [InlineData("invalid")]
    [InlineData("1junk")]
    public async Task InvalidOrSingleSelectSizeUsesDropdownSubset(string size)
    {
        using var renderer = Renderer(false);
        var page = await renderer.RenderAsync(Document($"<form><select name=mode size='{size}'><option>x</option></select></form>"),
            new(200, 400, 1), Cancellation);
        Assert.Null(Assert.Single(page.Forms).Error);
        Assert.Equal("select", Assert.Single(page.FormControls).Kind);
    }

    [Theory]
    [InlineData("<form method=GeT enctype=APPLICATION/X-WWW-FORM-URLENCODED target=_SELF accept-charset='utf-8 UTF8'><input name=q></form>")]
    [InlineData("<form method=put enctype=bogus><input name=q></form>")]
    [InlineData("<form action=''><input type=bogus name=q></form>")]
    [InlineData("<form action='  '><input name=q autocomplete=off></form>")]
    public async Task SupportedSpellingsAndInvalidKeywordsUseGetDefaults(string body)
    {
        using var renderer = Renderer(false);
        var page = await renderer.RenderAsync(Document(body), new(200, 400, 1), Cancellation);
        Assert.Equal(new PageForm("https://example.com/final/index.html", null), Assert.Single(page.Forms));
        Assert.Equal("text", Assert.Single(page.FormControls).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnstyledFormControlsUseSupportedDefaultLayout(bool process)
    {
        using var renderer = Renderer(process);
        var document = new LoadedPage(BrowserUrl.Parse("https://example.com/"),
            "<!doctype html><style>body{margin:0}</style><form><input name=q><button name=go value=1>Search</button></form>", 200, []);
        var page = await renderer.RenderAsync(document, new(200, 200, 1), Cancellation);
        Assert.Equal(["text", "button"], page.FormControls.Select(control => control.Kind));
        Assert.Equal("Search", page.FormControls[1].Label);
        Assert.All(page.FormControls, control => Assert.NotNull(control.Rect));
        Assert.True(page.FormControls[1].Rect!.Y > page.FormControls[0].Rect!.Y);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InlineControlsAndLinksKeepTreeTraversalOrder(bool process)
    {
        using var renderer = Renderer(process);
        var page = new LoadedPage(BrowserUrl.Parse("https://example.com/"),
            "<!doctype html><style>*{margin:0}</style><form><input name=a><a href=/next>x</a><input name=b></form>", 200, []);
        var rendered = await renderer.RenderAsync(page, new(360, 100, 1), Cancellation);

        Assert.Equal([0, 1], rendered.FormControls.Select(control => control.BeforeLink));
        Assert.Single(rendered.LinkTargets);
        Assert.True(rendered.FormControls[1].Rect!.X > rendered.LinkTargets[0].Rects[0].X);
        Assert.All(rendered.FormControls, control => Assert.Equal(160, control.Rect!.Width));
        Assert.All(rendered.FormControls, control => Assert.Equal(20, control.Rect!.Height));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OffscreenControlsHaveNoGeometryAndScrolledControlsAreClipped(bool process)
    {
        using var renderer = Renderer(process);
        var page = Document("<div style='height:300px'></div><form><input name=a><input name=b></form><div style='height:300px'></div>");
        var top = await renderer.RenderAsync(page, new(100, 100, 1), Cancellation);
        Assert.All(top.FormControls, control => Assert.Null(control.Rect));
        var scrolled = await renderer.RenderAsync(page with { DocumentId = Guid.NewGuid() }, new(100, 100, 1) { ScrollY = 230 }, Cancellation);
        Assert.Equal(new PageLinkRect(0, 70, 100, 20), scrolled.FormControls[0].Rect);
        Assert.Equal(new PageLinkRect(0, 90, 100, 10), scrolled.FormControls[1].Rect);
    }

    [Theory]
    [InlineData(257, 1, 1)]
    [InlineData(1, 1025, 1)]
    [InlineData(1, 1, 8193)]
    public async Task FormMetadataLimitsFailRendering(int forms, int controls, int nameLength)
    {
        using var renderer = Renderer(false);
        var body = string.Concat(Enumerable.Repeat("<form></form>", forms - 1))
            + "<form>" + string.Concat(Enumerable.Repeat($"<input type=hidden name={new string('n', nameLength)}>", controls)) + "</form>";
        await Assert.ThrowsAsync<PageNavigationException>(() => renderer.RenderAsync(Document(body), new(100, 100, 1), Cancellation));
    }

    [Fact]
    public async Task ExactFormAndControlCountsAreAccepted()
    {
        using var renderer = Renderer(false);
        var body = string.Concat(Enumerable.Repeat("<form></form>", 255))
            + "<form>" + string.Concat(Enumerable.Repeat("<input type=hidden name=n>", 1024)) + "</form>";
        var page = await renderer.RenderAsync(Document(body), new(100, 100, 1), Cancellation);
        Assert.Equal(256, page.Forms.Count);
        Assert.Equal(1024, page.FormControls.Count);
    }

    [Fact]
    public void UrlencodedSerializerMatchesWhatwgByteRules()
    {
        Assert.Equal("a+b=c%2Bd&%C3%A9=%E2%98%83&=&x*-._%7E%21%27%28%29=%25%26%3D%23&lone%EF%BF%BD=%F0%9F%98%80",
            FormSubmission.Serialize([("a b", "c+d"), ("\u00e9", "\u2603"), ("", ""), ("x*-._~!'()", "%&=#"),
                ("lone\uD800", "\U0001F600")]));
        Assert.Equal("", FormSubmission.Serialize([]));
    }

    [Theory]
    [InlineData("https://example.com/s?old=1#frag", "q=1", "https://example.com/s?q=1#frag")]
    [InlineData("https://example.com/s?old=1#frag", "", "https://example.com/s?#frag")]
    [InlineData("https://user:p%3Fw@example.com/s", "q=1", "https://user:p%3Fw@example.com/s?q=1")]
    [InlineData("data:text/html,hi?x#f", "q=1", "data:text/html,hi?q=1#f")]
    [InlineData("file:///tmp/a.html", "q=1", "file:///tmp/a.html?q=1")]
    public void ActionQueryIsReplacedAndFragmentKept(string action, string query, string expected) =>
        Assert.Equal(expected, FormSubmission.ApplyQuery(BrowserUrl.Parse(action), query, 8192).Href);

    [Fact]
    public void SubmissionUrlLengthIsCappedBeforeNavigation()
    {
        var action = BrowserUrl.Parse("https://example.com/s");
        var exact = new string('a', 8192 - "https://example.com/s?".Length);
        Assert.Equal(8192, FormSubmission.ApplyQuery(action, exact, 8192).Href.Length);
        Assert.Throws<BrowserLimitException>(() => FormSubmission.ApplyQuery(action, exact + "a", 8192));
    }

    private sealed class Harness : IDisposable
    {
        internal ControllerTests.Source Source { get; } = new();
        internal BrowserController Controller { get; }
        internal BrowserTab Tab { get; }
        internal BrowserWindowId Window { get; }
        internal PageViewport Viewport { get; } = new(200, 400, 1);
        internal Harness(string body, string encoding = "UTF-8", string url = "https://example.com/final/index.html")
        {
            Controller = new(() => Source, () => new StaticPageRenderer(FontPath, 100000));
            Window = Controller.Session.CreateWindow().Id;
            Tab = Controller.CreateTab(Window);
            Load(Tab, body, encoding, url);
        }
        internal void Load(BrowserTab tab, string body, string encoding = "UTF-8", string url = "https://example.com/final/index.html")
        {
            Controller.Navigate(tab.Id, url);
            Source.Requests[^1].Completion.SetResult(Document(body, url) with { CharacterEncoding = encoding });
            Pump();
            Assert.Null(tab.Error);
        }
        internal void Pump() => Controller.Pump(_ => Viewport);
        public void Dispose() => Controller.Dispose();
    }

    private sealed class LegacyPageSource(string html) : IPageSource
    {
        internal int LoadCount { get; private set; }

        public Task<LoadedPage> LoadAsync(BrowserUrl url, CancellationToken cancellationToken)
        {
            LoadCount++;
            return Task.FromResult(Document(html, url.Href));
        }

        public void Dispose() { }
    }

    private const string SearchForm = """
        <form action='/search?old=1#frag'>
        <input name=q value='x y'>
        <input type=hidden name=_CHARSET_ value=ignored>
        <input type=hidden name='' value=skip>
        <input name=dis value=d disabled>
        <input type=submit name=go value='Go!'>
        <input type=submit name=other value=no>
        <button type=button name=inert>inert</button>
        </form>
        """;

    [Fact]
    public void TypedValuesUseImplicitDefaultButtonAndUrlencodedGetNavigation()
    {
        using var harness = new Harness(SearchForm);
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        controller.FocusControl(tab, 0);
        Assert.Equal(0, controller.FocusedControlIndex(tab));
        Assert.Equal(-1, controller.FocusedLinkIndex(tab));
        Assert.True(controller.EditingFormControl(tab));
        Assert.Equal("x y+\u00e9", controller.InsertFormText(tab, "+\u00e9"));
        Assert.Equal(5, controller.FormControlCaret(tab));
        Assert.True(controller.ActivateFocusedLink(tab, harness.Viewport));
        Assert.Equal("https://example.com/search?q=x+y%2B%C3%A9&_CHARSET_=UTF-8&go=Go%21#frag",
            harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void TextareaAcceptsMultilineTextAndSubmitsNormalizedNewlines()
    {
        using var harness = new Harness("<form action='/message'><textarea name=body rows=3>first</textarea><button>Send</button></form>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;

        Assert.True(controller.FocusControl(tab, 0));
        Assert.True(controller.EditingFormControl(tab));
        Assert.Equal("first\nsecond", controller.InsertFormText(tab, "\nsecond"));
        Assert.False(controller.ActivateFocusedLink(tab, harness.Viewport));
        Assert.True(controller.FocusControl(tab, 1));
        Assert.True(controller.ActivateFocusedLink(tab, harness.Viewport));
        Assert.Equal("https://example.com/message?body=first%0D%0Asecond", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void TelephoneInputCanBeEditedAndSubmitted()
    {
        using var harness = new Harness("<form action='/call'><input type=tel name=phone value='555-0100' maxlength=16></form>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;

        Assert.True(controller.FocusControl(tab, 0));
        Assert.True(controller.EditingFormControl(tab));
        Assert.Equal("555-0100-42", controller.InsertFormText(tab, "-42"));
        Assert.True(controller.ActivateFocusedLink(tab, harness.Viewport));
        Assert.Equal("https://example.com/call?phone=555-0100-42", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void ClickedSubmitterIsTheOnlyButtonEntryAndFieldClickFocuses()
    {
        using var harness = new Harness(SearchForm);
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        var page = controller.Page(tab)!;
        var field = page.FormControls[0].Rect!;
        controller.FocusPage(tab);
        Assert.True(controller.ActivateLink(tab, field.X + 1, field.Y + 1, harness.Viewport));
        Assert.Equal(0, controller.FocusedControlIndex(tab));
        Assert.Single(harness.Source.Requests);
        var other = page.FormControls[5].Rect!;
        Assert.True(controller.ActivateLink(tab, other.X + 1, other.Y + 1, harness.Viewport));
        Assert.Equal("https://example.com/search?q=x+y&_CHARSET_=UTF-8&other=no#frag", harness.Source.Requests[^1].Url.Href);
        var disabled = page.FormControls[3].Rect!;
        Assert.False(controller.ActivateLink(tab, disabled.X + 1, disabled.Y + 1, harness.Viewport));
        Assert.Equal(2, harness.Source.Requests.Count);
    }

    [Fact]
    public void SingleSelectKeyboardSelectionSkipsDisabledOptionsAndSubmitsOrResets()
    {
        using var harness = new Harness("""
            <form action='/choose'>
              <select name=mode required>
                <option value=fast>Fast</option>
                <option value=disabled disabled>Unavailable</option>
                <option value=slow selected>Slow</option>
              </select>
              <button type=reset>Reset</button><button>Choose</button>
            </form>
            """);
        var controller = harness.Controller;
        var tab = harness.Tab.Id;

        Assert.Equal("slow", controller.FormControlValue(tab, 0));
        Assert.True(controller.FocusControl(tab, 0));
        Assert.True(controller.MoveFocusedSelect(tab, -1));
        Assert.Equal(0, controller.SelectedOptionIndex(tab, 0));
        Assert.Equal("fast", controller.FormControlValue(tab, 0));
        Assert.True(controller.FocusControl(tab, 1));
        Assert.True(controller.ActivateFocusedLink(tab, harness.Viewport));
        Assert.Equal("slow", controller.FormControlValue(tab, 0));
        Assert.Single(harness.Source.Requests);
        Assert.True(controller.FocusControl(tab, 0));
        Assert.True(controller.SetFocusedSelectEndpoint(tab, false));
        Assert.Equal("fast", controller.FormControlValue(tab, 0));
        Assert.True(controller.SetFocusedSelectEndpoint(tab, true));
        Assert.Equal(2, controller.SelectedOptionIndex(tab, 0));
        Assert.Equal("slow", controller.FormControlValue(tab, 0));
        Assert.True(controller.FocusControl(tab, 2));
        Assert.True(controller.ActivateFocusedLink(tab, harness.Viewport));
        Assert.Equal("https://example.com/choose?mode=slow", harness.Source.Requests[^1].Url.Href);
    }

    [Theory]
    [InlineData("<select name=mode><option disabled selected>Unavailable</option></select>")]
    [InlineData("<select name=mode></select>")]
    public void DisabledOrMissingSelectOptionIsOmittedFromSubmission(string select)
    {
        using var harness = new Harness($"<form>{select}<button>Go</button></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 1));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id, harness.Viewport));
        Assert.Equal("https://example.com/final/index.html?", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void RequiredSelectWithOnlyDisabledSelectionBlocksSubmission()
    {
        using var harness = new Harness("""
            <form><select name=mode required><option disabled selected>Choose</option></select><button>Go</button></form>
            """);
        Assert.Equal("", harness.Controller.FormControlValue(harness.Tab.Id, 0));
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 1));
        var error = Assert.Throws<PageNavigationException>(() =>
            harness.Controller.ActivateFocusedLink(harness.Tab.Id, harness.Viewport));
        Assert.Contains("required", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(harness.Source.Requests);
    }

    [Fact]
    public void ResetControlActivationRestoresOwnedValuesAndChecksWithoutNavigation()
    {
        using var harness = new Harness("""
            <form action='/send'>
            <input name=text value=base minlength=5>
            <textarea name=message>initial text</textarea>
            <input type=checkbox name=check checked>
            <input type=radio name=choice value=a checked>
            <input type=radio name=choice value=b>
            <input type=range name=level min=1 max=9 step=2 value=5>
            <input type=reset value=Clear>
            <button type=reset>Reset all</button>
            <button type=submit>Send</button>
            </form>
            """);
        var controller = harness.Controller;
        var tab = harness.Tab.Id;

        Assert.Equal(["text", "textarea", "checkbox", "radio", "radio", "range", "reset", "reset", "button"],
            controller.Page(tab)!.FormControls.Select(control => control.Kind));
        Assert.True(controller.FocusControl(tab, 0));
        controller.SelectAllFormControl(tab);
        Assert.Equal("x", controller.InsertFormText(tab, "x"));
        Assert.True(controller.FocusControl(tab, 1));
        Assert.Equal("initial text changed", controller.InsertFormText(tab, " changed"));
        Assert.True(controller.FocusControl(tab, 2));
        Assert.True(controller.ToggleFocusedCheckable(tab));
        Assert.True(controller.FocusControl(tab, 4));
        Assert.True(controller.ToggleFocusedCheckable(tab));
        Assert.True(controller.FocusControl(tab, 5));
        Assert.True(controller.AdjustFocusedRange(tab, 1));
        Assert.Equal("7", controller.FormControlValue(tab, 5));

        Assert.True(controller.FocusControl(tab, 6));
        Assert.True(controller.ActivateFocusedLink(tab, harness.Viewport));

        Assert.Equal(6, controller.FocusedControlIndex(tab));
        Assert.Equal("base", controller.FormControlValue(tab, 0));
        Assert.Equal("initial text", controller.FormControlValue(tab, 1));
        Assert.True(controller.FormControlChecked(tab, 2));
        Assert.True(controller.FormControlChecked(tab, 3));
        Assert.False(controller.FormControlChecked(tab, 4));
        Assert.Equal("5", controller.FormControlValue(tab, 5));
        Assert.Single(harness.Source.Requests);

        var resetButton = controller.Page(tab)!.FormControls[7].Rect!;
        Assert.True(controller.ActivateLink(tab, resetButton.X + 1, resetButton.Y + 1, harness.Viewport));
        Assert.Equal(7, controller.FocusedControlIndex(tab));
        Assert.Equal("base", controller.FormControlValue(tab, 0));
        Assert.Equal("initial text", controller.FormControlValue(tab, 1));
        Assert.Equal("5", controller.FormControlValue(tab, 5));
        Assert.Single(harness.Source.Requests);

        Assert.True(controller.FocusControl(tab, 8));
        Assert.True(controller.ActivateFocusedLink(tab, harness.Viewport));
        Assert.Equal("https://example.com/send?text=base&message=initial+text&check=on&choice=a&level=5",
            harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void TextFieldEnterUsesSubmitterAndNeverActivatesResetControl()
    {
        using var harness = new Harness("<form action='/send'><input name=q value=before><input type=reset><button type=submit>Send</button></form>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        Assert.True(controller.FocusControl(tab, 0));
        Assert.Equal("before-edited", controller.InsertFormText(tab, "-edited"));
        Assert.True(controller.ActivateFocusedLink(tab, harness.Viewport));
        Assert.Equal("https://example.com/send?q=before-edited",
            harness.Source.Requests[^1].Url.Href);
    }


    [Theory]
    [InlineData("<form><input name=a value=1></form>", true, "https://example.com/final/index.html?a=1")]
    [InlineData("<form><input name=a><input type=search name=b></form>", false, "")]
    [InlineData("<form><input name=a><input type=submit disabled><input type=submit name=s></form>", false, "")]
    [InlineData("<form><input name=a><button name=b value=v>go</button></form>", true, "https://example.com/final/index.html?a=&b=v")]
    [InlineData("<form><input name=a value='l1&#10;'><input type=hidden name='h&#13;' value='x&#10;y&#13;&#10;z'></form>", true,
        "https://example.com/final/index.html?a=l1&h%0D%0A=x%0D%0Ay%0D%0Az")]
    [InlineData("<input name=a value=formless>", false, "")]
    [InlineData("<form><input name=a readonly value=r></form>", true, "https://example.com/final/index.html?a=r")]
    public void ImplicitSubmissionFollowsDefaultButtonAndBlockingFieldRules(string body, bool submits, string expected)
    {
        using var harness = new Harness(body);
        harness.Controller.FocusControl(harness.Tab.Id, 0);
        Assert.Equal(submits, harness.Controller.ActivateFocusedLink(harness.Tab.Id, harness.Viewport));
        Assert.Equal(submits ? 2 : 1, harness.Source.Requests.Count);
        if (submits) { Assert.Equal(expected, harness.Source.Requests[^1].Url.Href); }
    }

    [Fact]
    public void EmptyEntryListStillReplacesQuery()
    {
        using var harness = new Harness("<form action='?x=1'><input type=submit></form>");
        harness.Controller.FocusControl(harness.Tab.Id, 0);
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?", harness.Source.Requests[^1].Url.Href);
    }

    [Theory]
    [InlineData("<form method=post><input name=q></form>", "method")]
    [InlineData("<form target=_blank><input name=q></form>", "target")]
    [InlineData("<form enctype=multipart/form-data><input name=q></form>", "enctype")]
    [InlineData("<form novalidate><input name=q></form>", "novalidate")]
    [InlineData("<form><input name=q required></form>", "required")]
    [InlineData("<form action='http://[bad'><input name=q></form>", "action")]
    public void UnsupportedOrInvalidFormsFailVisiblyWithoutNavigation(string body, string expected)
    {
        using var harness = new Harness(body);
        var page = harness.Controller.Page(harness.Tab.Id);
        harness.Controller.FocusControl(harness.Tab.Id, 0);
        var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(harness.Source.Requests);
        Assert.Same(page, harness.Controller.Page(harness.Tab.Id));
    }

    [Fact]
    public void PageInitiatedDataFormNavigationIsBlockedTransactionally()
    {
        using var harness = new Harness("<form action='data:text/html,untrusted'><input type=submit value=Go></form>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        var committed = controller.Page(tab)!;
        controller.FocusPage(tab);
        Assert.True(controller.FocusControl(tab, 0));

        var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab));

        Assert.Contains("Page-initiated data URL navigation is blocked", error.Message, StringComparison.Ordinal);
        Assert.Single(harness.Source.Requests);
        Assert.Same(committed, controller.Page(tab));
        Assert.Equal("https://example.com/final/index.html", harness.Tab.History.Current!.Href);
        Assert.Null(harness.Tab.Error);
    }

    [Theory]
    [InlineData("<a href='file:///etc/passwd'>local</a>", false)]
    [InlineData("<form action='file:///etc/passwd'><input type=submit value=Go></form>", true)]
    public void PageInitiatedFileNavigationIsBlockedTransactionally(string body, bool form)
    {
        using var harness = new Harness(body);
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        var committed = controller.Page(tab)!;
        controller.FocusPage(tab);
        if (form)
        {
            Assert.True(controller.FocusControl(tab, 0));
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab));
            Assert.Contains("Page-initiated file navigation is blocked", error.Message, StringComparison.Ordinal);
        }
        else
        {
            var rect = Assert.Single(committed.LinkTargets).Rects[0];
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateLink(tab,
                rect.X + rect.Width / 2, rect.Y + rect.Height / 2, harness.Viewport));
            Assert.Contains("Page-initiated file navigation is blocked", error.Message, StringComparison.Ordinal);
        }
        Assert.Single(harness.Source.Requests);
        Assert.Same(committed, controller.Page(tab));
        Assert.Null(harness.Tab.Error);
    }

    [Theory]
    [InlineData("<a href='https://outside.example/visit'>external</a>", false)]
    [InlineData("<form action='https://outside.example/search'><input name=q value=private><input type=submit></form>", true)]
    public void LocalFileDocumentsCannotInitiateNetworkNavigations(string body, bool form)
    {
        using var harness = new Harness(body, url: "file:///tmp/local-page.html");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        var committed = controller.Page(tab)!;
        controller.FocusPage(tab);
        if (form)
        {
            Assert.True(controller.FocusControl(tab, 1));
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab));
            Assert.Contains("network navigation is blocked from local file documents", error.Message, StringComparison.Ordinal);
        }
        else
        {
            var rect = Assert.Single(committed.LinkTargets).Rects[0];
            var error = Assert.Throws<PageNavigationException>(() => controller.ActivateLink(tab,
                rect.X + rect.Width / 2, rect.Y + rect.Height / 2, harness.Viewport));
            Assert.Contains("network navigation is blocked from local file documents", error.Message, StringComparison.Ordinal);
        }
        Assert.Single(harness.Source.Requests);
        Assert.Same(committed, controller.Page(tab));
        Assert.Equal("file:///tmp/local-page.html", harness.Tab.History.Current!.Href);
        Assert.Null(harness.Tab.Error);
    }

    [Fact]
    public void SecurePageCannotSubmitFormDataToHttpBeforeRequest()
    {
        var requests = new List<Uri>();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Style + "<form action='http://outside.example/search'><input name=q value=private>"
                    + "<input type=submit></form>", System.Text.Encoding.UTF8, "text/html")
            };
        })), () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(200, 400, 1);
        controller.Navigate(tab.Id, "https://secure.example/secure");
        PumpUntilComplete();
        Assert.Null(tab.Error);
        var committed = controller.Page(tab.Id);
        controller.FocusPage(tab.Id);
        Assert.True(controller.FocusControl(tab.Id, 1));
        var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab.Id));

        Assert.Contains("Cross-origin form submissions are blocked", error.Message, StringComparison.Ordinal);
        Assert.Equal(["https://secure.example/secure"], requests.Select(request => request.AbsoluteUri));
        Assert.Same(committed, controller.Page(tab.Id));
        Assert.Equal("https://secure.example/secure", tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void PumpUntilComplete()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (tab.IsLoading && DateTime.UtcNow < deadline)
            {
                controller.Pump(_ => viewport);
                Thread.Sleep(5);
            }
            controller.Pump(_ => viewport);
            Assert.False(tab.IsLoading);
        }
    }

    [Fact]
    public void OpaqueDataDocumentCannotSubmitFormValuesOverHttp()
    {
        var requests = new List<Uri>();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent("<!doctype html>", System.Text.Encoding.UTF8, "text/html")
            };
        })), () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(200, 400, 1);
        var html = Style + "<form action='http://outside.example/search'><input name=q value=private>"
            + "<input type=submit></form>";
        var dataUrl = "data:text/html," + Uri.EscapeDataString(html);
        controller.Navigate(tab.Id, dataUrl);
        PumpUntilComplete();
        Assert.Null(tab.Error);
        var committed = controller.Page(tab.Id);
        controller.FocusPage(tab.Id);
        Assert.True(controller.FocusControl(tab.Id, 1));
        var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab.Id));

        Assert.Contains("Network form submissions from opaque documents are blocked", error.Message, StringComparison.Ordinal);
        Assert.Null(tab.Error);
        Assert.Empty(requests);
        Assert.Same(committed, controller.Page(tab.Id));
        Assert.Equal(dataUrl, tab.History.Current!.Href);

        void PumpUntilComplete()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (tab.IsLoading && DateTime.UtcNow < deadline)
            {
                controller.Pump(_ => viewport);
                Thread.Sleep(5);
            }
            controller.Pump(_ => viewport);
            Assert.False(tab.IsLoading);
        }
    }

    [Fact]
    public void SecurePageCannotSubmitFormValuesToCrossOriginHttps()
    {
        var requests = new List<Uri>();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(Style + "<form action='https://outside.example/search'><input name=q value=private>"
                    + "<input type=submit></form>", System.Text.Encoding.UTF8, "text/html")
            };
        })), () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(200, 400, 1);
        controller.Navigate(tab.Id, "https://secure.example/secure");
        PumpUntilComplete();
        Assert.Null(tab.Error);
        var committed = controller.Page(tab.Id);
        controller.FocusPage(tab.Id);
        Assert.True(controller.FocusControl(tab.Id, 1));

        var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab.Id));

        Assert.Contains("Cross-origin form submissions are blocked", error.Message, StringComparison.Ordinal);
        Assert.Equal(["https://secure.example/secure"], requests.Select(request => request.AbsoluteUri));
        Assert.Same(committed, controller.Page(tab.Id));
        Assert.Equal("https://secure.example/secure", tab.History.Current!.Href);
        Assert.Null(tab.Error);

        void PumpUntilComplete()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (tab.IsLoading && DateTime.UtcNow < deadline)
            {
                controller.Pump(_ => viewport);
                Thread.Sleep(5);
            }
            controller.Pump(_ => viewport);
            Assert.False(tab.IsLoading);
        }
    }

    [Fact]
    public void OpaqueDataDocumentCannotSubmitFormValuesToHttps()
    {
        using var harness = new Harness("<form action='https://outside.example/search'><input name=q value=private><input type=submit></form>",
            url: "data:text/html,opaque");
        var committed = harness.Controller.Page(harness.Tab.Id);
        harness.Controller.FocusPage(harness.Tab.Id);
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 1));

        var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));

        Assert.Contains("Network form submissions from opaque documents are blocked", error.Message, StringComparison.Ordinal);
        Assert.Single(harness.Source.Requests);
        Assert.Same(committed, harness.Controller.Page(harness.Tab.Id));
        Assert.Equal("data:text/html,opaque", harness.Tab.History.Current!.Href);
        Assert.Null(harness.Tab.Error);
    }

    [Fact]
    public void LegacyPageSourceFailsClosedForFixedOriginFormNavigation()
    {
        var source = new LegacyPageSource(
            "<form action='/submit'><input name=q value=private><input type=submit></form>");
        using var controller = new BrowserController(() => source, () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(200, 400, 1);
        controller.Navigate(tab.Id, "https://secure.example/secure");
        controller.Pump(_ => viewport);
        Assert.Null(tab.Error);
        var committed = controller.Page(tab.Id);
        controller.FocusPage(tab.Id);
        Assert.True(controller.FocusControl(tab.Id, 1));

        var error = Assert.Throws<PageNavigationException>(() => controller.ActivateFocusedLink(tab.Id));

        Assert.Contains("does not enforce fixed-origin redirects", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, source.LoadCount);
        Assert.Same(committed, controller.Page(tab.Id));
        Assert.Equal("https://secure.example/secure", tab.History.Current!.Href);
        Assert.False(tab.IsLoading);
    }

    [Fact]
    public void SameOriginFormCannotRedirectSubmissionToAnotherOrigin()
    {
        var requests = new List<Uri>();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            if (request.RequestUri!.AbsolutePath == "/secure")
            {
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(Style + "<form action='/submit'><input name=q value=private>"
                        + "<input type=submit></form>", System.Text.Encoding.UTF8, "text/html")
                };
            }
            if (request.RequestUri.AbsolutePath == "/submit")
            {
                return new(HttpStatusCode.Found)
                { Headers = { Location = new Uri("https://outside.example/leak?q=private") } };
            }
            return new(HttpStatusCode.OK)
            { Content = new StringContent("<!doctype html>", System.Text.Encoding.UTF8, "text/html") };
        })), () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(200, 400, 1);
        controller.Navigate(tab.Id, "https://secure.example/secure");
        PumpUntilComplete();
        Assert.Null(tab.Error);
        var committed = controller.Page(tab.Id);
        controller.FocusPage(tab.Id);
        Assert.True(controller.FocusControl(tab.Id, 1));

        Assert.True(controller.ActivateFocusedLink(tab.Id));
        PumpUntilComplete();

        Assert.Contains("same origin", tab.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["https://secure.example/secure", "https://secure.example/submit?q=private"],
            requests.Select(request => request.AbsoluteUri));
        Assert.Same(committed, controller.Page(tab.Id));
        Assert.Equal("https://secure.example/secure", tab.History.Current!.Href);

        void PumpUntilComplete()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (tab.IsLoading && DateTime.UtcNow < deadline)
            {
                controller.Pump(_ => viewport);
                Thread.Sleep(5);
            }
            controller.Pump(_ => viewport);
            Assert.False(tab.IsLoading);
        }
    }

    [Fact]
    public void SameOriginFormMayFollowSameOriginRedirect()
    {
        var requests = new List<Uri>();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            if (request.RequestUri!.AbsolutePath == "/secure")
            {
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(Style + "<form action='/submit'><input name=q value=private>"
                        + "<input type=submit></form>", System.Text.Encoding.UTF8, "text/html")
                };
            }
            if (request.RequestUri.AbsolutePath == "/submit")
            {
                return new(HttpStatusCode.Found)
                { Headers = { Location = new Uri("https://secure.example/final?q=private") } };
            }
            return new(HttpStatusCode.OK)
            { Content = new StringContent("<!doctype html>", System.Text.Encoding.UTF8, "text/html") };
        })), () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(200, 400, 1);
        controller.Navigate(tab.Id, "https://secure.example/secure");
        PumpUntilComplete();
        Assert.Null(tab.Error);
        controller.FocusPage(tab.Id);
        Assert.True(controller.FocusControl(tab.Id, 1));

        Assert.True(controller.ActivateFocusedLink(tab.Id));
        PumpUntilComplete();

        Assert.Null(tab.Error);
        Assert.Equal("https://secure.example/final?q=private", tab.History.Current!.Href);
        Assert.Equal(["https://secure.example/secure", "https://secure.example/submit?q=private",
            "https://secure.example/final?q=private"], requests.Select(request => request.AbsoluteUri));

        void PumpUntilComplete()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (tab.IsLoading && DateTime.UtcNow < deadline)
            {
                controller.Pump(_ => viewport);
                Thread.Sleep(5);
            }
            controller.Pump(_ => viewport);
            Assert.False(tab.IsLoading);
        }
    }

    [Fact]
    public void SecureFormCannotFollowHttpDowngradeRedirect()
    {
        var requests = new List<Uri>();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request.RequestUri!);
            if (request.RequestUri!.AbsolutePath == "/secure")
            {
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(Style + "<form action='https://secure.example/search'><input name=q value=private>"
                        + "<input type=submit></form>", System.Text.Encoding.UTF8, "text/html")
                };
            }
            return new(HttpStatusCode.Found)
            { Headers = { Location = new Uri("http://secure.example/leak?q=private") } };
        })), () => new StaticPageRenderer(FontPath, 100000));
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(200, 400, 1);
        controller.Navigate(tab.Id, "https://secure.example/secure");
        PumpUntilComplete();
        Assert.Null(tab.Error);
        controller.FocusPage(tab.Id);
        Assert.True(controller.FocusControl(tab.Id, 1));
        Assert.True(controller.ActivateFocusedLink(tab.Id));
        PumpUntilComplete();

        Assert.Contains("Secure transport policy blocks HTTP loads", tab.Error, StringComparison.Ordinal);
        Assert.Equal(["https://secure.example/secure", "https://secure.example/search?q=private"],
            requests.Select(request => request.AbsoluteUri));
        Assert.Equal("https://secure.example/secure", tab.History.Current!.Href);

        void PumpUntilComplete()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (tab.IsLoading && DateTime.UtcNow < deadline)
            {
                controller.Pump(_ => viewport);
                Thread.Sleep(5);
            }
            controller.Pump(_ => viewport);
            Assert.False(tab.IsLoading);
        }
    }

    [Fact]
    public void UserAddressNavigationStillAllowsExplicitFileUrl()
    {
        using var harness = new Harness("<p>initial</p>");
        harness.Controller.Navigate(harness.Tab.Id, "file:///etc/passwd");
        Assert.Equal("file:///etc/passwd", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void NonUtf8DocumentEncodingIsRejected()
    {
        using var harness = new Harness("<form><input name=q></form>", "windows-1252");
        harness.Controller.FocusControl(harness.Tab.Id, 0);
        var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Contains("windows-1252", error.Message, StringComparison.Ordinal);
        Assert.Single(harness.Source.Requests);
    }

    [Theory]
    [InlineData("UTF-16LE")]
    [InlineData("UTF-16BE")]
    [InlineData("replacement")]
    public void Utf16AndReplacementDocumentsSubmitAsUtf8(string encoding)
    {
        using var harness = new Harness("<form><input name=q value='\u00e9'></form>", encoding);
        harness.Controller.FocusControl(harness.Tab.Id, 0);
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?q=%C3%A9", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void TextareaVerticalCaretMovementPreservesColumnAcrossShortLines()
    {
        using var harness = new Harness("<textarea name=body>12345\nx\n12345</textarea>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        controller.FocusControl(tab, 0);

        controller.EditFormControl(tab, FormEdit.Up);
        Assert.Equal(7, controller.FormControlCaret(tab));
        controller.EditFormControl(tab, FormEdit.Up);
        Assert.Equal(5, controller.FormControlCaret(tab));
        controller.EditFormControl(tab, FormEdit.Down);
        Assert.Equal(7, controller.FormControlCaret(tab));
        controller.EditFormControl(tab, FormEdit.Down);
        Assert.Equal(13, controller.FormControlCaret(tab));
        controller.EditFormControl(tab, FormEdit.Down);
        Assert.Equal(13, controller.FormControlCaret(tab));
    }

    [Fact]
    public void TextareaWheelScrollIsBoundedAndIndependentFromDocumentScroll()
    {
        using var harness = new Harness("<form><textarea name=body>one\ntwo\nthree\nfour\nfive</textarea></form>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        var rect = controller.Page(tab)!.FormControls[0].Rect!;

        Assert.Equal(0, controller.TextareaFirstLine(tab, 0));
        Assert.True(controller.ScrollTextareaAt(tab, rect.X + 2, rect.Y + 2, 3));
        Assert.Equal(3, controller.TextareaFirstLine(tab, 0));
        Assert.Equal(0, controller.ScrollY(tab));
        Assert.True(controller.ScrollTextareaAt(tab, rect.X + 2, rect.Y + 2, 300));
        Assert.Equal(3, controller.TextareaFirstLine(tab, 0));
        Assert.False(controller.ScrollTextareaAt(tab, rect.X + rect.Width + 1, rect.Y + 2, 1));
        Assert.True(controller.ScrollTextareaAt(tab, rect.X + 2, rect.Y + 2, -300));
        Assert.Equal(0, controller.TextareaFirstLine(tab, 0));
    }

    [Fact]
    public void TextareaHomeAndEndMoveWithinCurrentLine()
    {
        using var harness = new Harness("<textarea name=body>12345\nx\n12345</textarea>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        controller.FocusControl(tab, 0);

        controller.EditFormControl(tab, FormEdit.Home);
        Assert.Equal(8, controller.FormControlCaret(tab));
        controller.EditFormControl(tab, FormEdit.Up);
        Assert.Equal(6, controller.FormControlCaret(tab));
        controller.EditFormControl(tab, FormEdit.End);
        Assert.Equal(7, controller.FormControlCaret(tab));
        controller.EditFormControl(tab, FormEdit.Down);
        Assert.Equal(9, controller.FormControlCaret(tab));
    }

    [Fact]
    public void MaxLengthTruncatesInsertionAndOnlyDirtyOverlongValuesBlock()
    {
        using var harness = new Harness("<form><input name=a maxlength=3><input name=b value=abcdef maxlength=3></form>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        controller.FocusControl(tab, 0);
        Assert.Equal("abc", controller.InsertFormText(tab, "abcdef"));
        Assert.Equal("abc", controller.InsertFormText(tab, "d"));
        controller.FocusControl(tab, 0);
        controller.EditFormControl(tab, FormEdit.Home);
        Assert.Equal("abc", controller.InsertFormText(tab, "\U0001F600"));
        controller.FocusControl(tab, 1);
        Assert.Equal("abcdef", controller.FormControlValue(tab, 1));
        Assert.False(controller.ActivateFocusedLink(tab));
        Assert.Single(harness.Source.Requests);
        var clean = new Harness("<form><input name=b value=abcdef maxlength=3><input type=submit></form>");
        using (clean)
        {
            clean.Controller.FocusControl(clean.Tab.Id, 0);
            Assert.True(clean.Controller.ActivateFocusedLink(clean.Tab.Id));
            Assert.Equal("https://example.com/final/index.html?b=abcdef", clean.Source.Requests[^1].Url.Href);
            clean.Controller.FocusControl(clean.Tab.Id, 0);
            Assert.Equal("abcde", clean.Controller.EditFormControl(clean.Tab.Id, FormEdit.Backspace));
            var error = Assert.Throws<PageNavigationException>(() => clean.Controller.ActivateFocusedLink(clean.Tab.Id));
            Assert.Contains("maxlength", error.Message, StringComparison.Ordinal);
            Assert.Equal(2, clean.Source.Requests.Count);
        }
    }

    [Fact]
    public void MinLengthRejectsEditedShortValuesButAllowsUntouchedInitialValues()
    {
        using var clean = new Harness("<form><input name=q value=ab minlength=3></form>");
        Assert.True(clean.Controller.FocusControl(clean.Tab.Id, 0));
        Assert.True(clean.Controller.ActivateFocusedLink(clean.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?q=ab", clean.Source.Requests[^1].Url.Href);

        using var edited = new Harness("<form><input name=q minlength=3></form>");
        Assert.True(edited.Controller.FocusControl(edited.Tab.Id, 0));
        edited.Controller.InsertFormText(edited.Tab.Id, "ab");
        var error = Assert.Throws<PageNavigationException>(() => edited.Controller.ActivateFocusedLink(edited.Tab.Id));
        Assert.Contains("minlength 3", error.Message, StringComparison.Ordinal);
        Assert.Single(edited.Source.Requests);

        using var valid = new Harness("<form><input name=q minlength=3><button type=submit>go</button></form>");
        Assert.True(valid.Controller.FocusControl(valid.Tab.Id, 0));
        valid.Controller.InsertFormText(valid.Tab.Id, "abc");
        Assert.True(valid.Controller.FocusControl(valid.Tab.Id, 1));
        Assert.True(valid.Controller.ActivateFocusedLink(valid.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?q=abc", valid.Source.Requests[^1].Url.Href);

        using var textarea = new Harness("<form><textarea name=q minlength=3></textarea><button type=submit>go</button></form>");
        Assert.True(textarea.Controller.FocusControl(textarea.Tab.Id, 0));
        Assert.Equal(3, textarea.Controller.Page(textarea.Tab.Id)!.FormControls[0].MinLength);
        textarea.Controller.InsertFormText(textarea.Tab.Id, "ab");
        Assert.True(textarea.Controller.FocusControl(textarea.Tab.Id, 1));
        Assert.Throws<PageNavigationException>(() => textarea.Controller.ActivateFocusedLink(textarea.Tab.Id));
        Assert.Single(textarea.Source.Requests);
    }

    [Fact]
    public void UncheckedRadioIsOmittedAndMissingValueDefaultsToOn()
    {
        using var harness = new Harness("<form><input type=radio name=mode><button type=submit>go</button></form>");
        var radio = harness.Controller.Page(harness.Tab.Id)!.FormControls[0].Rect!;
        Assert.True(harness.Controller.ActivateLink(harness.Tab.Id, radio.X + 1, radio.Y + 1));
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 1));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.EndsWith("?mode=on", harness.Source.Requests[^1].Url.Href, StringComparison.Ordinal);
    }

    [Fact]
    public void RadioActivationSelectsOnlyOneGroupValueForSubmission()
    {
        using var harness = new Harness("<form><input type=radio name=mode value=a checked><input type=radio name=mode value=b><input type=radio name=other value=c><button type=submit>go</button></form>");
        Assert.True(harness.Controller.FormControlChecked(harness.Tab.Id, 0));
        var second = harness.Controller.Page(harness.Tab.Id)!.FormControls[1].Rect!;
        Assert.True(harness.Controller.ActivateLink(harness.Tab.Id, second.X + 1, second.Y + 1));
        Assert.False(harness.Controller.FormControlChecked(harness.Tab.Id, 0));
        Assert.True(harness.Controller.FormControlChecked(harness.Tab.Id, 1));
        Assert.False(harness.Controller.FormControlChecked(harness.Tab.Id, 2));
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 3));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?mode=b", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void CheckedMemberSatisfiesRequiredRadioGroup()
    {
        using var harness = new Harness("<form><input type=radio name=mode value=a required><input type=radio name=mode value=b checked><button type=submit>go</button></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 2));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.EndsWith("?mode=b", harness.Source.Requests[^1].Url.Href, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiredRadioGroupNeedsASelectedMember()
    {
        using var harness = new Harness("<form><input type=radio name=mode value=a required><input type=radio name=mode value=b><button type=submit>go</button></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 2));
        var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Contains("required", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(harness.Source.Requests);
        var radio = harness.Controller.Page(harness.Tab.Id)!.FormControls[1].Rect!;
        Assert.True(harness.Controller.ActivateLink(harness.Tab.Id, radio.X + 1, radio.Y + 1));
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 2));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.EndsWith("?mode=b", harness.Source.Requests[^1].Url.Href, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckboxPointerActivationTogglesSuccessfulFormEntries()
    {
        using var harness = new Harness("<form><input type=checkbox name=choice value=yes checked><input type=checkbox name=choice value=no><button type=submit>go</button></form>");
        Assert.True(harness.Controller.FormControlChecked(harness.Tab.Id, 0));
        Assert.False(harness.Controller.FormControlChecked(harness.Tab.Id, 1));
        var first = harness.Controller.Page(harness.Tab.Id)!.FormControls[0].Rect!;
        var second = harness.Controller.Page(harness.Tab.Id)!.FormControls[1].Rect!;
        Assert.True(harness.Controller.ActivateLink(harness.Tab.Id, first.X + 1, first.Y + 1));
        Assert.False(harness.Controller.FormControlChecked(harness.Tab.Id, 0));
        Assert.True(harness.Controller.ActivateLink(harness.Tab.Id, second.X + 1, second.Y + 1));
        Assert.True(harness.Controller.FormControlChecked(harness.Tab.Id, 1));
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 2));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?choice=no", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void RequiredCheckboxBlocksSubmissionUntilChecked()
    {
        using var harness = new Harness("<form><input type=checkbox name=agree required><button type=submit>go</button></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 1));
        var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Contains("required", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(harness.Source.Requests);
        var checkbox = harness.Controller.Page(harness.Tab.Id)!.FormControls[0].Rect!;
        Assert.True(harness.Controller.ActivateLink(harness.Tab.Id, checkbox.X + 1, checkbox.Y + 1));
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 1));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.EndsWith("?agree=on", harness.Source.Requests[^1].Url.Href, StringComparison.Ordinal);
    }

    [Fact]
    public void UncheckedCheckboxIsOmittedFromSubmission()
    {
        using var harness = new Harness("<form><input type=checkbox name=agree><button type=submit>go</button></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 1));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void SpaceStyleToggleChangesFocusedCheckboxState()
    {
        using var harness = new Harness("<form><input type=checkbox name=agree></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        Assert.True(harness.Controller.ToggleFocusedCheckable(harness.Tab.Id));
        Assert.True(harness.Controller.FormControlChecked(harness.Tab.Id, 0));
        Assert.True(harness.Controller.ToggleFocusedCheckable(harness.Tab.Id));
        Assert.False(harness.Controller.FormControlChecked(harness.Tab.Id, 0));
    }

    [Fact]
    public void UncheckedCheckboxIsOmittedAndMissingValueDefaultsToOn()
    {
        using var harness = new Harness("<form><input type=checkbox name=agree><button type=submit>go</button></form>");
        var checkbox = harness.Controller.Page(harness.Tab.Id)!.FormControls[0].Rect!;
        Assert.True(harness.Controller.ActivateLink(harness.Tab.Id, checkbox.X + 1, checkbox.Y + 1));
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 1));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?agree=on", harness.Source.Requests[^1].Url.Href);
    }

    [Theory]
    [InlineData("https://example.com/path", true)]
    [InlineData("mailto:user@example.com", true)]
    [InlineData("/relative/path", false)]
    [InlineData("not a url", false)]
    public void UrlInputValidatesAbsoluteUrlBeforeSubmitting(string value, bool valid)
    {
        using var harness = new Harness($"<form><input type=url name=site value=\"{value}\"></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        if (valid)
        {
            Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
            Assert.Equal("https://example.com/final/index.html?site=" + Uri.EscapeDataString(value), harness.Source.Requests[^1].Url.Href);
        }
        else
        {
            var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
            Assert.Contains("valid absolute URL", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(harness.Source.Requests);
        }
    }

    [Fact]
    public void EmptyOptionalUrlInputCanSubmit()
    {
        using var harness = new Harness("<form><input type=url name=site></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?site=", harness.Source.Requests[^1].Url.Href);
    }

    [Theory]
    [InlineData("5", true)]
    [InlineData("-2.5", true)]
    [InlineData("1e1", true)]
    [InlineData("", true)]
    [InlineData("1.2.3", false)]
    [InlineData("5.", false)]
    [InlineData("+5", false)]
    [InlineData("Infinity", false)]
    [InlineData(" 5", false)]
    [InlineData("5 ", false)]
    public void NumberInputValidatesNumberAndRangeBeforeSubmission(string value, bool valid)
    {
        using var harness = new Harness($"<form><input type=number name=count value=\"{value}\" min=-10 max=10 step=0.5></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        if (valid)
        {
            Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
            var expected = value.Length == 0 ? "" : Uri.EscapeDataString(value);
            Assert.EndsWith("?count=" + expected, harness.Source.Requests[^1].Url.Href, StringComparison.Ordinal);
        }
        else
        {
            var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
            Assert.Contains("number", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(harness.Source.Requests);
        }
    }

    [Theory]
    [InlineData("-11", "minimum")]
    [InlineData("11", "maximum")]
    public void NumberInputEnforcesRangeBounds(string value, string expected)
    {
        using var harness = new Harness($"<form><input type=number name=n value={value} min=-10 max=10></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(harness.Source.Requests);
    }

    [Theory]
    [InlineData("0.3", "min=0 step=0.1", true)]
    [InlineData("0.31", "min=0 step=0.1", false)]
    [InlineData("3", "min=1 step=2", true)]
    [InlineData("4", "min=1 step=2", false)]
    [InlineData("0.3", "step=any", true)]
    [InlineData("2.5", "min=0 step=0", false)]
    public void NumberInputValidatesStepGrid(string value, string attributes, bool valid)
    {
        using var harness = new Harness($"<form><input type=number name=n value=\"{value}\" {attributes}></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        if (valid)
        {
            Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
            Assert.EndsWith("?n=" + Uri.EscapeDataString(value), harness.Source.Requests[^1].Url.Href, StringComparison.Ordinal);
        }
        else
        {
            var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
            Assert.Contains("step", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(harness.Source.Requests);
        }
    }

    [Theory]
    [InlineData("min=10 max=1", "minimum")]
    [InlineData("multiple", "multiple")]
    public async Task UnsupportedNumberInputConstraintsAreVisible(string attributes, string expected)
    {
        using var renderer = Renderer(false);
        var page = await renderer.RenderAsync(Document($"<form><input type=number name=n {attributes}></form>"),
            new(200, 400, 1), Cancellation);
        Assert.Contains(expected, Assert.Single(page.Forms).Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("person@example.com", true)]
    [InlineData("first.last+tag@example.co.uk", true)]
    [InlineData("person@localhost", true)]
    [InlineData("person@-example.com", false)]
    [InlineData("person@example..com", false)]
    [InlineData("person@example.com,other@example.com", false)]
    [InlineData("person", false)]
    public void EmailInputValidatesSingleAddressBeforeSubmitting(string value, bool valid)
    {
        using var harness = new Harness($"<form><input type=email name=contact value=\"{value}\"></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        if (valid)
        {
            Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
            Assert.Equal("https://example.com/final/index.html?contact=" + Uri.EscapeDataString(value), harness.Source.Requests[^1].Url.Href);
        }
        else
        {
            var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
            Assert.Contains("valid email address", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(harness.Source.Requests);
        }
    }

    [Fact]
    public void EmailMultipleAcceptsAddressListsAndSubmitsSanitizedValue()
    {
        using var harness = new Harness("<form><input type=email name=contact value=\" first@example.com , second@example.org \" multiple></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        var value = Uri.EscapeDataString("first@example.com,second@example.org");
        Assert.Equal("https://example.com/final/index.html?contact=" + value, harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void EmptyOptionalEmailMultipleCanSubmit()
    {
        using var harness = new Harness("<form><input type=email name=contact multiple></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?contact=", harness.Source.Requests[^1].Url.Href);
    }

    [Theory]
    [InlineData("first@example.com,")]
    [InlineData("first@example.com,,second@example.org")]
    [InlineData("first@example.com,not-an-address")]
    public void EmailMultipleRejectsListsWithInvalidItems(string value)
    {
        using var harness = new Harness($"<form><input type=email name=contact value=\"{value}\" multiple></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Contains("valid email address list", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(harness.Source.Requests);
    }

    [Fact]
    public void RequiredEmailMultipleRejectsAnEmptyList()
    {
        using var harness = new Harness("<form><input type=email name=contact multiple required></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        var error = Assert.Throws<PageNavigationException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Contains("required", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(harness.Source.Requests);
    }

    [Fact]
    public void InputPlaceholderDoesNotBecomeSubmittedValue()
    {
        using var harness = new Harness("<form><input name=q placeholder=Search></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        Assert.Equal("", harness.Controller.FormControlValue(harness.Tab.Id, 0));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?q=", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void EmptyOptionalEmailInputCanSubmit()
    {
        using var harness = new Harness("<form><input type=email name=contact></form>");
        Assert.True(harness.Controller.FocusControl(harness.Tab.Id, 0));
        Assert.True(harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?contact=", harness.Source.Requests[^1].Url.Href);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReversedRangeInputBoundsAreVisibleAcrossRenderers(bool process)
    {
        using var renderer = Renderer(process);
        var page = await renderer.RenderAsync(Document("<form><input type=range min=10 max=1></form>"),
            new(200, 400, 1), Cancellation);
        Assert.Contains("minimum exceeds maximum", Assert.Single(page.Forms).Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RangeInputSupportsKeyboardAndPointerAdjustmentAndSubmits()
    {
        using var harness = new Harness("<form action='/range'><input type=range name=level min=-4 max=4 step=2 value=0><button>go</button></form>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        Assert.True(controller.FocusControl(tab, 0));
        Assert.False(controller.EditingFormControl(tab));
        Assert.True(controller.AdjustFocusedRange(tab, 1));
        Assert.Equal("2", controller.FormControlValue(tab, 0));
        Assert.True(controller.SetRangeFromPointer(tab, 0, 200));
        Assert.Equal("4", controller.FormControlValue(tab, 0));
        Assert.True(controller.FocusControl(tab, 1));
        Assert.True(controller.ActivateFocusedLink(tab));
        Assert.Equal("https://example.com/range?level=4", harness.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void PatternValidationRequiresFullMatchAndAllowsEmptyValues()
    {
        using var matching = new Harness("<form><input name=q value=abc pattern=\"[a-z]{3}\"><button type=submit>go</button></form>");
        Assert.True(matching.Controller.FocusControl(matching.Tab.Id, 0));
        Assert.True(matching.Controller.ActivateFocusedLink(matching.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?q=abc", matching.Source.Requests[^1].Url.Href);

        using var mismatch = new Harness("<form><input name=q value=abcd pattern=\"[a-z]{3}\"><button type=submit>go</button></form>");
        Assert.True(mismatch.Controller.FocusControl(mismatch.Tab.Id, 0));
        var error = Assert.Throws<PageNavigationException>(() => mismatch.Controller.ActivateFocusedLink(mismatch.Tab.Id));
        Assert.Contains("pattern", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(mismatch.Source.Requests);

        using var empty = new Harness("<form><input name=q pattern=\"[a-z]{3}\"><button type=submit>go</button></form>");
        Assert.True(empty.Controller.FocusControl(empty.Tab.Id, 0));
        Assert.True(empty.Controller.ActivateFocusedLink(empty.Tab.Id));
        Assert.Equal("https://example.com/final/index.html?q=", empty.Source.Requests[^1].Url.Href);
    }

    [Fact]
    public void EditingIsBoundedAndRejectsControlAndReadonlyInput()
    {
        using var harness = new Harness("<form><input name=a><input name=r readonly value=x></form>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        controller.FocusControl(tab, 0);
        Assert.Throws<PageNavigationException>(() => controller.InsertFormText(tab, "a\u0001"));
        controller.InsertFormText(tab, new string('a', RendererProtocol.MaxTextCharacters));
        Assert.Throws<BrowserLimitException>(() => controller.InsertFormText(tab, "a"));
        Assert.Equal(RendererProtocol.MaxTextCharacters, controller.FormControlValue(tab, 0).Length);
        controller.EditFormControl(tab, FormEdit.Left);
        Assert.Equal(RendererProtocol.MaxTextCharacters - 1, controller.FormControlCaret(tab));
        controller.FocusControl(tab, 1);
        Assert.False(controller.EditingFormControl(tab));
        Assert.Throws<PageNavigationException>(() => controller.InsertFormText(tab, "y"));
        Assert.Equal("x", controller.FormControlValue(tab, 1));
    }

    [Fact]
    public void OverlongSubmissionUrlFailsBeforeAnyRequest()
    {
        using var harness = new Harness("<form><input name=q></form>");
        harness.Controller.FocusControl(harness.Tab.Id, 0);
        harness.Controller.InsertFormText(harness.Tab.Id, new string('\u00e9', 1500));
        Assert.Throws<BrowserLimitException>(() => harness.Controller.ActivateFocusedLink(harness.Tab.Id));
        Assert.Single(harness.Source.Requests);
    }

    [Fact]
    public void MergedFocusOrderInterleavesVisibleControlsAndLinks()
    {
        using var harness = new Harness("""
            <a href=/a>a</a><form><input type=hidden name=h><input name=q><input name=d disabled>
            <button>b</button></form><a href=/b>b</a>
            """);
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        Assert.Equal(4, controller.PageFocusCount(tab));
        controller.FocusPage(tab);
        var order = new List<(int Link, int Control)>();
        for (var i = 0; i < 5; i++)
        {
            controller.MoveLinkFocus(tab);
            order.Add((controller.FocusedLinkIndex(tab), controller.FocusedControlIndex(tab)));
        }
        Assert.Equal([(0, -1), (-1, 1), (-1, 3), (1, -1), (0, -1)], order);
        controller.MoveLinkFocus(tab, backwards: true);
        Assert.Equal(3, controller.PageFocusPosition(tab));
        Assert.Equal(1, controller.FocusedLinkIndex(tab));
        controller.FocusPagePosition(tab, 1);
        Assert.Equal(1, controller.FocusedControlIndex(tab));
        controller.FocusLink(tab, 0);
        Assert.Equal(-1, controller.FocusedControlIndex(tab));
    }

    [Fact]
    public void SelectStateSurvivesRetainedDocumentResize()
    {
        using var harness = new Harness("<form><select name=mode><option value=fast>Fast</option><option value=slow>Slow</option></select></form>");
        var controller = harness.Controller;
        var tab = harness.Tab.Id;
        Assert.True(controller.FocusControl(tab, 0));
        Assert.True(controller.SetFocusedSelectEndpoint(tab, true));
        Assert.Equal("slow", controller.FormControlValue(tab, 0));

        controller.Resize(tab, new(201, harness.Viewport.Height, harness.Viewport.Scale));

        Assert.Equal("slow", controller.FormControlValue(tab, 0));
        Assert.Equal(0, controller.FocusedControlIndex(tab));
    }

    [Fact]
    public void FieldStateIsTabLocalSurvivesFailureAndRepaintButResetsOnNewDocument()
    {
        using var harness = new Harness("<div style='height:500px'></div><form><input name=q></form><div style='height:500px'></div>");
        var controller = harness.Controller;
        var first = harness.Tab;
        var second = controller.CreateTab(harness.Window);
        harness.Load(second, "<div style='height:500px'></div><form><input name=q></form><div style='height:500px'></div>");
        controller.Scroll(first.Id, 450);
        harness.Pump();
        controller.FocusControl(first.Id, 0);
        controller.InsertFormText(first.Id, "typed");
        Assert.Equal("", controller.FormControlValue(second.Id, 0));
        Assert.Equal(-1, controller.FocusedControlIndex(second.Id));
        controller.Scroll(first.Id, 10);
        harness.Pump();
        Assert.Null(first.Error);
        Assert.Equal("typed", controller.FormControlValue(first.Id, 0));
        Assert.Equal(0, controller.FocusedControlIndex(first.Id));
        var page = controller.Page(first.Id);
        Assert.True(controller.ActivateFocusedLink(first.Id));
        harness.Source.Requests[^1].Completion.SetException(new PageNavigationException("network failed"));
        harness.Pump();
        Assert.Contains("network failed", first.Error, StringComparison.Ordinal);
        Assert.Same(page, controller.Page(first.Id));
        Assert.Equal("https://example.com/final/index.html", first.History.Current!.Href);
        Assert.Equal("typed", controller.FormControlValue(first.Id, 0));
        Assert.Equal(0, controller.FocusedControlIndex(first.Id));
        Assert.True(controller.ActivateFocusedLink(first.Id));
        Assert.Equal("https://example.com/final/index.html?q=typed", harness.Source.Requests[^1].Url.Href);
        harness.Source.Requests[^1].Completion.SetResult(Document("<form><input name=q></form>",
            "https://example.com/final/index.html?q=typed"));
        harness.Pump();
        Assert.Equal("https://example.com/final/index.html?q=typed", first.History.Current!.Href);
        Assert.Equal("", controller.FormControlValue(first.Id, 0));
        Assert.Equal(-1, controller.FocusedControlIndex(first.Id));
    }

    [Fact]
    public void NetworkSubmissionIsBrokeredGetWithEncodedQueryThroughHsts()
    {
        var store = new HstsPolicyStore();
        var requests = new List<HttpRequestMessage>();
        using var controller = new BrowserController(() => new GetPageSource(new Handler(request =>
        {
            requests.Add(request);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Style + "<form action='http://example.test/find#top'><input name='q q' value='a b+\u00e9'></form>",
                    System.Text.Encoding.UTF8, "text/html")
            };
            response.Headers.TryAddWithoutValidation("Strict-Transport-Security", "max-age=60");
            return response;
        }), store), () => new StaticPageRenderer(FontPath, 100000), isolateOrigins: true);
        var tab = controller.CreateTab(controller.Session.CreateWindow().Id);
        var viewport = new PageViewport(200, 100, 1);
        controller.Navigate(tab.Id, "https://example.test/start");
        controller.Pump(_ => viewport);
        Assert.Null(tab.Error);
        controller.FocusControl(tab.Id, 0);
        Assert.True(controller.ActivateFocusedLink(tab.Id));
        controller.Pump(_ => viewport);
        Assert.Null(tab.Error);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.Null(requests[1].Content);
        Assert.Equal("https://example.test/find?q+q=a+b%2B%C3%A9", requests[1].RequestUri!.AbsoluteUri);
        Assert.Equal("https://example.test/find?q+q=a+b%2B%C3%A9#top", tab.History.Current!.Href);
        Assert.Equal("https://example.test", tab.Origin!.Serialize());
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
