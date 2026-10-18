namespace VisualWeb.Browser;

/// <summary>Bounded committed-text editing shared by the address bar and form fields.</summary>
public sealed class AddressEditor
{
    public string Text { get; private set; } = "";
    public int Caret { get; private set; }
    private int? selectionAnchor;
    private bool selectAll;
    public bool SelectAll
    {
        get => selectAll;
        set
        {
            selectAll = value;
            selectionAnchor = value ? 0 : null;
            if (value) { Caret = Text.Length; }
            preferredColumn = null;
        }
    }
    public bool HasSelection => SelectAll || selectionAnchor is { } anchor && anchor != Caret;
    public int SelectionStart => SelectAll ? 0 : Math.Min(selectionAnchor ?? Caret, Caret);
    public int SelectionEnd => SelectAll ? Text.Length : Math.Max(selectionAnchor ?? Caret, Caret);
    public int SelectionLength => SelectionEnd - SelectionStart;
    public string SelectedText => HasSelection ? Text[SelectionStart..SelectionEnd] : "";
    private int? preferredColumn;
    public void Reset(string text, bool selectAll = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text; Caret = text.Length; this.selectAll = false; selectionAnchor = null; preferredColumn = null;
        if (selectAll) { SelectAll = true; }
    }
    public string Insert(string value, int maximum, bool allowLineFeed = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Any(c => char.IsControl(c) && !(allowLineFeed && c == '\n')))
        { throw new PageNavigationException("Control characters are not accepted in the address bar."); }
        var start = SelectionLength > 0 ? SelectionStart : Caret;
        var end = SelectionLength > 0 ? SelectionEnd : Caret;
        var length = Text.Length - (end - start) + (long)value.Length;
        if (length > maximum) { throw new BrowserLimitException("Address length limit exceeded."); }
        Text = Text[..start] + value + Text[end..];
        Caret = start + value.Length;
        preferredColumn = null;
        ClearSelection();
        return Text;
    }
    public string Backspace()
    {
        if (SelectionLength > 0) { RemoveSelection(); }
        else if (Caret > 0)
        {
            var previous = Previous(Caret);
            Text = Text.Remove(previous, Caret - previous);
            Caret = previous;
            preferredColumn = null;
        }
        ClearSelection();
        return Text;
    }
    public string Delete()
    {
        if (SelectionLength > 0) { RemoveSelection(); }
        else if (Caret < Text.Length) { Text = Text.Remove(Caret, Next(Caret) - Caret); preferredColumn = null; }
        ClearSelection();
        return Text;
    }
    public void Left(bool extendSelection = false)
    {
        if (!extendSelection && HasSelection) { Caret = SelectionStart; ClearSelection(); }
        else { BeginMovement(extendSelection); Caret = Previous(Caret); EndMovement(extendSelection); }
        preferredColumn = null;
    }
    public void Right(bool extendSelection = false)
    {
        if (!extendSelection && HasSelection) { Caret = SelectionEnd; ClearSelection(); }
        else { BeginMovement(extendSelection); Caret = Next(Caret); EndMovement(extendSelection); }
        preferredColumn = null;
    }
    public void Home(bool extendSelection = false) { MoveTo(0, extendSelection); preferredColumn = null; }
    public void End(bool extendSelection = false) { MoveTo(Text.Length, extendSelection); preferredColumn = null; }
    internal void SetCaret(int position)
    {
        Caret = Math.Clamp(position, 0, Text.Length);
        if (Caret > 0 && Caret < Text.Length && char.IsHighSurrogate(Text[Caret - 1]) && char.IsLowSurrogate(Text[Caret]))
        { Caret--; }
        ClearSelection();
        preferredColumn = null;
    }
    public void HomeLine(bool extendSelection = false)
    {
        MoveTo((Caret == 0 ? -1 : Text.LastIndexOf('\n', Caret - 1)) + 1, extendSelection);
        preferredColumn = null;
    }
    public void HomeLine(IReadOnlyList<TextareaVisualLine> lines, bool extendSelection = false)
    {
        MoveTo(lines[FindVisualLine(lines, Caret)].Start, extendSelection);
        preferredColumn = null;
    }
    public void EndLine(bool extendSelection = false)
    {
        var end = Text.IndexOf('\n', Caret);
        MoveTo(end < 0 ? Text.Length : end, extendSelection);
        preferredColumn = null;
    }
    public void EndLine(IReadOnlyList<TextareaVisualLine> lines, bool extendSelection = false)
    {
        MoveTo(lines[FindVisualLine(lines, Caret)].End, extendSelection);
        preferredColumn = null;
    }
    public void Up(bool extendSelection = false) => MoveVertical(false, extendSelection);
    public void Down(bool extendSelection = false) => MoveVertical(true, extendSelection);
    public void Up(IReadOnlyList<TextareaVisualLine> lines, bool extendSelection = false) => MoveVertical(false, extendSelection, lines);
    public void Down(IReadOnlyList<TextareaVisualLine> lines, bool extendSelection = false) => MoveVertical(true, extendSelection, lines);
    internal static int FindVisualLine(IReadOnlyList<TextareaVisualLine> lines, int caret)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (caret < lines[index].End || caret == lines[index].End
                && (index == lines.Count - 1 || lines[index + 1].Start != caret)) { return index; }
        }
        return Math.Max(0, lines.Count - 1);
    }
    private void MoveVertical(bool down, bool extendSelection, IReadOnlyList<TextareaVisualLine>? lines = null)
    {
        BeginMovement(extendSelection);
        if (lines is { Count: > 0 })
        {
            var current = FindVisualLine(lines, Caret);
            preferredColumn ??= Caret - lines[current].Start;
            var target = current + (down ? 1 : -1);
            if ((uint)target < (uint)lines.Count)
            {
                Caret = Math.Min(lines[target].Start + preferredColumn.Value, lines[target].End);
                if (Caret > lines[target].Start && Caret < lines[target].End
                    && char.IsHighSurrogate(Text[Caret - 1]) && char.IsLowSurrogate(Text[Caret])) { Caret--; }
            }
            EndMovement(extendSelection);
            return;
        }
        var previousBreak = Caret == 0 ? -1 : Text.LastIndexOf('\n', Caret - 1);
        var lineStart = previousBreak + 1;
        var lineEnd = Text.IndexOf('\n', Caret);
        if (lineEnd < 0) { lineEnd = Text.Length; }
        preferredColumn ??= Caret - lineStart;
        if (down)
        {
            if (lineEnd != Text.Length)
            {
                lineStart = lineEnd + 1;
                lineEnd = Text.IndexOf('\n', lineStart);
                if (lineEnd < 0) { lineEnd = Text.Length; }
                Caret = Math.Min(lineStart + preferredColumn.Value, lineEnd);
            }
        }
        else
        {
            if (lineStart != 0)
            {
                lineEnd = lineStart - 1;
                lineStart = Text.LastIndexOf('\n', Math.Max(0, lineStart - 2)) + 1;
                Caret = Math.Min(lineStart + preferredColumn.Value, lineEnd);
            }
        }
        if (Caret > lineStart && Caret < lineEnd && char.IsHighSurrogate(Text[Caret - 1]) && char.IsLowSurrogate(Text[Caret])) { Caret--; }
        EndMovement(extendSelection);
    }
    private void MoveTo(int position, bool extendSelection)
    {
        BeginMovement(extendSelection);
        Caret = Math.Clamp(position, 0, Text.Length);
        EndMovement(extendSelection);
    }
    private void BeginMovement(bool extendSelection)
    {
        if (!extendSelection) { ClearSelection(); }
        else
        {
            selectionAnchor ??= Caret;
            selectAll = false;
        }
    }
    private void EndMovement(bool extendSelection)
    {
        if (!extendSelection) { ClearSelection(); }
        else if (selectionAnchor == Caret) { ClearSelection(); }
    }
    private void RemoveSelection()
    {
        var start = SelectionStart;
        Text = Text.Remove(start, SelectionLength);
        Caret = start;
        ClearSelection();
        preferredColumn = null;
    }
    private void ClearSelection()
    {
        selectAll = false;
        selectionAnchor = null;
    }
    private int Previous(int position) => position == 0 ? 0
        : position >= 2 && char.IsLowSurrogate(Text[position - 1]) && char.IsHighSurrogate(Text[position - 2]) ? position - 2 : position - 1;
    private int Next(int position) => position >= Text.Length ? Text.Length
        : position + 1 < Text.Length && char.IsHighSurrogate(Text[position]) && char.IsLowSurrogate(Text[position + 1]) ? position + 2 : position + 1;
}
