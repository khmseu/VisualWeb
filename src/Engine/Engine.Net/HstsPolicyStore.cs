using System.Net;
using System.Text;
using VisualWeb.Core.Url;

namespace VisualWeb.Engine.Net;

/// <summary>Bounded, thread-safe, in-memory HSTS policies owned by one browser session.</summary>
/// <remarks>Spec: rfc6797; <see href="https://www.rfc-editor.org/rfc/rfc6797.html#section-8">UA processing</see>.
/// No persistence, preload list or public-suffix policy. Hosts are normalized through Core.Url.</remarks>
public sealed class HstsPolicyStore
{
    private sealed record Policy(DateTimeOffset Expires, bool IncludeSubDomains);
    private readonly object gate = new();
    private readonly Dictionary<string, Policy> policies = new(StringComparer.OrdinalIgnoreCase);
    private readonly int maxEntries;
    private readonly TimeProvider timeProvider;

    public HstsPolicyStore(int maxEntries = 1024, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        this.maxEntries = maxEntries;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Processes only the first STS field of a response from authenticated HTTPS transport.</summary>
    /// <remarks>Spec: rfc6797; <see href="https://www.rfc-editor.org/rfc/rfc6797.html#section-8.1">header processing</see>
    /// and <see href="https://www.rfc-editor.org/rfc/rfc6797.html#section-6.1">directive grammar</see>.
    /// The trusted transport caller must report authentication accurately, with no certificate-error bypass.
    /// HTTP status does not affect learning. Invalid fields are ignored with diagnostics. Capacity exhaustion
    /// fails explicitly rather than silently evicting active security protections.</remarks>
    public void ProcessResponse(BrowserUrl url, IEnumerable<string> headerValues, bool authenticatedTransport,
        ICollection<string> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(headerValues);
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (!authenticatedTransport || url.Protocol != "https:" || DnsHost(url) is not { } host)
        {
            return;
        }

        var first = headerValues.FirstOrDefault();
        if (first is null) { return; }
        if (!TryParse(first, out var seconds, out var includeSubDomains))
        {
            diagnostics.Add("Ignored malformed Strict-Transport-Security header (RFC 6797 directive grammar).");
            return;
        }

        lock (gate)
        {
            var now = timeProvider.GetUtcNow();
            Expire(now);
            if (seconds == 0)
            {
                policies.Remove(host);
                return;
            }

            if (!policies.ContainsKey(host) && policies.Count >= maxEntries)
            {
                throw new ResourceLoadException(ResourceError.HstsCapacity,
                    "Session HSTS policy capacity exhausted; active policies were not evicted.");
            }

            var remaining = DateTimeOffset.MaxValue.UtcTicks - now.UtcTicks;
            var expires = seconds > (ulong)(remaining / TimeSpan.TicksPerSecond)
                ? DateTimeOffset.MaxValue
                : now.AddTicks((long)seconds * TimeSpan.TicksPerSecond);
            policies[host] = new(expires, includeSubDomains);
        }
    }

    /// <summary>Upgrades a known DNS-host HTTP URL, preserving components and mapping port 80 to HTTPS default.</summary>
    /// <remarks>Spec: rfc6797; <see href="https://www.rfc-editor.org/rfc/rfc6797.html#section-8.2">label matching</see>
    /// and <see href="https://www.rfc-editor.org/rfc/rfc6797.html#section-8.3">URI loading and port mapping</see>.
    /// IP literals and non-HTTP URLs are unchanged. The returned URL must be used for subsequent security checks.</remarks>
    public BrowserUrl Upgrade(BrowserUrl url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.Protocol != "http:" || DnsHost(url) is not { } host) { return url; }
        lock (gate)
        {
            Expire(timeProvider.GetUtcNow());
            var candidate = host;
            while (true)
            {
                if (policies.TryGetValue(candidate, out var policy)
                    && (candidate == host || policy.IncludeSubDomains))
                {
                    return RebuildHttps(url);
                }

                var dot = candidate.IndexOf('.');
                if (dot < 0) { return url; }
                candidate = candidate[(dot + 1)..];
            }
        }
    }

    private void Expire(DateTimeOffset now)
    {
        foreach (var host in policies.Where(pair => pair.Value.Expires <= now).Select(pair => pair.Key).ToArray())
        {
            policies.Remove(host);
        }
    }

