using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using VisualWeb.Core.Url;

namespace VisualWeb.Ipc.Contracts;

public sealed class IpcProtocolException(string message) : IOException(message);

/// <summary>Data-only clipped visible CSS viewport rectangle.</summary>
public sealed record PageLinkRect(
    [property: JsonRequired] double X,
    [property: JsonRequired] double Y,
    [property: JsonRequired] double Width,
    [property: JsonRequired] double Height)
{
    public bool Contains(double x, double y) =>
        x >= X && y >= Y && x < X + Width && y < Y + Height;
}

/// <summary>One rendered textual anchor with visible rectangles, document-space top offset, absolute destination and bounded new-tab metadata; no element identity.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/links.html#attr-hyperlink-target">hyperlink target</see>.
/// The browser maps <c>_blank</c> to a new tab and <c>_self</c>, <c>_parent</c>, and <c>_top</c> to the current
/// tab because nested browsing contexts are not implemented; named targets are rejected by the renderer.</remarks>
[method: JsonConstructor]
public sealed record PageLinkTarget(
    [property: JsonRequired] IReadOnlyList<PageLinkRect> Rects,
    [property: JsonRequired] string Url,
    [property: JsonRequired] bool OpenInNewTab,
    [property: JsonRequired] double DocumentY)
{
    public PageLinkTarget(IReadOnlyList<PageLinkRect> rects, string url) : this(rects, url, false) { }
    public PageLinkTarget(IReadOnlyList<PageLinkRect> rects, string url, bool openInNewTab)
        : this(rects, url, openInNewTab, rects.Count == 0 ? 0 : rects[0].Y) { }
    public PageLinkTarget(double x, double y, double width, double height, string url)
        : this(new[] { new PageLinkRect(x, y, width, height) }, url, false, y) { }

    // Compatibility accessors expose the first visible rectangle, or zero when the anchor is offscreen.
    [JsonIgnore] public double X => Rects.Count == 0 ? 0 : Rects[0].X;
    [JsonIgnore] public double Y => Rects.Count == 0 ? 0 : Rects[0].Y;
    [JsonIgnore] public double Width => Rects.Count == 0 ? 0 : Rects[0].Width;
    [JsonIgnore] public double Height => Rects.Count == 0 ? 0 : Rects[0].Height;
    public bool Contains(double x, double y) => Rects.Any(rect => rect.Contains(x, y));
}


/// <summary>Bounded rendered element ID or legacy anchor name and its document-space top offset for fragment navigation.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/browsing-the-web.html#navigate-fragid">navigate to a fragment</see> and
/// <see href="https://html.spec.whatwg.org/multipage/browsing-the-web.html#attr-a-name">the a element name attribute</see>.</remarks>
public sealed record PageFragmentTarget(
    [property: JsonRequired] string Id,
    [property: JsonRequired] double Y);

/// <summary>Browser-fetched, decoded linked stylesheet text keyed by its serialized absolute request URL.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/links.html#link-type-stylesheet">link type
/// "stylesheet"</see>. Data only: no response headers, final redirect URL, DOM node or fetch capability crosses IPC.</remarks>
public sealed record PageStylesheet(
    [property: JsonRequired] string Url,
    [property: JsonRequired] string Css);

/// <summary>One bounded visible text fragment and its clipped viewport rectangle for browser-owned selection.</summary>
public sealed record PageTextTarget(
    [property: JsonRequired] string Text,
    [property: JsonRequired] PageLinkRect Rect);

/// <summary>One parsed form owner: an absolute GET action, or a visible unsupported-semantics diagnostic.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-algorithm">form
/// submission algorithm</see>, <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fs-novalidate">novalidate</see>,
/// <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fs-formnovalidate">formnovalidate</see> and
/// <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fs-formaction">formaction</see>;
/// <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-attributes">submission targets</see>.
/// Only the bounded GET/urlencoded subset is representable; Error is set
/// (and Action empty) for anything else. TargetError is separate so a selected formtarget can override it;
/// NoValidate carries the form's validation-bypass flag, without navigation-policy privileges.
/// No element identity or script capability crosses IPC.</remarks>
public sealed record PageForm(
    [property: JsonRequired] string Action,
    [property: JsonRequired] string? Error)
{
    [JsonRequired]
    public bool OpenInNewTab { get; init; }
    [JsonRequired]
    public string? TargetError { get; init; }
    [JsonRequired]
    public bool NoValidate { get; init; }
}

