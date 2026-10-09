using System.Net;
using SDL3;
using VisualWeb.Platform.Abstractions;
using Xunit;

namespace VisualWeb.Browser.Tests;

public sealed class ShellTests
{
    [Fact]
    public void SpaceTogglesFocusedCheckboxAndUpdatesItsShellOverlay()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style><form><input type=checkbox name=ok checked></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        var control = shell.Controller.Page(tab.Id)!.FormControls[0];
        Assert.True(shell.Controller.FormControlChecked(tab.Id, 0));
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        shell.Tick();
        var checkedPixels = native.Pixels!.ToArray();
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Space, 0, 0, true, false));
        shell.Tick();
        Assert.False(shell.Controller.FormControlChecked(tab.Id, 0));
        Assert.NotEqual(checkedPixels, native.Pixels!.ToArray());
    }

    [Fact]
    public void EmptyFieldPlaceholderIsDrawnByTheShellAndNotSubmitted()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style><form><input name=q placeholder=Search></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);

        var page = shell.Controller.Page(tab.Id)!;
        var control = Assert.Single(page.FormControls);
        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var placeholderPixels = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState([""], -1, -1)).Pixels;
        var emptyPixels = chrome.Render(window, page with
        {
            FormControls = [control with { Placeholder = "" }]
        }, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState([""], -1, -1)).Pixels;
        var valuePixels = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["query"], -1, -1)).Pixels;

        Assert.Equal("Search", control.Placeholder);
        Assert.NotEqual(placeholderPixels, emptyPixels);
        Assert.NotEqual(placeholderPixels, valuePixels);
        Assert.Equal("", shell.Controller.FormControlValue(tab.Id, 0));
    }

    [Fact]
    public void PasswordOverlayDoesNotRevealFieldValue()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style><form><input type=password value=secret></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);

        var page = shell.Controller.Page(tab.Id)!;
        var control = Assert.Single(page.FormControls);
        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var first = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["secret"], 0, 6)).Pixels;
        var second = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["hidden"], 0, 6)).Pixels;
        var unfocusedSecret = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["secret"], -1, -1)).Pixels;
        var unfocusedHidden = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["hidden"], -1, -1)).Pixels;
        var oneScalar = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["a"], -1, -1)).Pixels;
        var supplementaryScalar = chrome.Render(window, page, system.Items[0].Size, system.Items[0].Density,
            forms: new ShellFormState(["💩"], -1, -1)).Pixels;

        Assert.Equal("password", control.Kind);
        Assert.Equal(first, second);
        Assert.Equal(unfocusedSecret, unfocusedHidden);
        Assert.Equal(oneScalar, supplementaryScalar);
    }

    [Fact]
    public void EnterActivatesFocusedResetAndRestoresShellOwnedFormState()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input,button{display:block;height:20px}</style>" +
            "<form><input name=text value=original><input type=checkbox name=checked checked>" +
            "<input type=range name=level min=1 max=9 step=2 value=5><button type=reset>Reset</button></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);

        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        Assert.Equal("original-edited", shell.Controller.InsertFormText(tab.Id, "-edited"));
        Assert.True(shell.Controller.FocusControl(tab.Id, 1));
        Assert.True(shell.Controller.ToggleFocusedCheckable(tab.Id));
        Assert.True(shell.Controller.FocusControl(tab.Id, 2));
        Assert.True(shell.Controller.AdjustFocusedRange(tab.Id, 1));
        Assert.True(shell.Controller.FocusControl(tab.Id, 3));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));

        Assert.Equal(3, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.Equal("original", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.True(shell.Controller.FormControlChecked(tab.Id, 1));
        Assert.Equal("5", shell.Controller.FormControlValue(tab.Id, 2));
        Assert.StartsWith("data:text/html,", tab.History.Current!.Href);
        Assert.Null(tab.Error);
    }

    [Fact]
    public void SpaceActivatesFocusedSubmitAndResetControls()
    {
        using var system = new Windows();
        var requests = new List<Uri>();
        using var shell = new DevelopmentShell(system, FontPath, pageTransportFactory: Transport);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "https://forms.example/form");
        Wait();

        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        Assert.Equal("original-edited", shell.Controller.InsertFormText(tab.Id, "-edited"));
        Assert.True(shell.Controller.FocusControl(tab.Id, 2));
        Key(SDL.Scancode.Space);
        Assert.Equal("original", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.StartsWith("https://forms.example/form", tab.History.Current!.Href);
        Assert.Single(requests);

        Assert.True(shell.Controller.FocusControl(tab.Id, 1));
        Key(SDL.Scancode.Space);
        Wait();
        Assert.Equal("https://forms.example/form?q=original", tab.History.Current!.Href);
        Assert.Equal("https://forms.example/form?q=original", requests[^1].AbsoluteUri);

        HttpMessageHandler Transport() => new HstsHandler(request =>
        {
            requests.Add(request.RequestUri!);
            var html = request.RequestUri!.Query.Length == 0
                ? "<!doctype html><style>*{margin:0}input,button{display:block;height:20px}</style>"
                    + "<form><input name=q value=original><button type=submit>Save</button><button type=reset>Reset</button></form>"
                : "<!doctype html><style>*{margin:0}</style><title>Saved</title>done";
            return new(HttpStatusCode.OK) { Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html") };
        });
        void Key(SDL.Scancode scan) => shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void ArrowKeysMoveWithinRadioGroupAndSpaceSelectsFocusedRadio()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;height:20px}</style><form><input type=radio name=mode value=a><input type=radio name=mode value=b></form>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Right, 0, 0, true, false));
        Assert.Equal(1, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.True(shell.Controller.FormControlChecked(tab.Id, 1));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Space, 0, 0, true, false));
        Assert.True(shell.Controller.FormControlChecked(tab.Id, 1));
        Assert.False(shell.Controller.FormControlChecked(tab.Id, 0));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 1.25)]
    [InlineData(true, 2)]
    public void KeyboardLinksTraverseFromAddressOutlineEveryRectAndActivate(bool multiprocess, float density)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        native.Density = density;
        var tab = window.ActiveTab!;
        var destination = "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><title>Keyboard destination</title><p>done</p>");
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            $"<!doctype html><style>*{{margin:0}}</style><a href='{destination}'>one <span>two</span> three</a> " +
            "<a href='javascript:alert(1)'>unsupported</a>"));
        Wait();
        var page = shell.Controller.Page(tab.Id)!;
        var original = page.Frame.Pixels.ToArray();
        var header = (int)Math.Ceiling(ShellChrome.Height * density);
        Assert.Equal(original, native.Pixels!.Skip(header * page.Frame.Stride));
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        shell.Tick();
        Assert.False(native.TextInput);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(tab.Id));
        Assert.Equal(original, page.Frame.Pixels.ToArray());
        foreach (var rect in page.LinkTargets[0].Rects)
        {
            var left = (int)Math.Floor(rect.X * density);
            var right = (int)Math.Ceiling((rect.X + rect.Width) * density) - 1;
            var top = header + (int)Math.Floor(rect.Y * density);
            var bottom = header + (int)Math.Ceiling((rect.Y + rect.Height) * density) - 1;
            for (var x = left; x <= right; x++) { BorderPixel(x, top); BorderPixel(x, bottom); }
            for (var y = top; y <= bottom; y++) { BorderPixel(left, y); BorderPixel(right, y); }
        }
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(tab.Id));
        Key(SDL.Scancode.Return);
        Assert.Contains("Unsupported link URL scheme: javascript:", tab.Error);
        Assert.Same(page, shell.Controller.Page(tab.Id));
        Key(SDL.Scancode.Tab, SDL.Keymod.Shift);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(tab.Id));
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        shell.Tick();
        Assert.Equal(original, native.Pixels!.Skip(header * page.Frame.Stride));
        Key(SDL.Scancode.Return);
        Wait();
        Assert.Equal(-1, shell.Controller.FocusedLinkIndex(tab.Id));
        var addressBarDocument = shell.Controller.Page(tab.Id)!;
        var committedUrl = tab.History.Current!.Href;
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Key(SDL.Scancode.Return);
        Assert.Contains("Page-initiated data URL navigation is blocked", tab.Error, StringComparison.Ordinal);
        Assert.False(tab.IsLoading);
        Assert.Same(addressBarDocument, shell.Controller.Page(tab.Id));
        Assert.Equal(committedUrl, tab.History.Current!.Href);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(tab.Id));

        void BorderPixel(int x, int y) => Assert.Equal(new byte[] { 128, 96, 64, 255 },
            native.Pixels!.Skip(y * page.Frame.Stride + x * 4).Take(4));
        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PointerClickFocusesTextFieldWithoutStartingPageTextSelection(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;width:160px;height:24px}</style>" +
            "<form><input name=q value=start></form>"));
        Wait();

        var field = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(field.X + 8), (float)(ShellChrome.Height + field.Y + 8)));

        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.True(shell.Controller.EditingFormControl(tab.Id));
        Assert.Empty(shell.Controller.SelectedTextRects(tab.Id));
        Assert.True(native.TextInput);
        shell.Dispatch(new TextEntered(native.Id, "typed"));
        Assert.Equal("starttyped", shell.Controller.FormControlValue(tab.Id, 0));

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void SingleSelectPopupSkipsDisabledOptionsAndDismissesWithEscape()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select name=mode>" +
            "<option value=one>One</option><option value=blocked disabled>Blocked</option>" +
            "<option value=three>Three</option></select></form>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        var popupY = ShellChrome.Height + rect.Y + rect.Height;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(rect.X + 4), (float)(ShellChrome.Height + rect.Y + 4)));
        shell.Tick();

        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 30)));
        Assert.Equal(0, shell.Controller.SelectedOptionIndex(tab.Id, 0));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 50)));
        Assert.Equal(2, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(rect.X + 4), (float)(ShellChrome.Height + rect.Y + 4)));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Escape, 0, 0, true, false));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 10)));
        Assert.Equal(2, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void EnterAndSpaceToggleFocusedSelectPopupForKeyboardOptionPicking()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select name=mode>" +
            "<option value=one>One</option><option value=blocked disabled>Blocked</option>" +
            "<option value=three>Three</option></select></form>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        var popupY = ShellChrome.Height + rect.Y + rect.Height;
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));

        Key(SDL.Scancode.Return);
        shell.Tick();
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 50)));
        Assert.Equal(2, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        Key(SDL.Scancode.Space);
        shell.Tick();
        Key(SDL.Scancode.Escape);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(rect.X + 4), (float)(popupY + 10)));
        Assert.Equal(2, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        void Key(SDL.Scancode code) => shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void SingleSelectPopupWheelScrollsToOptionsOutsideTheInitialWindow()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var options = string.Concat(Enumerable.Range(0, 20).Select(index =>
            $"<option value={index}>{index}</option>"));
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><form><select name=mode>" + options + "</select></form>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        var popupX = rect.X + 4;
        var popupY = ShellChrome.Height + rect.Y + rect.Height;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)popupX, (float)(ShellChrome.Height + rect.Y + 4)));
        shell.Tick();
        shell.Dispatch(new PointerMoved(native.Id, (float)popupX, (float)(popupY + 10)));
        shell.Dispatch(new PointerScrolled(native.Id, 0, -4));
        shell.Tick();
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)popupX, (float)(popupY + 230)));
        Assert.Equal(19, shell.Controller.SelectedOptionIndex(tab.Id, 0));

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlASelectionCanBeReplacedByClipboardPaste(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}input{display:block;width:160px;height:24px}</style>" +
            "<form><input name=q value=original></form>"));
        Wait();
        var field = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(field.X + 8), (float)(ShellChrome.Height + field.Y + 8)));

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.A, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.True(shell.Controller.FormControlSelectAll(tab.Id));
        shell.Tick();
        var pixelX = (int)field.X + (int)field.Width - 2;
        var pixelY = (int)ShellChrome.Height + (int)field.Y + 2;
        Assert.Equal(new byte[] { 249, 213, 176, 255 }, native.Pixels!
            .Skip(pixelY * native.PixelSize.Width * 4 + pixelX * 4).Take(4));

        native.SetClipboardText("replacement");
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.V, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.Equal("replacement", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.False(shell.Controller.FormControlSelectAll(tab.Id));

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 1.25)]
    public void FormFieldsTakeTabFocusAndBlockPageInitiatedDataSubmission(bool multiprocess, float density)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        native.Density = density;
        var tab = window.ActiveTab!;
        var destination = "data:text/html," + Uri.EscapeDataString("<!doctype html><title>Result</title><style>*{margin:0}</style><p>done</p><!--");
        var initialUrl = "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0} input{display:block;width:150px;height:24px}</style>" +
            $"<a href='{destination}'>link</a><form action='{destination}'><input name=q value=a>" +
            "<input type=submit value=Send></form><div style='height:2000px'></div>");
        shell.Controller.Navigate(tab.Id, initialUrl);
        Wait();
        var page = shell.Controller.Page(tab.Id)!;
        var original = page.Frame.Pixels.ToArray();
        var header = (int)Math.Ceiling(ShellChrome.Height * density);
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(tab.Id));
        Assert.False(native.TextInput);
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.True(native.TextInput);
        shell.Tick();
        Assert.Equal(original, page.Frame.Pixels.ToArray());
        var field = page.FormControls[0].Rect!;
        var row = header + (int)Math.Floor(field.Y * density);
        var left = (int)Math.Floor(field.X * density);
        Assert.Equal(new byte[] { 128, 96, 64, 255 }, native.Pixels!.Skip(row * page.Frame.Stride + left * 4).Take(4));
        Assert.NotEqual(original.Skip(((int)Math.Floor((field.Y + 4) * density)) * page.Frame.Stride).Take(page.Frame.Stride),
            native.Pixels!.Skip((header + (int)Math.Floor((field.Y + 4) * density)) * page.Frame.Stride).Take(page.Frame.Stride));
        shell.Dispatch(new TextEntered(native.Id, "b c"));
        Assert.Equal("ab c", shell.Controller.FormControlValue(tab.Id, 0));
        Key(SDL.Scancode.Backspace);
        Key(SDL.Scancode.Home);
        Key(SDL.Scancode.End);
        Key(SDL.Scancode.Pagedown);
        Key(SDL.Scancode.Down);
        Key(SDL.Scancode.Up);
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        Assert.Equal("ab ", shell.Controller.FormControlValue(tab.Id, 0));
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.False(native.TextInput);
        Key(SDL.Scancode.Tab, SDL.Keymod.Shift);
        Assert.True(native.TextInput);
        Key(SDL.Scancode.Return);
        Assert.Contains("Page-initiated data URL navigation is blocked", tab.Error, StringComparison.Ordinal);
        Assert.Equal(initialUrl, tab.History.Current!.Href);
        Assert.Equal("ab ", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.True(native.TextInput);

        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextareaEnterInsertsLineAndTabEnterSubmits(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        var requests = new List<Uri>();
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer, pageTransportFactory: Transport);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "https://forms.example/form");
        Wait();
        var textarea = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
            (float)(textarea.X + 8), (float)(ShellChrome.Height + textarea.Y + 8)));

        Assert.True(native.TextInput);
        Key(SDL.Scancode.Return);
        shell.Dispatch(new TextEntered(native.Id, "second"));
        Key(SDL.Scancode.Up);
        shell.Dispatch(new TextEntered(native.Id, "!"));
        Key(SDL.Scancode.Down);
        shell.Dispatch(new TextEntered(native.Id, "?"));
        Key(SDL.Scancode.Home);
        shell.Dispatch(new TextEntered(native.Id, "^"));
        Key(SDL.Scancode.End);
        shell.Dispatch(new TextEntered(native.Id, "$"));
        Assert.Equal("first!\n^second?$", shell.Controller.FormControlValue(tab.Id, 0));
        Assert.Single(tab.History.Entries);
        Assert.Single(requests);
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedControlIndex(tab.Id));
        Assert.False(native.TextInput);
        Key(SDL.Scancode.Return);
        Wait();
        Assert.Equal("Result", tab.Title);
        Assert.Equal("https://forms.example/message?body=first%21%0D%0A%5Esecond%3F%24&send=yes", tab.History.Current!.Href);
        Assert.Equal(2, requests.Count);

        HttpMessageHandler Transport() => new HstsHandler(request =>
        {
            requests.Add(request.RequestUri!);
            var html = request.RequestUri!.AbsolutePath == "/form"
                ? "<!doctype html><style>*{margin:0}</style><form action='/message'>" +
                  "<textarea name=body>first</textarea><button name=send value=yes>Send</button></form>"
                : "<!doctype html><style>*{margin:0}</style><title>Result</title><p>sent</p>";
            return new(HttpStatusCode.OK) { Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html") };
        });
        void Key(SDL.Scancode scan) => shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InputButtonIsPaintedFocusedAndInertByClickOrEnter(bool click)
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var initialUrl = "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>body{margin:0}</style><form action='https://example.com/save'><input name=q value=x>"
            + "<input type=button name=b value=Press></form>");
        shell.Controller.Navigate(tab.Id, initialUrl);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
        Assert.True(tab.Error is null, tab.Error);
        var page = shell.Controller.Page(tab.Id)!;
        var button = page.FormControls[1];
        Assert.Equal("inert", button.Kind);
        Assert.Equal("Press", button.Label);
        shell.Tick();
        var header = (int)Math.Ceiling(ShellChrome.Height * native.Density);
        var bounds = button.Rect!;
        var left = (int)Math.Floor(bounds.X * native.Density);
        var right = (int)Math.Ceiling((bounds.X + bounds.Width) * native.Density);
        var top = header + (int)Math.Floor(bounds.Y * native.Density);
        var bottom = header + (int)Math.Ceiling((bounds.Y + bounds.Height) * native.Density);
        Assert.Contains(Enumerable.Range(top, bottom - top).SelectMany(y => Enumerable.Range(left, right - left)
            .Select(x => native.Pixels!.Skip(y * page.Frame.Stride + x * 4).Take(3).ToArray())),
            pixel => pixel.All(channel => channel < 60));

        if (click)
        {
            shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
                (float)(bounds.X + 1), (float)(ShellChrome.Height + bounds.Y + 1)));
        }
        else
        {
            Assert.True(shell.Controller.FocusControl(tab.Id, 1));
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        }
        shell.Tick();
        Assert.Null(tab.Error);
        Assert.False(tab.IsLoading);
        Assert.Same(page, shell.Controller.Page(tab.Id));
        Assert.Equal(initialUrl, tab.History.Current!.Href);

        Assert.True(shell.Controller.FocusControl(tab.Id, 1));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Space, 0, 0, true, false));
        Assert.Null(tab.Error);
        Assert.False(tab.IsLoading);
        Assert.Same(page, shell.Controller.Page(tab.Id));
        Assert.Equal(initialUrl, tab.History.Current!.Href);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnstyledFormButtonIsPaintedAndBlocksDataActionByClickOrEnter(bool click)
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var destination = "data:text/html," + Uri.EscapeDataString("<!doctype html><style>body{margin:0}</style><title>Submitted</title>done<!--");
        var initialUrl = "data:text/html," + Uri.EscapeDataString(
            $"<!doctype html><style>body{{margin:0}}</style><form action='{destination}'><input name=q value=x><button name=go value=1>Search</button></form>");
        shell.Controller.Navigate(tab.Id, initialUrl);
        Wait();
        var page = shell.Controller.Page(tab.Id)!;
        var button = page.FormControls[1];
        Assert.Equal("Search", button.Label);
        Assert.NotNull(button.Rect);
        shell.Tick();
        var header = (int)Math.Ceiling(ShellChrome.Height * native.Density);
        var bounds = button.Rect!;
        var left = (int)Math.Floor(bounds.X * native.Density);
        var right = (int)Math.Ceiling((bounds.X + bounds.Width) * native.Density);
        var top = header + (int)Math.Floor(bounds.Y * native.Density);
        var bottom = header + (int)Math.Ceiling((bounds.Y + bounds.Height) * native.Density);
        Assert.Contains(Enumerable.Range(top, bottom - top).SelectMany(y => Enumerable.Range(left, right - left)
            .Select(x => native.Pixels!.Skip(y * page.Frame.Stride + x * 4).Take(3).ToArray())),
            pixel => pixel.All(channel => channel < 60));

        if (click)
        {
            shell.Dispatch(new PointerButtonChanged(native.Id, 1, true,
                (float)(bounds.X + 1), (float)(ShellChrome.Height + bounds.Y + 1)));
        }
        else
        {
            shell.Controller.FocusControl(tab.Id, 0);
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        }
        Assert.Contains("Page-initiated data URL navigation is blocked", tab.Error, StringComparison.Ordinal);
        Assert.False(tab.IsLoading);
        Assert.Same(page, shell.Controller.Page(tab.Id));
        Assert.Equal(initialUrl, tab.History.Current!.Href);

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DragSelectsVisibleTextAndCtrlCCopiesSelection(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>body{margin:0} p{margin:0}</style><p>Hello world!</p><div style='height:1000px'></div>"));
        Wait();
        var page = shell.Controller.Page(tab.Id)!;
        Assert.Equal("Hello world!", string.Concat(page.TextTargets.Select(target => target.Text)));
        var first = page.TextTargets[0].Rect;
        var last = page.TextTargets[^1].Rect;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, (float)(first.X + first.Width / 2),
            (float)(ShellChrome.Height + first.Y + first.Height / 2)));
        shell.Dispatch(new PointerMoved(native.Id, (float)(last.X + last.Width / 2),
            (float)(ShellChrome.Height + last.Y + last.Height / 2)));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, (float)(last.X + last.Width / 2),
            (float)(ShellChrome.Height + last.Y + last.Height / 2)));
        Assert.Equal("Hello world!", shell.Controller.SelectedText(tab.Id));
        shell.Tick();
        var pixelX = (int)Math.Floor(first.X * native.Density);
        var pixelY = (int)Math.Floor(first.Y * native.Density);
        var pageOffset = pixelY * page.Frame.Stride + pixelX * 4;
        var chromeOffset = ((int)Math.Ceiling(ShellChrome.Height * native.Density) + pixelY) * page.Frame.Stride + pixelX * 4;
        Assert.False(page.Frame.Pixels.Span.Slice(pageOffset, 4).SequenceEqual(native.Pixels!.AsSpan(chromeOffset, 4)));
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.C, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.Equal("Hello world!", native.ClipboardText);
        shell.Controller.Scroll(tab.Id, 20);
        var scrollDeadline = DateTime.UtcNow.AddSeconds(30);
        while (shell.Controller.SelectedText(tab.Id).Length > 0 && DateTime.UtcNow < scrollDeadline)
        { shell.Tick(); Thread.Sleep(5); }
        Assert.Equal("", shell.Controller.SelectedText(tab.Id));
        Assert.True(shell.Controller.ScrollY(tab.Id) > 0);

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.True(tab.Error is null, tab.Error);
        }
    }

    [Fact]
    public void UnsupportedFormSubmissionIsReportedInTheTabWithoutNavigation()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0} input{display:block}</style><form method=post><input name=q></form>"));
        do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading);
        var page = shell.Controller.Page(tab.Id);
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedControlIndex(tab.Id));
        Key(SDL.Scancode.Return);
        Assert.Contains("Unsupported form method: post", tab.Error, StringComparison.Ordinal);
        Assert.False(tab.IsLoading);
        Assert.Same(page, shell.Controller.Page(tab.Id));

        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeyboardFocusTraversesChromeAndRestoresIndependentTabFocus(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var first = window.ActiveTab!;
        Load(first);
        Key(SDL.Scancode.Tab);
        Assert.False(shell.Controller.PageHasFocus(first.Id));
        Assert.Equal(-1, shell.Controller.FocusedLinkIndex(first.Id));
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(first.Id));
        Key(SDL.Scancode.Tab, SDL.Keymod.Shift);
        Assert.True(native.TextInput);
        Assert.False(shell.Controller.PageHasFocus(first.Id));
        Key(SDL.Scancode.Tab);
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(first.Id));
        var second = shell.Controller.CreateTab(window.Id);
        Load(second);
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Key(SDL.Scancode.Tab);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(second.Id));
        Key(SDL.Scancode.Tab, SDL.Keymod.Ctrl);
        shell.Tick();
        Assert.Equal(first.Id, window.ActiveTabId);
        Assert.True(shell.Controller.PageHasFocus(first.Id));
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(first.Id));
        Key(SDL.Scancode.Tab);
        Assert.False(shell.Controller.PageHasFocus(first.Id));
        Assert.False(native.TextInput);
        Key(SDL.Scancode.Tab, SDL.Keymod.Shift);
        Assert.True(shell.Controller.PageHasFocus(first.Id));
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(first.Id));
        Key(SDL.Scancode.Tab, SDL.Keymod.Ctrl);
        shell.Tick();
        Assert.Equal(second.Id, window.ActiveTabId);
        Assert.Equal(0, shell.Controller.FocusedLinkIndex(second.Id));
        Assert.True(shell.Controller.PageHasFocus(second.Id));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, 499, 300));
        Key(SDL.Scancode.Tab);
        Assert.Equal(1, shell.Controller.FocusedLinkIndex(second.Id));

        void Load(BrowserTab tab)
        {
            shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
                "<!doctype html><style>*{margin:0}</style><a href='#one'>one</a> <a href='#two'>two</a>"));
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
    }

    [Fact]
    public void TabTraversesDrawnChromeBeforeLinksAndNoLinksWrapInChrome()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        shell.Tick();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        using var chrome = new ShellChrome(FontPath, 1_000_000);
        var targets = chrome.Render(window, shell.Controller.Page(tab.Id), native.Size, native.Density).Targets;
        Assert.Empty(shell.Controller.Page(tab.Id)!.LinkTargets);
        foreach (var target in targets)
        {
            Key(SDL.Keymod.None);
            shell.Tick();
            Assert.False(shell.Controller.PageHasFocus(tab.Id));
            Assert.Equal(target.Action == ChromeAction.Address, native.TextInput);
            var offset = (int)target.Bounds.Y * native.Size.Width * 4 + (int)target.Bounds.X * 4;
            Assert.Equal(new byte[] { 128, 96, 64, 255 }, native.Pixels!.Skip(offset).Take(4));
        }
        Key(SDL.Keymod.None);
        Assert.False(native.TextInput);
        Key(SDL.Keymod.Shift);
        Assert.True(native.TextInput);
        Assert.Single(tab.History.Entries);

        void Key(SDL.Keymod mod) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Tab, 0, (ushort)mod, true, false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsupportedLinkClickReportsVisibleErrorWithoutReplacingPage(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var tab = shell.OpenWindow().ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><a href='javascript:alert(1)'>link</a>"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (tab.IsLoading && DateTime.UtcNow < deadline) { shell.Tick(); Thread.Sleep(5); }
        Assert.False(tab.IsLoading);
        Assert.Null(tab.Error);
        var old = shell.Controller.Page(tab.Id)!;
        var link = Assert.Single(old.LinkTargets);
        shell.Dispatch(new PointerButtonChanged(system.Items[0].Id, 1, true,
            (float)(link.X + 1), (float)(ShellChrome.Height + link.Y + 1)));
        Assert.Contains("Unsupported link URL scheme: javascript:", tab.Error);
        Assert.Same(old, shell.Controller.Page(tab.Id));
        Assert.Single(tab.History.Entries);
        Assert.False(tab.IsLoading);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 1.25)]
    [InlineData(true, 1.25)]
    public void PrimaryPageClicksUseLogicalCoordinatesAndBlockDataDestinations(bool multiprocess, float density)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        native.Density = density;
        var tab = window.ActiveTab!;
        var destination = "data:text/html," + Uri.EscapeDataString("<!doctype html><style>*{margin:0}</style><title>Clicked</title><p>destination</p>");
        var html = $"<!doctype html><style>*{{margin:0}}div{{height:800px}}</style><div></div><a href='{destination}'>visible link</a>";
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(html));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        shell.Controller.Scroll(tab.Id, double.MaxValue);
        Wait(() => shell.Controller.Page(tab.Id)!.LinkTargets.Count > 0);
        var old = shell.Controller.Page(tab.Id)!;
        var link = old.LinkTargets[0];
        var x = (float)(link.X + link.Width / 2);
        var y = (float)(link.Y + link.Height / 2 + ShellChrome.Height);
        shell.Dispatch(new PointerButtonChanged(native.Id, 3, true, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 2, true, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, 110));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, -1, y));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, 500, y));
        Assert.Same(old, shell.Controller.Page(tab.Id));
        Assert.False(tab.IsLoading);
        Assert.Single(tab.History.Entries);
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, y));
        Assert.Contains("Page-initiated data URL navigation is blocked", tab.Error, StringComparison.Ordinal);
        Assert.False(tab.IsLoading);
        Assert.Equal("data:text/html," + Uri.EscapeDataString(html), tab.History.Current!.Href);
        Assert.Same(old, shell.Controller.Page(tab.Id));
        Assert.NotEqual(0, shell.Controller.ScrollY(tab.Id));
        Assert.Single(shell.Controller.Page(tab.Id)!.LinkTargets);

        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!ready() && DateTime.UtcNow < deadline) { shell.Tick(); Thread.Sleep(5); }
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerticalScrollbarCanJumpAndDrag(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var html = "<!doctype html><style>*{margin:0}div{height:2000px;background-color:red}</style><div></div>";
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(html));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);

        var x = native.Size.Width - 5;
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, true, x, 250));
        Wait(() => !tab.IsLoading);
        Assert.True(shell.Controller.ScrollY(tab.Id) > 0);
        Assert.NotEqual(new byte[] { 255, 0, 0, 255 }, native.Pixels!.AsSpan(150 * native.Size.Width * 4 + x * 4, 4).ToArray());

        shell.Dispatch(new PointerMoved(native.Id, x, native.Size.Height - 1));
        Wait(() => !tab.IsLoading);
        var maximum = shell.Controller.Page(tab.Id)!.ScrollHeight - ShellChrome.Viewport(native.Size, 1)!.Value.Height;
        Assert.Equal(maximum, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerButtonChanged(native.Id, 1, false, x, native.Size.Height - 1));

        void Wait(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!ready() && DateTime.UtcNow < deadline) { shell.Tick(); Thread.Sleep(5); }
            Assert.Null(tab.Error);
            Assert.True(ready());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WheelArrowAndPageKeysScrollActivePageButNotChromeOrEditableFields(bool multiprocess)
    {
        using var system = new Windows();
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        var html = "<!doctype html><style>*{margin:0}div{height:2000px;background-color:red}</style><div></div>";
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(html));
        Wait(() => !tab.IsLoading && shell.Controller.Page(tab.Id) is not null);
        shell.Dispatch(new PointerMoved(native.Id, 10, 200));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        Wait(() => !tab.IsLoading);
        Assert.Equal(48, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerMoved(native.Id, 220, 70));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        Assert.Equal(48, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        shell.Dispatch(new PointerMoved(native.Id, 10, 200));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        Key(SDL.Scancode.Pagedown);
        Key(SDL.Scancode.Home);
        Assert.Equal(48, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Escape);
        Key(SDL.Scancode.Down);
        Assert.Equal(96, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Up);
        Assert.Equal(48, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Pagedown);
        Assert.True(shell.Controller.ScrollY(tab.Id) > 48);
        Key(SDL.Scancode.End);
        var maximum = 2000 - ShellChrome.Viewport(native.Size, 1)!.Value.Height;
        Assert.Equal(maximum, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerScrolled(native.Id, 0, float.MaxValue));
        Assert.Equal(maximum, shell.Controller.ScrollY(tab.Id));
        Key(SDL.Scancode.Pageup);
        Assert.True(shell.Controller.ScrollY(tab.Id) < maximum);
        Key(SDL.Scancode.Home);
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerScrolled(native.Id, 0, float.NaN));
        Assert.Contains("finite", tab.Error);
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));

        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None)
        {
            shell.Dispatch(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
            shell.Tick();
        }
        void Wait(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            do { shell.Tick(); Thread.Sleep(10); } while (!condition() && DateTime.UtcNow < deadline);
            Assert.True(condition(), tab.Error);
        }
    }

    private static string FontPath => Path.Combine(AppContext.BaseDirectory, "Data", "NotoSans.ttf");
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HstsSessionStoreIsSharedAcrossTabsAndWindowsButNotIndependentShells(bool multiprocess)
    {
        var requests = new List<Uri>();
        HttpMessageHandler Transport() => new HstsHandler(request =>
        {
            requests.Add(request.RequestUri!);
            Assert.False(request.Headers.Contains("Cookie"));
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<!doctype html><title>Secure</title><style>body{margin:0}</style>",
                    System.Text.Encoding.UTF8, "text/html")
            };
            response.Headers.TryAddWithoutValidation("Strict-Transport-Security", "max-age=60; includeSubDomains");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "secret=value; Secure; Path=/");
            return response;
        });
        var renderer = multiprocess ? Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll") : null;
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, rendererPath: renderer, pageTransportFactory: Transport);
        var firstWindow = shell.OpenWindow();
        Navigate(shell, firstWindow.ActiveTab!, "https://example.test/learn");
        var secondTab = shell.Controller.CreateTab(firstWindow.Id);
        Navigate(shell, secondTab, "http://example.test:80/second");
        Assert.Equal("https://example.test/second", secondTab.History.Current!.Href);
        Assert.Equal("https://example.test", secondTab.Origin!.Serialize());
        var secondWindow = shell.OpenWindow();
        Navigate(shell, secondWindow.ActiveTab!, "http://child.example.test/third");
        Assert.Equal("https", requests[^1].Scheme);
        shell.Controller.Session.MoveTab(secondTab.Id, secondWindow.Id);
        Navigate(shell, secondTab, "http://example.test/moved");
        Assert.Equal("https", requests[^1].Scheme);
        using var otherSystem = new Windows();
        using var independent = new DevelopmentShell(otherSystem, FontPath, rendererPath: renderer, pageTransportFactory: Transport);
        var independentWindow = independent.OpenWindow();
        Navigate(independent, independentWindow.ActiveTab!, "http://example.test/independent");
        Assert.Equal("http", requests[^1].Scheme);

        static void Navigate(DevelopmentShell target, BrowserTab tab, string input)
        {
            target.Controller.Navigate(tab.Id, input);
            Assert.True(SpinWait.SpinUntil(() =>
            {
                target.Tick();
                return !tab.IsLoading;
            }, TimeSpan.FromSeconds(20)), "Navigation did not finish.");
            Assert.Null(tab.Error);
        }
    }

    private sealed class HstsHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
    [Fact]
    public void FakePlatformReceivesPagePixelsAndRoutesTabsAddressAndWindowLifecycle()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var first = shell.OpenWindow();
        shell.Tick();
        Assert.NotNull(shell.Controller.Page(first.ActiveTab!.Id));
        var native = system.Items[0];
        Assert.NotNull(native.Pixels);
        Key(SDL.Scancode.T, SDL.Keymod.Ctrl);
        Assert.Equal(2, first.Tabs.Count);
        var tab = first.ActiveTab!;
        Key(SDL.Scancode.L, SDL.Keymod.Ctrl);
        Assert.True(native.TextInput);
        var blue = "data:text/html," + Uri.EscapeDataString("<!doctype html><style>body{margin:0;background-color:blue}</style>");
        system.Events.Enqueue(new TextEntered(native.Id, blue));
        shell.Tick();
        Key(SDL.Scancode.Return);
        Assert.False(native.TextInput);
        Assert.Equal(2, tab.History.Entries.Count);
        var at = 120 * native.Size.Width * 4;
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, native.Pixels!.AsSpan(at, 4).ToArray());
        Key(SDL.Scancode.N, SDL.Keymod.Ctrl);
        Assert.Equal(2, shell.Windows.Count);
        Key(SDL.Scancode.M, SDL.Keymod.Ctrl);
        Assert.Single(first.Tabs);
        Assert.Equal(2, shell.Controller.Session.Windows.Single(w => w.Id != first.Id).Tabs.Count);
        var otherNative = system.Items[1];
        system.Events.Enqueue(new CloseRequested(otherNative.Id));
        shell.Tick();
        Assert.True(otherNative.Disposed);
        Assert.False(native.Disposed);
        Assert.Single(shell.Windows);
        Key(SDL.Scancode.W, SDL.Keymod.Ctrl);
        Assert.True(native.Disposed);
        Assert.Empty(shell.Windows);

        void Key(SDL.Scancode scan, SDL.Keymod mod = SDL.Keymod.None)
        {
            system.Events.Enqueue(new KeyChanged(native.Id, (int)scan, 0, (ushort)mod, true, false));
            shell.Tick();
        }
    }
    [Fact]
    public void TextareaViewportFollowsCaretBeyondItsVisibleRows()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style>" +
            "<form><textarea>one\ntwo\nthree\nfour\nfive</textarea></form>"));
        Wait();
        var control = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        Assert.True(shell.Controller.FocusControl(tab.Id, 0),
            $"Controls: {shell.Controller.Page(tab.Id)!.FormControls.Count}, rectangle: {shell.Controller.Page(tab.Id)!.FormControls.FirstOrDefault()?.Rect}");
        shell.Tick();
        var before = Capture();
        Key(SDL.Scancode.Home);
        Key(SDL.Scancode.Up);
        shell.Tick();
        var after = Capture();
        Assert.NotEqual(before, after);
        Assert.Equal(3, shell.Controller.TextareaFirstLine(tab.Id, 0));
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerMoved(native.Id, (float)(control.X + 5), (float)(ShellChrome.Height + control.Y + 5)));
        shell.Dispatch(new PointerScrolled(native.Id, 0, -1));
        Assert.Equal(0, shell.Controller.TextareaFirstLine(tab.Id, 0));
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));
        shell.Dispatch(new PointerScrolled(native.Id, 0, 1));
        Assert.Equal(3, shell.Controller.TextareaFirstLine(tab.Id, 0));
        Assert.Equal(0, shell.Controller.ScrollY(tab.Id));

        byte[] Capture()
        {
            var pixels = native.Pixels ?? throw new InvalidOperationException("Shell pixels were not presented.");
            var left = (int)Math.Floor(control.X * native.Density);
            var top = (int)Math.Ceiling(ShellChrome.Height * native.Density) + (int)Math.Floor(control.Y * native.Density);
            var width = (int)Math.Ceiling(control.Width * native.Density);
            var height = (int)Math.Ceiling(control.Height * native.Density);
            var result = new byte[height * width * 4];
            for (var row = 0; row < height; row++)
            {
                pixels.AsSpan((top + row) * native.Size.Width * 4 + left * 4, width * 4)
                    .CopyTo(result.AsSpan(row * width * 4));
            }
            return result;
        }
        void Key(SDL.Scancode code) =>
            shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void TextareaSoftWrapCreatesVisualCaretRows()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString(
            "<!doctype html><style>*{margin:0}</style><textarea cols=5 rows=2>abcdefghij\nz</textarea>"));
        Wait();
        var rect = shell.Controller.Page(tab.Id)!.FormControls[0].Rect!;
        shell.Tick();
        Assert.True(shell.Controller.ScrollTextareaAt(tab.Id, rect.X + 2, rect.Y + 2, 300));
        Assert.True(shell.Controller.TextareaFirstLine(tab.Id, 0) > 0);
        Assert.True(shell.Controller.ScrollTextareaAt(tab.Id, rect.X + 2, rect.Y + 2, -300));
        Assert.Equal(0, shell.Controller.TextareaFirstLine(tab.Id, 0));
        Assert.True(shell.Controller.FocusControl(tab.Id, 0));
        Key(SDL.Scancode.Home);
        Key(SDL.Scancode.Up);
        Assert.InRange(shell.Controller.FormControlCaret(tab.Id), 1, 10);
        Key(SDL.Scancode.End);
        Assert.Equal(10, shell.Controller.FormControlCaret(tab.Id));
        Key(SDL.Scancode.Down);
        Assert.Equal(12, shell.Controller.FormControlCaret(tab.Id));

        void Key(SDL.Scancode code) => shell.Dispatch(new KeyChanged(native.Id, (int)code, 0, 0, true, false));
        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void CloseActiveTabRepaintsRemainingTabRatherThanLeavingStalePixels()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        shell.Tick();
        var first = window.ActiveTab!;
        var other = shell.Controller.CreateTab(window.Id);
        shell.Controller.Navigate(other.Id, "data:text/html," + Uri.EscapeDataString("<!doctype html><style>body{margin:0;background-color:blue}</style>"));
        shell.Tick();
        var count = shell.PresentedFrames;
        system.Events.Enqueue(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.W, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        shell.Tick();
        Assert.Equal(first.Id, window.ActiveTabId);
        Assert.True(shell.PresentedFrames > count);
        Assert.NotEqual((byte)255, system.Items[0].Pixels![120 * system.Items[0].Size.Width * 4]);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    [InlineData(1.3)]
    public void ChromeAndPageCompositionMatchesPhysicalSurfaceAtFractionalDensity(double density)
    {
        using var chrome = new ShellChrome(FontPath, 500_000);
        var session = new BrowserSession();
        var window = session.CreateWindow(); session.CreateTab(window.Id);
        var size = new PixelSize(641, 401);
        var viewport = ShellChrome.Viewport(size, density)!.Value;
        using var fonts = new VisualWeb.Engine.Paint.PaintFontRegistry();
        var page = new BrowserPage(VisualWeb.Engine.Paint.CpuRasterizer.Render(
            new(viewport.Width, viewport.Height, []), fonts, viewport.Scale, cancellationToken: TestContext.Current.CancellationToken), "Title", "Ready");
        var frame = chrome.Render(window, page, size, density);
        Assert.Equal(size, frame.Size);
        Assert.Equal(641 * 4, frame.Stride);
        Assert.Equal(641 * 401 * 4, frame.Pixels.Length);
        var header = (int)Math.Ceiling(120 * density);
        Assert.All(frame.Pixels.AsSpan(header * frame.Stride).ToArray(), value => Assert.Equal(255, value));
        Assert.Null(ShellChrome.Viewport(new(641, header), density));
    }
    [Fact]
    public void ChromeHitTestingSeparatesAddressAndControlsAndSanitizesPageTitle()
    {
        using var chrome = new ShellChrome(FontPath, 500_000);
        var session = new BrowserSession();
        var window = session.CreateWindow(); var tab = session.CreateTab(window.Id);
        var editor = new AddressEditor();
        editor.Reset("https://example.com/\u05d0", selectAll: true);
        var frame = chrome.Render(window, null, new(641, 401), 1, editor);
        Assert.Equal(ChromeAction.NewTab, ShellChrome.Hit(frame.Targets, 100, 70)!.Action);
        Assert.Equal(ChromeAction.Address, ShellChrome.Hit(frame.Targets, 220, 70)!.Action);
        Assert.Null(ShellChrome.Hit(frame.Targets, 100, 200));
        Assert.DoesNotContain(frame.Targets, t => t.Action == ChromeAction.Back);
        Assert.Equal(tab.Id, ShellChrome.Hit(frame.Targets, 80, 40)!.Tab);
        Assert.Equal(ChromeAction.CloseTab, ShellChrome.Hit(frame.Targets, 170, 40)!.Action);
    }
    [Fact]
    public void MoveLastTabCreatesWindowWithoutAnUnwantedHomeTabEvenAtTabLimit()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath, new() { MaxTabs = 1 });
        var original = shell.OpenWindow();
        shell.Tick();
        var tab = original.ActiveTab!;
        system.Events.Enqueue(new KeyChanged(system.Items[0].Id, (int)SDL.Scancode.M, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        shell.Tick();
        var window = Assert.Single(shell.Controller.Session.Windows);
        Assert.NotEqual(original.Id, window.Id);
        Assert.Same(tab, Assert.Single(window.Tabs));
        Assert.True(system.Items[0].Disposed);
        Assert.False(system.Items[1].Disposed);
        Assert.Null(tab.Error);
    }
    [Fact]
    public void TabCloseButtonAndFocusedAddressCaretUseActualEventTargets()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        shell.Tick();
        var native = system.Items[0];
        system.Events.Enqueue(new PointerButtonChanged(native.Id, 1, true, 100, 70));
        shell.Tick();
        Assert.Equal(2, window.Tabs.Count);
        var active = window.ActiveTab!;
        system.Events.Enqueue(new PointerButtonChanged(native.Id, 1, true, 220, 70));
        system.Events.Enqueue(new TextEntered(native.Id, "data:text/html,a"));
        shell.Tick();
        Assert.True(native.TextInput);
        Assert.Equal("data:text/html,a", active.AddressText);
        system.Events.Enqueue(new KeyChanged(native.Id, (int)SDL.Scancode.Left, 0, 0, true, false));
        system.Events.Enqueue(new TextEntered(native.Id, "b"));
        shell.Tick();
        Assert.Equal("data:text/html,ba", active.AddressText);
        system.Events.Enqueue(new PointerButtonChanged(native.Id, 1, true, 290, 40));
        shell.Tick();
        Assert.Single(window.Tabs);
        Assert.False(shell.Controller.Session.Contains(active.Id));
        Assert.False(native.TextInput);
    }
    [Fact]
    public void ControlVPastesClipboardTextIntoSelectedAddressAndNavigates()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var window = shell.OpenWindow();
        var native = system.Items[0];
        var tab = window.ActiveTab!;
        Wait();

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.L, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        var destination = "data:text/html," + Uri.EscapeDataString("<!doctype html><title>Pasted address</title><p>done</p>");
        native.SetClipboardText(destination);
        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.V, 0, (ushort)SDL.Keymod.Ctrl, true, false));
        Assert.Equal(destination, tab.AddressText);

        shell.Dispatch(new KeyChanged(native.Id, (int)SDL.Scancode.Return, 0, 0, true, false));
        Wait();
        Assert.Equal("Pasted address", tab.Title);

        void Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do { shell.Tick(); Thread.Sleep(5); } while (tab.IsLoading && DateTime.UtcNow < deadline);
            Assert.False(tab.IsLoading);
            Assert.Null(tab.Error);
        }
    }

    [Fact]
    public void OversizeWindowDoesNotTerminateOtherWindowsAndRecoversOnResize()
    {
        using var system = new Windows();
        using var shell = new DevelopmentShell(system, FontPath);
        var first = shell.OpenWindow(); shell.OpenWindow();
        shell.Tick();
        var native = system.Items[0];
        native.Size = new(4096, 4096);
        shell.Tick();
        Assert.Contains("framebuffer limit", native.Title);
        Assert.False(system.Items[1].Disposed);
        native.Size = new(1000, 720);
        shell.Tick();
        Assert.DoesNotContain("framebuffer limit", native.Title);
        Assert.NotNull(shell.Controller.Page(first.ActiveTab!.Id));
    }
    [Fact]
    public void EveryMultiprocessShellModeEnablesOriginIsolatedNavigationWithoutOptOut()
    {
        var rendererPath = Path.Combine(AppContext.BaseDirectory, "Renderer", "VisualWeb.Renderer.dll");
        using (var system = new Windows())
        using (var single = new DevelopmentShell(system, FontPath)) { Assert.False(single.Controller.IsolatesOrigins); }
        using (var system = new Windows())
        using (var confined = new DevelopmentShell(system, FontPath, rendererPath: rendererPath, requireSandbox: true))
        {
            Assert.True(confined.Controller.IsolatesOrigins);
        }
        using var unsandboxedSystem = new Windows();
        using var shell = new DevelopmentShell(unsandboxedSystem, FontPath, rendererPath: rendererPath);
        Assert.True(shell.Controller.IsolatesOrigins);
        var tab = shell.OpenWindow().ActiveTab!;
        Wait();
        Assert.Null(tab.Error);
        var home = tab.Origin;
        Assert.NotNull(home);
        shell.Controller.Navigate(tab.Id, "data:text/html," + Uri.EscapeDataString("<!doctype html><style>body{margin:0;background-color:blue}</style>"));
        Wait();
        Assert.Null(tab.Error);
        Assert.False(home!.IsSameOrigin(tab.Origin));
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, unsandboxedSystem.Items[0].Pixels!.AsSpan(120 * unsandboxedSystem.Items[0].Size.Width * 4, 4).ToArray());

        void Wait()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                shell.Tick();
                if (!tab.IsLoading) { return; }
                Thread.Sleep(5);
            } while (timer.Elapsed < TimeSpan.FromSeconds(30));
            Assert.Fail("Multiprocess shell navigation timed out.");
        }
    }
    private sealed class Windows : IWindowSystem
    {
        internal List<Window> Items { get; } = [];
        internal Queue<WindowEvent> Events { get; } = [];
        public string Backend => "fake";
        public event Action? QuitRequested;
        internal void Quit() => QuitRequested?.Invoke();
        public IPlatformWindow CreateWindow(WindowOptions options)
        {
            var window = new Window(new((uint)Items.Count + 1), new(options.Width, options.Height));
            Items.Add(window); return window;
        }
        public void PumpEvents()
        {
            while (Events.TryDequeue(out var input)) { Items.Single(w => w.Id == input.Window).Dispatch(input); }
        }
        public void Dispose() { foreach (var window in Items) { window.Dispose(); } QuitRequested = null; }
    }
    private sealed class Window(WindowId id, PixelSize size) : IPlatformWindow, IPixelSurface
    {
        public WindowId Id => id;
        internal PixelSize Size { get; set; } = size;
        internal float Density { get; set; } = 1;
        public PixelSize LogicalSize => new((int)(Size.Width / Density), (int)(Size.Height / Density));
        public PixelSize PixelSize => Size;
        public float DisplayScale => 1;
        public float PixelDensity => Density;
        public IPixelSurface Surface => this;
        public event Action<WindowEvent>? EventReceived;
        internal bool TextInput { get; private set; }
        internal bool Disposed { get; private set; }
        internal byte[]? Pixels { get; private set; }
        internal string Title { get; private set; } = "";
        internal void Dispatch(WindowEvent input) => EventReceived?.Invoke(input);
        public void SetTitle(string title) => Title = title;
        public void SetTextInput(bool enabled) => TextInput = enabled;
        internal string ClipboardText { get; private set; } = "";
        public void SetClipboardText(string text) => ClipboardText = text;
        public string GetClipboardText() => ClipboardText;
        public void Present(ReadOnlySpan<byte> pixels, PixelSize frameSize, int stride)
        {
            Assert.Equal(Size, frameSize);
            PixelBuffer.Validate(pixels.Length, frameSize, stride);
            Pixels = pixels.ToArray();
        }
        public void Dispose() { Disposed = true; EventReceived = null; }
    }
}
