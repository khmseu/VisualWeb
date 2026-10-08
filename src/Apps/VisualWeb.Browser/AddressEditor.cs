namespace VisualWeb.Browser;

/// <summary>Committed-text address editing; no clipboard or IME preedit yet.</summary>
public sealed class AddressEditor
{
    public string Text { get; private set; } = "";
    public int Caret { get; private set; }
    public bool SelectAll { get; set; }
    private int? preferredColumn;
    public void Reset(string text, bool selectAll = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text; Caret = text.Length; SelectAll = selectAll; preferredColumn = null;
    }
    public string Insert(string value, int maximum, bool allowLineFeed = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Any(c => char.IsControl(c) && !(allowLineFeed && c == '\n')))
        { throw new PageNavigationException("Control characters are not accepted in the address bar."); }
        var length = (SelectAll ? 0 : Text.Length) + (long)value.Length;
        if (length > maximum) { throw new BrowserLimitException("Address length limit exceeded."); }
        var prefix = SelectAll ? "" : Text[..Caret];
        var suffix = SelectAll ? "" : Text[Caret..];
        Text = prefix + value + suffix;
        Caret = prefix.Length + value.Length;
        preferredColumn = null;
        SelectAll = false;
        return Text;
    }
    public string Backspace()
    {
        if (SelectAll) { Reset(""); }
        else if (Caret > 0)
        {
            var previous = Previous(Caret);
            Text = Text.Remove(previous, Caret - previous);
            Caret = previous;
            preferredColumn = null;
        }
        return Text;
    }
    public string Delete()
    {
        if (SelectAll) { Reset(""); }
        else if (Caret < Text.Length) { Text = Text.Remove(Caret, Next(Caret) - Caret); preferredColumn = null; }
        return Text;
    }
    public void Left() { Caret = SelectAll ? 0 : Previous(Caret); SelectAll = false; preferredColumn = null; }
    public void Right() { Caret = SelectAll ? Text.Length : Next(Caret); SelectAll = false; preferredColumn = null; }
    public void Home() { Caret = 0; SelectAll = false; preferredColumn = null; }
    public void End() { Caret = Text.Length; SelectAll = false; preferredColumn = null; }
    public void HomeLine()
    {
        Caret = (Caret == 0 ? -1 : Text.LastIndexOf('\n', Caret - 1)) + 1;
        SelectAll = false;
        preferredColumn = null;
    }
    public void HomeLine(IReadOnlyList<TextareaVisualLine> lines)
    {
        Caret = lines[FindVisualLine(lines, Caret)].Start;
        SelectAll = false;
        preferredColumn = null;
    }
    public void EndLine()
    {
        var end = Text.IndexOf('\n', Caret);
        Caret = end < 0 ? Text.Length : end;
        SelectAll = false;
        preferredColumn = null;
    }
    public void EndLine(IReadOnlyList<TextareaVisualLine> lines)
    {
        Caret = lines[FindVisualLine(lines, Caret)].End;
        SelectAll = false;
        preferredColumn = null;
    }
    public void Up() => MoveVertical(false);
    public void Down() => MoveVertical(true);
    public void Up(IReadOnlyList<TextareaVisualLine> lines) => MoveVertical(false, lines);
    public void Down(IReadOnlyList<TextareaVisualLine> lines) => MoveVertical(true, lines);
    internal static int FindVisualLine(IReadOnlyList<TextareaVisualLine> lines, int caret)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (caret < lines[index].End || caret == lines[index].End
                && (index == lines.Count - 1 || lines[index + 1].Start != caret)) { return index; }
        }
        return Math.Max(0, lines.Count - 1);
    }
    private void MoveVertical(bool down, IReadOnlyList<TextareaVisualLine>? lines = null)
    {
        if (lines is { Count: > 0 })
        {
            var current = FindVisualLine(lines, Caret);
            preferredColumn ??= Caret - lines[current].Start;
            SelectAll = false;
            var target = current + (down ? 1 : -1);
            if ((uint)target >= (uint)lines.Count) { return; }
            Caret = Math.Min(lines[target].Start + preferredColumn.Value, lines[target].End);
            if (Caret > lines[target].Start && Caret < lines[target].End
                && char.IsHighSurrogate(Text[Caret - 1]) && char.IsLowSurrogate(Text[Caret])) { Caret--; }
            return;
        }
        var previousBreak = Caret == 0 ? -1 : Text.LastIndexOf('\n', Caret - 1);
        var lineStart = previousBreak + 1;
        var lineEnd = Text.IndexOf('\n', Caret);
        if (lineEnd < 0) { lineEnd = Text.Length; }
        preferredColumn ??= Caret - lineStart;
        SelectAll = false;
        if (down)
        {
            if (lineEnd == Text.Length) { return; }
            lineStart = lineEnd + 1;
            lineEnd = Text.IndexOf('\n', lineStart);
            if (lineEnd < 0) { lineEnd = Text.Length; }
        }
        else
        {
            if (lineStart == 0) { return; }
            lineEnd = lineStart - 1;
            lineStart = Text.LastIndexOf('\n', Math.Max(0, lineStart - 2)) + 1;
        }
        Caret = Math.Min(lineStart + preferredColumn.Value, lineEnd);
        if (Caret > lineStart && Caret < lineEnd
            && char.IsHighSurrogate(Text[Caret - 1]) && char.IsLowSurrogate(Text[Caret]))
        { Caret--; }
    }
    private int Previous(int position) => position == 0 ? 0
        : position >= 2 && char.IsLowSurrogate(Text[position - 1]) && char.IsHighSurrogate(Text[position - 2]) ? position - 2 : position - 1;
    private int Next(int position) => position >= Text.Length ? Text.Length
        : position + 1 < Text.Length && char.IsHighSurrogate(Text[position]) && char.IsLowSurrogate(Text[position + 1]) ? position + 2 : position + 1;
}
