using VisualWeb.Core.Url;

namespace VisualWeb.Browser;

public readonly record struct BrowserWindowId(long Value);
public readonly record struct TabId(long Value);
public sealed class BrowserLimitException(string message) : Exception(message);

public sealed record BrowserOptions
{
    public int MaxWindows { get; init; } = 8;
    public int MaxTabs { get; init; } = 32;
    public int MaxHistoryEntries { get; init; } = 128;
    public int MaxAddressCharacters { get; init; } = 8192;
    public int MaxPendingLoads { get; init; } = 64;
    public int MaxFramePixels { get; init; } = 4_194_304;
    internal void Validate()
    {
        if (MaxWindows <= 0 || MaxTabs <= 0 || MaxHistoryEntries <= 0
            || MaxAddressCharacters <= 0 || MaxPendingLoads <= 0 || MaxFramePixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BrowserOptions));
        }
    }
}

/// <summary>URL development session history with bounded same-document fragment traversal.</summary>
/// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/browsing-the-web.html#session-history">session history</see>.
/// User link fragment navigation and traversal between entries for the retained document update the active URL and scroll locally.
/// Cross-document traversal reloads. No joint frame history, bfcache, script History API or persistence is implemented.</remarks>
public sealed class NavigationHistory
{
    private readonly List<BrowserUrl> entries = [];
    private readonly List<long> documentIds = [];
    private readonly int maximum;
    private long nextDocumentId;
    public IReadOnlyList<BrowserUrl> Entries { get; }
    public int Index { get; private set; } = -1;
    public BrowserUrl? Current => Index < 0 ? null : entries[Index];
    public bool CanGoBack => Index > 0;
    public bool CanGoForward => Index >= 0 && Index + 1 < entries.Count;
    public NavigationHistory(int maximum = 128)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        this.maximum = maximum;
        Entries = entries.AsReadOnly();
    }
    public void Commit(BrowserUrl url, int? traversalIndex = null, bool replace = false)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (traversalIndex is { } target)
        {
            if (target < 0 || target >= entries.Count) { throw new ArgumentOutOfRangeException(nameof(traversalIndex)); }
            entries[target] = url;
            Index = target;
        }
        else if (replace && Index >= 0) { entries[Index] = url; }
        else { Append(url, checked(++nextDocumentId)); }
    }
    internal void CommitSameDocument(BrowserUrl url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (Index < 0) { throw new InvalidOperationException("Cannot add a same-document entry before the first document."); }
        Append(url, documentIds[Index]);
    }
    internal bool IsSameDocumentAsCurrent(int index)
    {
        if (index < 0 || index >= documentIds.Count) { throw new ArgumentOutOfRangeException(nameof(index)); }
        return Index >= 0 && documentIds[Index] == documentIds[index];
    }
    private void Append(BrowserUrl url, long documentId)
    {
        var forwardCount = entries.Count - Index - 1;
        if (forwardCount > 0)
        {
            entries.RemoveRange(Index + 1, forwardCount);
            documentIds.RemoveRange(Index + 1, forwardCount);
        }
        entries.Add(url);
        documentIds.Add(documentId);
        if (entries.Count > maximum)
        {
            entries.RemoveAt(0);
            documentIds.RemoveAt(0);
        }
        Index = entries.Count - 1;
    }
}

public sealed class BrowserTab
{
    public TabId Id { get; }
    public NavigationHistory History { get; }
    public string AddressText { get; internal set; } = "";
    public string Title { get; internal set; } = "New tab";
    public string Status { get; internal set; } = "Development mode - no sandbox";
    public string? Error { get; internal set; }
    public bool IsLoading { get; internal set; }
    /// <summary>Origin of the currently published document, or null before the first successful publication.</summary>
    /// <remarks>Spec: html; <see href="https://html.spec.whatwg.org/multipage/browsers.html#concept-origin">origin</see>.
    /// Browser-owned identity only: never derived from or sent to the renderer, and not used for policy enforcement.</remarks>
    public SecurityOrigin? Origin { get; internal set; }
    internal long Generation { get; set; }
    internal BrowserTab(TabId id, int historyLimit) { Id = id; History = new(historyLimit); }
}

