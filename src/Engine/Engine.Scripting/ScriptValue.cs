namespace VisualWeb.Engine.Scripting;

public enum ScriptValueKind { Undefined, Null, Boolean, Number, String, BigInt }

/// <summary>A copied ECMAScript primitive; never contains a native or managed host object.</summary>
/// <remarks>Spec: ecmascript; <see href="https://tc39.es/ecma262/#sec-ecmascript-language-types">language types</see>.
/// BigInt uses an exact invariant decimal string; Number preserves NaN, infinities and negative zero.</remarks>
public sealed record ScriptValue(ScriptValueKind Kind, bool Boolean = false, double Number = 0, string? Text = null);

public sealed class ScriptExecutionException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class ScriptLimitException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class UnsupportedScriptValueException(string message) : Exception(message);
