namespace VisualWeb.Engine.Dom;

/// <summary>Text or comment data; changing data has no observer/event side effects yet.</summary>
/// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#interface-characterdata">CharacterData</see>.</remarks>
public abstract class DomCharacterData : DomNode
{
    private string data;
    protected DomCharacterData(DomDocument document, string data) : base(document)
    {
        ArgumentNullException.ThrowIfNull(data);
        this.data = data;
    }

    public string Data
    {
        get => data;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            data = value;
        }
    }

    public override string? TextContent { get => data; set => data = value ?? ""; }
}

public sealed class DomText : DomCharacterData
{
    internal DomText(DomDocument document, string data) : base(document, data) { }
    public override DomNodeType NodeType => DomNodeType.Text;
    public override string NodeName => "#text";
}

public sealed class DomComment : DomCharacterData
{
    internal DomComment(DomDocument document, string data) : base(document, data) { }
    public override DomNodeType NodeType => DomNodeType.Comment;
    public override string NodeName => "#comment";
}

public sealed class DomProcessingInstruction : DomCharacterData
{
    internal DomProcessingInstruction(DomDocument document, string target, string data) : base(document, data) => Target = target;
    public string Target { get; }
    public override DomNodeType NodeType => DomNodeType.ProcessingInstruction;
    public override string NodeName => Target;
}

public sealed class DomDocumentType : DomNode
{
    internal DomDocumentType(DomDocument document, string name, string publicId, string systemId) : base(document)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(publicId);
        ArgumentNullException.ThrowIfNull(systemId);
        Name = name;
        PublicId = publicId;
        SystemId = systemId;
    }

    public string Name { get; }
    public string PublicId { get; }
    public string SystemId { get; }
    public override DomNodeType NodeType => DomNodeType.DocumentType;
    public override string NodeName => Name;
}