/// <summary>One tree-ordered supported form control with initial state and optional clipped visible border box.</summary>
/// <remarks>Kind is text, search, email, tel, url, password, date, time, month, week, number, range, color, checkbox, radio, select, textarea, hidden, submit/reset (input) or button/reset (button type=submit/reset). Form is the owner index or -1.
/// BeforeLink is the number of visible link targets preceding the control in tree order. Value is the initial
/// value; the browser shell owns user edits. Placeholder is bounded display-only text; Multiple is supported for email and select controls. FormNoValidate, FormAction and FormTargetOpenInNewTab apply only to submit buttons.
/// A null FormTargetOpenInNewTab inherits the form target unless FormTargetError rejects the selected submitter.
/// Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#attr-fs-formtarget">formtarget</see>.
/// TextareaWrapColumns is present only for textarea hard wrapping. SelectRows is one for the compact select subset.
/// Label carries submit/reset-input text or button text.</remarks>
public sealed record PageFormControl(
    [property: JsonRequired] int Form,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Value,
    [property: JsonRequired] string Label,
    [property: JsonRequired] string? Pattern,
    [property: JsonRequired] bool Disabled,
    [property: JsonRequired] bool ReadOnly,
    [property: JsonRequired] bool Required,
    [property: JsonRequired] int MinLength,
    [property: JsonRequired] int MaxLength,
    [property: JsonRequired] int BeforeLink,
    [property: JsonRequired] PageLinkRect? Rect,
    [property: JsonRequired] bool Checked,
    [property: JsonRequired] double? Minimum,
    [property: JsonRequired] double? Maximum,
    [property: JsonRequired] double? Step,
    [property: JsonRequired] bool StepAny)
{
    [JsonRequired]
    public PageFormOption[] Options { get; init; } = [];
    [JsonRequired]
    public bool Multiple { get; init; }
    [JsonRequired]
    public string Placeholder { get; init; } = "";
    [JsonRequired]
    public bool FormNoValidate { get; init; }
    [JsonRequired]
    public string? FormAction { get; init; }
    [JsonRequired]
    public string? FormActionError { get; init; }
    [JsonRequired]
    public bool? FormTargetOpenInNewTab { get; init; }
    [JsonRequired]
    public string? FormTargetError { get; init; }
    [JsonRequired]
    public int TextareaWrapColumns { get; init; }
    [JsonRequired]
    public bool TextareaWrapHard { get; init; }
    /// <summary>Visible rows for a bounded select control; single-row selects report one.</summary>
    [JsonRequired]
    public int SelectRows { get; init; } = 1;
}

/// <summary>Data-only option value, label and initial disabled/selected state for a supported select control.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/form-elements.html#the-option-element">option element</see>.
/// The browser owns subsequent selection state; no DOM identity crosses IPC.</remarks>
public sealed record PageFormOption(
    [property: JsonRequired] string Value,
    [property: JsonRequired] string Label,
    [property: JsonRequired] bool Disabled,
    [property: JsonRequired] bool Selected);

/// <summary>Checks the browser's bounded year-0001-through-9999 subset of HTML date values.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#valid-date-string">valid date string</see>.</remarks>
/// <summary>Validates the bounded simple-color subset used by browser-owned color controls.</summary>
public static class FormColor
{
    public const string DefaultValue = "#000000";

    public static bool IsValid(string value) => value.Length == 7 && value[0] == '#'
        && value[1..].All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    public static string Normalize(string? value) => value is { } candidate && IsValid(candidate)
        ? candidate.ToLowerInvariant() : DefaultValue;
}

