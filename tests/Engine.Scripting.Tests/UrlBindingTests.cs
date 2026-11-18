using VisualWeb.Engine.Scripting;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class UrlBindingTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void UrlInterfaceOperationsAndAttributesHaveWebIdlDescriptors()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const operation = (owner, key, enumerable = true) => {
                    const d = Object.getOwnPropertyDescriptor(owner, key);
                    return d && typeof d.value === 'function' && d.writable === true
                        && d.enumerable === enumerable && d.configurable === true;
                };
                const attribute = (owner, key, writable) => {
                    const d = Object.getOwnPropertyDescriptor(owner, key);
                    return d && typeof d.get === 'function'
                        && (writable ? typeof d.set === 'function' : d.set === undefined)
                        && d.enumerable === true && d.configurable === true;
                };
                const tag = (owner, value) => {
                    const d = Object.getOwnPropertyDescriptor(owner, Symbol.toStringTag);
                    return d && d.value === value && !d.writable && !d.enumerable && d.configurable;
                };
                const constructor = (prototype, value) => {
                    const d = Object.getOwnPropertyDescriptor(prototype, 'constructor');
                    return d && d.value === value && d.writable === true
                        && d.enumerable === false && d.configurable === true;
                };
                const url = URL.prototype, params = URLSearchParams.prototype;
                return Object.isExtensible(url) && Object.isExtensible(params)
                    && tag(url, 'URL') && tag(params, 'URLSearchParams')
                    && constructor(url, URL) && constructor(params, URLSearchParams)
                    && ['toString', 'toJSON'].every(key => operation(url, key))
                    && ['parse', 'canParse'].every(key => operation(URL, key))
                    && ['append', 'delete', 'get', 'getAll', 'has', 'set', 'sort',
                        'keys', 'values', 'entries', 'forEach', 'toString']
                        .every(key => operation(params, key))
                    && operation(params, Symbol.iterator, false)
                    && params[Symbol.iterator] === params.entries
                    && ['href', 'protocol', 'username', 'password', 'host', 'hostname',
                        'port', 'pathname', 'search', 'hash'].every(key => attribute(url, key, true))
                    && ['origin', 'searchParams'].every(key => attribute(url, key, false))
                    && attribute(params, 'size', false);
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlFunctionsExposeRequiredArgumentCounts()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const params = URLSearchParams.prototype;
                const functions = [
                    [URL, 1], [URL.parse, 1], [URL.canParse, 1], [URLSearchParams, 0],
                    [URL.prototype.toString, 0], [URL.prototype.toJSON, 0],
                    [params.append, 2], [params.delete, 1], [params.get, 1],
                    [params.getAll, 1], [params.has, 1], [params.set, 2], [params.sort, 0],
                    [params.keys, 0], [params.values, 0], [params.entries, 0],
                    [params[Symbol.iterator], 0], [params.forEach, 1], [params.toString, 0]
                ];
                return functions.every(([fn, length]) => {
                    const d = Object.getOwnPropertyDescriptor(fn, 'length');
                    return d.value === length && !d.writable && !d.enumerable && d.configurable;
                });
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void OptionalUndefinedArgumentsActAsMissingWithoutChangingOtherValues()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const url = 'https://example.test/';
                const params = new URLSearchParams('a=1&a=2&b=undefined');
                const has = params.has('a', undefined) && !params.has('a', 'undefined')
                    && params.has('b', 'undefined');
                params.delete('a', undefined);
                return new URL(url, undefined).href === url
                    && URL.parse(url, undefined).href === url && URL.canParse(url, undefined)
                    && new URLSearchParams(undefined).toString() === ''
                    && has && params.toString() === 'b=undefined';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlSearchParamsHasAndDeleteMatchPinnedOptionalValueWptCases()
    {
        using var host = new V8ScriptHost();
        Assert.Equal("true,true,true,c=&e=&first=null", host.Evaluate("""
            (() => {
                const params = new URLSearchParams('a=b&a=d&c&e&');
                const hasExpectedValues = params.has('a', 'b')
                    && !params.has('a', 'c')
                    && params.has('a', 'd')
                    && params.has('e', '');
                params.append('first', null);
                const convertsNullValue = !params.has('first', '')
                    && params.has('first', 'null');
                params.delete('a', 'b');
                const valueDeleteKeepsOtherDuplicates = params.has('a', 'd');
                params.delete('a', undefined);
                return [hasExpectedValues, convertsNullValue, valueDeleteKeepsOtherDuplicates, params.toString()]
                    .join(',');
            })()
            """, Cancellation).Text);
    }

    [Fact]
    public void MutablePublicPrototypesKeepPrivateStateAndBrandsProtected()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const url = new URL('https://example.test/?a=1');
                const params = url.searchParams;
                const get = URLSearchParams.prototype.get;
                const append = URLSearchParams.prototype.append;
                const href = Object.getOwnPropertyDescriptor(URL.prototype, 'href');
                let rejected = 0;
                for (const action of [
                    () => URL('https://example.test/'),
                    () => URLSearchParams(),
                    () => get.call(Object.create(URLSearchParams.prototype), 'a'),
                    () => href.get.call(Object.create(URL.prototype))
                ]) {
                    try { action(); } catch (error) { if (error instanceof TypeError) ++rejected; }
                }
                URL.prototype.toString = () => 'replaced';
                URLSearchParams.prototype.get = () => 'replaced';
                Object.defineProperty(URL.prototype, 'href', {value: 'public', configurable: true});
                delete URLSearchParams.prototype.append;
                append.call(params, 'b', '2');
                return rejected === 4 && !('__visualwebUrl' in globalThis)
                    && get.call(params, 'a') === '1'
                    && href.get.call(url) === 'https://example.test/?a=1&b=2'
                    && url.toString() === 'replaced' && params.get('a') === 'replaced';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlConstructorAndStaticParseMethodsFollowBrowserContracts()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const url = new URL('../item?q=one', 'https://example.test/a/b');
                const parsed = URL.parse('https://example.test/');
                return url.href === 'https://example.test/item?q=one'
                    && url instanceof URL
                    && URL.canParse('../item', 'https://example.test/a/')
                    && !URL.canParse('not a url')
                    && URL.parse('not a url') === null
                    && parsed instanceof URL && parsed.href === 'https://example.test/';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlToJsonMatchesPinnedWptJsonSerialization()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const url = new URL('https://example.com/');
                return JSON.stringify(url) === '"https://example.com/"'
                    && url.toJSON() === 'https://example.com/';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlStaticsMatchPinnedWptParseAndCanParseCases()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const parseCases = [
                    [undefined, undefined, false],
                    ['aaa:b', undefined, true],
                    [undefined, 'aaa:b', false],
                    ['aaa:/b', undefined, true],
                    [undefined, 'aaa:/b', true],
                    ['https://test:test', undefined, false],
                    ['a', 'https://b/', true]
                ];
                for (const [input, base, expected] of parseCases) {
                    const parsed = URL.parse(input, base);
                    if (expected) {
                        if (parsed === null || parsed.href !== new URL(input, base).href) return false;
                    } else if (parsed !== null) return false;
                }
                const canParseCases = [
                    [undefined, undefined, false],
                    ['aaa:b', undefined, true],
                    [undefined, 'https://test:test/', false],
                    ['aaa:/b', undefined, true],
                    [undefined, 'aaa:/b', true],
                    ['https://test:test', undefined, false],
                    ['a', 'https://b/', true]
                ];
                for (const [input, base, expected] of canParseCases) {
                    if (URL.canParse(input, base) !== expected) return false;
                }
                return URL.parse('https://example/') !== URL.parse('https://example/');
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlConstructorSanitizesJavascriptOnlyWptInput()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const url = new URL(
                    'http://example.com/\uD800\uD801\uDFFE\uDFFF\uFDD0\uFDCF\uFDEF\uFDF0\uFFFE\uFFFF?' +
                    '\uD800\uD801\uDFFE\uDFFF\uFDD0\uFDCF\uFDEF\uFDF0\uFFFE\uFFFF');
                return url.href === 'http://example.com/%EF%BF%BD%F0%90%9F%BE%EF%BF%BD%EF%B7%90%EF%B7%8F%EF%B7%AF%EF%B7%B0%EF%BF%BE%EF%BF%BF?%EF%BF%BD%F0%90%9F%BE%EF%BF%BD%EF%B7%90%EF%B7%8F%EF%B7%AF%EF%B7%B0%EF%BF%BE%EF%BF%BF'
                    && url.origin === 'http://example.com'
                    && url.pathname === '/%EF%BF%BD%F0%90%9F%BE%EF%BF%BD%EF%B7%90%EF%B7%8F%EF%B7%AF%EF%B7%B0%EF%BF%BE%EF%BF%BF'
                    && url.search === '?%EF%BF%BD%F0%90%9F%BE%EF%BF%BD%EF%B7%90%EF%B7%8F%EF%B7%AF%EF%B7%B0%EF%BF%BE%EF%BF%BF';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlComponentSettersMatchPinnedControlCharacterWptCases()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                function urlString({
                    scheme = 'https', username = 'username', password = 'password',
                    host = 'host', port = '8000', pathname = 'path',
                    search = 'query', hash = 'fragment'
                }) {
                    return `${scheme}://${username}:${password}@${host}:${port}/${pathname}?${search}#${hash}`;
                }
                function urlRecord(scheme) { return new URL(urlString({scheme})); }
                function check(url, property, expected, href) {
                    if (url[property] !== expected || url.href !== href) {
                        throw new Error(`${property}: ${url[property]} / ${url.href}; expected ${expected} / ${href}`);
                    }
                }
                const stripped = new Set([0x09, 0x0A, 0x0D]);
                for (const scheme of ['https', 'wpt++']) {
                    for (const point of [0x00, 0x09, 0x0A, 0x0D, 0x1F]) {
                        const isStripped = stripped.has(point);
                        const character = String.fromCodePoint(point);
                        const targetScheme = scheme === 'https' ? 'http' : 'wpt--';
                        const expectedScheme = scheme === 'https'
                            ? (isStripped ? 'http' : 'https')
                            : (isStripped ? 'wpt--' : 'wpt++');
                        for (const input of [character + targetScheme, targetScheme + character]) {
                            const url = urlRecord(scheme);
                            url.protocol = input;
                            check(url, 'protocol', expectedScheme + ':', urlString({scheme: expectedScheme}));
                        }
                        for (const property of ['username', 'password']) {
                            for (const [part, input] of [
                                [encodeURIComponent(character) + 'test', character + 'test'],
                                ['te' + encodeURIComponent(character) + 'st', 'te' + character + 'st'],
                                ['test' + encodeURIComponent(character), 'test' + character]
                            ]) {
                                const url = urlRecord(scheme);
                                url[property] = input;
                                check(url, property, part, urlString({scheme, [property]: part}));
                            }
                        }
                        const encodedHostCharacter = scheme === 'https' ? character : encodeURIComponent(character);
                        for (const [part, input] of [
                            [encodedHostCharacter + 'test', character + 'test'],
                            ['te' + encodedHostCharacter + 'st', 'te' + character + 'st'],
                            ['test' + encodedHostCharacter, 'test' + character]
                        ]) {
                            const expected = point === 0 || (scheme === 'https' && point === 0x1F)
                                ? 'host' : isStripped ? 'test' : part;
                            const url = urlRecord(scheme);
                            url.host = input;
                            check(url, 'host', expected + ':8000', urlString({scheme, host: expected}));
                            const hostnameUrl = urlRecord(scheme);
                            hostnameUrl.hostname = input;
                            check(hostnameUrl, 'hostname', expected, urlString({scheme, host: expected}));
                        }
                        for (const [input, expected] of [
                            [character + '9000', isStripped ? '9000' : '8000'],
                            ['90' + character + '00', isStripped ? '9000' : '90'],
                            ['9000' + character, '9000']
                        ]) {
                            const url = urlRecord(scheme);
                            url.port = input;
                            check(url, 'port', expected, urlString({scheme, port: expected}));
                        }
                        for (const [property, separator] of [
                            ['pathname', '/'], ['search', '?'], ['hash', '#']
                        ]) {
                            for (const [part, input] of [
                                [encodeURIComponent(character) + 'test', character + 'test'],
                                ['te' + encodeURIComponent(character) + 'st', 'te' + character + 'st'],
                                ['test' + encodeURIComponent(character), 'test' + character]
                            ]) {
                                const expected = separator + (isStripped ? 'test' : part);
                                const url = urlRecord(scheme);
                                url[property] = input;
                                check(url, property, expected, urlString({scheme, [property]: expected.slice(1)}));
                            }
                        }
                    }
                }
                return true;
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlAttributesAreMutableAndSearchParamsRemainLinkedAndSameObject()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const url = new URL('http://user:pass@example.test:80/a?x=1#old');
                const params = url.searchParams;
                const identity = params === url.searchParams;
                url.protocol = 'https';
                url.hostname = 'other.test';
                url.pathname = '/next';
                url.hash = 'new';
                params.append('y', 'two');
                const fromParams = url.href === 'https://user:pass@other.test/next?x=1&y=two#new';
                url.search = '?z=3';
                const fromUrl = params === url.searchParams && params.toString() === 'z=3';
                url.href = 'https://example.test/final?a=4';
                const parts = new URL('https://old:secret@example.test:8443/item?q=1#x');
                const getters = parts.protocol === 'https:' && parts.username === 'old'
                    && parts.password === 'secret' && parts.host === 'example.test:8443'
                    && parts.hostname === 'example.test' && parts.port === '8443'
                    && parts.pathname === '/item' && parts.search === '?q=1' && parts.hash === '#x';
                parts.username = 'a b';
                parts.password = 'c@d';
                parts.host = 'other.test:9443';
                parts.hostname = 'final.test';
                parts.port = '';
                return identity && fromParams && fromUrl && params === url.searchParams
                    && params.toString() === 'a=4' && url.origin === 'https://example.test'
                    && getters && parts.href === 'https://a%20b:c%40d@final.test/item?q=1#x';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SearchParamsPreserveLeadingQuestionMarkQueryData()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const url = new URL('http://example.org/file??a=b&c=d');
                const initial = url.search === '??a=b&c=d'
                    && url.searchParams.toString() === '%3Fa=b&c=d';
                url.href = 'http://example.org/file??a=b';
                return initial && url.search === '??a=b'
                    && url.searchParams.toString() === '%3Fa=b';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlBindingsUseWebIdlStringConversionAndInterfaceSpecificErrors()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                let constructorTypeError = false, symbolTypeError = false, pairTypeError = false;
                let brandTypeError = false, missingTypeError = false;
                try { new URL('not a url'); } catch (error) { constructorTypeError = error instanceof TypeError; }
                try { new URL(Symbol('url')); } catch (error) { symbolTypeError = error instanceof TypeError; }
                try { new URLSearchParams([['only-one']]); } catch (error) { pairTypeError = error instanceof TypeError; }
                try { URL.prototype.toString.call({}); } catch (error) { brandTypeError = error instanceof TypeError; }
                try { URLSearchParams.prototype.append.call({}, 'a', 'b'); } catch (error) { brandTypeError = brandTypeError && error instanceof TypeError; }
                try { URLSearchParams.prototype.get.call(new URLSearchParams(),); } catch (error) { missingTypeError = error instanceof TypeError; }
                const params = new URLSearchParams('a=%ED%A0%80');
                params.set('lone', '\uD800');
                return constructorTypeError && symbolTypeError && pairTypeError && brandTypeError
                    && missingTypeError && params.get('lone') === '\uFFFD'
                    && params.toString() === 'a=%EF%BF%BD%EF%BF%BD%EF%BF%BD&lone=%EF%BF%BD';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void StringConversionUsesWebIdlStringHintAndRejectsSymbols()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                let hints = [];
                const ordinary = new URL({
                    toString() { return 'https://example.test/from-string'; },
                    valueOf() { return 'https://example.test/from-value'; }
                });
                const exotic = {
                    [Symbol.toPrimitive](hint) {
                        hints.push(hint);
                        return hint === 'string'
                            ? 'https://example.test/from-hint'
                            : 'https://example.test/from-default';
                    }
                };
                const hinted = new URL(exotic);
                const params = new URLSearchParams();
                params.append({
                    [Symbol.toPrimitive](hint) { hints.push(hint); return hint === 'string' ? 'key' : 'bad-key'; }
                }, {
                    [Symbol.toPrimitive](hint) { hints.push(hint); return hint === 'string' ? 'value' : 'bad-value'; }
                });
                let symbolError = false;
                try { params.append(Symbol('name'), 'value'); }
                catch (error) { symbolError = error instanceof TypeError; }
                return ordinary.href === 'https://example.test/from-string'
                    && hinted.href === 'https://example.test/from-hint'
                    && params.toString() === 'key=value' && hints.join(',') === 'string,string,string'
                    && symbolError;
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlSearchParamsUnionConvertsPrimitiveAndNullInitializersToStrings()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const number = new URLSearchParams(42);
                const boolean = new URLSearchParams(false);
                const nullish = new URLSearchParams(null);
                let symbolError = false;
                try { new URLSearchParams(Symbol('init')); }
                catch (error) { symbolError = error instanceof TypeError; }
                return number.toString() === '42='
                    && boolean.toString() === 'false='
                    && nullish.toString() === 'null=' && symbolError;
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlSearchParamsRecordConversionIncludesEnumerableSymbolsAndNormalizesDuplicateKeys()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const collision = {first: 'one'};
                collision['\uD800'] = 'old';
                collision['\uFFFD'] = 'new';
                collision.last = 'four';
                const params = new URLSearchParams(collision);
                const nonEnumerable = {kept: 'value'};
                Object.defineProperty(nonEnumerable, Symbol('ignored'), {value: 'ignored'});
                const skipped = new URLSearchParams(nonEnumerable);
                const withSymbol = {name: 'value'};
                Object.defineProperty(withSymbol, Symbol('record key'), {value: 'bad', enumerable: true});
                let symbolError = false, inputLimit = false;
                try { new URLSearchParams(withSymbol); }
                catch (error) { symbolError = error instanceof TypeError; }
                const oversized = {};
                for (let index=0; index<1025; ++index) oversized['key' + index] = 'value';
                try { new URLSearchParams(oversized); }
                catch (error) { inputLimit = error instanceof TypeError; }
                return params.size === 3
                    && params.toString() === 'first=one&%EF%BF%BD=new&last=four'
                    && params.get('\uFFFD') === 'new' && skipped.toString() === 'kept=value'
                    && symbolError && inputLimit;
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlSearchParamsReadsTheIteratorMethodOnceAndRejectsNonCallableIterator()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                let gets = 0, calls = 0, nonCallableError = false;
                const iterable = {
                    get [Symbol.iterator]() {
                        ++gets;
                        return function*() { ++calls; yield ['name', 'value']; };
                    }
                };
                const params = new URLSearchParams(iterable);
                try { new URLSearchParams({[Symbol.iterator]: 1, name: 'value'}); }
                catch (error) { nonCallableError = error instanceof TypeError; }
                return gets === 1 && calls === 1 && params.toString() === 'name=value' && nonCallableError;
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlBindingsRemainUsableAfterMutablePrototypeAndObjectTampering()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                String.prototype.startsWith = String.prototype.slice = String.prototype.charCodeAt =
                    function() { throw new Error('mutable string intrinsic used'); };
                Object.keys = function() { throw new Error('mutable Object.keys used'); };
                Array.prototype.push = function() { throw new Error('mutable Array.push used'); };
                const url = new URL('https://example.test/path?old=1');
                const record = new URLSearchParams({key: 'value'});
                const sequence = new URLSearchParams([['name', 'value']]);
                record.append('other', 'entry');
                return url.pathname === '/path' && url.searchParams.get('old') === '1'
                    && record.toString() === 'key=value&other=entry'
                    && sequence.has('name', 'value');
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SearchParamsConstructorsIterableMethodsForEachAndStringifierWork()
    {
        using var host = new V8ScriptHost();
        Assert.Equal("true,true,true,true,true,true,true,true,true", host.Evaluate("""
            (() => {
                const sequence = new URLSearchParams([['b', '2'], ['a', '1'], ['b', '3']]);
                const record = new URLSearchParams({x: 1, y: true});
                const seen = [];
                const thisArg = {};
                sequence.forEach(function(value, name, owner) {
                    if (this !== thisArg || owner !== sequence) throw new Error('forEach arguments');
                    seen.push(name + '=' + value);
                    if (name === 'b' && value === '2') sequence.append('c', '4');
                }, thisArg);
                const entries = [...sequence].map(pair => pair.join('=')).join(',');
                const keys = [...sequence.keys()].join(',');
                const values = [...sequence.values()].join(',');
                sequence.sort();
                return [sequence instanceof URLSearchParams,
                    sequence.toString() === 'a=1&b=2&b=3&c=4',
                    `${sequence}` === sequence.toString(),
                    entries === 'b=2,a=1,b=3,c=4',
                    keys === 'b,a,b,c', values === '2,1,3,4',
                    seen.join(',') === 'b=2,a=1,b=3,c=4',
                    record.toString() === 'x=1&y=true',
                    sequence.size === 4 && sequence.getAll('b').join(',') === '2,3'].join(',');
            })()
            """, Cancellation).Text);
    }

    [Fact]
    public void UrlSearchParamsConstructorCopiesAnotherInstance()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const seed = new URLSearchParams('a=b&c=d');
                const copy = new URLSearchParams(seed);
                const initial = copy.get('a') === 'b' && copy.get('c') === 'd' && !copy.has('e');
                seed.append('e', 'f');
                const seedChangeIsIndependent = !copy.has('e');
                copy.append('g', 'h');
                return initial && seedChangeIsIndependent && !seed.has('g');
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SearchParamsIteratorsUseWebIdlIteratorPrototypeAndResultObjects()
    {
        using var host = new V8ScriptHost();
        Assert.Equal("", host.Evaluate("""
            (() => {
                const params = new URLSearchParams('a=1&b=2');
                const iterators = [
                    params.entries(), params.keys(), params.values(), params[Symbol.iterator]()
                ];
                const prototype = Object.getPrototypeOf(iterators[0]);
                const next = Object.getOwnPropertyDescriptor(prototype, 'next');
                const tag = Object.getOwnPropertyDescriptor(prototype, Symbol.toStringTag);
                const paramsPrototype = URLSearchParams.prototype;
                const nativeIteratorPrototype =
                    Object.getPrototypeOf(Object.getPrototypeOf([][Symbol.iterator]()));
                const result = iterators[0].next();
                const last = iterators[0].next();
                const done = iterators[0].next();
                const checks = {
                    sharedPrototype: iterators.every(value => Object.getPrototypeOf(value) === prototype),
                    nativeIteratorPrototype: Object.getPrototypeOf(prototype) === nativeIteratorPrototype,
                    noOwnProperties: Object.keys(iterators[0]).length === 0,
                    selfIterable: iterators[0][Symbol.iterator]() === iterators[0],
                    classString: Object.prototype.toString.call(iterators[0]) === '[object URLSearchParams Iterator]',
                    nextLength: next.value.length === 0,
                    nextWritable: next.writable,
                    nextEnumerable: next.enumerable,
                    nextConfigurable: next.configurable,
                    methodNames: paramsPrototype.keys.name === 'keys'
                        && paramsPrototype.values.name === 'values'
                        && paramsPrototype.entries.name === 'entries' && next.value.name === 'next',
                    tag: tag.value === 'URLSearchParams Iterator' && !tag.writable && !tag.enumerable && tag.configurable,
                    firstResult: result.value[0] === 'a' && result.value[1] === '1' && !result.done,
                    secondResult: last.value[0] === 'b' && last.value[1] === '2' && !last.done,
                    resultShape: Object.keys(result).join(',') === 'value,done'
                        && Object.getOwnPropertyDescriptor(result, 'value').writable
                        && Object.getOwnPropertyDescriptor(result, 'value').enumerable
                        && Object.getOwnPropertyDescriptor(result, 'value').configurable,
                    doneResult: done.value === undefined && done.done
                };
                return Object.entries(checks).filter(([, value]) => !value).map(([key]) => key).join(',');
            })()
            """, Cancellation).Text);
    }

    [Fact]
    public void SearchParamsIteratorsUseIndexBasedLiveMutationSemantics()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const params = new URLSearchParams('a=1&b=2&c=3');
                const entries = params.entries();
                const first = entries.next();
                params.delete('b');
                const second = entries.next();
                params.append('d', '4');
                const third = entries.next();
                const end = entries.next();
                params.append('e', '5');
                const resumed = entries.next();

                const keys = params.keys();
                const values = params.values();
                return first.value.join('=') === 'a=1'
                    && second.value.join('=') === 'c=3'
                    && third.value.join('=') === 'd=4'
                    && end.done && resumed.value.join('=') === 'e=5' && !resumed.done
                    && keys.next().value === 'a' && values.next().value === '1'
                    && [...params].map(pair => pair.join('=')).join(',') === 'a=1,c=3,d=4,e=5';
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlSearchParamsIterationMatchesPinnedWptMutationCases()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const url = new URL('http://a.b/c?a=1&b=2&c=3&d=4');
                const replaced = [];
                for (const pair of url.searchParams) {
                    url.search = 'x=1&y=2&z=3';
                    replaced.push(pair.join('='));
                }
                const deleteNext = new URLSearchParams('param0=0&param1=1&param2=2');
                const afterNextDelete = [];
                for (const pair of deleteNext) {
                    if (pair[0] === 'param0') deleteNext.delete('param1');
                    afterNextDelete.push(pair.join('='));
                }
                const deleteCurrent = new URLSearchParams('param0=0&param1=1&param2=2');
                const afterCurrentDelete = [];
                for (const pair of deleteCurrent) {
                    if (pair[0] === 'param0') deleteCurrent.delete('param0');
                    else afterCurrentDelete.push(pair.join('='));
                }
                const deleteEachSeen = new URLSearchParams('param0=0&param1=1&param2=2');
                const seenBeforeDelete = [];
                for (const pair of deleteEachSeen) {
                    seenBeforeDelete.push(pair[0]);
                    deleteEachSeen.delete(pair[0]);
                }
                const empty = [...new URLSearchParams()].length === 0;
                return replaced.join(',') === 'a=1,y=2,z=3'
                    && afterNextDelete.join(',') === 'param0=0,param2=2'
                    && afterCurrentDelete.join(',') === 'param2=2'
                    && seenBeforeDelete.join(',') === 'param0,param2'
                    && deleteEachSeen.toString() === 'param1=1' && empty;
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void SearchParamsIteratorNextRejectsUnbrandedReceivers()
    {
        using var host = new V8ScriptHost();
        Assert.True(host.Evaluate("""
            (() => {
                const next = Object.getPrototypeOf(new URLSearchParams().entries()).next;
                let rejected = false;
                try { next.call({}); } catch (error) { rejected = error instanceof TypeError; }
                return rejected;
            })()
            """, Cancellation).Boolean);
    }

    [Fact]
    public void UrlInterfacesEnforceTheirPerExecutionAndSerializationBudgets()
    {
        using var host = new V8ScriptHost(TimeSpan.FromSeconds(10));
        Assert.True(host.Evaluate("""
            (() => {
                let serializationLimit = false, entryLimit = false, callbackLimit = false;
                try { new URL('https://example.test/' + 'é'.repeat(22000)); }
                catch (error) { serializationLimit = error instanceof RangeError; }
                try { new URLSearchParams(Array.from({length:1025}, () => ['x', '1'])); }
                catch (error) { entryLimit = error instanceof TypeError; }
                const params = new URLSearchParams();
                try { for (let index=0; index<5000; ++index) params.get('x'); }
                catch (error) { callbackLimit = error instanceof RangeError; }
                return serializationLimit && entryLimit && callbackLimit;
            })()
            """, Cancellation).Boolean);
    }
}
