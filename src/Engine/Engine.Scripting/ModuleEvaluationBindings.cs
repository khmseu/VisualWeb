namespace VisualWeb.Engine.Scripting;

internal static class ModuleEvaluationBindings
{
    internal const string Bootstrap = """
        (() => {
            const apply = Function.prototype.call.bind(Function.prototype.call);
            const then = Promise.prototype.then;
            return promise => {
                let state = 0;
                apply(then, promise, () => { state = 1; }, () => { state = 2; });
                return () => state;
            };
        })()
        """;
}
