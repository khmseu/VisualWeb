using System.Globalization;
using System.Text.Json;
using VisualWeb.Core.Url;

namespace VisualWeb.Engine.Scripting;

/// <summary>Private primitive-only bridge for the URL and URLSearchParams JavaScript facades.</summary>
internal sealed class UrlBindings
{
    internal const int MaxHandles = 1024;
    internal const int MaxCalls = 4096;
    internal const int MaxEntries = 1024;
    internal const int MaxTextCharacters = 65536;

    private sealed class State(MutableBrowserUrl? url, UrlSearchParams parameters)
    {
        internal MutableBrowserUrl? Url { get; } = url;
        internal UrlSearchParams Parameters { get; } = parameters;
    }

    private readonly Dictionary<int, State> states = [];
    private CancellationToken cancellation;
    private int calls;
    private int characters;

    internal void Begin(CancellationToken cancellationToken)
    {
        calls = 0;
        characters = 0;
        cancellation = cancellationToken;
    }

    internal void Dispose() => states.Clear();

    internal string Invoke(string operation, int id, string value)
    {
        try
        {
            if (++calls > MaxCalls) { throw new ScriptLimitException("URL callback count limit exceeded."); }
            Budget(value);
            cancellation.ThrowIfCancellationRequested();
            var result = operation switch
            {
                "url-create" => CreateUrl(value),
                "url-parse" => ParseUrl(value),
                "url-can-parse" => CanParseUrl(value),
                "params-create" => CreateParameters(value),
                "url-get" => GetUrl(id, value),
                "url-set" => SetUrl(id, value),
                "params-get" => GetParameters(id, value),
                "params-set" => SetParameters(id, value),
                "params-entry" => ParameterEntry(id, value),
                _ => throw new InvalidOperationException("Unknown private URL operation.")
            };
            Budget(result);
            return "ok:" + result;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return "limit:URL operation canceled.";
        }
        catch (ScriptLimitException exception) { return "limit:" + exception.Message; }
        catch (Exception exception) when (exception is UrlParseException or ArgumentException or FormatException
            or JsonException or InvalidOperationException or OverflowException)
        {
            // Only a private string status crosses the bridge; CLR exception objects never enter V8.
            return "error:" + (exception.Message.Length <= 256 ? exception.Message : exception.Message[..256]);
        }
        catch (Exception)
        {
            return "error:URL operation failed.";
        }
    }

