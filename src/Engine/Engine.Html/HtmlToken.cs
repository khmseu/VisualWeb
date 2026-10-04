namespace VisualWeb.Engine.Html;

/// <summary>HTML tokenizer output, before tree construction.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#tokenization">tokenization</see>.</remarks>
public abstract record HtmlToken;
public sealed record HtmlCharacters(string Data) : HtmlToken;
public sealed record HtmlComment(string Data) : HtmlToken;
public sealed record HtmlProcessingInstruction(string Target, string Data) : HtmlToken;
public sealed record HtmlDoctype(string? Name, string? PublicId, string? SystemId, bool ForceQuirks) : HtmlToken;
public sealed record HtmlTag(string Name, IReadOnlyDictionary<string, string> Attributes, bool IsEndTag, bool SelfClosing) : HtmlToken;
public sealed record HtmlEndOfFile : HtmlToken;
public sealed record HtmlParseError(string Code, int Offset);
public enum HtmlTextMode { Data, Rcdata, Rawtext, ScriptData, Plaintext, Cdata }

/// <summary>Implementation safety limits, not limits imposed by HTML.</summary>
public sealed record HtmlParserOptions
{
    public int MaxInputCharacters { get; init; } = 16 * 1024 * 1024;
    public int MaxNodes { get; init; } = 1_000_000;
    public int MaxDepth { get; init; } = 512;
    public int MaxErrors { get; init; } = 10_000;
    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxInputCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxNodes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaxErrors);
    }
}

public sealed class HtmlLimitException(string message) : InvalidOperationException(message);
public sealed class UnsupportedHtmlException(string message) : NotSupportedException(message);
