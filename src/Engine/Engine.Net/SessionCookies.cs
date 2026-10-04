using System.Net;

namespace VisualWeb.Engine.Net;

internal sealed class SessionCookies
{
    private readonly CookieContainer cookies = new();
    private readonly object gate = new();

    public string Header(Uri uri)
    {
        lock (gate)
        {
            return cookies.GetCookieHeader(uri);
        }
    }

    // Basic host-only cookies use the BCL store, not a full browser cookie policy.
    // Spec: http-cookies; https://www.rfc-editor.org/rfc/rfc6265.html#section-5.3
    public void Store(Uri uri, IEnumerable<string> values, List<string> diagnostics)
    {
        lock (gate)
        {
            foreach (var value in values)
            {
                var parts = value.Split(';', StringSplitOptions.TrimEntries);
                var equals = parts[0].IndexOf('=');
                if (equals <= 0 || parts[0][..equals].Any(c => !char.IsAsciiLetterOrDigit(c)
                    && c is not ('!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~')))
                {
                    diagnostics.Add("Rejected malformed Set-Cookie name.");
                    continue;
                }

                var attributes = parts.Skip(1).Select(p => p.Split('=', 2, StringSplitOptions.TrimEntries)).ToArray();
                bool Has(string name) => attributes.Any(p => p[0].Equals(name, StringComparison.OrdinalIgnoreCase));
                var secure = Has("Secure");
                var name = parts[0][..equals];
                var paths = attributes.Where(p => p[0].Equals("Path", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (Has("Domain") || Has("SameSite") || Has("Partitioned"))
                {
                    diagnostics.Add("Rejected Set-Cookie with unsupported Domain, SameSite or Partitioned policy.");
                    continue;
                }

                if (secure && uri.Scheme != "https"
                    || name.StartsWith("__Secure-", StringComparison.OrdinalIgnoreCase) && !secure
                    || name.StartsWith("__Host-", StringComparison.OrdinalIgnoreCase)
                        && (!secure || paths.Length == 0 || paths.Any(p => p is not [_, "/"])))
                {
                    diagnostics.Add("Rejected Set-Cookie with invalid secure transport or prefix requirements.");
                    continue;
                }

                if (uri.Scheme != "https" && cookies.GetAllCookies().Cast<Cookie>().Any(old =>
                    old.Secure && !old.Expired && old.Name == name
                    && old.Domain.TrimStart('.').Equals(uri.IdnHost, StringComparison.OrdinalIgnoreCase)))
                {
                    diagnostics.Add("Rejected insecure overwrite of a stored Secure cookie.");
                    continue;
                }

                try
                {
                    cookies.SetCookies(uri, value);
                }
                catch (CookieException)
                {
                    diagnostics.Add("Rejected malformed Set-Cookie value.");
                }
            }
        }
    }
}
