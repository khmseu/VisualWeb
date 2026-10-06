using System.Globalization;
using System.Runtime.CompilerServices;
using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;

namespace VisualWeb.Engine.Scripting;

/// <summary>Private primitive-only live DOM callback and branded JavaScript facade bootstrap.</summary>
/// <remarks>Specs: dom, html, webidl;
/// <see href="https://dom.spec.whatwg.org/#dom-node-textcontent">textContent</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-nonelementparentnode-getelementbyid">ID lookup</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-element-setattribute">attributes</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-element-toggleattribute">toggleAttribute</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-element-id">id reflection</see>,
/// <see href="https://dom.spec.whatwg.org/#interface-characterdata">CharacterData</see>,
/// <see href="https://dom.spec.whatwg.org/#interface-text">Text splitting and wholeText</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-node-normalize">normalize</see>,
/// <see href="https://dom.spec.whatwg.org/#concept-node-pre-insert">tree mutation</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-childnode-remove">self removal</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-document-doctype">doctype lookup</see>,
/// <see href="https://dom.spec.whatwg.org/#interface-documenttype">doctype metadata</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-document-createelement">node factories</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-document-createcomment">comment factory</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-document-createprocessinginstruction">processing instruction factory</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-processinginstruction-target">processing instruction target</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-node-contains">contains</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-node-getrootnode">getRootNode</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-node-issamenode">isSameNode</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-node-isequalnode">isEqualNode</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-node-nodename">nodeName</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-node-ownerdocument">ownerDocument</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-element-tagname">Element names</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-element-getattributenames">attribute name snapshots</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-element-hasattributes">attribute presence</see>,
/// <see href="https://dom.spec.whatwg.org/#interface-parentnode">element children</see>,
/// <see href="https://dom.spec.whatwg.org/#interface-nondocumenttypechildnode">element siblings</see>,
/// <see href="https://webidl.spec.whatwg.org/#es-DOMString">DOMString conversion</see>.</remarks>
internal sealed class DomBindings : IDisposable
{
    internal const int MaxHandles = 1024;
    internal const int MaxCalls = 4096;
    internal const int MaxNodes = 8192;
    internal const int MaxTextCharacters = 65536;
    internal const int MaxAttributes = 128;
    private static readonly ConditionalWeakTable<DomDocument, DomBindings> Owners = new();
    private readonly DomDocument document;
    private readonly Dictionary<DomNode, int> identities = [];
    private readonly List<DomNode> nodes = [];
    private int calls;
    private int characters;
    private CancellationToken cancellation;
    private static readonly CssOptions QueryOptions = new()
    {
        MaxInputCharacters = 4096,
        MaxTokens = 8192,
        MaxDepth = 64,
        MaxElements = MaxNodes,
        MaxMatchOperations = 65536
    };

    internal DomBindings(DomDocument document)
    {
        this.document = document;
        lock (Owners)
        {
            if (Owners.TryGetValue(document, out _)) { throw new InvalidOperationException("Document already belongs to a V8 host."); }
            Owners.Add(document, this);
        }
    }

    internal void Begin(CancellationToken cancellationToken) { calls = 0; characters = 0; cancellation = cancellationToken; }

