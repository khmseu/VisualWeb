using System.Collections.Frozen;
using System.Globalization;
using VisualWeb.Core.Url;

namespace VisualWeb.Engine.Net;

/// <summary>Shared Fetch bad-port predicate applied before every HTTP(S) request and redirect hop.</summary>
/// <remarks>Spec: fetch; <see href="https://fetch.spec.whatwg.org/#block-bad-port">should be blocked due to a bad port</see>
/// with the <see href="https://fetch.spec.whatwg.org/#bad-port">bad port</see> table
/// (<see href="https://fetch.spec.whatwg.org/#port-blocking">port blocking</see>). Only HTTP(S) URLs are checked;
/// a null (scheme-default) URL port is never bad.</remarks>
internal static class BadPortPolicy
{
    private static readonly FrozenSet<int> BadPorts = new[]
    {
        0, 1, 7, 9, 11, 13, 15, 17, 19, 20, 21, 22, 23, 25, 37, 42, 43, 53, 69, 77, 79, 87, 95, 101, 102, 103, 104,
        109, 110, 111, 113, 115, 117, 119, 123, 135, 137, 139, 143, 161, 179, 389, 427, 465, 512, 513, 514, 515, 526,
        530, 531, 532, 540, 548, 554, 556, 563, 587, 601, 636, 989, 990, 993, 995, 1719, 1720, 1723, 2049, 3659, 4045,
        4190, 5060, 5061, 6000, 6566, 6665, 6666, 6667, 6668, 6669, 6679, 6697, 10080,
    }.ToFrozenSet();

    internal static bool IsBlocked(BrowserUrl url) =>
        url.Protocol is "http:" or "https:"
        && url.Port.Length != 0
        && BadPorts.Contains(int.Parse(url.Port, NumberStyles.None, CultureInfo.InvariantCulture));

    internal static void ThrowIfBlocked(BrowserUrl url)
    {
        if (IsBlocked(url))
        {
            throw new ResourceLoadException(ResourceError.BlockedPort,
                $"Fetch bad port: HTTP(S) requests to port {url.Port} are blocked.");
        }
    }
}
