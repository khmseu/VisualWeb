using System.Text;

namespace VisualWeb.Engine.Dom;

public abstract partial class DomNode
{
    /// <summary>Remove empty descendant Text nodes and merge adjacent Text siblings.</summary>
    /// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#dom-node-normalize">normalize</see>.
    /// The first nonempty Text identity survives; removed nodes retain their data and ownership.
    /// Observer and live-range reactions are deferred. Cancellation precedes all writes.</remarks>
    public void Normalize(CancellationToken cancellationToken = default)
        => Normalize(int.MaxValue, int.MaxValue, null, cancellationToken);

    internal void Normalize(int maxNodes, int maxTextCharacters, Action<string>? checkText, CancellationToken cancellationToken)
    {
        var pending = new Stack<DomNode>();
        var updates = new List<(DomText Text, string Data)>();
        var removals = new HashSet<DomNode>();
        var parents = new List<DomNode>();
        var count = 0;
        pending.Push(this);
        while (pending.TryPop(out var parent))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (parent.children.Count > maxNodes - count)
            { throw new DomException(DomError.NotSupported, "DOM normalization subtree limit exceeded."); }
            count += parent.children.Count;
            DomText? survivor = null;
            StringBuilder? merged = null;
            var removed = false;
            foreach (var child in parent.children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (child is DomText text)
                {
                    if (survivor is null && text.Length != 0)
                    {
                        if (text.Length > maxTextCharacters)
                        { throw new DomException(DomError.NotSupported, "DOM normalization text storage limit exceeded."); }
                        survivor = text;
                    }
                    else
                    {
                        if (survivor is not null)
                        {
                            var length = merged?.Length ?? survivor.Length;
                            if (text.Length > maxTextCharacters - length)
                            { throw new DomException(DomError.NotSupported, "DOM normalization text storage limit exceeded."); }
                            merged ??= new StringBuilder(survivor.Data);
                            merged.Append(text.Data);
                        }
                        removals.Add(text); removed = true;
                    }
                }
                else
                {
                    FinishRun();
                    if (child.children.Count != 0) { pending.Push(child); }
                }
            }
            FinishRun();
            if (removed) { parents.Add(parent); }

            void FinishRun()
            {
                if (survivor is not null)
                {
                    var data = merged?.ToString() ?? survivor.Data;
                    checkText?.Invoke(data);
                    if (merged is not null) { updates.Add((survivor, data)); }
                }
                survivor = null; merged = null;
            }
        }

        // No callbacks or cancellation points in the bounded commit: preflight covers every run.
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var (text, data) in updates) { text.Data = data; }
        foreach (var parent in parents)
        {
            parent.children.RemoveAll(child =>
            {
                if (!removals.Contains(child)) { return false; }
                child.ParentNode = null;
                return true;
            });
        }
    }
}