public sealed class BrowserWindow
{
    private readonly List<BrowserTab> tabs = [];
    public BrowserWindowId Id { get; }
    public IReadOnlyList<BrowserTab> Tabs { get; }
    public TabId? ActiveTabId { get; internal set; }
    public BrowserTab? ActiveTab => tabs.FirstOrDefault(tab => tab.Id == ActiveTabId);
    internal List<BrowserTab> MutableTabs => tabs;
    internal BrowserWindow(BrowserWindowId id) { Id = id; Tabs = tabs.AsReadOnly(); }
}

/// <summary>UI-thread shell identities and tab/window lifecycle, not renderer isolation.</summary>
public sealed class BrowserSession
{
    private readonly List<BrowserWindow> windows = [];
    private long nextWindow;
    private long nextTab;
    public BrowserOptions Options { get; }
    public IReadOnlyList<BrowserWindow> Windows { get; }
    public BrowserSession(BrowserOptions? options = null)
    {
        Options = options ?? new();
        Options.Validate();
        Windows = windows.AsReadOnly();
    }
    public BrowserWindow CreateWindow()
    {
        if (windows.Count >= Options.MaxWindows) { throw new BrowserLimitException("Window limit exceeded."); }
        var window = new BrowserWindow(new(checked(++nextWindow)));
        windows.Add(window);
        return window;
    }
    public BrowserTab CreateTab(BrowserWindowId windowId)
    {
        var window = Window(windowId);
        if (windows.Sum(w => w.Tabs.Count) >= Options.MaxTabs) { throw new BrowserLimitException("Tab limit exceeded."); }
        var tab = new BrowserTab(new(checked(++nextTab)), Options.MaxHistoryEntries);
        window.MutableTabs.Add(tab);
        window.ActiveTabId = tab.Id;
        return tab;
    }
    public BrowserWindow Window(BrowserWindowId id) => windows.FirstOrDefault(window => window.Id == id)
        ?? throw new ArgumentException("Unknown browser window.", nameof(id));
    public BrowserTab Tab(TabId id) => windows.SelectMany(window => window.Tabs).FirstOrDefault(tab => tab.Id == id)
        ?? throw new ArgumentException("Unknown tab.", nameof(id));
    public bool Contains(TabId id) => windows.Any(window => window.Tabs.Any(tab => tab.Id == id));
    public void Activate(BrowserWindowId windowId, TabId tabId)
    {
        var window = Window(windowId);
        if (!window.Tabs.Any(tab => tab.Id == tabId)) { throw new ArgumentException("Tab does not belong to window.", nameof(tabId)); }
        window.ActiveTabId = tabId;
    }
    public void CloseTab(TabId id)
    {
        var source = windows.FirstOrDefault(window => window.Tabs.Any(tab => tab.Id == id))
            ?? throw new ArgumentException("Unknown tab.", nameof(id));
        Detach(source, Tab(id));
        if (source.Tabs.Count == 0) { windows.Remove(source); }
    }
    public void CloseWindow(BrowserWindowId id) => windows.Remove(Window(id));
    public void MoveTab(TabId id, BrowserWindowId destination)
    {
        var target = Window(destination);
        var source = windows.Single(window => window.Tabs.Any(tab => tab.Id == id));
        if (source == target) { Activate(destination, id); return; }
        var tab = Tab(id);
        Detach(source, tab);
        target.MutableTabs.Add(tab);
        target.ActiveTabId = id;
        if (source.Tabs.Count == 0) { windows.Remove(source); }
    }
    private static void Detach(BrowserWindow window, BrowserTab tab)
    {
        var index = window.MutableTabs.IndexOf(tab);
        window.MutableTabs.RemoveAt(index);
        if (window.ActiveTabId == tab.Id)
        {
            window.ActiveTabId = window.Tabs.Count == 0 ? null : window.Tabs[Math.Min(index, window.Tabs.Count - 1)].Id;
        }
    }
}