    private string CreateUrl(string value)
    {
        using var document = JsonDocument.Parse(value);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() is < 1 or > 2)
        { throw new ArgumentException("Invalid URL constructor arguments."); }
        var input = RequiredString(root[0]);
        var baseUrl = root.GetArrayLength() == 2 && root[1].ValueKind != JsonValueKind.Null
            ? BrowserUrl.Parse(RequiredString(root[1]))
            : null;
        var parsed = BrowserUrl.Parse(input, baseUrl);
        CheckUrl(parsed);
        var url = new MutableBrowserUrl(parsed);
        return Add(new State(url, url.SearchParams)).ToString(CultureInfo.InvariantCulture);
    }

    private string ParseUrl(string value)
    {
        using var document = JsonDocument.Parse(value);
        var root = document.RootElement;
        var input = RequiredString(root[0]);
        var baseUrl = root.GetArrayLength() > 1 ? RequiredString(root[1]) : null;
        var url = BrowserUrl.ParseOrNull(input, baseUrl);
        if (url is null) { return "null"; }
        CheckUrl(url);
        var mutable = new MutableBrowserUrl(url);
        return Add(new State(mutable, mutable.SearchParams)).ToString(CultureInfo.InvariantCulture);
    }

    private static string CanParseUrl(string value)
    {
        using var document = JsonDocument.Parse(value);
        var root = document.RootElement;
        var input = RequiredString(root[0]);
        var baseUrl = root.GetArrayLength() > 1 ? RequiredString(root[1]) : null;
        return BrowserUrl.CanParse(input, baseUrl) ? "true" : "false";
    }

    private string CreateParameters(string value)
    {
        using var document = JsonDocument.Parse(value);
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var type) || !root.TryGetProperty("value", out var initial))
        { throw new ArgumentException("Invalid URLSearchParams constructor arguments."); }

        UrlSearchParams parameters;
        if (type.GetString() == "string")
        {
            parameters = new UrlSearchParams(RequiredString(initial));
        }
        else if (type.GetString() == "pairs" && initial.ValueKind == JsonValueKind.Array)
        {
            var pairs = new List<IReadOnlyList<string>>();
            foreach (var pair in initial.EnumerateArray())
            {
                if (pairs.Count >= MaxEntries || pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2)
                { throw new ArgumentException("Each URLSearchParams pair must contain exactly two values."); }
                pairs.Add([RequiredString(pair[0]), RequiredString(pair[1])]);
            }
            parameters = new UrlSearchParams(pairs);
        }
        else if (type.GetString() == "record" && initial.ValueKind == JsonValueKind.Array)
        {
            var record = new List<KeyValuePair<string, string>>();
            foreach (var pair in initial.EnumerateArray())
            {
                if (record.Count >= MaxEntries || pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2)
                { throw new ArgumentException("Invalid URLSearchParams record."); }
                record.Add(new(RequiredString(pair[0]), RequiredString(pair[1])));
            }
            parameters = new UrlSearchParams(record);
        }
        else { throw new ArgumentException("Invalid URLSearchParams constructor arguments."); }

        CheckParameters(parameters);
        return Add(new State(null, parameters)).ToString(CultureInfo.InvariantCulture);
    }

    private string GetUrl(int id, string attribute)
    {
        var url = RequireUrl(id).Snapshot;
        return attribute switch
        {
            "href" => JsonSerializer.Serialize(url.Href),
            "origin" => JsonSerializer.Serialize(url.SerializedOrigin),
            "protocol" => JsonSerializer.Serialize(url.Protocol),
            "username" => JsonSerializer.Serialize(url.Username),
            "password" => JsonSerializer.Serialize(url.Password),
            "host" => JsonSerializer.Serialize(url.Host),
            "hostname" => JsonSerializer.Serialize(url.Hostname),
            "port" => JsonSerializer.Serialize(url.Port),
            "pathname" => JsonSerializer.Serialize(url.Pathname),
            "search" => JsonSerializer.Serialize(url.Search),
            "hash" => JsonSerializer.Serialize(url.Hash),
            _ => throw new InvalidOperationException("Unknown URL attribute.")
        };
    }

    private string SetUrl(int id, string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var attribute = RequiredString(root[0]);
        var value = RequiredString(root[1]);
        var state = RequireUrl(id);
        if (attribute == "href")
        {
            CheckUrl(BrowserUrl.Parse(value));
            state.Href = value;
            return "";
        }
        if (attribute == "search")
        {
            CheckUrl(state.Snapshot.WithSearch(value));
            state.Search = value;
            return "";
        }

        var current = state.Snapshot;
        var replacement = attribute switch
        {
            "protocol" => current.WithProtocol(value),
            "username" => current.WithUsername(value),
            "password" => current.WithPassword(value),
            "host" => current.WithHost(value),
            "hostname" => current.WithHostname(value),
            "port" => current.WithPort(value),
            "pathname" => current.WithPathname(value),
            "hash" => current.WithHash(value),
            _ => throw new InvalidOperationException("Unknown URL attribute.")
        };
        CheckUrl(replacement);
        if (replacement.Href != current.Href) { state.Href = replacement.Href; }
        return "";
    }

    private string GetParameters(int id, string operation)
    {
        var parameters = RequireState(id).Parameters;
        return operation switch
        {
            "size" => parameters.Size.ToString(CultureInfo.InvariantCulture),
            "toString" => JsonSerializer.Serialize(parameters.ToString()),
            _ => GetParameterByName(parameters, operation)
        };
    }

    private string GetParameterByName(UrlSearchParams parameters, string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var operation = RequiredString(root[0]);
        var name = RequiredString(root[1]);
        return operation switch
        {
            "get" => JsonSerializer.Serialize(parameters.Get(name)),
            "getAll" => JsonSerializer.Serialize(parameters.GetAll(name)),
            "has" => JsonSerializer.Serialize(root.GetArrayLength() == 3
                ? parameters.Has(name, RequiredString(root[2]))
                : parameters.Has(name)),
            _ => throw new InvalidOperationException("Unknown URLSearchParams operation.")
        };
    }

    private string SetParameters(int id, string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var operation = RequiredString(root[0]);
        var parameters = RequireState(id).Parameters;
        var name = root.GetArrayLength() > 1 ? RequiredString(root[1]) : "";
        var value = root.GetArrayLength() > 2 ? RequiredString(root[2]) : "";
        var preview = new UrlSearchParams(parameters.Entries()
            .Select(pair => (IReadOnlyList<string>)[pair.Key, pair.Value]));
        ApplyMutation(preview, operation, name, value, root.GetArrayLength() > 2);
        CheckParameters(preview);
        if (RequireState(id).Url is { } url) { CheckUrl(url.Snapshot.WithSearchParams(preview)); }
        ApplyMutation(parameters, operation, name, value, root.GetArrayLength() > 2);
        return "";
    }

    private static void ApplyMutation(UrlSearchParams parameters, string operation, string name, string value,
        bool hasValue)
    {
        switch (operation)
        {
            case "append": parameters.Append(name, value); break;
            case "set": parameters.Set(name, value); break;
            case "delete":
                if (hasValue) { parameters.Delete(name, value); }
                else { parameters.Delete(name); }
                break;
            case "sort": parameters.Sort(); break;
            default: throw new InvalidOperationException("Unknown URLSearchParams mutation.");
        }
    }

    private string ParameterEntry(int id, string value)
    {
        var index = int.Parse(value, CultureInfo.InvariantCulture);
        var parameters = RequireState(id).Parameters;
        if (index < 0 || index >= parameters.Size) { return "null"; }
        var pair = parameters.Entries().Skip(index).First();
        return JsonSerializer.Serialize(new[] { pair.Key, pair.Value });
    }

    private MutableBrowserUrl RequireUrl(int id) => RequireState(id).Url
        ?? throw new ArgumentException("Illegal URL receiver.");

    private State RequireState(int id) => states.TryGetValue(id, out var state)
        ? state
        : throw new ArgumentException("Illegal URLSearchParams receiver.");

    private int Add(State state)
    {
        if (states.Count >= MaxHandles) { throw new ScriptLimitException("URL wrapper identity limit exceeded."); }
        var id = states.Count + 1;
        states.Add(id, state);
        return id;
    }

    private static string RequiredString(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString()!
        : throw new ArgumentException("Web IDL string conversion failed.");

    private static void CheckParameters(UrlSearchParams parameters)
    {
        if (parameters.Size > MaxEntries) { throw new ScriptLimitException("URLSearchParams entry limit exceeded."); }
        if (parameters.ToString().Length > MaxTextCharacters)
        { throw new ScriptLimitException("URLSearchParams text limit exceeded."); }
    }

    private static void CheckUrl(BrowserUrl url)
    {
        if (url.Href.Length > MaxTextCharacters)
        { throw new ScriptLimitException("URL serialization text limit exceeded."); }
        CheckParameters(url.CreateSearchParams());
    }

    private void Budget(string value)
    {
        if (value.Length > MaxTextCharacters || (characters += value.Length) > 4 * MaxTextCharacters)
        { throw new ScriptLimitException("URL callback text limit exceeded."); }
    }

    internal const string Bootstrap = """
        (() => {
            const bridge = globalThis.__visualwebUrl;
            delete globalThis.__visualwebUrl;
            const apply = Function.prototype.call.bind(Function.prototype.call);
            const stringify = JSON.stringify, parse = JSON.parse;
            const string = String;
            const stringStartsWith = String.prototype.startsWith, stringSlice = String.prototype.slice;
            const stringCharCodeAt = String.prototype.charCodeAt;
            const arrayPush = Array.prototype.push;
            const ownKeys = Reflect.ownKeys, getOwnPropertyDescriptor = Object.getOwnPropertyDescriptor;
            const create = Object.create, define = Object.defineProperty;
            const objectGetPrototypeOf = Object.getPrototypeOf, arrayValues = Array.prototype.values;
            const weakGet = WeakMap.prototype.get, weakSet = WeakMap.prototype.set;
            const TypeErrorCtor = TypeError, RangeErrorCtor = RangeError;
            const iterator = Symbol.iterator, toStringTag = Symbol.toStringTag;
            const named = (fn, name) => {
                define(fn, 'name', {value:name, configurable:true});
                return fn;
            };
            const nativeIteratorPrototype =
                objectGetPrototypeOf(objectGetPrototypeOf(apply(arrayValues, [], [])));
            const urlBrands = new WeakMap(), paramsBrands = new WeakMap(), paramsCache = new WeakMap();
            const iteratorStates = new WeakMap();
            const urlPrototype = create(Object.prototype), paramsPrototype = create(Object.prototype);
            const paramsIteratorPrototype = create(nativeIteratorPrototype);
            define(urlPrototype, toStringTag, {value:'URL', configurable:true});
            define(paramsPrototype, toStringTag, {value:'URLSearchParams', configurable:true});
            const required = (count, minimum) => {
                if (count < minimum) throw new TypeErrorCtor('Not enough arguments');
            };
            const brand = (brands, receiver, name) => {
                const id = apply(weakGet, brands, receiver);
                if (id === undefined) throw new TypeErrorCtor('Illegal ' + name + ' receiver');
                return id;
            };
            const urlBrand = receiver => brand(urlBrands, receiver, 'URL');
            const paramsBrand = receiver => brand(paramsBrands, receiver, 'URLSearchParams');
            const startsWith = (value, prefix) => apply(stringStartsWith, value, prefix);
            const slice = (value, start, end) => apply(stringSlice, value, start, end);
            const charCodeAt = (value, index) => apply(stringCharCodeAt, value, index);
            const push = (array, value) => apply(arrayPush, array, value);
            const call = (operation, id, value = '') => {
                const result = bridge(operation, id, value);
                if (startsWith(result, 'limit:')) throw new RangeErrorCtor(slice(result, 6));
                if (startsWith(result, 'error:')) throw new TypeErrorCtor(slice(result, 6));
                if (!startsWith(result, 'ok:')) throw new TypeErrorCtor('URL operation failed');
                return slice(result, 3);
            };
            const usv = value => {
                if (typeof value === 'symbol') throw new TypeErrorCtor('Cannot convert a Symbol value to a string');
                let text = string(value), result = '';
                for (let i=0;i<text.length;++i) {
                    const first = charCodeAt(text, i);
                    if (first >= 0xD800 && first <= 0xDBFF) {
                        if (i + 1 < text.length) {
                            const second = charCodeAt(text, i + 1);
                            if (second >= 0xDC00 && second <= 0xDFFF) {
                                result += text[i] + text[++i]; continue;
                            }
                        }
                        result += '\uFFFD';
                    } else if (first >= 0xDC00 && first <= 0xDFFF) result += '\uFFFD';
                    else result += text[i];
                }
                if (result.length > 65536) throw new TypeErrorCtor('URL string limit exceeded');
                return result;
            };
            const json = value => stringify(value);
            const readUrl = (id, name) => parse(call('url-get', id, name));
            const writeUrl = (id, name, value) =>
                call('url-set', id, json([name, usv(value)]));
            function URL(url, base = undefined) {
                if (!new.target) throw new TypeErrorCtor("URL constructor must be called with 'new'");
                required(arguments.length, 1);
                const input = usv(url);
                const args = base !== undefined ? [input, usv(base)] : [input];
                const id = +call('url-create', 0, json(args));
                apply(weakSet, urlBrands, this, id);
            }
            define(urlPrototype, 'constructor', {value: URL, writable: true, configurable: true});
            const urlAttributes = ['href','origin','protocol','username','password','host','hostname','port','pathname','search','hash'];
            for (const name of urlAttributes) define(urlPrototype, name, {
                enumerable: true, configurable: true,
                get: named(function() {return readUrl(urlBrand(this), name);}, `get ${name}`),
                ...(name === 'origin' ? {} : {
                    set: named(function(value) {writeUrl(urlBrand(this), name, value);}, `set ${name}`)
                })
            });
            define(urlPrototype, 'searchParams', {enumerable: true, configurable: true, get: named(function() {
                const id = urlBrand(this);
                let result = apply(weakGet, paramsCache, this);
                if (result === undefined) {
                    result = create(paramsPrototype);
                    apply(weakSet, paramsBrands, result, id);
                    apply(weakSet, paramsCache, this, result);
                }
                return result;
            }, 'get searchParams')});
            define(urlPrototype, 'toString', {
                enumerable: true, configurable: true, writable: true,
                value: named(function() {return readUrl(urlBrand(this), 'href');}, 'toString')
            });
            define(urlPrototype, 'toJSON', {
                enumerable: true, configurable: true, writable: true,
                value: named(function() {return readUrl(urlBrand(this), 'href');}, 'toJSON')
            });
            define(URL, 'parse', {enumerable:true, configurable:true, writable:true, value: named(function(url, base = undefined) {
                required(arguments.length, 1);
                const args = base !== undefined ? [usv(url), usv(base)] : [usv(url)];
                const result = call('url-parse', 0, json(args));
                return result === 'null' ? null : makeUrl(+result);
            }, 'parse')});
            define(URL, 'canParse', {enumerable:true, configurable:true, writable:true, value: named(function(url, base = undefined) {
                required(arguments.length, 1);
                const args = base !== undefined ? [usv(url), usv(base)] : [usv(url)];
                return call('url-can-parse', 0, json(args)) === 'true';
            }, 'canParse')});
            function makeUrl(id) {
                const result = create(urlPrototype);
                apply(weakSet, urlBrands, result, id);
                return result;
            }
            const initParams = init => {
                if (typeof init === 'string') return {type:'string', value:usv(init)};
                if (init !== null && (typeof init === 'object' || typeof init === 'function')) {
                    const iteratorMethod = init[iterator];
                    if (iteratorMethod !== null && iteratorMethod !== undefined) {
                        if (typeof iteratorMethod !== 'function')
                            throw new TypeErrorCtor('URLSearchParams iterator must be callable');
                        const iterable = create(null);
                        define(iterable, iterator, {value:function() {return apply(iteratorMethod, init);}});
                        const pairs = [];
                        for (const pair of iterable) {
                            if (pairs.length >= 1024) throw new TypeErrorCtor('URLSearchParams entry limit exceeded');
                            if (pair === null || pair === undefined) throw new TypeErrorCtor('Invalid URLSearchParams pair');
                            const values = [];
                            for (const value of pair) {
                                if (values.length >= 3) throw new TypeErrorCtor('Each URLSearchParams pair must contain exactly two values');
                                push(values, usv(value));
                            }
                            if (values.length !== 2) throw new TypeErrorCtor('Each URLSearchParams pair must contain exactly two values');
                            push(pairs, values);
                        }
                        return {type:'pairs', value:pairs};
                    }
                    const keys = ownKeys(init), record = [];
                    for (let index=0; index<keys.length; ++index) {
                        const key = keys[index], descriptor = getOwnPropertyDescriptor(init, key);
                        if (!descriptor || !descriptor.enumerable) continue;
                        if (record.length >= 1024) throw new TypeErrorCtor('URLSearchParams entry limit exceeded');
                        push(record, [usv(key), usv(init[key])]);
                    }
                    return {type:'record', value:record};
                }
                return {type:'string', value:usv(init)};
            };
            function URLSearchParams(init = '') {
                if (!new.target) throw new TypeErrorCtor("URLSearchParams constructor must be called with 'new'");
                const id = +call('params-create', 0, json(initParams(init)));
                apply(weakSet, paramsBrands, this, id);
            }
            define(paramsPrototype, 'constructor', {value: URLSearchParams, writable: true, configurable: true});
            define(paramsPrototype, 'size', {
                enumerable:true, configurable:true,
                get: named(function() {return +call('params-get', paramsBrand(this), 'size');}, 'get size')
            });
            for (const method of ['append','set']) define(paramsPrototype, method, {
                enumerable:true, configurable:true, writable:true,
                value:named(function(name, value) {
                    const id = paramsBrand(this); required(arguments.length, 2);
                    const first = usv(name), second = usv(value);
                    call('params-set', id, json([method, first, second]));
                }, method)
            });
            define(paramsPrototype, 'delete', {enumerable:true, configurable:true, writable:true, value:named(function(name, value = undefined) {
                const id = paramsBrand(this); required(arguments.length, 1);
                const first = usv(name);
                if (value !== undefined) call('params-set', id, json(['delete', first, usv(value)]));
                else call('params-set', id, json(['delete', first]));
            }, 'delete')});
            define(paramsPrototype, 'get', {enumerable:true, configurable:true, writable:true, value:named(function(name) {
                const id = paramsBrand(this); required(arguments.length, 1);
                return parse(call('params-get', id, json(['get', usv(name)])));
            }, 'get')});
            define(paramsPrototype, 'getAll', {enumerable:true, configurable:true, writable:true, value:named(function(name) {
                const id = paramsBrand(this); required(arguments.length, 1);
                return parse(call('params-get', id, json(['getAll', usv(name)])));
            }, 'getAll')});
            define(paramsPrototype, 'has', {enumerable:true, configurable:true, writable:true, value:named(function(name, value = undefined) {
                const id = paramsBrand(this); required(arguments.length, 1);
                const args = ['has', usv(name)];
                if (value !== undefined) push(args, usv(value));
                return parse(call('params-get', id, json(args)));
            }, 'has')});
            define(paramsPrototype, 'sort', {enumerable:true, configurable:true, writable:true, value:named(function() {
                const id = paramsBrand(this);
                call('params-set', id, json(['sort']));
            }, 'sort')});
            const entry = (id, index) => {
                const result = call('params-entry', id, '' + index);
                return parse(result);
            };
            define(paramsIteratorPrototype, 'next', {enumerable:true, configurable:true, writable:true,
                value:function next() {
                    const state = apply(weakGet, iteratorStates, this);
                    if (state === undefined) throw new TypeErrorCtor('Illegal URLSearchParams iterator receiver');
                    if (state.index >= 1024) return {value:undefined, done:true};
                    const pair = entry(state.id, state.index);
                    if (pair === null) return {value:undefined, done:true};
                    ++state.index;
                    return {value:state.kind === 'keys' ? pair[0] : state.kind === 'values' ? pair[1] : pair, done:false};
                }});
            define(paramsIteratorPrototype, toStringTag, {value:'URLSearchParams Iterator', configurable:true});
            const makeIterator = (id, kind) => {
                const result = create(paramsIteratorPrototype);
                apply(weakSet, iteratorStates, result, {id, kind, index:0});
                return result;
            };
            const iterate = (kind, name) => {
                const method = function() {
                    return makeIterator(paramsBrand(this), kind);
                };
                define(method, 'name', {value:name, configurable:true});
                return method;
            };
            const keys = iterate('keys', 'keys');
            const values = iterate('values', 'values');
            const entries = iterate('entries', 'entries');
            define(paramsPrototype, 'keys', {enumerable:true, configurable:true, writable:true, value:keys});
            define(paramsPrototype, 'values', {enumerable:true, configurable:true, writable:true, value:values});
            define(paramsPrototype, 'entries', {enumerable:true, configurable:true, writable:true, value:entries});
            define(paramsPrototype, iterator, {configurable:true, writable:true, value:entries});
            define(paramsPrototype, 'forEach', {enumerable:true, configurable:true, writable:true, value:named(function(callback, thisArg = undefined) {
                const id = paramsBrand(this);
                if (typeof callback !== 'function') throw new TypeErrorCtor('URLSearchParams forEach callback must be callable');
                for (let index=0;;++index) {
                    const pair = entry(id, index);
                    if (pair === null) return;
                    apply(callback, thisArg, pair[1], pair[0], this);
                }
            }, 'forEach')});
            define(paramsPrototype, 'toString', {enumerable:true, configurable:true, writable:true, value:named(function() {
                return parse(call('params-get', paramsBrand(this), 'toString'));
            }, 'toString')});
            define(URL, 'prototype', {value:urlPrototype, writable:false});
            define(URLSearchParams, 'prototype', {value:paramsPrototype, writable:false});
            define(globalThis, 'URL', {value:URL, writable:true, configurable:true});
            define(globalThis, 'URLSearchParams', {value:URLSearchParams, writable:true, configurable:true});
        })()
        """;
}
