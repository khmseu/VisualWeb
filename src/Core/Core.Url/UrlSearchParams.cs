using System.Text;

namespace VisualWeb.Core.Url;

/// <summary>Provides a bounded string-initialization, lookup, and append subset of WHATWG <c>URLSearchParams</c>.</summary>
/// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#interface-urlsearchparams">URLSearchParams</see> and
/// <see href="https://url.spec.whatwg.org/#concept-urlencoded-string-parser">the URL-encoded string parser</see>.
/// This .NET subset has no linked URL object and does not provide other mutations, stringification, or Web IDL.</remarks>
public sealed class UrlSearchParams
{
    private static readonly UTF8Encoding Utf8 = new(false, false);
    private readonly List<(string Name, string Value)> _entries;

    /// <summary>Initializes from a URL-encoded string, removing one leading question mark if present.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-urlsearchparams-urlsearchparams">URLSearchParams constructor</see>.</remarks>
    public UrlSearchParams(string init = "")
    {
        ArgumentNullException.ThrowIfNull(init);
        _entries = Parse(init.StartsWith('?') ? init[1..] : init);
    }

    /// <summary>Gets the number of parsed name-value tuples.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-urlsearchparams-size">URLSearchParams.size</see>.</remarks>
    public int Size => _entries.Count;

    /// <summary>Appends a scalar-value name and value pair to the end of the tuple list.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-urlsearchparams-append">URLSearchParams.append()</see>.
    /// This standalone Core object has no associated URL to synchronize.</remarks>
    public void Append(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        _entries.Add((ToScalarValueString(name), ToScalarValueString(value)));
    }

    /// <summary>Gets the first value associated with <paramref name="name"/>, or <see langword="null"/>.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-urlsearchparams-get">URLSearchParams.get()</see>.</remarks>
    public string? Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var scalarName = ToScalarValueString(name);
        foreach (var entry in _entries)
        {
            if (entry.Name == scalarName) { return entry.Value; }
        }

        return null;
    }

    /// <summary>Gets a fresh array of all values associated with <paramref name="name"/>, in tuple order.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-urlsearchparams-getall">URLSearchParams.getAll()</see>.</remarks>
    public string[] GetAll(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var scalarName = ToScalarValueString(name);
        return _entries.Where(entry => entry.Name == scalarName).Select(entry => entry.Value).ToArray();
    }

    /// <summary>Determines whether an entry has the given scalar-value name.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-urlsearchparams-has">URLSearchParams.has()</see>.
    /// This overload checks only the name.</remarks>
    public bool Has(string name) => Get(name) is not null;

    /// <summary>Determines whether an entry has the given scalar-value name and value.</summary>
    /// <remarks>Spec: url; <see href="https://url.spec.whatwg.org/#dom-urlsearchparams-has">URLSearchParams.has()</see>.</remarks>
    public bool Has(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        var scalarName = ToScalarValueString(name);
        var scalarValue = ToScalarValueString(value);
        return _entries.Any(entry => entry.Name == scalarName && entry.Value == scalarValue);
    }

    private static List<(string Name, string Value)> Parse(string input)
    {
        var bytes = Utf8.GetBytes(input);
        var entries = new List<(string Name, string Value)>();
        var start = 0;
        while (start < bytes.Length)
        {
            var separator = Array.IndexOf(bytes, (byte)'&', start);
            var end = separator < 0 ? bytes.Length : separator;
            if (end > start)
            {
                var equals = Array.IndexOf(bytes, (byte)'=', start, end - start);
                var nameEnd = equals < 0 ? end : equals;
                var valueStart = equals < 0 ? end : equals + 1;
                entries.Add((
                    DecodeComponent(bytes, start, nameEnd - start),
                    DecodeComponent(bytes, valueStart, end - valueStart)));
            }

            if (separator < 0) { break; }
            start = separator + 1;
        }

        return entries;
    }

    // Compact decoded bytes inside this component without touching delimiters.
    private static string DecodeComponent(byte[] bytes, int start, int length)
    {
        var end = start + length;
        var read = start;
        var written = start;
        while (read < end)
        {
            var value = bytes[read++];
            if (value == (byte)'+')
            {
                value = (byte)' ';
            }
            else if (value == (byte)'%' && read + 1 < end)
            {
                var high = HexValue(bytes[read]);
                var low = HexValue(bytes[read + 1]);
                if (high >= 0 && low >= 0)
                {
                    value = (byte)((high << 4) | low);
                    read += 2;
                }
            }

            bytes[written++] = value;
        }

        return Utf8.GetString(bytes.AsSpan(start, written - start));
    }

    private static int HexValue(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - '0',
        >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
        >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
        _ => -1
    };

    private static string ToScalarValueString(string value) => Utf8.GetString(Utf8.GetBytes(value));
}
