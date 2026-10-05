using System.Globalization;
using System.Runtime.CompilerServices;
using VisualWeb.Engine.Dom;

namespace VisualWeb.Engine.Scripting;

/// <summary>Private primitive-only live DOM callback and branded JavaScript facade bootstrap.</summary>
/// <remarks>Specs: dom, html, webidl;
/// <see href="https://dom.spec.whatwg.org/#dom-node-textcontent">textContent</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-nonelementparentnode-getelementbyid">ID lookup</see>,
/// <see href="https://webidl.spec.whatwg.org/#es-DOMString">DOMString conversion</see>.</remarks>
internal sealed class DomBindings : IDisposable
{
    internal const int MaxHandles = 1024;
    internal const int MaxCalls = 4096;
    internal const int MaxNodes = 8192;
    internal const int MaxTextCharacters = 65536;
    private static readonly ConditionalWeakTable<DomDocument, DomBindings> Owners = new();
    private readonly DomDocument document;
    private readonly Dictionary<DomElement, int> identities = [];
    private readonly List<DomElement> elements = [];
    private int calls;
    private int characters;

    internal DomBindings(DomDocument document)
    {
        this.document = document;
        lock (Owners)
        {
            if (Owners.TryGetValue(document, out _)) { throw new InvalidOperationException("Document already belongs to a V8 host."); }
            Owners.Add(document, this);
        }
    }

    internal void Begin() { calls = 0; characters = 0; }

    internal string Invoke(string operation, int handle, string value)
    {
        try
        {
            if (++calls > MaxCalls) { throw new ScriptLimitException("DOM callback count limit exceeded."); }
            Budget(value);
            var result = operation switch
            {
                "lookup" => Lookup(value),
                "title-get" => Title(),
                "title-set" => SetTitle(value),
                "text-get" => Text(Element(handle)),
                "text-set" => SetText(Element(handle), value),
                _ => throw new InvalidOperationException("Unknown private DOM operation.")
            };
            Budget(result);
            return "ok:" + result;
        }
        catch (Exception exception) when (exception is ScriptLimitException or DomException or InvalidOperationException)
        {
            // A string error crosses the bridge, never a CLR exception object or native node.
            return "error:" + exception.Message;
        }
    }

    private void Budget(string value)
    {
        if (value.Length > MaxTextCharacters || (characters += value.Length) > 4 * MaxTextCharacters)
        { throw new ScriptLimitException("DOM callback text limit exceeded."); }
    }
    private IEnumerable<DomElement> Descendants()
        => Traverse(document).OfType<DomElement>();

    private static IEnumerable<DomNode> Traverse(DomNode root)
    {
        var count = 0;
        var pending = new Stack<DomNode>();
        Push(root);
        while (pending.TryPop(out var node))
        {
            if (++count > MaxNodes) { throw new ScriptLimitException("DOM traversal node limit exceeded."); }
            yield return node;
            Push(node);
        }
        void Push(DomNode node)
        {
            if (pending.Count + node.ChildNodes.Count + count > MaxNodes)
            { throw new ScriptLimitException("DOM traversal node limit exceeded."); }
            for (var i = node.ChildNodes.Count - 1; i >= 0; i--) { pending.Push(node.ChildNodes[i]); }
        }
    }
    private string Lookup(string id)
    {
        var element = id.Length == 0 ? null : Descendants().FirstOrDefault(e => e.GetAttribute("id") == id);
        if (element is null) { return "0"; }
        if (!identities.TryGetValue(element, out var identity))
        {
            if (elements.Count >= MaxHandles) { throw new ScriptLimitException("DOM wrapper identity limit exceeded."); }
            elements.Add(element); identity = elements.Count; identities.Add(element, identity);
        }
        return identity.ToString(CultureInfo.InvariantCulture);
    }
    private DomElement Element(int handle)
    {
        if (handle <= 0 || handle > elements.Count) { throw new InvalidOperationException("Invalid private DOM identity."); }
        var element = elements[handle - 1];
        if (element.OwnerDocument != document) { throw new InvalidOperationException("DOM node was adopted into another document."); }
        return element;
    }
    private static string Text(DomElement element)
    {
        var result = new System.Text.StringBuilder();
        foreach (var node in Traverse(element))
        {
            if (node is not DomText text) { continue; }
            if (result.Length + text.Data.Length > MaxTextCharacters) { throw new ScriptLimitException("DOM textContent limit exceeded."); }
            result.Append(text.Data);
        }
        return result.ToString();
    }
    private string Title()
    {
        var title = Descendants().FirstOrDefault(e => e.LocalName == "title");
        return string.Join(" ", (title is null ? "" : Text(title))
            .Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries));
    }
    private string SetTitle(string value)
    {
        var title = Descendants().FirstOrDefault(e => e.LocalName == "title");
        if (title is not null) { _ = Text(title); }
        document.Title = value;
        return "";
    }
    private static string SetText(DomElement element, string value)
    {
        _ = Text(element);
        element.TextContent = value;
        return "";
    }
    public void Dispose()
    {
        lock (Owners) { Owners.Remove(document); }
        identities.Clear(); elements.Clear();
    }

    internal const string Bootstrap = """
        (() => {
            const bridge = globalThis.__visualwebDom;
            delete globalThis.__visualwebDom;
            const apply = Function.prototype.call.bind(Function.prototype.call);
            const slice = String.prototype.slice, TypeErrorCtor = TypeError;
            const create = Object.create, define = Object.defineProperty, freeze = Object.freeze;
            const map = new Map(), brands = new WeakMap();
            const mapGet = Map.prototype.get, mapSet = Map.prototype.set;
            const brandGet = WeakMap.prototype.get, brandSet = WeakMap.prototype.set;
            const document = create(null), prototype = create(null);
            const call = (operation, handle, value) => {
                const result = bridge(operation, handle, value);
                if (result[0] !== 'o') throw new TypeErrorCtor(apply(slice, result, 6));
                return apply(slice, result, 3);
            };
            const brand = receiver => {
                const id = apply(brandGet, brands, receiver);
                if (!id) throw new TypeErrorCtor('Illegal Element receiver');
                return id;
            };
            define(prototype, 'textContent', {
                enumerable: true,
                get() { return call('text-get', brand(this), ''); },
                set(value) { const id = brand(this); call('text-set', id, value == null ? '' : `${value}`); }
            });
            freeze(prototype);
            define(document, 'title', {
                enumerable: true,
                get() { if (this !== document) throw new TypeErrorCtor('Illegal Document receiver'); return call('title-get', 0, ''); },
                set(value) { if (this !== document) throw new TypeErrorCtor('Illegal Document receiver'); call('title-set', 0, `${value}`); }
            });
            define(document, 'getElementById', {
                enumerable: true,
                value: function(id) {
                    if (this !== document) throw new TypeErrorCtor('Illegal Document receiver');
                    if (arguments.length === 0) throw new TypeErrorCtor('getElementById requires one argument');
                    const handle = +call('lookup', 0, `${id}`);
                    if (!handle) return null;
                    let wrapper = apply(mapGet, map, handle);
                    if (!wrapper) {
                        wrapper = create(prototype);
                        apply(brandSet, brands, wrapper, handle);
                        apply(mapSet, map, handle, wrapper);
                    }
                    return wrapper;
                }
            });
            define(globalThis, 'document', { value: document, enumerable: true });
        })()
        """;
}