    private static string? DnsHost(BrowserUrl url)
    {
        var host = url.Hostname;
        if (host.Length == 0 || host[0] == '[' || IPAddress.TryParse(host, out _)) { return null; }
        // A final DNS root label is not a distinct host; all other empty labels are invalid.
        host = host.EndsWith('.') ? host[..^1] : host;
        if (host.Length == 0 || host.Split('.').Any(label => label.Length == 0)) { return null; }
        return host.ToLowerInvariant();
    }

    private static BrowserUrl RebuildHttps(BrowserUrl url)
    {
        var credentials = url.Username.Length == 0 && url.Password.Length == 0 ? ""
            : url.Username + (url.Password.Length == 0 ? "" : ":" + url.Password) + "@";
        var port = url.Port is "" or "80" ? "" : ":" + url.Port;
        // URL component getters intentionally hide empty query/fragment delimiters.
        // Preserve those using serialized delimiter presence, as the loader does for fragments.
        var fragmentAt = url.Href.IndexOf('#');
        var queryAt = url.Href.IndexOf('?');
        var search = url.Search.Length != 0 ? url.Search
            : queryAt >= 0 && (fragmentAt < 0 || queryAt < fragmentAt) ? "?" : "";
        var hash = url.Hash.Length != 0 ? url.Hash : fragmentAt >= 0 ? "#" : "";
        return BrowserUrl.Parse($"https://{credentials}{url.Hostname}{port}{url.Pathname}{search}{hash}");
    }

    private static bool TryParse(string input, out ulong seconds, out bool includeSubDomains)
    {
        seconds = 0;
        includeSubDomains = false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var position = 0;
        while (position < input.Length)
        {
            SkipWhitespace(input, ref position);
            if (position == input.Length) { break; }
            if (input[position] == ';') { position++; continue; }
            var start = position;
            while (position < input.Length && IsToken(input[position])) { position++; }
            if (position == start) { return false; }
            var name = input[start..position];
            if (!seen.Add(name)) { return false; }
            SkipWhitespace(input, ref position);
            string? value = null;
            if (position < input.Length && input[position] == '=')
            {
                position++;
                SkipWhitespace(input, ref position);
                if (!ReadValue(input, ref position, out value)) { return false; }
                SkipWhitespace(input, ref position);
            }

            if (name.Equals("max-age", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(value)) { return false; }
                foreach (var digit in value)
                {
                    if (digit is < '0' or > '9') { return false; }
                    var number = (ulong)(digit - '0');
                    seconds = seconds > (ulong.MaxValue - number) / 10 ? ulong.MaxValue : seconds * 10 + number;
                }
            }
            else if (name.Equals("includeSubDomains", StringComparison.OrdinalIgnoreCase))
            {
                if (value is not null) { return false; }
                includeSubDomains = true;
            }

            if (position < input.Length && input[position++] != ';') { return false; }
        }

        return seen.Contains("max-age");
    }

    private static bool ReadValue(string input, ref int position, out string? value)
    {
        value = null;
        if (position == input.Length) { return false; }
        if (input[position] != '"')
        {
            var start = position;
            while (position < input.Length && IsToken(input[position])) { position++; }
            if (position == start) { return false; }
            value = input[start..position];
            return true;
        }

        position++;
        var text = new StringBuilder();
        while (position < input.Length)
        {
            var character = input[position++];
            if (character == '"') { value = text.ToString(); return true; }
            if (character == '\\')
            {
                // RFC 2616 quoted-pair = "\" CHAR (US-ASCII), including escaped digits.
                if (position == input.Length || input[position] > 127) { return false; }
                text.Append(input[position++]);
            }
            else
            {
                // TEXT permits LWS and non-ASCII octets, but no other unescaped controls.
                if (character != '\t' && (character < ' ' || character == 127 || character > 255)) { return false; }
                text.Append(character);
            }
        }

        return false;
    }

    private static void SkipWhitespace(string input, ref int position)
    {
        while (position < input.Length && input[position] is ' ' or '\t') { position++; }
    }

    private static bool IsToken(char character) =>
        character is > ' ' and < '\u007f' && !"()<>@,;:\\\"/[]?={}".Contains(character);
}
