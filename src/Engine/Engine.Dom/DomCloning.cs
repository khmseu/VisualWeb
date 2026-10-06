namespace VisualWeb.Engine.Dom;

public abstract partial class DomNode
{
    /// <summary>Creates a detached copy of this node, optionally including its descendants.</summary>
    /// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#dom-node-clonenode">cloneNode</see>.
    /// Event listeners and parent links are not copied. Document cloning preserves its mode.</remarks>
    public DomNode CloneNode(bool deep = false, CancellationToken cancellationToken = default)
        => CloneNode(deep, int.MaxValue, int.MaxValue, int.MaxValue, static _ => { }, cancellationToken);

    internal DomNode CloneNode(bool deep, int maxNodes, int maxTextCharacters, int maxAttributes, Action<string> checkText,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkText);
        var sources = new List<DomNode>();
        var pending = new Stack<DomNode>();
        pending.Push(this);
        while (pending.TryPop(out var source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sources.Count == maxNodes) { throw new InvalidOperationException("DOM clone traversal limit exceeded."); }
            sources.Add(source);
            ValidatePayload(source);
            if (!deep) { continue; }
            if (pending.Count + (long)source.ChildNodes.Count + sources.Count > maxNodes)
            { throw new InvalidOperationException("DOM clone traversal limit exceeded."); }
            for (var index = source.ChildNodes.Count - 1; index >= 0; index--)
            { pending.Push(source.ChildNodes[index]); }
        }

        var clones = new Dictionary<DomNode, DomNode>(sources.Count);
        var cloneOwner = this is DomDocument ? (DomDocument)CloneShallow(this, null) : NodeDocument;
        if (this is DomDocument) { clones.Add(this, cloneOwner); }
        foreach (var source in sources)
        {
            if (clones.ContainsKey(source)) { continue; }
            cancellationToken.ThrowIfCancellationRequested();
            clones.Add(source, CloneShallow(source, cloneOwner));
        }
        if (deep)
        {
            foreach (var source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var child in source.ChildNodes)
                { clones[source].AppendClonedChild(clones[child]); }
            }
        }
        return clones[this];

        void ValidatePayload(DomNode source)
        {
            switch (source)
            {
                case DomElement element:
                    if (element.Attributes.Count > maxAttributes)
                    { throw new InvalidOperationException("DOM clone attribute count limit exceeded."); }
                    long stored = 0;
                    foreach (var attribute in element.Attributes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        stored += attribute.Key.Length + (long)attribute.Value.Length;
                        if (stored > maxTextCharacters)
                        { throw new InvalidOperationException("DOM clone attribute storage limit exceeded."); }
                        checkText(attribute.Key); checkText(attribute.Value);
                    }
                    checkText(element.LocalName);
                    break;
                case DomProcessingInstruction instruction:
                    checkText(instruction.Target); checkText(instruction.Data);
                    break;
                case DomCharacterData data:
                    checkText(data.Data);
                    break;
                case DomDocumentType doctype:
                    checkText(doctype.Name); checkText(doctype.PublicId); checkText(doctype.SystemId);
                    break;
            }
        }
    }

    private static DomNode CloneShallow(DomNode source, DomDocument? owner)
    {
        return source switch
        {
            DomDocument document => new DomDocument { Mode = document.Mode },
            DomDocumentFragment => new DomDocumentFragment(owner!),
            DomElement element => CloneElement(element, owner!),
            DomText text => new DomText(owner!, text.Data),
            DomComment comment => new DomComment(owner!, comment.Data),
            DomProcessingInstruction instruction =>
                new DomProcessingInstruction(owner!, instruction.Target, instruction.Data),
            DomDocumentType doctype =>
                new DomDocumentType(owner!, doctype.Name, doctype.PublicId, doctype.SystemId),
            _ => throw new InvalidOperationException("Unsupported DOM node type for cloning.")
        };
    }

    private static DomElement CloneElement(DomElement source, DomDocument owner)
    {
        var clone = new DomElement(owner, source.LocalName);
        foreach (var attribute in source.Attributes) { clone.SetAttributeFromParser(attribute.Key, attribute.Value); }
        return clone;
    }

    private void AppendClonedChild(DomNode child)
    {
        children.Add(child);
        child.ParentNode = this;
    }
}