public static class FormDate
{
    /// <summary>Returns the Gregorian day ordinal for a valid bounded date.</summary>
    public static bool TryGetDayNumber(string value, out int dayNumber)
    {
        dayNumber = 0;
        if (!IsValid(value)) { return false; }
        var year = int.Parse(value.AsSpan(0, 4), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
        var month = int.Parse(value.AsSpan(5, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
        var day = int.Parse(value.AsSpan(8, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
        dayNumber = new DateOnly(year, month, day).DayNumber;
        return true;
    }

    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 10 || value[4] != '-' || value[7] != '-') { return false; }
        for (var index = 0; index < value.Length; index++)
        {
            if (index is 4 or 7) { continue; }
            if (!char.IsAsciiDigit(value[index])) { return false; }
        }
        var year = int.Parse(value.AsSpan(0, 4), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
        var month = int.Parse(value.AsSpan(5, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
        var day = int.Parse(value.AsSpan(8, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
        if (year == 0 || month is < 1 or > 12) { return false; }
        var leap = year % 400 == 0 || year % 4 == 0 && year % 100 != 0;
        var days = month switch
        {
            2 => leap ? 29 : 28,
            4 or 6 or 9 or 11 => 30,
            _ => 31,
        };
        return day is >= 1 && day <= days;
    }
}

/// <summary>Checks the bounded HTML time-string grammar without time-zone or leap-second forms.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#valid-time-string">valid time string</see>.</remarks>
public static class FormTime
{
    /// <summary>Returns milliseconds since midnight for a valid bounded time.</summary>
    public static bool TryGetMilliseconds(string value, out int milliseconds)
    {
        milliseconds = 0;
        if (!IsValid(value)) { return false; }
        var hour = TwoDigits(value, 0);
        var minute = TwoDigits(value, 3);
        var second = value.Length >= 8 ? TwoDigits(value, 6) : 0;
        var fraction = value.Length > 8 ? int.Parse(value.AsSpan(9).ToString().PadRight(3, '0'), System.Globalization.CultureInfo.InvariantCulture) : 0;
        milliseconds = ((hour * 60 + minute) * 60 + second) * 1000 + fraction;
        return true;
    }

    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length is not (5 or 8 or 10 or 11 or 12)
            || value[2] != ':' || value.Length >= 8 && value[5] != ':') { return false; }
        if (!Digits(value.AsSpan(0, 2)) || !Digits(value.AsSpan(3, 2))) { return false; }
        var hour = TwoDigits(value, 0);
        var minute = TwoDigits(value, 3);
        if (hour > 23 || minute > 59) { return false; }
        if (value.Length == 5) { return true; }
        if (!Digits(value.AsSpan(6, 2))) { return false; }
        var second = TwoDigits(value, 6);
        if (second > 59) { return false; }
        if (value.Length == 8) { return true; }
        if (value[8] != '.' || !Digits(value.AsSpan(9))) { return false; }
        return true;
    }

    private static bool Digits(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character)) { return false; }
        }
        return true;
    }

    private static int TwoDigits(string value, int offset) =>
        (value[offset] - '0') * 10 + value[offset + 1] - '0';
}

/// <summary>Checks a bounded HTML month string: a positive year with at least four digits and a two-digit month.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#valid-month-string">valid month string</see>.</remarks>
public static class FormMonth
{
    /// <summary>Returns a bounded comparable ordinal for a valid month string.</summary>
    public static bool TryGetMonthNumber(string value, out long monthNumber)
    {
        monthNumber = 0;
        if (!IsValid(value)) { return false; }
        var separator = value.IndexOf('-');
        if (!long.TryParse(value.AsSpan(0, separator), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var year)) { return false; }
        monthNumber = checked(year * 12 + (value[^2] - '0') * 10 + value[^1] - '0');
        return true;
    }

    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var separator = value.IndexOf('-');
        if (separator < 4 || value.Length != separator + 3) { return false; }
        var nonzeroYear = false;
        foreach (var digit in value.AsSpan(0, separator))
        {
            if (!char.IsAsciiDigit(digit)) { return false; }
            nonzeroYear |= digit != '0';
        }
        var monthText = value.AsSpan(separator + 1, 2);
        if (!char.IsAsciiDigit(monthText[0]) || !char.IsAsciiDigit(monthText[1])) { return false; }
        var month = (monthText[0] - '0') * 10 + monthText[1] - '0';
        return nonzeroYear && month is >= 1 and <= 12;
    }
}

/// <summary>Checks strict HTML week strings and the 52/53-week ISO week-year boundary.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/common-microsyntaxes.html#valid-week-string">valid week string</see>.</remarks>
public static class FormWeek
{
    /// <summary>Returns a bounded comparable ordinal for a valid ISO week string.</summary>
    public static bool TryGetWeekNumber(string value, out long weekNumber)
    {
        weekNumber = 0;
        if (!IsValid(value) || !long.TryParse(value.AsSpan(0, value.IndexOf("-W", StringComparison.Ordinal)),
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var year)) { return false; }
        try
        {
            var precedingYear = checked(year - 1);
            var daysBeforeYear = checked(365 * precedingYear + precedingYear / 4 - precedingYear / 100 + precedingYear / 400);
            var fourthOfJanuary = checked(daysBeforeYear + 3);
            var weekOneMonday = fourthOfJanuary - fourthOfJanuary % 7;
            var week = (value[^2] - '0') * 10 + value[^1] - '0';
            weekNumber = checked(weekOneMonday / 7 + week - 1);
            return true;
        }
        catch (OverflowException) { return false; }
    }

    public static bool IsValid(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var separator = value.IndexOf("-W", StringComparison.Ordinal);
        if (separator < 4 || value.Length != separator + 4) { return false; }
        var nonzeroYear = false;
        var yearModulo400 = 0;
        foreach (var digit in value.AsSpan(0, separator))
        {
            if (!char.IsAsciiDigit(digit)) { return false; }
            nonzeroYear |= digit != '0';
            yearModulo400 = (yearModulo400 * 10 + digit - '0') % 400;
        }
        var weekText = value.AsSpan(separator + 2, 2);
        if (!nonzeroYear || !char.IsAsciiDigit(weekText[0]) || !char.IsAsciiDigit(weekText[1])) { return false; }
        var week = (weekText[0] - '0') * 10 + weekText[1] - '0';
        var priorYear = (yearModulo400 + 399) % 400;
        var januaryFirst = (int)((365L * priorYear + priorYear / 4 - priorYear / 100 + priorYear / 400) % 7);
        var leapYear = yearModulo400 == 0 || yearModulo400 % 4 == 0 && yearModulo400 % 100 != 0;
        var weeksInYear = januaryFirst == 3 || leapYear && januaryFirst == 2 ? 53 : 52;
        return week is >= 1 && week <= weeksInYear;
    }
}

/// <summary>Validates and sanitizes bounded HTML email values and comma-separated address lists.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/input.html#email-state-(type=email)">Email state</see>.</remarks>
public static class FormEmail
{
    private static readonly char[] AsciiWhitespace = [' ', '\t', '\n', '\r', '\f'];
    private static readonly Regex Address = new(
        @"\A[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static string Sanitize(string value, bool multiple)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!multiple)
        {
            return value.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal)
                .Trim(AsciiWhitespace);
        }
        return string.Join(',', value.Split(',').Select(token => token.Trim(AsciiWhitespace)));
    }

    public static bool IsValid(string value, bool multiple)
    {
        var sanitized = Sanitize(value, multiple);
        if (sanitized.Length == 0) { return true; }
        if (!multiple) { return Address.IsMatch(sanitized); }
        return sanitized.Split(',').All(token => token.Length > 0 && Address.IsMatch(token));
    }
}

/// <summary>HTML valid floating-point syntax with finite invariant-culture parsing.</summary>
public static class FormNumber
{
    private static readonly Regex Syntax = new(
        @"\A-?(?:[0-9]+(?:\.[0-9]+)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static bool TryParse(string value, out double number)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Syntax.IsMatch(value) && double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out number) && double.IsFinite(number)) { return true; }
        number = 0;
        return false;
    }

