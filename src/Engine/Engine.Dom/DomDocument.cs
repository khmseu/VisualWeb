using System.Xml;

namespace VisualWeb.Engine.Dom;

/// <summary>An HTML document and factory for its renderer-local node objects.</summary>
/// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#interface-document">Document</see>.
/// HTML-only element and attribute names; namespace APIs are deferred.</remarks>
public sealed class DomDocument() : DomNode(null)
{
    public override DomNodeType NodeType => DomNodeType.Document;
    public override string NodeName => "#document";
    public DomElement? DocumentElement => ChildNodes.OfType<DomElement>().FirstOrDefault();
    public DomDocumentType? Doctype => ChildNodes.OfType<DomDocumentType>().FirstOrDefault();
    public DomDocumentMode Mode { get; set; } = DomDocumentMode.NoQuirks;
    public DomElement? Head => DocumentElement?.ChildNodes.OfType<DomElement>().FirstOrDefault(e => e.LocalName == "head");
    public DomElement? Body => DocumentElement?.ChildNodes.OfType<DomElement>().FirstOrDefault(e => e.LocalName == "body");

    /// <summary>The first HTML title's text with ASCII whitespace stripped and collapsed.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/dom.html#document.title">document.title</see>.
    /// An absent title is created in the head; without a head the setter does nothing.</remarks>
    public string Title
    {
        get => string.Join(" ", (Descendants().OfType<DomElement>().FirstOrDefault(e => e.LocalName == "title")?.TextContent ?? "")
            .Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries));
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var title = Descendants().OfType<DomElement>().FirstOrDefault(e => e.LocalName == "title");
            if (title is null)
            {
                if (Head is not { } head) { return; }
                title = CreateElement("title");
                head.AppendChild(title);
            }
            title.TextContent = value;
        }
    }

    public DomElement CreateElement(string localName)
    {
        ValidateName(localName);
        return new(this, AsciiLower(localName));
    }

    public DomText CreateTextNode(string data) => new(this, data);
    public DomComment CreateComment(string data) => new(this, data);
    public DomProcessingInstruction CreateProcessingInstruction(string target, string data)
    {
        ArgumentNullException.ThrowIfNull(target);
        try
        {
            XmlConvert.VerifyName(target);
        }
        catch (XmlException exception)
        {
            throw new DomException(DomError.InvalidCharacter, exception.Message);
        }
        ArgumentNullException.ThrowIfNull(data);
        if (data.Contains("?>", StringComparison.Ordinal))
        {
            throw new DomException(DomError.InvalidCharacter, "Processing instruction data cannot contain '?>'.");
        }

        return new(this, target, data);
    }
    public DomDocumentFragment CreateDocumentFragment() => new(this);
    public DomDocumentType CreateDocumentType(string name, string publicId = "", string systemId = "")
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Any(c => c is '\0' or '\t' or '\n' or '\f' or '\r' or ' ' or '>'))
        {
            throw new DomException(DomError.InvalidCharacter, "Name is not a valid doctype name.");
        }

        return new(this, name, publicId, systemId);
    }

    public DomNode AdoptNode(DomNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node is DomDocument)
        {
            throw new DomException(DomError.NotSupported, "Documents cannot be adopted.");
        }

        node.Adopt(this);
        return node;
    }

    public DomElement? GetElementById(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.Length == 0 ? null : Descendants().OfType<DomElement>().FirstOrDefault(e => e.GetAttribute("id") == id);
    }

    internal static void ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var valid = name.Length > 0 && (char.IsAsciiLetter(name[0])
            ? !name.Any(c => c is '\0' or '\t' or '\n' or '\f' or '\r' or ' ' or '/' or '>')
            : (name[0] is ':' or '_' or >= '\u0080') && name.Skip(1).All(c =>
                char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or ':' or '_' or >= '\u0080'));
        if (!valid)
        {
            throw new DomException(DomError.InvalidCharacter, "Name is not a valid HTML local name.");
        }
    }

    internal static void ValidateAttributeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || name.Any(c => c is '\0' or '\t' or '\n' or '\f' or '\r' or ' ' or '/' or '=' or '>'))
        {
            throw new DomException(DomError.InvalidCharacter, "Name is not a valid attribute local name.");
        }
    }

    internal static string AsciiLower(string value) =>
        string.Create(value.Length, value, (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                span[index] = source[index] is >= 'A' and <= 'Z' ? (char)(source[index] + 32) : source[index];
            }
        });
}

public enum DomDocumentMode { NoQuirks, LimitedQuirks, Quirks }

public sealed class DomDocumentFragment : DomNode
{
    internal DomDocumentFragment(DomDocument document) : base(document) { }
    public override DomNodeType NodeType => DomNodeType.DocumentFragment;
    public override string NodeName => "#document-fragment";
}
