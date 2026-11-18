using VisualWeb.Engine.Scripting;
using Xunit;

namespace VisualWeb.Engine.Scripting.Tests;

public sealed class UrlBindingTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

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
