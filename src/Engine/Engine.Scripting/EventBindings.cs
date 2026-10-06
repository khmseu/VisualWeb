namespace VisualWeb.Engine.Scripting;

/// <summary>Native-JavaScript event brands, listener lists and synchronous propagation.</summary>
/// <remarks>Specs: dom, webidl;
/// <see href="https://dom.spec.whatwg.org/#concept-event-dispatch">dispatch</see>,
/// <see href="https://dom.spec.whatwg.org/#concept-event-listener-inner-invoke">listener invocation</see>.
/// Listener errors deliberately fail the execution task instead of HTML report-and-continue.</remarks>
internal static class EventBindings
{
    internal const string Bootstrap = """
        (() => {
            const apply = Function.prototype.call.bind(Function.prototype.call);
            const get = WeakMap.prototype.get, set = WeakMap.prototype.set;
            const define = Object.defineProperty, create = Object.create;
            const TypeErrorCtor = TypeError;
            const targets = new WeakMap(), events = new WeakMap();
            let retained = 0, invoked = 0, depth = 0, failure = 0, active = false;
            const list = () => {const value = create(null); value.length = 0; return value;};
            const copyList = source => {
                const result = list();
                for (let i=0; i<source.length; ++i) result[result.length++] = source[i];
                return result;
            };
            const error = text => { throw new TypeErrorCtor(text); };
            const limit = () => {failure = 2; error('Event resource limit exceeded');};
            const task = () => {if (!active || failure) error('Event task is unavailable');};
            const target = receiver => {
                const state = apply(get, targets, receiver);
                if (!state) error('Illegal EventTarget receiver');
                if (state.validate) state.validate();
                return state;
            };
            const event = receiver => {
                const state = apply(get, events, receiver);
                if (!state) error('Illegal Event receiver');
                return state;
            };
            const dictionary = value => {
                if (value != null && typeof value !== 'object' && typeof value !== 'function')
                    error('Event options must be a dictionary');
                return value == null ? create(null) : value;
            };
            const typeName = value => {
                const text = `${value}`;
                if (text.length > 65536) limit();
                return text;
            };
            class Event {
                constructor(type, options) {
                    if (arguments.length < 1) error('Event requires a type');
                    task();
                    const name = typeName(type), init = dictionary(options);
                    const bubbles = !!init.bubbles, cancelable = !!init.cancelable, composed = !!init.composed;
                    apply(set, events, this, {type: name, bubbles, cancelable, composed,
                        target: null, currentTarget: null, phase: 0, path: list(), canceled: false,
                        stop: false, immediate: false, passive: false, dispatching: false});
                    define(this, 'isTrusted', {enumerable: true, get() {return false;}});
                }
                preventDefault() {const state = event(this); if (state.cancelable && !state.passive) state.canceled = true;}
                stopPropagation() {event(this).stop = true;}
                stopImmediatePropagation() {const state = event(this); state.stop = state.immediate = true;}
                composedPath() {
                    const source = event(this).path, result = [];
                    for (let i=0; i<source.length; ++i) define(result, i, {value: source[i], enumerable: true, configurable: true, writable: true});
                    return result;
                }
            }
            for (const [name, field] of [
                ['type','type'], ['bubbles','bubbles'], ['cancelable','cancelable'], ['composed','composed'],
                ['target','target'], ['currentTarget','currentTarget'], ['eventPhase','phase'], ['defaultPrevented','canceled']
            ]) define(Event.prototype, name, {enumerable: true, get() {return event(this)[field];}});
            define(Event.prototype, 'cancelBubble', {enumerable: true,
                get() {return event(this).stop;}, set(value) {const state = event(this); if (value) state.stop = true;}});
            define(Event.prototype, 'returnValue', {enumerable: true,
                get() {return !event(this).canceled;},
                set(value) {const state = event(this); if (!value && state.cancelable && !state.passive) state.canceled = true;}});
            for (const [name, value] of [['NONE',0],['CAPTURING_PHASE',1],['AT_TARGET',2],['BUBBLING_PHASE',3]]) {
                define(Event, name, {value, enumerable: true});
                define(Event.prototype, name, {value, enumerable: true});
            }
            const callbackValue = callback => {
                if (callback == null) return null;
                if (typeof callback !== 'function' && typeof callback !== 'object')
                    error('Event listener must be a callback object');
                return callback;
            };
            const optionsValue = (options, more) => {
                if (options != null && typeof options !== 'object' && typeof options !== 'function')
                    return {capture: !!options, once: false, passive: false};
                const init = dictionary(options), capture = !!init.capture;
                if (!more) return {capture};
                const once = !!init.once, passive = !!init.passive, signal = init.signal;
                if (signal !== undefined) error('AbortSignal event options are deferred');
                return {capture, once, passive};
            };
            const remove = (state, listener) => {
                if (listener.removed) return;
                listener.removed = true; --retained;
                for (let i=0; i<state.listeners.length; ++i)
                    if (state.listeners[i] === listener) {
                        for (let j=i+1; j<state.listeners.length; ++j) state.listeners[j-1] = state.listeners[j];
                        delete state.listeners[--state.listeners.length]; break;
                    }
            };
            const addEventListener = function(type, callback, options) {
                const state = target(this); task();
                if (arguments.length < 2) error('addEventListener requires type and callback');
                const name = typeName(type), fn = callbackValue(callback), init = optionsValue(options, true);
                if (fn === null) return;
                for (let i=0; i<state.listeners.length; ++i) {
                    const item = state.listeners[i];
                    if (item.type === name && item.callback === fn && item.capture === init.capture) return;
                }
                if (retained >= 1024) limit();
                state.listeners[state.listeners.length++] = {type: name, callback: fn, capture: init.capture,
                    once: init.once, passive: init.passive, removed: false};
                ++retained;
            };
            const removeEventListener = function(type, callback, options) {
                const state = target(this); task();
                if (arguments.length < 2) error('removeEventListener requires type and callback');
                const name = typeName(type), fn = callbackValue(callback), init = optionsValue(options, false);
                for (let i=0; i<state.listeners.length; ++i) {
                    const item = state.listeners[i];
                    if (item.type === name && item.callback === fn && item.capture === init.capture) {
                        remove(state, item); return;
                    }
                }
            };
            const invoke = (receiver, value, state, capture, phase) => {
                if (state.stop || failure) return;
                const owner = apply(get, targets, receiver);
                state.currentTarget = receiver; state.phase = phase;
                const listeners = copyList(owner.listeners);
                for (let i=0; i<listeners.length; ++i) {
                    const item = listeners[i];
                    if (item.removed || item.type !== state.type || item.capture !== capture) continue;
                    if (invoked >= 4096) limit();
                    ++invoked;
                    if (item.once) remove(owner, item);
                    state.passive = item.passive;
                    try {
                        if (owner.validate) owner.validate();
                        if (typeof item.callback === 'function') apply(item.callback, receiver, value);
                        else {
                            const fn = item.callback.handleEvent;
                            if (typeof fn !== 'function') error('Listener handleEvent must be callable');
                            apply(fn, item.callback, value);
                        }
                    } catch (exception) {if (!failure) failure = 1; throw exception;}
                    finally {state.passive = false;}
                    if (state.immediate || failure) return;
                }
            };
            const dispatchEvent = function(value) {
                target(this); task();
                if (arguments.length < 1) error('dispatchEvent requires an Event');
                const state = event(value);
                if (state.dispatching) error('Event is already being dispatched');
                if (depth >= 32) limit();
                const path = list();
                let node = this;
                while (node !== null) {
                    if (path.length >= 1024) limit();
                    path[path.length++] = node;
                    const owner = target(node);
                    node = owner.parent ? owner.parent() : null;
                }
                ++depth;
                state.target = this; state.path = path; state.dispatching = true;
                try {
                    for (let i=path.length-1; i>=0; --i)
                        invoke(path[i], value, state, true, i === 0 ? 2 : 1);
                    for (let i=0; i<path.length; ++i) {
                        if (i !== 0 && !state.bubbles) continue;
                        invoke(path[i], value, state, false, i === 0 ? 2 : 3);
                    }
                    return !state.canceled;
                } finally {
                    --depth; state.dispatching = state.stop = state.immediate = state.passive = false;
                    state.currentTarget = null; state.phase = 0; state.path = list();
                }
            };
            const install = prototype => {
                for (const [name, fn] of [
                    ['addEventListener',addEventListener], ['removeEventListener',removeEventListener], ['dispatchEvent',dispatchEvent]
                ]) define(prototype, name, {value: fn, enumerable: true, configurable: true, writable: true});
            };
            class EventTarget {
                constructor() {task(); apply(set, targets, this, {listeners: list(), parent: null, validate: null});}
            }
            install(EventTarget.prototype);
            define(globalThis, 'Event', {value: Event, configurable: true, writable: true});
            define(globalThis, 'EventTarget', {value: EventTarget, configurable: true, writable: true});
            return (operation, receiver, parent, validate) => {
                if (operation === 'install') {install(receiver); return 0;}
                if (operation === 'register') {
                    apply(set, targets, receiver, {listeners: list(), parent, validate}); return 0;
                }
                if (operation === 'begin') {invoked = depth = failure = 0; active = true; return 0;}
                if (operation === 'end') active = false;
                return failure;
            };
        })()
        """;
}
