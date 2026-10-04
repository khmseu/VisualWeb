using System.Collections.ObjectModel;

namespace VisualWeb.Engine.Dom;

/// <summary>An HTML-namespace element with ordered, case-insensitive attribute names.</summary>
/// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#interface-element">Element</see>.
/// Namespace-aware attributes and element-specific behavior are deferred.</remarks>
public sealed class DomElement : DomNode
{
    public const string HtmlNamespace = "http://www.w3.org/1999/xhtml";
    private readonly Dictionary<string, string> attributes = new(StringComparer.Ordinal);
    public string LocalName { get; }
    public string NamespaceUri => HtmlNamespace;
    public IReadOnlyDictionary<string, string> Attributes { get; }
    public override DomNodeType NodeType => DomNodeType.Element;
    public override string NodeName => string.Create(LocalName.Length, LocalName, (span, name) =>
    {
        for (var index = 0; index < name.Length; index++)
        {
            span[index] = name[index] is >= 'a' and <= 'z' ? (char)(name[index] - 32) : name[index];
        }
    });

    internal DomElement(DomDocument document, string name) : base(document)
    {
        LocalName = name;
        Attributes = new ReadOnlyDictionary<string, string>(attributes);
    }

    public string? GetAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return attributes.GetValueOrDefault(DomDocument.AsciiLower(name));
    }

    public void SetAttribute(string name, string value)
    {
        DomDocument.ValidateAttributeName(name);
        ArgumentNullException.ThrowIfNull(value);
        attributes[DomDocument.AsciiLower(name)] = value;
    }

    internal void SetAttributeFromParser(string name, string value) => attributes[name] = value;

    public void RemoveAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        attributes.Remove(DomDocument.AsciiLower(name));
    }
}