    internal string Invoke(string operation, int handle, int other, int reference, string name, string value)
    {
        try
        {
            if (++calls > MaxCalls) { throw new ScriptLimitException("DOM callback count limit exceeded."); }
            Budget(name); Budget(value);
            cancellation.ThrowIfCancellationRequested();
            var result = operation switch
            {
                "lookup" => Lookup(value),
                "query-first" or "query-all" or "matches" or "closest" => Query(operation, Node(handle), value),
                "title-get" => Observe(Title()),
                "title-set" => SetTitle(value),
                "text-get" => Observe(Text(Node(handle))),
                "text-set" => SetText(Node(handle), value),
                "node-value-get" => Observe(Node(handle) is DomCharacterData data ? data.Data : null),
                "node-value-set" => SetNodeValue(Node(handle), value),
                "data-get" => Observe(CharacterData(handle).Data),
                "data-length" => Observe(CharacterData(handle).Length.ToString(CultureInfo.InvariantCulture)),
                "data-set" or "data-append" or "data-insert" or "data-delete" or "data-replace" or "data-substring"
                    => CharacterOperation(operation, CharacterData(handle), unchecked((uint)other), unchecked((uint)reference), value),
                "text-split" => SplitText(TextNode(handle), unchecked((uint)other)),
                "whole-text" => WholeText(TextNode(handle)),
                "normalize" => Normalize(Node(handle)),
                "remove-self" => RemoveSelf(Node(handle)),
                "attribute-get" => Observe(Element(handle).GetAttribute(name)),
                "attribute-has" => Observe(Element(handle).GetAttribute(name) is null ? "false" : "true"),
                "attributes-has" => Observe(Element(handle).Attributes.Count == 0 ? "false" : "true"),
                "attribute-names" => AttributeNames(Element(handle)),
                "attribute-set" => SetAttribute(Element(handle), name, value),
                "attribute-remove" => RemoveAttribute(Element(handle), name),
                "attribute-toggle" => ToggleAttribute(Element(handle), name, value),
                "class-input" => CheckClassInput(handle),
                "create-element" => Create("element", value),
                "create-text" => Create("text", value),
                "create-comment" => Create("comment", value),
                "create-instruction" => Create("instruction", value, name),
                "instruction-target" => Observe(Instruction(handle).Target),
                "doctype-name" => Observe(Doctype(handle).Name),
                "doctype-public" => Observe(Doctype(handle).PublicId),
                "doctype-system" => Observe(Doctype(handle).SystemId),
                "document-doctype" => DocumentDoctype(),
                "create-fragment" => Create("fragment", ""),
                "document-element" or "document-head" or "document-body" => DocumentRoot(operation),
                "parent" => Identity(Node(handle).ParentNode),
                "parent-element" => Identity(Node(handle).ParentNode as DomElement),
                "root" => Identity(InspectAncestors(Node(handle), null, root: true)),
                "contains" => Observe(other < 0 ? ValidateNullNode(handle) :
                    InspectAncestors(Node(other), Node(handle), root: false) is null ? "false" : "true"),
                "same-node" => Observe(other < 0 ? ValidateNullNode(handle) : Node(handle) == Node(other) ? "true" : "false"),
                "equal-node" => Observe(Node(handle).IsEqualNode(other < 0 ? null : Node(other), MaxNodes,
                    CheckEqualityNode, cancellation) ? "true" : "false"),
                "has-children" => Observe(Node(handle).ChildNodes.Count == 0 ? "false" : "true"),
                "first-element" or "last-element" or "element-count" => ElementChildren(Node(handle), operation),
                "previous-element" => ElementSibling(Node(handle), previous: true),
                "next-element" => ElementSibling(Node(handle), previous: false),
                "first-child" => Identity(Node(handle).FirstChild),
                "last-child" => Identity(Node(handle).LastChild),
                "previous-sibling" => Sibling(Node(handle), previous: true),
                "next-sibling" => Sibling(Node(handle), previous: false),
                "node-type" => Observe(((int)Node(handle).NodeType).ToString(CultureInfo.InvariantCulture)),
                "node-name" => NodeName(Node(handle)),
                "owner-document" => Identity(Node(handle).OwnerDocument),
                "local-name" => Observe(Element(handle).LocalName),
                "tag-name" => NodeName(Element(handle)),
                "namespace-uri" => Observe(Element(handle).NamespaceUri),
                "prefix" => ElementPrefix(handle),
                "connected" => Connected(Node(handle)),
                "append" or "insert" or "remove" or "replace" => Mutate(operation, Node(handle), Node(other),
                    reference < 0 ? null : Node(reference)),
                _ => throw new InvalidOperationException("Unknown private DOM operation.")
            };
            return result is null ? "null:" : "ok:" + result;
        }
        catch (FormatException exception) { return "syntax:" + exception.Message; }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        { return "error:DOM operation canceled."; }
        catch (Exception exception) when (exception is ScriptLimitException or DomException or InvalidOperationException
            or CssLimitException or UnsupportedCssException)
        {
            // A string error crosses the bridge, never a CLR exception object or native node.
            return "error:" + exception.Message;
        }
    }

