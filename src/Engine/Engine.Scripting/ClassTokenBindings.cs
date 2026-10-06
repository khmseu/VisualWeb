namespace VisualWeb.Engine.Scripting;

/// <summary>Private native-JavaScript live class token facade over the primitive DOM bridge.</summary>
/// <remarks>Spec: dom; <see href="https://dom.spec.whatwg.org/#interface-domtokenlist">DOMTokenList</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-element-classname">className</see>,
/// <see href="https://dom.spec.whatwg.org/#dom-element-classlist">classList</see>.
/// Full WebIDL prototypes and DOMException objects are deferred.</remarks>
internal static class ClassTokenBindings
{
    internal const string Bootstrap = """
        ((elementPrototype, elementBrand, call) => {
            const apply = Function.prototype.call.bind(Function.prototype.call);
            const create = Object.create, define = Object.defineProperty, freeze = Object.freeze;
            const get = Reflect.get, has = Reflect.has;
            const weakGet = WeakMap.prototype.get, weakSet = WeakMap.prototype.set;
            const string = String, slice = String.prototype.slice;
            const TypeErrorCtor = TypeError, SyntaxErrorCtor = SyntaxError, ErrorCtor = Error, ProxyCtor = Proxy;
            const iterator = Symbol.iterator;
            const owners = new WeakMap(), cache = new WeakMap(), prototype = create(null);
            const brand = receiver => {
                const id = apply(weakGet, owners, receiver);
                if (id === undefined) throw new TypeErrorCtor('Illegal DOMTokenList receiver');
                return id;
            };
            const required = (count, minimum) => {
                if (count < minimum) throw new TypeErrorCtor('Not enough arguments');
            };
            const whitespace = c => c === ' ' || c === '\t' || c === '\n' || c === '\r' || c === '\f';
            const list = () => {const result = create(null); result.length = 0; return result;};
            const find = (tokens, token) => {
                for (let i=0;i<tokens.length;++i) if (tokens[i] === token) return i;
                return -1;
            };
            const append = (tokens, token) => {
                if (tokens.length >= 1024) throw new TypeErrorCtor('DOM class token count limit exceeded');
                tokens[tokens.length++] = token;
            };
            const read = id => {
                const raw = call('attribute-get', id, '', -1, -1, 'class'), tokens = list();
                if (raw !== null) {
                    let start = 0;
                    for (let i=0;i<=raw.length;++i) {
                        if (i !== raw.length && !whitespace(raw[i])) continue;
                        if (i > start) {
                            const token = apply(slice, raw, start, i);
                            if (find(tokens, token) < 0) append(tokens, token);
                        }
                        start = i + 1;
                    }
                }
                return {raw, tokens};
            };
            const validate = token => {
                if (token === '') throw new SyntaxErrorCtor('Class token must not be empty');
                for (let i=0;i<token.length;++i) if (whitespace(token[i])) {
                    const error = new ErrorCtor('Class token must not contain ASCII whitespace');
                    const descriptor = create(null);
                    descriptor.value = 'InvalidCharacterError'; descriptor.configurable = true;
                    define(error, 'name', descriptor);
                    throw error;
                }
            };
            const convert = (id, args, count = args.length) => {
                if (count > 1024) throw new TypeErrorCtor('DOM class argument count limit exceeded');
                const tokens = list(); let text = '';
                for (let i=0;i<count;++i) {
                    const token = `${args[i]}`;
                    if (text.length + token.length > 65536) throw new TypeErrorCtor('DOM class argument text limit exceeded');
                    text += token;
                    tokens[tokens.length++] = token;
                }
                call('class-input', id, text);
                return tokens;
            };
            const write = (id, state, tokens) => {
                if (state.raw === null && tokens.length === 0) return;
                let text = '';
                for (let i=0;i<tokens.length;++i) {
                    if (i) text += ' ';
                    text += tokens[i];
                    if (text.length > 65536) throw new TypeErrorCtor('DOM class serialization limit exceeded');
                }
                call('attribute-set', id, text, -1, -1, 'class');
            };
            define(prototype, 'length', {enumerable: true, get() {return read(brand(this)).tokens.length;}});
            define(prototype, 'item', {enumerable: true, value: function(index) {
                const id = brand(this); required(arguments.length, 1);
                const converted = (+index) >>> 0;
                return read(id).tokens[converted] ?? null;
            }});
            define(prototype, 'contains', {enumerable: true, value: function(token) {
                const id = brand(this); required(arguments.length, 1);
                const converted = `${token}`;
                call('class-input', id, converted);
                return find(read(id).tokens, converted) >= 0;
            }});
            for (const method of ['add', 'remove']) define(prototype, method, {enumerable: true, value: function(...args) {
                const id = brand(this), converted = convert(id, args);
                for (let i=0;i<converted.length;++i) validate(converted[i]);
                const state = read(id), result = list();
                for (let i=0;i<state.tokens.length;++i)
                    if (method === 'add' || find(converted, state.tokens[i]) < 0) append(result, state.tokens[i]);
                if (method === 'add') for (let i=0;i<converted.length;++i)
                    if (find(result, converted[i]) < 0) append(result, converted[i]);
                write(id, state, result);
            }});
            define(prototype, 'toggle', {enumerable: true, value: function(token, force) {
                const id = brand(this); required(arguments.length, 1);
                const converted = `${token}`, forced = force === undefined ? undefined : !!force;
                call('class-input', id, converted);
                validate(converted);
                const state = read(id), present = find(state.tokens, converted) >= 0;
                const wanted = forced === undefined ? !present : forced;
                if (wanted === present) return present;
                const result = list();
                for (let i=0;i<state.tokens.length;++i)
                    if (state.tokens[i] !== converted) append(result, state.tokens[i]);
                if (wanted) append(result, converted);
                write(id, state, result);
                return wanted;
            }});
            define(prototype, 'replace', {enumerable: true, value: function(token, replacement) {
                const id = brand(this); required(arguments.length, 2);
                const converted = convert(id, arguments, 2);
                const oldToken = converted[0], newToken = converted[1];
                if (oldToken === '' || newToken === '') throw new SyntaxErrorCtor('Class token must not be empty');
                validate(oldToken); validate(newToken);
                const state = read(id);
                if (find(state.tokens, oldToken) < 0) return false;
                const result = list();
                for (let i=0;i<state.tokens.length;++i) {
                    const current = state.tokens[i];
                    if (current === oldToken || current === newToken) {
                        if (find(result, newToken) < 0) append(result, newToken);
                    } else append(result, current);
                }
                write(id, state, result);
                return true;
            }});
            define(prototype, 'supports', {enumerable: true, value: function(token) {
                const id = brand(this); required(arguments.length, 1);
                const converted = `${token}`;
                call('class-input', id, converted);
                throw new TypeErrorCtor('The class attribute has no supported token vocabulary');
            }});
            const value = receiver => call('attribute-get', brand(receiver), '', -1, -1, 'class') ?? '';
            const setValue = (receiver, newValue) => {
                const id = brand(receiver), converted = `${newValue}`;
                call('attribute-set', id, converted, -1, -1, 'class');
            };
            define(prototype, 'value', {enumerable: true, get() {return value(this);}, set(newValue) {setValue(this, newValue);}});
            define(prototype, 'toString', {enumerable: true, value: function() {return value(this);}});
            define(prototype, 'forEach', {enumerable: true, value: function(callback, receiver) {
                const id = brand(this);
                if (typeof callback !== 'function') throw new TypeErrorCtor('DOMTokenList forEach requires a callable callback');
                for (let i=0;;++i) {
                    const tokens = read(id).tokens;
                    if (i >= tokens.length) return;
                    apply(callback, receiver, tokens[i], i, this);
                }
            }});
            const iterate = kind => function() {
                const id = brand(this);
                read(id);
                return (function*() {
                    for (let i=0;;++i) {
                        const tokens = read(id).tokens;
                        if (i >= tokens.length) return;
                        yield kind === 'keys' ? i : kind === 'entries' ? [i, tokens[i]] : tokens[i];
                    }
                })();
            };
            const values = iterate('values');
            define(prototype, 'values', {enumerable: true, value: values});
            define(prototype, iterator, {value: values});
            define(prototype, 'keys', {enumerable: true, value: iterate('keys')});
            define(prototype, 'entries', {enumerable: true, value: iterate('entries')});
            freeze(prototype);
            const index = key => {
                if (typeof key !== 'string' || key === '') return -1;
                const number = +key;
                return number >= 0 && number < 4294967295 && string(number) === key && number % 1 === 0 ? number : -1;
            };
            const facade = id => {
                const target = create(prototype), handler = create(null);
                handler.get = (target, key, receiver) => {
                    const i = index(key);
                    return i < 0 ? get(target, key, receiver) : read(id).tokens[i];
                };
                handler.has = (target, key) => {
                    const i = index(key);
                    return i < 0 ? has(target, key) : i < read(id).tokens.length;
                };
                handler.ownKeys = () => {
                    const tokens = read(id).tokens, keys = list();
                    for (let i=0;i<tokens.length;++i) keys[keys.length++] = string(i);
                    return keys;
                };
                handler.getOwnPropertyDescriptor = (target, key) => {
                    const i = index(key);
                    if (i < 0) return undefined;
                    const tokens = read(id).tokens;
                    if (i >= tokens.length) return undefined;
                    const descriptor = create(null);
                    descriptor.value = tokens[i]; descriptor.enumerable = true;
                    descriptor.configurable = true; descriptor.writable = false;
                    return descriptor;
                };
                handler.set = (target, key, newValue, receiver) => {
                    if (key !== 'value') return false;
                    setValue(receiver, newValue); return true;
                };
                handler.defineProperty = () => false;
                handler.deleteProperty = (target, key) => index(key) < 0;
                handler.setPrototypeOf = () => false;
                handler.preventExtensions = () => false;
                const result = new ProxyCtor(target, handler);
                apply(weakSet, owners, result, id);
                return result;
            };
            const classes = receiver => {
                const id = elementBrand(receiver);
                call('attribute-has', id, '', -1, -1, 'class');
                let result = apply(weakGet, cache, receiver);
                if (!result) {result = facade(id); apply(weakSet, cache, receiver, result);}
                return result;
            };
            define(elementPrototype, 'className', {enumerable: true,
                get() {return call('attribute-get', elementBrand(this), '', -1, -1, 'class') ?? '';},
                set(newValue) {const id = elementBrand(this); call('attribute-set', id, `${newValue}`, -1, -1, 'class');}
            });
            define(elementPrototype, 'classList', {enumerable: true,
                get() {return classes(this);}, set(newValue) {setValue(classes(this), newValue);}
            });
        })
        """;
}