    public static bool IsStepAligned(double value, double baseValue, double step)
    {
        var quotient = (value - baseValue) / step;
        if (!double.IsFinite(quotient) || Math.Abs(quotient) > 1_000_000_000_000d) { return false; }
        var nearest = Math.Round(quotient);
        // Allow the rounding error from binary64 arithmetic on decimal values such as 0.3 / 0.1.
        var tolerance = Math.Max(1, Math.Abs(quotient)) * 2.2204460492503131e-16 * 4;
        return Math.Abs(quotient - nearest) <= tolerance;
    }
}

/// <summary>Bounded full-value pattern matching shared by renderer validation and the browser broker.</summary>
public static class FormPattern
{
    public const int MaxCharacters = 256;
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);

    public static bool IsValid(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Length > MaxCharacters) { return false; }
        try { _ = Create(pattern); return true; }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }

    public static bool Matches(string pattern, string value) => Create(pattern).IsMatch(value);

    private static Regex Create(string pattern) => new($"\\A(?:{pattern})\\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, Timeout);
}

/// <summary>Versioned data-only renderer messages. No DOM, native handles, paths or broker capabilities.</summary>
/// <remarks>Internal protocol; not a web interface. Each channel serves exactly one tab.</remarks>
public sealed record RendererMessage
{
    public int Version { get; init; } = RendererProtocol.Version;
    public string Kind { get; init; } = "";
    public long Id { get; init; }
    public string? Url { get; init; }
    public string? Html { get; init; }
    public int StatusCode { get; init; }
    public string[]? Diagnostics { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Scale { get; init; }
    public double ScrollY { get; init; }
    public double ScrollHeight { get; init; }
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
    public int Stride { get; init; }
    public string? Title { get; init; }
    public string? Status { get; init; }
    public string? Error { get; init; }
    public string? SandboxProfile { get; init; }
    public Guid DocumentId { get; init; }
    public Guid CommittedDocumentId { get; init; }
    public bool ExecuteInlineScripts { get; init; }
    public bool ReuseDocument { get; init; }
    public PageLinkTarget[]? LinkTargets { get; init; }
    public PageFragmentTarget[]? FragmentTargets { get; init; }
    public PageStylesheet[]? Stylesheets { get; init; }
    public PageForm[]? Forms { get; init; }
    public PageFormControl[]? FormControls { get; init; }
    public PageTextTarget[]? TextTargets { get; init; }
}

public static class RendererProtocol
{
    private const string HelloKind = "hello";
    private const string RenderKind = "render";
    private const string FrameKind = "frame";
    private const string ErrorKind = "error";

    public const int Version = 38;
    public const double MaxScrollHeight = 10_000_000;
    public const int MaxHeaderBytes = 32 * 1024 * 1024;
    public const int MaxPixels = 4_194_304;
    public const int MaxPayloadBytes = MaxPixels * 4;
    public const int MaxHtmlCharacters = 4 * 1024 * 1024;
    public const int MaxTextCharacters = 8192;
    public const int MaxFormPatternCharacters = FormPattern.MaxCharacters;
    public const int MaxFormPatterns = 64;
    public const int MaxLinkTargets = 4096;
    public const int MaxFragmentTargets = 4096;
    public const int MaxFragmentMetadataBytes = 1024 * 1024;
    public const int MaxLinkRects = 64;
    public const int MaxLinkMetadataBytes = 1024 * 1024;
    public const int MaxForms = 256;
    public const int MaxFormControls = 1024;
    public const int MaxSelectOptions = 1024;
    /// <summary>Budget for the UTF-8 JSON serialization of forms plus controls.</summary>
    public const int MaxFormMetadataBytes = 1024 * 1024;
    public const int MaxTextTargets = 32_768;
    /// <summary>Budget for serialized text-selection target data, separate from frame pixels and form metadata.</summary>
    public const int MaxTextMetadataBytes = 1024 * 1024;
    public const int MaxStylesheets = 32;
    public const int MaxStylesheetCharacters = 256 * 1024;
    /// <summary>Budget for the UTF-8 JSON serialization of the whole linked stylesheet collection.</summary>
    public const int MaxStylesheetBytes = 1024 * 1024;

