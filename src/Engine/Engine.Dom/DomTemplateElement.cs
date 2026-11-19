namespace VisualWeb.Engine.Dom;

/// <summary>A native HTML template element with a separate contents fragment.</summary>
/// <remarks>
/// Spec: html; <see href="https://html.spec.whatwg.org/multipage/dom.html#template-contents">template contents</see>
/// and <see href="https://html.spec.whatwg.org/multipage/dom.html#dom-template-content">content</see> (local ID `html`).
/// The fragment interface is dom; <see href="https://dom.spec.whatwg.org/#interface-documentfragment">DocumentFragment</see>
/// (local ID `dom`).
/// Parser construction and script-facing bindings remain deferred.
/// </remarks>
public sealed class DomTemplateElement : DomElement
{
    /// <summary>The stable, separate fragment containing this template's children.</summary>
    /// <remarks>
    /// Spec: html; <see href="https://html.spec.whatwg.org/multipage/dom.html#template-contents">template contents</see>
    /// and <see href="https://html.spec.whatwg.org/multipage/dom.html#dom-template-content">content</see> (local ID `html`).
    /// The fragment interface is dom; <see href="https://dom.spec.whatwg.org/#interface-documentfragment">DocumentFragment</see>
    /// (local ID `dom`).
    /// Its owner document is the document's inert template-contents owner document.
    /// </remarks>
    public DomDocumentFragment Content { get; }

    internal DomTemplateElement(DomDocument document, string name) : base(document, name)
    {
        Content = new DomDocumentFragment(document.TemplateContentsOwnerDocument);
    }
}
