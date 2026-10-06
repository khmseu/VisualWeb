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

    /// <summary>Number of UTF-16 code units, not Unicode scalar values.</summary>
    /// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#dom-characterdata-length">length</see>.</remarks>
    public int Length => data.Length;

    /// <summary>Copy code units, clamping count at the end of the data.</summary>
    /// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#dom-characterdata-substringdata">substringData</see>.</remarks>
    public string SubstringData(uint offset, uint count)
    {
        var length = CheckedCount(offset, count);
        return data.Substring((int)offset, length);
    }

    /// <summary>Replace code units after validating the offset, without observer/range reactions.</summary>
    /// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#concept-cd-replace">replace data</see>.</remarks>
    public void ReplaceData(uint offset, uint count, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var length = CheckedCount(offset, count);
        data = string.Concat(data.AsSpan(0, (int)offset), value.AsSpan(), data.AsSpan((int)offset + length));
    }

    private int CheckedCount(uint offset, uint count)
    {
        if (offset > data.Length) { throw new DomException(DomError.IndexSize, "CharacterData offset exceeds its length."); }
        return (int)Math.Min(count, (uint)data.Length - offset);
    }
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
