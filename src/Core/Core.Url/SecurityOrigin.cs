using System.Globalization;

namespace VisualWeb.Core.Url;

/// <summary>Immutable security-origin identity: a tuple origin or a unique opaque origin.</summary>
/// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#concept-url-origin">origin of a URL</see>.
/// Spec: html; <see href="https://html.spec.whatwg.org/multipage/browsers.html#concept-origin">origin</see>,
/// <see href="https://html.spec.whatwg.org/multipage/browsers.html#concept-origin-opaque">opaque origin</see>,
/// <see href="https://html.spec.whatwg.org/multipage/browsers.html#same-origin">same origin</see> and
/// <see href="https://html.spec.whatwg.org/multipage/browsers.html#ascii-serialisation-of-an-origin">serialization of an origin</see>.
/// This models URL origins only. Tuple origins are compared by scheme, host and effective port (the domain
/// component is always null). Opaque origins are equal only to the very same instance; their "null"
/// serialization is never an identity. Document/sandbox/inherited origins and document.domain belong to
/// later phases, as does any blob URL entry registry: a <c>blob:</c> URL yields a tuple origin only when its
/// path parses as an http(s) URL, otherwise a fresh opaque origin. <c>file:</c> URLs always yield
/// opaque origins, which the URL spec permits.</remarks>
public sealed class SecurityOrigin : IEquatable<SecurityOrigin>
{
    private SecurityOrigin(string? scheme, string? host, int port)
    {
        Scheme = scheme;
        Host = host;
        Port = port;
    }

    /// <summary>Tuple scheme, lowercase; null for an opaque origin.</summary>
    public string? Scheme { get; }

    /// <summary>Tuple host in URL serializer form (IDNA-normalized, bracketed IPv6); null for an opaque origin.</summary>
    public string? Host { get; }

    /// <summary>Effective port (the scheme default when the URL has none); 0 for an opaque origin.</summary>
    public int Port { get; }

    public bool IsOpaque => Scheme is null;

    /// <summary>Creates a new opaque origin that is equal only to itself.</summary>
    public static SecurityOrigin CreateOpaque() => new(null, null, 0);

    internal static SecurityOrigin FromTuple(string scheme, string host, string port) =>
        new(scheme, host, port.Length == 0 ? DefaultPort(scheme) : int.Parse(port, CultureInfo.InvariantCulture));

    /// <summary>Same origin per HTML: tuple components equal, or the identical opaque origin.</summary>
    public bool IsSameOrigin(SecurityOrigin? other) => Equals(other);

    /// <summary>Display/serialization form; opaque origins give "null", which must never be compared.</summary>
    public string Serialize()
    {
        if (IsOpaque)
        {
            return "null";
        }

        return Port == DefaultPort(Scheme!) ? $"{Scheme}://{Host}" : $"{Scheme}://{Host}:{Port}";
    }

    public bool Equals(SecurityOrigin? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null && !IsOpaque && !other.IsOpaque
            && Port == other.Port
            && string.Equals(Scheme, other.Scheme, StringComparison.Ordinal)
            && string.Equals(Host, other.Host, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as SecurityOrigin);

    public override int GetHashCode() => IsOpaque
        ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this)
        : HashCode.Combine(Scheme, Host, Port);

    public override string ToString() => Serialize();

    private static int DefaultPort(string scheme) => scheme switch
    {
        "ftp" => 21,
        "http" or "ws" => 80,
        "https" or "wss" => 443,
        _ => 0,
    };
}
