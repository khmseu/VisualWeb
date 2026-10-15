using System.Net;
using System.Text;
using VisualWeb.Core.Url;

namespace VisualWeb.Engine.Net;

/// <summary>Bounded immutable matcher for the pinned Public Suffix List.</summary>
/// <remarks>Spec: public-suffix-list;
/// <see href="https://publicsuffix.org/list/#format">list format and prevailing rule</see>.
/// Includes both ICANN and PRIVATE sections. It is not a registrable-domain or site computation API.</remarks>
internal sealed class PublicSuffixList
{
    internal const int MaxCharacters = 1_000_000;
    internal const int MaxLineCharacters = 512;
    internal const int MaxRules = 20_000;
    internal const int MaxLabels = 32;
    private const string ResourceName = "VisualWeb.Engine.Net.public_suffix_list.dat";
    private static readonly Lazy<PublicSuffixList> DefaultValue = new(LoadDefault);
    private readonly HashSet<string> exact;
    private readonly HashSet<string> wildcard;
    private readonly HashSet<string> exceptions;

    private PublicSuffixList(HashSet<string> exact, HashSet<string> wildcard, HashSet<string> exceptions)
    {
        this.exact = exact;
        this.wildcard = wildcard;
        this.exceptions = exceptions;
        RuleCount = exact.Count + wildcard.Count + exceptions.Count;
    }

    internal static PublicSuffixList Default => DefaultValue.Value;
    internal int RuleCount { get; }

    internal static PublicSuffixList Parse(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length > MaxCharacters)
        { throw new InvalidDataException($"Public Suffix List character limit ({MaxCharacters}) exceeded."); }

        var exact = new HashSet<string>(StringComparer.Ordinal);
        var wildcard = new HashSet<string>(StringComparer.Ordinal);
        var exceptions = new HashSet<string>(StringComparer.Ordinal);
        using var reader = new StringReader(data);
        while (reader.ReadLine() is { } input)
        {
            if (input.Length > MaxLineCharacters)
            { throw new InvalidDataException($"Public Suffix List line limit ({MaxLineCharacters}) exceeded."); }
            var line = input.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) { continue; }

            var target = exact;
            if (line[0] == '!')
            {
                target = exceptions;
                line = line[1..];
            }
            else if (line.StartsWith("*.", StringComparison.Ordinal))
            {
                target = wildcard;
                line = line[2..];
            }

            var rule = NormalizeDomain(line);
            if (!target.Add(rule)) { throw new InvalidDataException($"Duplicate Public Suffix List rule '{line}'."); }
            if (exact.Count + wildcard.Count + exceptions.Count > MaxRules)
            { throw new InvalidDataException($"Public Suffix List rule limit ({MaxRules}) exceeded."); }
        }

        if (exact.Count + wildcard.Count + exceptions.Count == 0)
        { throw new InvalidDataException("Public Suffix List contains no rules."); }
        return new(exact, wildcard, exceptions);
    }

    internal bool IsPublicSuffix(string host)
    {
        var normalized = NormalizeDomain(host);
        var labels = normalized.Split('.');
        var exceptionLength = 0;
        var prevailingLength = 1;
        for (var index = 0; index < labels.Length; index++)
        {
            var suffix = string.Join('.', labels, index, labels.Length - index);
            var suffixLength = labels.Length - index;
            if (exceptions.Contains(suffix))
            {
                exceptionLength = Math.Max(exceptionLength, suffixLength);
            }
            if (exact.Contains(suffix))
            {
                prevailingLength = Math.Max(prevailingLength, suffixLength);
            }
            if (index > 0 && wildcard.Contains(suffix))
            {
                prevailingLength = Math.Max(prevailingLength, suffixLength + 1);
            }
        }

        var publicSuffixLength = exceptionLength > 0 ? exceptionLength - 1 : prevailingLength;
        return labels.Length == publicSuffixLength;
    }

    private static PublicSuffixList LoadDefault()
    {
        using var stream = typeof(PublicSuffixList).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException($"Missing embedded Public Suffix List resource '{ResourceName}'.");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        return Parse(reader.ReadToEnd());
    }

    private static string NormalizeDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain) || domain.Contains('/') || domain.Contains(':'))
        { throw new InvalidDataException($"Invalid Public Suffix List domain '{domain}'."); }
        var parsed = BrowserUrl.ParseResult($"https://{domain}/").Url;
        if (parsed is null || parsed.Hostname.Length == 0 || parsed.Hostname[0] == '['
            || IPAddress.TryParse(parsed.Hostname, out _))
        {
            throw new InvalidDataException($"Invalid Public Suffix List domain '{domain}'.");
        }
        var normalized = parsed.Hostname.EndsWith('.') ? parsed.Hostname[..^1] : parsed.Hostname;
        var labels = normalized.Split('.');
        if (labels.Length > MaxLabels || labels.Any(label => label.Length == 0))
        { throw new InvalidDataException($"Invalid Public Suffix List domain '{domain}'."); }
        return normalized.ToLowerInvariant();
    }
}
