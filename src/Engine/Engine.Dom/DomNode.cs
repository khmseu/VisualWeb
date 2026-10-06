using System.Collections.ObjectModel;

namespace VisualWeb.Engine.Dom;

/// <summary>Renderer-local nodes with checked, adopting tree mutations.</summary>
/// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#concept-node-pre-insert">pre-insert</see>
/// and <see href="https://dom.spec.whatwg.org/#concept-node-adopt">adopt</see>.
/// No events, live ranges, observers, shadow DOM or custom-element reactions yet.</remarks>
public abstract partial class DomNode
{
    private readonly List<DomNode> children = [];
    public IReadOnlyList<DomNode> ChildNodes { get; }
    public DomNode? ParentNode { get; private set; }
    public DomDocument? OwnerDocument { get; private set; }
    public abstract DomNodeType NodeType { get; }
    public abstract string NodeName { get; }
    public DomNode? FirstChild => children.FirstOrDefault();
    public DomNode? LastChild => children.LastOrDefault();
    public DomNode? PreviousSibling => ParentNode is { } parent && parent.children.IndexOf(this) is > 0 and var index
        ? parent.children[index - 1] : null;
    public DomNode? NextSibling => ParentNode is { } parent && parent.children.IndexOf(this) + 1 is var index
        && index < parent.children.Count ? parent.children[index] : null;
    public bool IsConnected
    {
        get
        {
            var root = this;
            while (root.ParentNode is { } parent)
            {
                root = parent;
            }

            return root is DomDocument;
        }
    }

    protected DomNode(DomDocument? ownerDocument)
    {
        OwnerDocument = ownerDocument;
        ChildNodes = new ReadOnlyCollection<DomNode>(children);
    }

    /// <summary>Descendant text, excluding comments; null for Document and DocumentType.</summary>
    /// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#dom-node-textcontent">textContent</see>.</remarks>
    public virtual string? TextContent
    {
        get => this is DomDocument or DomDocumentType ? null
            : string.Concat(Descendants().OfType<DomText>().Select(text => text.Data));
        set
        {
            if (this is DomDocument or DomDocumentType)
            {
                return;
            }

            foreach (var child in children.ToArray())
            {
                RemoveChild(child);
            }

            if (!string.IsNullOrEmpty(value))
            {
                AppendChild(NodeDocument.CreateTextNode(value));
            }
        }
    }

    public DomNode AppendChild(DomNode node) => InsertBefore(node, null);

    public DomNode InsertBefore(DomNode node, DomNode? referenceChild)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (referenceChild is not null && referenceChild.ParentNode != this)
        {
            throw new DomException(DomError.NotFound, "Reference child is not a child of this node.");
        }

        if (referenceChild == node)
        {
            referenceChild = node.NextSibling;
        }

        ValidateInsertion(node, referenceChild, null);
        InsertValidated(node, referenceChild);
        return node;
    }

    public DomNode ReplaceChild(DomNode node, DomNode child)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(child);
        if (child.ParentNode != this)
        {
            throw new DomException(DomError.NotFound, "Replaced node is not a child of this node.");
        }

        if (node == child)
        {
            return child;
        }

        var reference = child.NextSibling;
        if (reference == node)
        {
            reference = node.NextSibling;
        }

        ValidateInsertion(node, reference, child);
        RemoveChild(child);
        InsertValidated(node, reference);
        return child;
    }

    public DomNode RemoveChild(DomNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child.ParentNode != this)
        {
            throw new DomException(DomError.NotFound, "Removed node is not a child of this node.");
        }

        children.Remove(child);
        child.ParentNode = null;
        return child;
    }

    public IEnumerable<DomNode> Descendants()
    {
        var pending = new Stack<DomNode>(children.AsEnumerable().Reverse());
        while (pending.TryPop(out var node))
        {
            yield return node;
            for (var index = node.children.Count - 1; index >= 0; index--)
            {
                pending.Push(node.children[index]);
            }
        }
    }

    internal DomDocument NodeDocument => this as DomDocument ?? OwnerDocument
        ?? throw new InvalidOperationException("Node has no document.");

    internal void Adopt(DomDocument document)
    {
        ParentNode?.RemoveChild(this);
        OwnerDocument = document;
        foreach (var descendant in Descendants())
        {
            descendant.OwnerDocument = document;
        }
    }

    private void ValidateInsertion(DomNode node, DomNode? reference, DomNode? replaced)
    {
        if (this is not (DomDocument or DomDocumentFragment or DomElement)
            || node is not (DomDocumentFragment or DomDocumentType or DomElement or DomCharacterData))
        {
            throw Hierarchy("This node type cannot participate in this insertion.");
        }

        for (DomNode? ancestor = this; ancestor is not null; ancestor = ancestor.ParentNode)
        {
            if (ancestor == node)
            {
                throw Hierarchy("Insertion would create a cycle.");
            }
        }

        var inserted = node is DomDocumentFragment ? node.children.ToArray() : [node];
        if (this is not DomDocument)
        {
            if (inserted.Any(n => n is DomDocumentType))
            {
                throw Hierarchy("A doctype may only be inserted in a document.");
            }

            return;
        }

        var final = children.Where(n => n != replaced && !inserted.Contains(n)).ToList();
        var index = reference is null ? final.Count : final.IndexOf(reference);
        final.InsertRange(index, inserted);
        if (final.Any(n => n is DomText)
            || final.Count(n => n is DomElement) > 1 || final.Count(n => n is DomDocumentType) > 1
            || final.FindIndex(n => n is DomDocumentType) is >= 0 and var doctype
                && final.FindIndex(n => n is DomElement) is >= 0 and var element && doctype > element)
        {
            throw Hierarchy("A document allows one element after one optional doctype, and no text children.");
        }
    }

    private void InsertValidated(DomNode node, DomNode? reference)
    {
        var inserted = node is DomDocumentFragment ? node.children.ToArray() : [node];
        if (node is DomDocumentFragment)
        {
            node.Adopt(NodeDocument);
        }

        foreach (var child in inserted)
        {
            child.Adopt(NodeDocument);
            var index = reference is null ? children.Count : children.IndexOf(reference);
            children.Insert(index, child);
            child.ParentNode = this;
        }
    }

    private static DomException Hierarchy(string message) => new(DomError.HierarchyRequest, message);
}

public enum DomNodeType { Element = 1, Text = 3, ProcessingInstruction = 7, Comment = 8, Document = 9, DocumentType = 10, DocumentFragment = 11 }
public enum DomError { HierarchyRequest, NotFound, InvalidCharacter, NotSupported, IndexSize }
public sealed class DomException(DomError error, string message) : InvalidOperationException(message)
{
    public DomError Error { get; } = error;
}
