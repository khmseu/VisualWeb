using System.Text;
using VisualWeb.Engine.Dom;

namespace VisualWeb.Engine.Html;

/// <summary>Serializes the supported HTML-only DOM fragment subset.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#html-fragment-serialisation-algorithm"/>
/// and <see href="https://html.spec.whatwg.org/multipage/parsing.html#the-innerhtml-property:dom-element-innerhtml"/>
/// <see href="https://html.spec.whatwg.org/multipage/parsing.html#the-innerhtml-property:dom-element-innerhtml-2"/>
/// and <see href="https://html.spec.whatwg.org/multipage/parsing.html#the-innerhtml-property:dom-element-innerhtml-3"/>
/// (local ID <c>html</c>). Serialized output is raw markup, not sanitized markup.</remarks>
public static class HtmlFragmentSerializer
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.Ordinal)
    {
        "area", "base", "basefont", "bgsound", "br", "col", "embed", "hr", "img", "input", "keygen",
        "link", "meta", "param", "source", "track", "wbr"
    };

    private static readonly HashSet<string> RawTextElements = new(StringComparer.Ordinal)
    {
        "iframe", "noembed", "noframes", "plaintext", "script", "style", "xmp"
    };

    private static readonly HashSet<string> UnsupportedElements = new(StringComparer.Ordinal)
    {
        "frame", "frameset", "math", "svg"
    };

    /// <summary>Serializes an element's children, using template content when applicable.</summary>
    /// <param name="element">The HTML element whose children are serialized.</param>
    /// <param name="maxNodes">Maximum visited descendant nodes.</param>
    /// <param name="maxOutputCharacters">Maximum UTF-16 output length.</param>
    /// <param name="maxAttributes">Maximum attributes on any serialized element.</param>
    /// <param name="cancellationToken">Cancellation checked during traversal and escaping.</param>
    /// <returns>Raw serialized markup; callers must not treat it as sanitized.</returns>
    /// <exception cref="UnsupportedHtmlException">The element or child tree requires an unsupported algorithm.</exception>
    /// <exception cref="HtmlLimitException">A serialization budget is exceeded.</exception>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/parsing.html#html-fragment-serialisation-algorithm"/>
    /// and <see href="https://html.spec.whatwg.org/multipage/parsing.html#the-innerhtml-property:dom-element-innerhtml"/>
    /// <see href="https://html.spec.whatwg.org/multipage/parsing.html#the-innerhtml-property:dom-element-innerhtml-2"/>
    /// and <see href="https://html.spec.whatwg.org/multipage/parsing.html#the-innerhtml-property:dom-element-innerhtml-3"/>
    /// (local ID <c>html</c>).</remarks>
    public static string SerializeChildren(DomElement element, int maxNodes = 8192, int maxOutputCharacters = 65536,
        int maxAttributes = 128, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(element);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxOutputCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttributes);
        EnsureSupported(element);

        var serializer = new Serializer(maxNodes, maxOutputCharacters, maxAttributes, cancellationToken);
        var children = element is DomTemplateElement template ? template.Content.ChildNodes : element.ChildNodes;
        serializer.Children(children);
        return serializer.Output.ToString();
    }

    private static void EnsureSupported(DomElement element)
    {
        if (element.NamespaceUri != DomElement.HtmlNamespace || UnsupportedElements.Contains(element.LocalName))
        {
            throw new UnsupportedHtmlException($"HTML fragment serialization for <{element.LocalName}> is not implemented.");
        }
    }

    private sealed class Serializer(int maxNodes, int maxOutputCharacters, int maxAttributes, CancellationToken cancellationToken)
    {
        internal StringBuilder Output { get; } = new();
        private int nodes;

        internal void Children(IReadOnlyList<DomNode> children)
        {
            var pending = new Stack<(DomNode Node, bool RawTextParent, bool Closing)>();
            for (var index = children.Count - 1; index >= 0; index--)
            {
                pending.Push((children[index], false, false));
            }

            while (pending.TryPop(out var entry))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Closing)
                {
                    var closingElement = (DomElement)entry.Node;
                    Append("</"); Append(closingElement.LocalName); Append(">");
                    continue;
                }

                if (++nodes > maxNodes) { throw new HtmlLimitException("HTML serialization node count limit exceeded."); }
                switch (entry.Node)
                {
                    case DomText text:
                        AppendText(text.Data, entry.RawTextParent);
                        break;
                    case DomComment comment:
                        Append("<!--"); Append(comment.Data); Append("-->");
                        break;
                    case DomProcessingInstruction instruction:
                        Append("<?"); Append(instruction.Target);
                        // The HTML fragment serialization algorithm inserts this space even for empty data.
                        Append(" "); Append(instruction.Data);
                        Append("?>");
                        break;
                    case DomElement element:
                        Element(element, pending);
                        break;
                    default:
                        throw new UnsupportedHtmlException($"HTML fragment serialization for {entry.Node.NodeType} nodes is not implemented.");
                }
            }
        }

        private void Element(DomElement element, Stack<(DomNode Node, bool RawTextParent, bool Closing)> pending)
        {
            EnsureSupported(element);
            if (element.Attributes.Count > maxAttributes)
            {
                throw new HtmlLimitException("HTML serialization attribute count limit exceeded.");
            }

            Append("<"); Append(element.LocalName);
            foreach (var (name, value) in element.Attributes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Append(" "); Append(name); Append("=\""); AppendAttribute(value); Append("\"");
            }
            Append(">");
            if (VoidElements.Contains(element.LocalName)) { return; }

            var children = element is DomTemplateElement template ? template.Content.ChildNodes : element.ChildNodes;
            pending.Push((element, false, true));
            var raw = RawTextElements.Contains(element.LocalName);
            for (var index = children.Count - 1; index >= 0; index--)
            {
                pending.Push((children[index], raw, false));
            }
        }

        private void AppendText(string value, bool raw)
        {
            if (raw)
            {
                Append(value);
                return;
            }

            foreach (var character in value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Append(character switch
                {
                    '&' => "&amp;",
                    '\u00a0' => "&nbsp;",
                    '<' => "&lt;",
                    '>' => "&gt;",
                    _ => character.ToString()
                });
            }
        }

        private void AppendAttribute(string value)
        {
            foreach (var character in value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Append(character switch
                {
                    '&' => "&amp;",
                    '\u00a0' => "&nbsp;",
                    '"' => "&quot;",
                    _ => character.ToString()
                });
            }
        }

        private void Append(string value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.Length > maxOutputCharacters - Output.Length)
            {
                throw new HtmlLimitException("HTML serialization output limit exceeded.");
            }
            Output.Append(value);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
