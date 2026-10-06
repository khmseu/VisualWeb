namespace VisualWeb.Engine.Dom;

public abstract partial class DomNode
{
    /// <summary>Compare node payloads, unordered attributes and ordered descendant trees.</summary>
    /// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#dom-node-isequalnode">isEqualNode</see>
    /// and <see href="https://dom.spec.whatwg.org/#concept-node-equals">node equality</see>.
    /// Identity, owner document, parent, listeners and document mode do not affect equality.</remarks>
    public bool IsEqualNode(DomNode? other, CancellationToken cancellationToken = default)
        => IsEqualNode(other, int.MaxValue, null, cancellationToken);

    internal bool IsEqualNode(DomNode? other, int maxNodes, Action<DomNode>? checkNode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (other is null) { return false; }
        CheckTree(this); CheckTree(other);
        var pairs = new Stack<(DomNode Left, DomNode Right)>();
        pairs.Push((this, other));
        while (pairs.TryPop(out var pair))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (left, right) = pair;
            if (left.GetType() != right.GetType() || left.children.Count != right.children.Count) { return false; }
            var equal = left switch
            {
                DomElement element => EqualElement(element, (DomElement)right),
                DomProcessingInstruction instruction => instruction.Target == ((DomProcessingInstruction)right).Target
                    && instruction.Data == ((DomProcessingInstruction)right).Data,
                DomCharacterData data => data.Data == ((DomCharacterData)right).Data,
                DomDocumentType doctype => doctype.Name == ((DomDocumentType)right).Name
                    && doctype.PublicId == ((DomDocumentType)right).PublicId && doctype.SystemId == ((DomDocumentType)right).SystemId,
                DomDocument or DomDocumentFragment => true,
                _ => throw new DomException(DomError.NotSupported, "Unsupported node equality interface.")
            };
            if (!equal) { return false; }
            for (var i = left.children.Count - 1; i >= 0; i--) { pairs.Push((left.children[i], right.children[i])); }
        }
        return true;

        void CheckTree(DomNode root)
        {
            var pending = new Stack<DomNode>(); pending.Push(root);
            var count = 0;
            while (pending.TryPop(out var node))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++count > maxNodes || node.children.Count > maxNodes - count - pending.Count)
                { throw new DomException(DomError.NotSupported, "DOM equality node scan limit exceeded."); }
                checkNode?.Invoke(node);
                for (var i = node.children.Count - 1; i >= 0; i--) { pending.Push(node.children[i]); }
            }
        }

        bool EqualElement(DomElement left, DomElement right)
        {
            if (left.NamespaceUri != right.NamespaceUri || left.LocalName != right.LocalName
                || left.Attributes.Count != right.Attributes.Count) { return false; }
            foreach (var attribute in left.Attributes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!right.Attributes.TryGetValue(attribute.Key, out var value) || attribute.Value != value) { return false; }
            }
            return true;
        }
    }
}
