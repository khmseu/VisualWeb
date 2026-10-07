namespace VisualWeb.Browser;

/// <summary>Committed-text address editing; no clipboard or IME preedit yet.</summary>
public sealed class AddressEditor
{
    public string Text { get; private set; } = "";
    public int Caret { get; private set; }
    public bool SelectAll { get; set; }
    public void Reset(string text, bool selectAll = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text; Caret = text.Length; SelectAll = selectAll;
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
        }
        return Text;
    }
    public string Delete()
    {
        if (SelectAll) { Reset(""); }
        else if (Caret < Text.Length) { Text = Text.Remove(Caret, Next(Caret) - Caret); }
        return Text;
    }
    public void Left() { Caret = SelectAll ? 0 : Previous(Caret); SelectAll = false; }
    public void Right() { Caret = SelectAll ? Text.Length : Next(Caret); SelectAll = false; }
    public void Home() { Caret = 0; SelectAll = false; }
    public void End() { Caret = Text.Length; SelectAll = false; }
    private int Previous(int position) => position == 0 ? 0
        : position >= 2 && char.IsLowSurrogate(Text[position - 1]) && char.IsHighSurrogate(Text[position - 2]) ? position - 2 : position - 1;
    private int Next(int position) => position >= Text.Length ? Text.Length
        : position + 1 < Text.Length && char.IsHighSurrogate(Text[position]) && char.IsLowSurrogate(Text[position + 1]) ? position + 2 : position + 1;
}