    private string? Observe(string? value) { if (value is not null) { Budget(value); } return value; }
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
        return Identity(id.Length == 0 ? null : Descendants().FirstOrDefault(e => e.GetAttribute("id") == id));
    }
    private string Query(string operation, DomNode root, string source)
    {
        var selector = CssSelectorList.Parse(source, QueryOptions, cancellation);
        if (selector.Diagnostics.Count != 0) { throw new FormatException("Invalid selector tokenization."); }
        if (operation == "matches")
        {
            if (root is not DomElement element) { throw new InvalidOperationException("Illegal Element receiver."); }
            var matched = selector.Match(element, cancellation) is not null ? "true" : "false";
            Budget(matched);
            return matched;
        }
        if (operation is "query-first" or "query-all" && root is not (DomDocument or DomElement or DomDocumentFragment))
        { throw new InvalidOperationException("Illegal ParentNode receiver."); }
        IEnumerable<DomElement> candidates = operation == "closest" ? Ancestors(root) : Traverse(root).OfType<DomElement>();
        var matches = selector.Filter(candidates, cancellation);
        if (operation != "query-all") { return Identity(matches.FirstOrDefault()); }
        var result = matches.Take(MaxHandles + 1).ToList();
        if (result.Count > MaxHandles) { throw new ScriptLimitException("DOM query result limit exceeded."); }
        var added = result.Where(element => !identities.ContainsKey(element)).ToList();
        if (nodes.Count + added.Count > MaxHandles) { throw new ScriptLimitException("DOM wrapper identity limit exceeded."); }
        var next = nodes.Count;
        var reserved = added.ToDictionary(element => element, _ => ++next);
        var text = string.Join(",", result.Select(element => (identities.TryGetValue(element, out var id) ? id : reserved[element])
            .ToString(CultureInfo.InvariantCulture) + ":1"));
        Budget(text);
        foreach (var element in added) { nodes.Add(element); identities.Add(element, nodes.Count); }
        return text;
    }

    private static IEnumerable<DomElement> Ancestors(DomNode root)
    {
        if (root is not DomElement) { throw new InvalidOperationException("Illegal Element receiver."); }
        var count = 0;
        for (DomNode? node = root; node is not null; node = node.ParentNode)
        {
            if (++count > MaxNodes) { throw new ScriptLimitException("DOM ancestor node limit exceeded."); }
            if (node is DomElement element) { yield return element; }
        }
    }
    private string Identity(DomNode? node)
    {
        if (node is null) { Budget(""); return ""; }
        if (node == document) { Budget("0:9"); return "0:9"; }
        if (node.OwnerDocument != document) { throw new InvalidOperationException("DOM node belongs to another document."); }
        var known = identities.TryGetValue(node, out var identity);
        if (!known && nodes.Count >= MaxHandles) { throw new ScriptLimitException("DOM wrapper identity limit exceeded."); }
        var result = (known ? identity : nodes.Count + 1).ToString(CultureInfo.InvariantCulture) + ":" + (int)node.NodeType;
        Budget(result);
        if (!known)
        {
            nodes.Add(node); identities.Add(node, nodes.Count);
        }
        return result;
    }
    private DomNode Node(int handle)
    {
        if (handle == 0) { return document; }
        if (handle < 0 || handle > nodes.Count) { throw new InvalidOperationException("Invalid private DOM identity."); }
        var node = nodes[handle - 1];
        if (node.OwnerDocument != document) { throw new InvalidOperationException("DOM node was adopted into another document."); }
        return node;
    }
    private DomElement Element(int handle) => Node(handle) as DomElement
        ?? throw new InvalidOperationException("Illegal Element receiver.");
    private DomCharacterData CharacterData(int handle) => Node(handle) as DomCharacterData
        ?? throw new InvalidOperationException("Illegal CharacterData receiver.");
    private DomText TextNode(int handle) => Node(handle) as DomText
        ?? throw new InvalidOperationException("Illegal Text receiver.");
    private DomProcessingInstruction Instruction(int handle) => Node(handle) as DomProcessingInstruction
        ?? throw new InvalidOperationException("Illegal ProcessingInstruction receiver.");
    private DomDocumentType Doctype(int handle) => Node(handle) as DomDocumentType
        ?? throw new InvalidOperationException("Illegal DocumentType receiver.");
    private string NodeName(DomNode node)
    {
        if (node is DomElement element)
        {
            Budget(element.LocalName);
            return element.NodeName;
        }
        return Observe(node.NodeName)!;
    }
    private string? ElementPrefix(int handle)
    {
        _ = Element(handle);
        return null;
    }
    private string WholeText(DomText text)
    {
        if (text.ParentNode?.ChildNodes.Count > MaxNodes) { throw new ScriptLimitException("DOM text sibling scan limit exceeded."); }
        var result = new System.Text.StringBuilder();
        foreach (var sibling in text.GetContiguousTextNodes())
        {
            cancellation.ThrowIfCancellationRequested();
            if (result.Length + (long)sibling.Length > MaxTextCharacters)
            { throw new ScriptLimitException("DOM wholeText result limit exceeded."); }
            result.Append(sibling.Data);
        }
        return Observe(result.ToString())!;
    }
    private string SplitText(DomText text, uint offset)
    {
        if (offset > text.Length) { throw new DomException(DomError.IndexSize, "Text split offset exceeds its length."); }
        if (text.Length > MaxTextCharacters) { throw new ScriptLimitException("DOM Text split storage limit exceeded."); }
        if (nodes.Count >= MaxHandles) { throw new ScriptLimitException("DOM wrapper identity limit exceeded."); }
        if (text.ParentNode is { } parent)
        {
            CheckAncestors(parent);
            var count = 0;
            foreach (var node in Traverse(parent))
            {
                cancellation.ThrowIfCancellationRequested();
                if (++count >= MaxNodes) { throw new ScriptLimitException("DOM destination subtree limit exceeded."); }
            }
        }
        var result = (nodes.Count + 1).ToString(CultureInfo.InvariantCulture) + ":3";
        Budget(result);
        cancellation.ThrowIfCancellationRequested();
        var created = text.SplitText(offset);
        nodes.Add(created); identities.Add(created, nodes.Count);
        return result;
    }
    private string Normalize(DomNode node)
    {
        node.Normalize(MaxNodes, MaxTextCharacters, Budget, cancellation);
        return "";
    }
    private string RemoveSelf(DomNode node)
    {
        if (node is not (DomElement or DomCharacterData or DomDocumentType))
        { throw new InvalidOperationException("Illegal ChildNode receiver."); }
        if (node.ParentNode is { } parent)
        {
            CheckAncestors(parent);
            foreach (var descendant in Traverse(parent)) { cancellation.ThrowIfCancellationRequested(); }
        }
        cancellation.ThrowIfCancellationRequested();
        node.Remove();
        return "";
    }
    private void CheckEqualityNode(DomNode node)
    {
        if (node != document && node.OwnerDocument != document)
        { throw new InvalidOperationException("DOM equality node belongs to another document."); }
        switch (node)
        {
            case DomElement element:
                Budget(element.NamespaceUri); Budget(element.LocalName);
                if (element.Attributes.Count > MaxAttributes) { throw new ScriptLimitException("DOM equality attribute count limit exceeded."); }
                long stored = 0;
                foreach (var attribute in element.Attributes)
                {
                    cancellation.ThrowIfCancellationRequested();
                    stored += attribute.Key.Length + (long)attribute.Value.Length;
                    if (stored > MaxTextCharacters) { throw new ScriptLimitException("DOM equality attribute storage limit exceeded."); }
                    Budget(attribute.Key); Budget(attribute.Value);
                }
                break;
            case DomProcessingInstruction instruction:
                Budget(instruction.Target); Budget(instruction.Data); break;
            case DomCharacterData data:
                Budget(data.Data); break;
            case DomDocumentType doctype:
                Budget(doctype.Name); Budget(doctype.PublicId); Budget(doctype.SystemId); break;
        }
    }
    private string SetNodeValue(DomNode node, string value)
        => node is DomCharacterData data ? CharacterOperation("data-set", data, 0, 0, value) : "";
    private string CharacterOperation(string operation, DomCharacterData data, uint offset, uint count, string value)
    {
        if (operation == "data-set") { offset = 0; count = (uint)data.Length; }
        else if (operation == "data-append") { offset = (uint)data.Length; count = 0; }
        else if (operation == "data-insert") { count = 0; }
        if (offset > data.Length) { throw new DomException(DomError.IndexSize, "CharacterData offset exceeds its length."); }
        var removed = Math.Min(count, (uint)data.Length - offset);
        if (operation == "data-substring")
        {
            if (removed > MaxTextCharacters) { throw new ScriptLimitException("DOM CharacterData result limit exceeded."); }
            return Observe(data.SubstringData(offset, count))!;
        }
        if (operation == "data-delete") { value = ""; }
        if (data.Length - removed + (long)value.Length > MaxTextCharacters)
        { throw new ScriptLimitException("DOM CharacterData storage limit exceeded."); }
        data.ReplaceData(offset, count, value);
        return "";
    }
    private static string? Text(DomNode element)
    {
        if (element is DomDocument or DomDocumentType) { return null; }
        if (element is DomCharacterData data) { return data.Data; }
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
        return string.Join(" ", (title is null ? "" : Text(title) ?? "")
            .Split([' ', '\t', '\n', '\r', '\f'], StringSplitOptions.RemoveEmptyEntries));
    }
    private string SetTitle(string value)
    {
        var title = Descendants().FirstOrDefault(e => e.LocalName == "title");
        if (title is not null) { _ = Text(title); }
        document.Title = value;
        return "";
    }
    private static string SetText(DomNode element, string value)
    {
        _ = Text(element);
        element.TextContent = value;
        return "";
    }
    private string Create(string kind, string value, string name = "")
    {
        if (nodes.Count >= MaxHandles) { throw new ScriptLimitException("DOM wrapper identity limit exceeded."); }
        return Identity(kind switch
        {
            "element" => document.CreateElement(value),
            "text" => document.CreateTextNode(value),
            "comment" => document.CreateComment(value),
            "instruction" => document.CreateProcessingInstruction(name, value),
            _ => document.CreateDocumentFragment()
        });
    }
    private static string SetAttribute(DomElement element, string name, string value)
    {
        CheckAttributeStorage(element, name, value);
        element.SetAttribute(name, value);
        return "";
    }
    private string AttributeNames(DomElement element)
    {
        if (element.Attributes.Count > MaxAttributes)
        { throw new ScriptLimitException("DOM attribute name count limit exceeded."); }
        var result = new System.Text.StringBuilder();
        foreach (var name in element.Attributes.Keys)
        {
            cancellation.ThrowIfCancellationRequested();
            var prefix = name.Length.ToString(CultureInfo.InvariantCulture) + ":";
            if (result.Length + (long)prefix.Length + name.Length > MaxTextCharacters)
            { throw new ScriptLimitException("DOM attribute name result limit exceeded."); }
            result.Append(prefix); result.Append(name);
        }
        return Observe(result.ToString())!;
    }
    private static void CheckAttributeStorage(DomElement element, string name, string value)
    {
        var count = element.Attributes.Count;
        if (count > MaxAttributes) { throw new ScriptLimitException("DOM attribute count limit exceeded."); }
        long characters = 0;
        var replaced = false;
        foreach (var attribute in element.Attributes)
        {
            if (AsciiEquals(attribute.Key, name)) { replaced = true; continue; }
            characters += attribute.Key.Length + (long)attribute.Value.Length;
            if (characters > MaxTextCharacters) { throw new ScriptLimitException("DOM attribute storage limit exceeded."); }
        }
        if (count + (replaced ? 0 : 1) > MaxAttributes || characters + name.Length + value.Length > MaxTextCharacters)
        { throw new ScriptLimitException("DOM attribute storage limit exceeded."); }
    }
    private string ToggleAttribute(DomElement element, string name, string force)
    {
        bool? forced = force switch
        {
            "" => null,
            "true" => true,
            "false" => false,
            _ => throw new InvalidOperationException("Invalid private attribute force.")
        };
        var present = element.GetAttribute(name) is not null;
        if (!present && forced != false) { CheckAttributeStorage(element, name, ""); }
        Budget((forced ?? !present) ? "true" : "false");
        return element.ToggleAttribute(name, forced) ? "true" : "false";
    }
    private static bool AsciiEquals(string left, string right)
    {
        if (left.Length != right.Length) { return false; }
        for (var i = 0; i < left.Length; i++)
        {
            var c = right[i];
            if (left[i] != (c is >= 'A' and <= 'Z' ? c + 32 : c)) { return false; }
        }
        return true;
    }
    private static string RemoveAttribute(DomElement element, string name)
    {
        element.RemoveAttribute(name);
        return "";
    }
    private string CheckClassInput(int handle)
    {
        _ = Element(handle);
        return "";
    }
    private string ValidateNullNode(int handle)
    {
        _ = Node(handle);
        return "false";
    }
    private DomNode? InspectAncestors(DomNode node, DomNode? sought, bool root)
    {
        var count = 0;
        for (DomNode? current = node; current is not null; current = current.ParentNode)
        {
            cancellation.ThrowIfCancellationRequested();
            if (++count > MaxNodes) { throw new ScriptLimitException("DOM ancestor scan limit exceeded."); }
            if (root ? current.ParentNode is null : current == sought) { return current; }
        }
        return null;
    }
    private string ElementChildren(DomNode node, string operation)
    {
        if (node is not (DomDocument or DomElement or DomDocumentFragment))
        { throw new InvalidOperationException("Illegal ParentNode receiver."); }
        if (node.ChildNodes.Count > MaxNodes) { throw new ScriptLimitException("DOM child scan limit exceeded."); }
        DomElement? result = null;
        var count = 0;
        foreach (var child in node.ChildNodes)
        {
            cancellation.ThrowIfCancellationRequested();
            if (child is not DomElement element) { continue; }
            count++;
            if (operation == "first-element") { return Identity(element); }
            result = element;
        }
        return operation == "element-count" ? Observe(count.ToString(CultureInfo.InvariantCulture))! : Identity(result);
    }
    private string ElementSibling(DomNode node, bool previous)
    {
        if (node is not (DomElement or DomCharacterData))
        { throw new InvalidOperationException("Illegal NonDocumentTypeChildNode receiver."); }
        if (node.ParentNode is not { } parent) { return Identity(null); }
        if (parent.ChildNodes.Count > MaxNodes) { throw new ScriptLimitException("DOM sibling scan limit exceeded."); }
        DomElement? preceding = null;
        var found = false;
        foreach (var child in parent.ChildNodes)
        {
            cancellation.ThrowIfCancellationRequested();
            if (child == node)
            {
                if (previous) { return Identity(preceding); }
                found = true;
            }
            else if (child is DomElement element)
            {
                if (found) { return Identity(element); }
                preceding = element;
            }
        }
        return Identity(null);
    }
    private string Sibling(DomNode node, bool previous)
    {
        if (node.ParentNode?.ChildNodes.Count > MaxNodes) { throw new ScriptLimitException("DOM sibling scan limit exceeded."); }
        return Identity(previous ? node.PreviousSibling : node.NextSibling);
    }
    private string DocumentRoot(string operation)
    {
        if (document.ChildNodes.Count > MaxNodes || document.DocumentElement?.ChildNodes.Count > MaxNodes)
        { throw new ScriptLimitException("DOM document root scan limit exceeded."); }
        return Identity(operation switch
        {
            "document-element" => document.DocumentElement,
            "document-head" => document.Head,
            _ => document.Body
        });
    }
    private string DocumentDoctype()
    {
        if (document.ChildNodes.Count > MaxNodes)
        { throw new ScriptLimitException("DOM document doctype scan limit exceeded."); }
        return Identity(document.Doctype);
    }
    private string Connected(DomNode node)
    {
        CheckAncestors(node);
        return Observe(node.IsConnected ? "true" : "false")!;
    }
    private static void CheckAncestors(DomNode node)
    {
        var count = 0;
        for (DomNode? current = node; current is not null; current = current.ParentNode)
        {
            if (++count > MaxNodes) { throw new ScriptLimitException("DOM ancestor scan limit exceeded."); }
        }
    }
    private static string Mutate(string operation, DomNode parent, DomNode child, DomNode? reference)
    {
        CheckAncestors(parent);
        _ = Traverse(parent).Count();
        _ = Traverse(child).Count();
        if (child.ParentNode?.ChildNodes.Count > MaxNodes) { throw new ScriptLimitException("DOM source child scan limit exceeded."); }
        if (operation is "append" or "insert" or "replace")
        {
            var destination = Traverse(parent).ToHashSet();
            if (operation == "replace" && reference is not null && child != reference)
            {
                destination.Remove(reference);
                foreach (var node in Traverse(reference)) { destination.Remove(node); }
            }
            if (child is not DomDocumentFragment) { Add(child); }
            foreach (var node in Traverse(child)) { Add(node); }
            void Add(DomNode node)
            {
                if (destination.Add(node) && destination.Count > MaxNodes)
                { throw new ScriptLimitException("DOM destination subtree limit exceeded."); }
            }
        }
        switch (operation)
        {
            case "append": parent.AppendChild(child); break;
            case "insert": parent.InsertBefore(child, reference); break;
            case "remove": parent.RemoveChild(child); break;
            case "replace":
                if (reference is null) { throw new InvalidOperationException("replaceChild requires an existing child."); }
                parent.ReplaceChild(child, reference); break;
        }
        return "";
    }
    public void Dispose()
    {
        lock (Owners) { Owners.Remove(document); }
        identities.Clear(); nodes.Clear();
    }

    internal const string Bootstrap = """
        ((events, lifecycle, installClasses) => {
            const bridge = globalThis.__visualwebDom;
            delete globalThis.__visualwebDom;
            const apply = Function.prototype.call.bind(Function.prototype.call);
            const slice = String.prototype.slice, indexOf = String.prototype.indexOf;
            const TypeErrorCtor = TypeError, SyntaxErrorCtor = SyntaxError;
            const create = Object.create, define = Object.defineProperty, freeze = Object.freeze;
            const map = new Map(), brands = new WeakMap(), elementBrands = new WeakMap(), parentBrands = new WeakMap(),
                characterBrands = new WeakMap(), textBrands = new WeakMap(), instructionBrands = new WeakMap(),
                childBrands = new WeakMap(), doctypeBrands = new WeakMap();
            const mapGet = Map.prototype.get, mapSet = Map.prototype.set;
            const brandGet = WeakMap.prototype.get, brandSet = WeakMap.prototype.set;
            const document = create(null), prototype = create(null), nodePrototype = create(null);
            apply(brandSet, brands, document, 0);
            apply(brandSet, parentBrands, document, 0);
            const call = (operation, handle, value = '', other = -1, reference = -1, name = '') => {
                const result = bridge(operation, handle, other, reference, name, value);
                if (result[0] === 'n') return null;
                if (result[0] === 's') throw new SyntaxErrorCtor(apply(slice, result, 7));
                if (result[0] !== 'o') throw new TypeErrorCtor(apply(slice, result, 6));
                return apply(slice, result, 3);
            };
            const brand = receiver => {
                const id = apply(brandGet, brands, receiver);
                if (id === undefined) throw new TypeErrorCtor('Illegal Node receiver');
                return id;
            };
            const documentBrand = receiver => {
                if (receiver !== document) throw new TypeErrorCtor('Illegal Document receiver');
            };
            const elementBrand = receiver => {
                const id = apply(brandGet, elementBrands, receiver);
                if (id === undefined) throw new TypeErrorCtor('Illegal Element receiver');
                return id;
            };
            const parentBrand = receiver => {
                const id = apply(brandGet, parentBrands, receiver);
                if (id === undefined) throw new TypeErrorCtor('Illegal ParentNode receiver');
                return id;
            };
            const characterBrand = receiver => {
                const id = apply(brandGet, characterBrands, receiver);
                if (id === undefined) throw new TypeErrorCtor('Illegal CharacterData receiver');
                return id;
            };
            const textBrand = receiver => {
                const id = apply(brandGet, textBrands, receiver);
                if (id === undefined) throw new TypeErrorCtor('Illegal Text receiver');
                return id;
            };
            const instructionBrand = receiver => {
                const id = apply(brandGet, instructionBrands, receiver);
                if (id === undefined) throw new TypeErrorCtor('Illegal ProcessingInstruction receiver');
                return id;
            };
            const childBrand = receiver => {
                const id = apply(brandGet, childBrands, receiver);
                if (id === undefined) throw new TypeErrorCtor('Illegal ChildNode receiver');
                return id;
            };
            const doctypeBrand = receiver => {
                const id = apply(brandGet, doctypeBrands, receiver);
                if (id === undefined) throw new TypeErrorCtor('Illegal DocumentType receiver');
                return id;
            };
            const required = (count, minimum) => {
                if (count < minimum) throw new TypeErrorCtor('Not enough arguments');
            };
            if (events) events('register', document, () => null, () => call('node-type', 0));
            if (lifecycle) events('document', document);
            const wrap = value => {
                if (!value) return null;
                const separator = apply(indexOf, value, ':');
                const handle = +apply(slice, value, 0, separator);
                if (handle === 0) return document;
                let wrapper = apply(mapGet, map, handle);
                if (!wrapper) {
                    const type = +apply(slice, value, separator + 1);
                    wrapper = create(type === 1 ? prototype : nodePrototype);
                    apply(brandSet, brands, wrapper, handle);
                    if (type === 1) apply(brandSet, elementBrands, wrapper, handle);
                    if (type === 1 || type === 11) apply(brandSet, parentBrands, wrapper, handle);
                    if (type === 3 || type === 7 || type === 8) apply(brandSet, characterBrands, wrapper, handle);
                    if (type === 3) apply(brandSet, textBrands, wrapper, handle);
                    if (type === 7) apply(brandSet, instructionBrands, wrapper, handle);
                    if (type === 10) apply(brandSet, doctypeBrands, wrapper, handle);
                    if (type === 1 || type === 3 || type === 7 || type === 8 || type === 10)
                        apply(brandSet, childBrands, wrapper, handle);
                    apply(mapSet, map, handle, wrapper);
                    if (events) events('register', wrapper, () => wrap(call('parent', handle)), () => call('node-type', handle));
                }
                return wrapper;
            };
            const lists = new WeakMap(), listPrototype = create(null);
            const listBrand = receiver => {
                const values = apply(brandGet, lists, receiver);
                if (!values) throw new TypeErrorCtor('Illegal NodeList receiver');
                return values;
            };
            define(listPrototype, 'length', {enumerable: true, get() {return listBrand(this).length;}});
            define(listPrototype, 'item', {enumerable: true, value: function(index) {
                const values = listBrand(this); required(arguments.length, 1);
                return values[(+index) >>> 0] ?? null;
            }});
            define(listPrototype, 'forEach', {enumerable: true, value: function(callback, receiver) {
                const values = listBrand(this);
                if (typeof callback !== 'function') throw new TypeErrorCtor('NodeList forEach requires a callable callback');
                for (let i=0; i<values.length; ++i) apply(callback, receiver, values[i], i, this);
            }});
            const values = function() {
                const items = listBrand(this);
                return (function*(){for(let i=0;i<items.length;++i)yield items[i];})();
            };
            define(listPrototype, 'values', {enumerable: true, value: values});
            define(listPrototype, Symbol.iterator, {value: values});
            define(listPrototype, 'keys', {enumerable: true, value: function() {
                const items = listBrand(this);
                return (function*(){for(let i=0;i<items.length;++i)yield i;})();
            }});
            define(listPrototype, 'entries', {enumerable: true, value: function() {
                const items = listBrand(this);
                return (function*(){for(let i=0;i<items.length;++i)yield [i,items[i]];})();
            }});
            freeze(listPrototype);
            const nodeList = text => {
                const result = create(listPrototype), items = create(null);
                items.length = 0;
                if (text) {
                    let offset = 0;
                    while (offset < text.length) {
                        let end = apply(indexOf, text, ',', offset);
                        if (end < 0) end = text.length;
                        const node = wrap(apply(slice, text, offset, end));
                        const i = items.length;
                        items[items.length++] = node;
                        define(result, i, {value: node, enumerable: true});
                        offset = end + 1;
                    }
                }
                apply(brandSet, lists, result, items);
                return freeze(result);
            };
            const installQueries = target => {
                define(target, 'querySelector', {enumerable: true, value: function(selectors) {
                    const id = parentBrand(this); required(arguments.length, 1);
                    return wrap(call('query-first', id, `${selectors}`));
                }});
                define(target, 'querySelectorAll', {enumerable: true, value: function(selectors) {
                    const id = parentBrand(this); required(arguments.length, 1);
                    return nodeList(call('query-all', id, `${selectors}`));
                }});
            };
            installQueries(document); installQueries(prototype); installQueries(nodePrototype);
            define(prototype, 'matches', {enumerable: true, value: function(selectors) {
                const id = elementBrand(this); required(arguments.length, 1);
                return call('matches', id, `${selectors}`) === 'true';
            }});
            define(prototype, 'closest', {enumerable: true, value: function(selectors) {
                const id = elementBrand(this); required(arguments.length, 1);
                return wrap(call('closest', id, `${selectors}`));
            }});
            const installNode = target => {
                if (events) events('install', target);
                define(target, 'textContent', {
                    enumerable: true,
                    get() { return call('text-get', brand(this)); },
                    set(value) { const id = brand(this); call('text-set', id, value == null ? '' : `${value}`); }
                });
                define(target, 'nodeValue', {enumerable: true,
                    get() {return call('node-value-get', brand(this));},
                    set(value) {const id=brand(this);call('node-value-set',id,value==null?'':`${value}`);}
                });
                for (const [property, operation] of [
                    ['parentNode', 'parent'], ['parentElement', 'parent-element'], ['firstChild', 'first-child'], ['lastChild', 'last-child'],
                    ['previousSibling', 'previous-sibling'], ['nextSibling', 'next-sibling']
                ]) define(target, property, {enumerable: true, get() { return wrap(call(operation, brand(this))); }});
                define(target, 'nodeType', {enumerable: true, get() {return +call('node-type', brand(this)); }});
                define(target, 'nodeName', {enumerable: true, get() {return call('node-name', brand(this));}});
                define(target, 'ownerDocument', {enumerable: true, get() {return wrap(call('owner-document', brand(this)));}});
                define(target, 'isConnected', {enumerable: true, get() {return call('connected', brand(this)) === 'true'; }});
                define(target, 'hasChildNodes', {enumerable: true, value: function() {
                    return call('has-children', brand(this)) === 'true';
                }});
                define(target, 'normalize', {enumerable: true, value: function() {
                    call('normalize', brand(this));
                }});
                define(target, 'contains', {enumerable: true, value: function(node) {
                    const id = brand(this); required(arguments.length, 1);
                    return call('contains', id, '', node == null ? -1 : brand(node)) === 'true';
                }});
                define(target, 'isSameNode', {enumerable: true, value: function(node = null) {
                    const id = brand(this);
                    return call('same-node', id, '', node == null ? -1 : brand(node)) === 'true';
                }});
                define(target, 'isEqualNode', {enumerable: true, value: function(node = null) {
                    const id = brand(this);
                    return call('equal-node', id, '', node == null ? -1 : brand(node)) === 'true';
                }});
                define(target, 'getRootNode', {enumerable: true, value: function(options) {
                    const id = brand(this);
                    if (options != null && typeof options !== 'object' && typeof options !== 'function')
                        throw new TypeErrorCtor('getRootNode options must be a dictionary');
                    if (options != null) {const composed = !!options.composed;}
                    return wrap(call('root', id));
                }});
                define(target, 'appendChild', {enumerable: true, value: function(node) {
                    const id = brand(this); required(arguments.length, 1);
                    call('append', id, '', brand(node)); return node;
                }});
                define(target, 'insertBefore', {enumerable: true, value: function(node, reference) {
                    const id = brand(this); required(arguments.length, 2);
                    call('insert', id, '', brand(node), reference == null ? -1 : brand(reference)); return node;
                }});
                define(target, 'removeChild', {enumerable: true, value: function(node) {
                    const id = brand(this); required(arguments.length, 1);
                    call('remove', id, '', brand(node)); return node;
                }});
                define(target, 'replaceChild', {enumerable: true, value: function(node, child) {
                    const id = brand(this); required(arguments.length, 2);
                    call('replace', id, '', brand(node), brand(child)); return child;
                }});
            };
            installNode(document); installNode(prototype); installNode(nodePrototype);
            const unscopables=create(null);
            define(unscopables,'remove',{value:true,enumerable:true});
            freeze(unscopables);
            for(const target of [prototype,nodePrototype]){
                define(target,'remove',{enumerable:true,value:function(){
                    call('remove-self',childBrand(this));
                }});
                define(target,Symbol.unscopables,{value:unscopables});
            }
            for (const [property, operation] of [
                ['localName', 'local-name'], ['tagName', 'tag-name'], ['namespaceURI', 'namespace-uri'], ['prefix', 'prefix']
            ]) define(prototype, property, {enumerable: true, get() {return call(operation, elementBrand(this));}});
            define(nodePrototype, 'data', {enumerable: true,
                get() {return call('data-get', characterBrand(this));},
                set(value) {const id=characterBrand(this);call('data-set',id,value===null?'':`${value}`);}
            });
            define(nodePrototype, 'length', {enumerable: true, get() {return +call('data-length',characterBrand(this));}});
            define(nodePrototype, 'substringData', {enumerable: true, value: function(offset,count) {
                const id=characterBrand(this);required(arguments.length,2);
                const start=(+offset)>>>0, amount=(+count)>>>0;
                return call('data-substring',id,'',start|0,amount|0);
            }});
            define(nodePrototype, 'appendData', {enumerable: true, value: function(value) {
                const id=characterBrand(this);required(arguments.length,1);
                call('data-append',id,`${value}`);
            }});
            define(nodePrototype, 'insertData', {enumerable: true, value: function(offset,value) {
                const id=characterBrand(this);required(arguments.length,2);
                const start=(+offset)>>>0, text=`${value}`;
                call('data-insert',id,text,start|0,0);
            }});
            define(nodePrototype, 'deleteData', {enumerable: true, value: function(offset,count) {
                const id=characterBrand(this);required(arguments.length,2);
                const start=(+offset)>>>0, amount=(+count)>>>0;
                call('data-delete',id,'',start|0,amount|0);
            }});
            define(nodePrototype, 'replaceData', {enumerable: true, value: function(offset,count,value) {
                const id=characterBrand(this);required(arguments.length,3);
                const start=(+offset)>>>0, amount=(+count)>>>0, text=`${value}`;
                call('data-replace',id,text,start|0,amount|0);
            }});
            define(nodePrototype, 'splitText', {enumerable: true, value: function(offset) {
                const id=textBrand(this);required(arguments.length,1);
                const start=(+offset)>>>0;
                return wrap(call('text-split',id,'',start|0));
            }});
            define(nodePrototype, 'wholeText', {enumerable: true, get() {
                return call('whole-text',textBrand(this));
            }});
            define(nodePrototype, 'target', {enumerable: true, get() {
                return call('instruction-target',instructionBrand(this));
            }});
            for (const [property, operation] of [
                ['name', 'doctype-name'], ['publicId', 'doctype-public'], ['systemId', 'doctype-system']
            ]) define(nodePrototype, property, {enumerable: true, get() {
                return call(operation, doctypeBrand(this));
            }});
            for (const target of [document, prototype, nodePrototype]) {
                for (const [property, operation] of [
                    ['firstElementChild', 'first-element'], ['lastElementChild', 'last-element']
                ]) define(target, property, {enumerable: true, get() {
                    return wrap(call(operation, parentBrand(this)));
                }});
                define(target, 'childElementCount', {enumerable: true, get() {
                    return +call('element-count', parentBrand(this));
                }});
            }
            for (const target of [prototype, nodePrototype]) {
                for (const [property, operation] of [
                    ['previousElementSibling', 'previous-element'], ['nextElementSibling', 'next-element']
                ]) define(target, property, {enumerable: true, get() {return wrap(call(operation, brand(this)));}});
            }
            for (const [method, operation] of [
                ['getAttribute', 'attribute-get'], ['hasAttribute', 'attribute-has'], ['removeAttribute', 'attribute-remove']
            ]) define(prototype, method, {enumerable: true, value: function(name) {
                const id = elementBrand(this); required(arguments.length, 1);
                const result = call(operation, id, '', -1, -1, `${name}`);
                return operation === 'attribute-has' ? result === 'true' : operation === 'attribute-remove' ? undefined : result;
            }});
            define(prototype, 'hasAttributes', {enumerable: true, value: function() {
                return call('attributes-has',elementBrand(this))==='true';
            }});
            define(prototype, 'getAttributeNames', {enumerable: true, value: function() {
                const text=call('attribute-names',elementBrand(this)),result=[];
                let offset=0,index=0;
                while(offset<text.length){
                    const separator=apply(indexOf,text,':',offset),length=+apply(slice,text,offset,separator);
                    offset=separator+1;
                    define(result,index++,{value:apply(slice,text,offset,offset+length),
                        enumerable:true,writable:true,configurable:true});
                    offset+=length;
                }
                return result;
            }});
            define(prototype, 'setAttribute', {enumerable: true, value: function(name, value) {
                const id = elementBrand(this); required(arguments.length, 2);
                const convertedName = `${name}`, convertedValue = `${value}`;
                call('attribute-set', id, convertedValue, -1, -1, convertedName);
            }});
            define(prototype, 'toggleAttribute', {enumerable: true, value: function(name, force) {
                const id = elementBrand(this); required(arguments.length, 1);
                const convertedName = `${name}`, convertedForce = force === undefined ? '' : force ? 'true' : 'false';
                return call('attribute-toggle', id, convertedForce, -1, -1, convertedName) === 'true';
            }});
            define(prototype, 'id', {enumerable: true,
                get() {return call('attribute-get', elementBrand(this), '', -1, -1, 'id') ?? '';},
                set(value) {const id = elementBrand(this);call('attribute-set', id, `${value}`, -1, -1, 'id');}
            });
            installClasses(prototype, elementBrand, call);
            freeze(prototype); freeze(nodePrototype);
            define(document, 'title', {
                enumerable: true,
                get() { documentBrand(this); return call('title-get', 0); },
                set(value) { documentBrand(this); call('title-set', 0, `${value}`); }
            });
            define(document, 'getElementById', {
                enumerable: true,
                value: function(id) {
                    documentBrand(this); required(arguments.length, 1);
                    return wrap(call('lookup', 0, `${id}`));
                }
            });
            for (const [property, operation] of [
                ['documentElement', 'document-element'], ['head', 'document-head'], ['body', 'document-body'],
                ['doctype', 'document-doctype']
            ]) define(document, property, {enumerable: true, get() {documentBrand(this); return wrap(call(operation, 0)); }});
            for (const [method, operation] of [
                ['createElement', 'create-element'], ['createTextNode', 'create-text'], ['createComment', 'create-comment']
            ]) define(document, method, {enumerable: true, value: function(value) {
                documentBrand(this); required(arguments.length, 1);
                if (operation === 'create-element' && arguments.length > 1 && arguments[1] !== undefined)
                    throw new TypeErrorCtor('createElement options/custom elements are deferred');
                return wrap(call(operation, 0, `${value}`));
            }});
            define(document, 'createDocumentFragment', {enumerable: true, value: function() {
                documentBrand(this); return wrap(call('create-fragment', 0));
            }});
            define(document, 'createProcessingInstruction', {enumerable: true, value: function(target, data) {
                documentBrand(this); required(arguments.length, 2);
                const convertedTarget=`${target}`,convertedData=`${data}`;
                return wrap(call('create-instruction',0,convertedData,-1,-1,convertedTarget));
            }});
            define(globalThis, 'document', { value: document, enumerable: true });
        })
        """;
}