    [SuppressMessage("Sonar", "S3776:Cognitive Complexity of methods should not be too high", Justification = "This is the versioned untrusted-message boundary; ordered kind scoping and payload checks form one auditable finite protocol state machine.")]
    public static void Validate(RendererMessage message, int payloadLength)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Version != Version) { throw new IpcProtocolException("Unsupported renderer protocol version."); }
        if (message.Kind != FrameKind && message.LinkTargets is not null)
        { throw new IpcProtocolException("Link targets belong only to frame replies."); }
        if (message.Kind != FrameKind && message.FragmentTargets is not null)
        { throw new IpcProtocolException("Fragment targets belong only to frame replies."); }
        if (message.Kind != FrameKind && (message.Forms is not null || message.FormControls is not null))
        { throw new IpcProtocolException("Form metadata belongs only to frame replies."); }
        if (message.Kind != FrameKind && message.TextTargets is not null)
        { throw new IpcProtocolException("Text targets belong only to frame replies."); }
        if (message.Kind != RenderKind && message.Stylesheets is not null)
        { throw new IpcProtocolException("Linked stylesheets belong only to render requests."); }
        if ((message.Kind != RenderKind && message.ScrollY != 0) || (message.Kind != FrameKind && message.ScrollHeight != 0))
        { throw new IpcProtocolException("Scroll fields belong only to render requests and frame replies respectively."); }
        if (message.Kind != RenderKind && (message.DocumentId != Guid.Empty || message.CommittedDocumentId != Guid.Empty
            || message.ExecuteInlineScripts || message.ReuseDocument))
        { throw new IpcProtocolException("Document policy fields belong only to render requests."); }
        if (message.Kind == HelloKind)
        {
            if (message.Id != 0 || payloadLength != 0 || message.SandboxProfile is { Length: > MaxTextCharacters })
            {
                throw new IpcProtocolException("Invalid renderer handshake.");
            }
            return;
        }
        if (message.Id <= 0) { throw new IpcProtocolException("Renderer request identity must be positive."); }
        switch (message.Kind)
        {
            case RenderKind:
                if (payloadLength != 0 || message.DocumentId == Guid.Empty || message.Html is null || message.Html.Length > MaxHtmlCharacters
                    || string.IsNullOrEmpty(message.Url) || message.Url.Length > MaxTextCharacters
                    || message.StatusCode is < 100 or > 599 || message.Diagnostics is null || message.Diagnostics.Length > 64
                    || message.Diagnostics.Any(d => d is null || d.Length > MaxTextCharacters))
                {
                    throw new IpcProtocolException("Invalid renderer request fields or limits.");
                }
                ValidateStylesheets(message.Stylesheets);
                Dimensions(message.Width, message.Height, message.Scale);
                if (!double.IsFinite(message.ScrollY) || message.ScrollY < 0 || message.ScrollY > 1e9)
                { throw new IpcProtocolException("Invalid renderer scroll offset."); }
                break;
            case FrameKind:
                var dimensions = Dimensions(message.Width, message.Height, message.Scale);
                if (message.PixelWidth <= 0 || message.PixelHeight <= 0
                    || message.PixelWidth != dimensions.Width || message.PixelHeight != dimensions.Height
                    || (long)message.PixelWidth * message.PixelHeight > MaxPixels
                    || (long)message.PixelWidth * 4 != message.Stride
                    || (long)message.Stride * message.PixelHeight != payloadLength
                    || message.Title is null || message.Title.Length > MaxTextCharacters
                    || message.Status is null || message.Status.Length > MaxTextCharacters)
                {
                    throw new IpcProtocolException("Invalid renderer frame dimensions, stride, text or bytes.");
                }
                if (!double.IsFinite(message.ScrollHeight) || message.ScrollHeight < 0 || message.ScrollHeight > MaxScrollHeight)
                { throw new IpcProtocolException("Invalid renderer scroll height."); }
                ValidateLinks(message.LinkTargets, message.Width, message.Height);
                ValidateFragmentTargets(message.FragmentTargets, message.ScrollHeight);
                ValidateForms(message.Forms, message.FormControls, message.LinkTargets!.Length, message.Width, message.Height);
                ValidateTextTargets(message.TextTargets, message.Width, message.Height);
                break;
            case ErrorKind:
                if (payloadLength != 0 || string.IsNullOrEmpty(message.Error) || message.Error.Length > MaxTextCharacters)
                {
                    throw new IpcProtocolException("Invalid renderer failure reply.");
                }
                break;
            default: throw new IpcProtocolException("Unknown renderer message kind.");
        }
    }
    public static void ValidateLinks(IReadOnlyList<PageLinkTarget>? links, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0
            || links is null || links.Count > MaxLinkTargets)
        { throw new IpcProtocolException("Invalid renderer link viewport or count."); }
        long urlBytes = 0;
        foreach (var link in links)
        {
            if (link is null || link.Rects is null || link.Rects.Count > MaxLinkRects
                || string.IsNullOrEmpty(link.Url) || link.Url.Length > MaxTextCharacters
                || !double.IsFinite(link.DocumentY) || link.DocumentY < 0 || link.DocumentY > MaxScrollHeight)
            { throw new IpcProtocolException("Invalid renderer link rectangle count, URL or document offset limit."); }
            foreach (var rect in link.Rects)
            {
                if (rect is null || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y)
                    || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height)
                    || rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
                    || rect.X + rect.Width > width || rect.Y + rect.Height > height)
                { throw new IpcProtocolException("Invalid renderer link rectangle."); }
            }
            urlBytes += Encoding.UTF8.GetByteCount(link.Url);
            if (urlBytes > MaxLinkMetadataBytes)
            { throw new IpcProtocolException("Renderer link metadata byte limit exceeded."); }
            var parsed = BrowserUrl.ParseResult(link.Url);
            if (parsed.Url?.Href != link.Url)
            { throw new IpcProtocolException("Renderer link URL must be serialized and absolute."); }
        }
        if (JsonSerializer.SerializeToUtf8Bytes(links).Length > MaxLinkMetadataBytes)
        { throw new IpcProtocolException("Renderer link metadata byte limit exceeded."); }
    }
    public static void ValidateFragmentTargets(IReadOnlyList<PageFragmentTarget>? targets, double scrollHeight)
    {
        if (targets is null || targets.Count > MaxFragmentTargets || !double.IsFinite(scrollHeight)
            || scrollHeight < 0 || scrollHeight > MaxScrollHeight)
        { throw new IpcProtocolException("Invalid renderer fragment target collection or scroll extent."); }
        long bytes = 0;
        foreach (var target in targets)
        {
            if (target is null || target.Id is null || target.Id.Length > MaxTextCharacters
                || !double.IsFinite(target.Y) || target.Y < 0 || target.Y > scrollHeight)
            { throw new IpcProtocolException("Invalid renderer fragment target ID or offset."); }
            bytes += Encoding.UTF8.GetByteCount(target.Id);
            if (bytes > MaxFragmentMetadataBytes)
            { throw new IpcProtocolException("Renderer fragment target metadata byte limit exceeded."); }
        }
        if (JsonSerializer.SerializeToUtf8Bytes(targets).Length > MaxFragmentMetadataBytes)
        { throw new IpcProtocolException("Renderer fragment target metadata byte limit exceeded."); }
    }

    /// <summary>Checks form/control counts, owner indexes, kinds, string limits, action URLs, geometry and the JSON budget.</summary>
    [SuppressMessage("Sonar", "S3776:Cognitive Complexity of methods should not be too high", Justification = "This is the bounded untrusted form-metadata validator; consolidating its ordered invariants preserves a single auditable protocol boundary.")]
    [SuppressMessage("Sonar", "S1192:String literals should not be duplicated", Justification = "The explicit form and attribute kind literals define the finite wire contract and remain local to its validator.")]
    public static void ValidateForms(IReadOnlyList<PageForm>? forms, IReadOnlyList<PageFormControl>? controls,
        int linkCount, double width, double height)
    {
        if (forms is null || controls is null || forms.Count > MaxForms || controls.Count > MaxFormControls)
        { throw new IpcProtocolException("Invalid renderer form metadata collection or count."); }
        foreach (var form in forms)
        {
            if (form is null || form.Action is null || form.Action.Length > MaxTextCharacters
                || form.Error is { Length: 0 or > MaxTextCharacters }
                || form.TargetError is { Length: 0 or > MaxTextCharacters }
                || form.TargetError is not null && form.OpenInNewTab)
            { throw new IpcProtocolException("Invalid renderer form fields or limits."); }
            if (form.Error is not null)
            {
                if (form.Action.Length != 0) { throw new IpcProtocolException("Unsupported renderer forms carry no action."); }
                continue;
            }
            var parsed = BrowserUrl.ParseResult(form.Action).Url;
            if (parsed?.Href != form.Action || parsed.Protocol is not ("http:" or "https:" or "file:" or "data:"))
            { throw new IpcProtocolException("Renderer form action must be a serialized absolute http, https, file or data URL."); }
        }
        var lastLink = 0;
        var patternCount = 0;
        var optionCount = 0;
        foreach (var control in controls)
        {
            if (control is null || control.Form < -1 || control.Form >= forms.Count
                || control.Kind is not ("text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "month" or "week" or "number" or "range" or "color" or "checkbox" or "radio" or "select" or "textarea" or "hidden" or "submit" or "reset" or "button" or "inert")
                || control.Name is null || control.Name.Length > MaxTextCharacters
                || control.Value is null || control.Value.Length > MaxTextCharacters
                || control.Label is null || control.Label.Length > MaxTextCharacters
                || control.Placeholder is null || control.Placeholder.Length > MaxTextCharacters
                || control.Kind is not ("submit" or "reset" or "button" or "inert") && control.Label.Length != 0
                || control.Kind is not ("checkbox" or "radio") && control.Checked
                || control.Multiple && control.Kind is not ("email" or "select")
                || control.FormNoValidate && control.Kind is not ("submit" or "button")
                || control.FormAction is { Length: > MaxTextCharacters }
                || control.FormActionError is { Length: 0 or > MaxTextCharacters }
                || (control.FormAction is not null || control.FormActionError is not null) && control.Kind is not ("submit" or "button")
                || control.FormActionError is not null && control.FormAction is not null
                || control.FormTargetError is { Length: 0 or > MaxTextCharacters }
                || (control.FormTargetOpenInNewTab is not null || control.FormTargetError is not null)
                    && control.Kind is not ("submit" or "button")
                || control.FormTargetError is not null && control.FormTargetOpenInNewTab is not null
                || control.FormAction is { } formAction && (BrowserUrl.ParseResult(formAction).Url is not { } actionUrl
                    || actionUrl.Href != formAction || actionUrl.Protocol is not ("http:" or "https:" or "file:" or "data:"))
                || control.Kind == "textarea" && (control.TextareaWrapColumns is < 0 or > 128
                    || (control.TextareaWrapColumns > 0) != control.TextareaWrapHard)
                || control.Kind != "textarea" && (control.TextareaWrapHard || control.TextareaWrapColumns != 0)
                || control.Kind == "select" && control.SelectRows is < 1 or > 12
                || control.Kind != "select" && control.SelectRows != 1
                || control.Kind == "email" && control.Value != FormEmail.Sanitize(control.Value, control.Multiple)
                || control.Placeholder.Length > 0 && control.Kind is not ("text" or "search" or "email" or "tel" or "url" or "password" or "number" or "textarea")
                || control.Kind != "textarea" && (control.Placeholder.Contains('\r') || control.Placeholder.Contains('\n'))
                || control.ReadOnly && control.Kind is not ("text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "month" or "week" or "number" or "color" or "textarea")
                || control.Required && control.Kind is not ("text" or "search" or "email" or "tel" or "url" or "password" or "date" or "time" or "month" or "week" or "number" or "color" or "checkbox" or "radio" or "select" or "textarea")
                || control.Minimum is { } minimum && !double.IsFinite(minimum)
                || control.Maximum is { } maximum && !double.IsFinite(maximum)
                || control.Step is { } step && (!double.IsFinite(step) || step <= 0)
                || control.Kind is not ("number" or "range" or "date" or "time" or "month" or "week") && (control.Minimum is not null || control.Maximum is not null
                    || control.Step is not null || control.StepAny)
                || (control.Kind is "number" or "range" or "date" or "time" or "month" or "week") && (control.StepAny ? control.Step is not null : control.Step is null)
                || control.Kind == "date" && (control.Minimum is { } dateMinimum && dateMinimum != Math.Truncate(dateMinimum)
                    || control.Maximum is { } dateMaximum && dateMaximum != Math.Truncate(dateMaximum))
                || control.Kind == "time" && (control.Minimum is { } timeMinimum && timeMinimum != Math.Truncate(timeMinimum)
                    || control.Maximum is { } timeMaximum && timeMaximum != Math.Truncate(timeMaximum))
                || control.Kind == "month" && (control.Minimum is { } monthMinimum && monthMinimum != Math.Truncate(monthMinimum)
                    || control.Maximum is { } monthMaximum && monthMaximum != Math.Truncate(monthMaximum))
                || control.Kind == "week" && (control.Minimum is { } weekMinimum && weekMinimum != Math.Truncate(weekMinimum)
                    || control.Maximum is { } weekMaximum && weekMaximum != Math.Truncate(weekMaximum))
                || control.Kind == "range" && (control.Minimum is null || control.Maximum is null
                    || control.Minimum > control.Maximum || control.MinLength != -1 || control.MaxLength != -1
                    || control.Required || control.ReadOnly || control.Pattern is not null)
                || control.Pattern is { Length: > MaxFormPatternCharacters }
                || control.Pattern is not null && control.Kind is not ("text" or "search" or "email" or "tel" or "url" or "password")
                || control.Pattern is { } pattern && !FormPattern.IsValid(pattern)
                || control.Pattern is not null && ++patternCount > MaxFormPatterns
                || control.MinLength < -1 || control.MinLength > MaxTextCharacters
                || control.MaxLength < -1 || (control.Kind is "date" or "time" or "month" or "week") && control.MaxLength != -1
                || control.BeforeLink < 0 || control.BeforeLink > linkCount
                || control.Kind is not ("text" or "search" or "email" or "tel" or "url" or "password" or "textarea") && control.MinLength != -1
                || control.Options is null || (long)optionCount + control.Options.Length > MaxSelectOptions
                || control.Kind != "select" && control.Options.Length != 0
                || control.Options.Any(option => option is null || option.Value is null || option.Label is null
                    || option.Value.Length > MaxTextCharacters || option.Label.Length > MaxTextCharacters)
                || !control.Multiple && control.Options.Count(option => option.Selected) > 1
                || control.Multiple && control.Kind is not ("email" or "select"))
            { throw new IpcProtocolException("Invalid renderer form control fields or limits."); }
            optionCount += control.Options.Length;
            if (control.Kind == "select" && control.Value != control.Options.FirstOrDefault(option => option.Selected && !option.Disabled)?.Value
                && !(control.Options.All(option => !option.Selected || option.Disabled) && control.Value.Length == 0))
            { throw new IpcProtocolException("Invalid renderer select value or selected option."); }
            if (control.Kind == "date" && control.Value.Length > 0 && !FormDate.IsValid(control.Value))
            { throw new IpcProtocolException("Invalid renderer date value."); }
            if (control.Kind == "time" && control.Value.Length > 0 && !FormTime.IsValid(control.Value))
            { throw new IpcProtocolException("Invalid renderer time value."); }
            if (control.Kind == "month" && control.Value.Length > 0 && !FormMonth.IsValid(control.Value))
            { throw new IpcProtocolException("Invalid renderer month value."); }
            if (control.Kind == "week" && control.Value.Length > 0 && !FormWeek.IsValid(control.Value))
            { throw new IpcProtocolException("Invalid renderer week value."); }
            if (control.Kind == "color" && !FormColor.IsValid(control.Value))
            { throw new IpcProtocolException("Invalid renderer color value."); }
            if (control.Kind == "range" && (!FormNumber.TryParse(control.Value, out var rangeValue)
                || rangeValue < control.Minimum!.Value || rangeValue > control.Maximum!.Value
                || !control.StepAny && !FormNumber.IsStepAligned(rangeValue, control.Minimum.Value, control.Step!.Value)))
            { throw new IpcProtocolException("Invalid renderer range value or step alignment."); }
            if (control.Rect is not { } rect) { continue; }
            if (control.Kind == "hidden" || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y)
                || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height)
                || rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
                || rect.X + rect.Width > width || rect.Y + rect.Height > height || control.BeforeLink < lastLink)
            { throw new IpcProtocolException("Invalid renderer form control rectangle or order."); }
            lastLink = control.BeforeLink;
        }
        if (JsonSerializer.SerializeToUtf8Bytes(forms).Length + (long)JsonSerializer.SerializeToUtf8Bytes(controls).Length
            > MaxFormMetadataBytes)
        { throw new IpcProtocolException("Renderer form metadata byte limit exceeded."); }
    }
    /// <summary>Checks the bounded visible text-selection snapshot and its viewport geometry.</summary>
    public static void ValidateTextTargets(IReadOnlyList<PageTextTarget>? targets, double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0
            || targets is null || targets.Count > MaxTextTargets)
        { throw new IpcProtocolException("Invalid renderer text target viewport or count."); }
        long textBytes = 0;
        foreach (var target in targets)
        {
            var rect = target?.Rect;
            if (target is null || string.IsNullOrEmpty(target.Text) || target.Text.Length > MaxTextCharacters
                || rect is null || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y)
                || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height)
                || rect.X < 0 || rect.Y < 0 || rect.Width <= 0 || rect.Height <= 0
                || rect.X + rect.Width > width || rect.Y + rect.Height > height)
            { throw new IpcProtocolException("Invalid renderer text target fields or rectangle."); }
            textBytes += Encoding.UTF8.GetByteCount(target.Text);
            if (textBytes > MaxTextMetadataBytes)
            { throw new IpcProtocolException("Renderer text target byte limit exceeded."); }
        }
        if (JsonSerializer.SerializeToUtf8Bytes(targets).Length > MaxTextMetadataBytes)
        { throw new IpcProtocolException("Renderer text target metadata byte limit exceeded."); }
    }
    /// <summary>Checks count, per-sheet characters, unique serialized absolute URLs and the UTF-8 JSON wire budget.</summary>
    public static void ValidateStylesheets(IReadOnlyList<PageStylesheet>? stylesheets)
    {
        if (stylesheets is null || stylesheets.Count > MaxStylesheets)
        { throw new IpcProtocolException("Invalid linked stylesheet collection or count."); }
        var urls = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sheet in stylesheets)
        {
            if (sheet is null || string.IsNullOrEmpty(sheet.Url) || sheet.Url.Length > MaxTextCharacters
                || sheet.Css is null || sheet.Css.Length > MaxStylesheetCharacters)
            { throw new IpcProtocolException("Invalid linked stylesheet URL or character limit."); }
            if (BrowserUrl.ParseResult(sheet.Url).Url?.Href != sheet.Url)
            { throw new IpcProtocolException("Linked stylesheet URL must be serialized and absolute."); }
            if (!urls.Add(sheet.Url)) { throw new IpcProtocolException("Linked stylesheet URLs must be unique."); }
        }
        if (JsonSerializer.SerializeToUtf8Bytes(stylesheets).Length > MaxStylesheetBytes)
        { throw new IpcProtocolException("Linked stylesheet byte limit exceeded."); }
    }
    public static (int Width, int Height) Dimensions(double width, double height, double scale)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(scale)
            || width <= 0 || height <= 0 || scale <= 0 || width > 1e9 || height > MaxScrollHeight || scale > 1e9)
        {
            throw new IpcProtocolException("Invalid renderer viewport.");
        }
        var physicalWidth = Math.Ceiling(width * scale);
        var physicalHeight = Math.Ceiling(height * scale);
        if (physicalWidth < 1 || physicalHeight < 1 || physicalWidth > int.MaxValue || physicalHeight > int.MaxValue
            || physicalWidth * physicalHeight > MaxPixels)
        {
            throw new IpcProtocolException("Renderer viewport exceeds the framebuffer limit.");
        }
        return ((int)physicalWidth, (int)physicalHeight);
    }
}
