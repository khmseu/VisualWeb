using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;

namespace VisualWeb.Engine.Layout;

public static partial class StaticLayout
{
    private sealed partial class Builder
    {
        private const int MaxHtmlColumnSpan = 1000;
        private sealed record TableCell(DomElement Element, int Column, int ColumnSpan);
        private sealed record TableRow(DomElement Element, IReadOnlyList<TableCell> Cells);
        private sealed record TableGroup(DomElement Element, IReadOnlyList<TableRow> Rows);
        private sealed record TablePlan(IReadOnlyList<TableGroup> Groups, double[] Columns, double Width, double Spacing);

        // css2-tables: automatic layout permits UA-defined distribution between intrinsic constraints.
        private TablePlan PlanTable(DomElement table, double containingWidth, int depth)
        {
            var style = Style(table);
            RejectTableAttributes(table);
            RequireAutoHeight(style);
            var spacing = ((CssLength)style["border-spacing"]).Value;
            var groups = new List<TableGroup>();
            var columnCount = 0;
            foreach (var group in TableChildren(table))
            {
                var display = Display(group);
                if (group.LocalName is not ("thead" or "tbody" or "tfoot")
                    || display is not ("table-header-group" or "table-row-group" or "table-footer-group"))
                { throw new UnsupportedLayoutException("Tables require explicit HTML row groups; captions, columns and anonymous table boxes are unsupported."); }
                CheckTablePart(group, depth + 1);
                var rows = new List<TableRow>();
                foreach (var row in TableChildren(group))
                {
                    if (row.LocalName != "tr" || Display(row) != "table-row")
                    { throw new UnsupportedLayoutException("Table row groups require tr rows."); }
                    CheckTablePart(row, depth + 2);
                    var positionedCells = new List<TableCell>();
                    var column = 0;
                    foreach (var cell in TableChildren(row))
                    {
                        if (cell.LocalName is not ("td" or "th") || Display(cell) != "table-cell")
                        { throw new UnsupportedLayoutException("Table rows require td/th cells."); }
                        if (cell.GetAttribute("rowspan") is { } rowSpan && rowSpan != "1")
                        { throw new UnsupportedLayoutException("Table cells spanning rows are unsupported."); }
                        var columnSpan = ParseColumnSpan(cell.GetAttribute("colspan"));
                        if (columnSpan > options.MaxTableColumns - column)
                        { throw new LayoutLimitException("Table column limit exceeded."); }
                        RejectTableAttributes(cell);
                        var cellStyle = Style(cell);
                        if (cellStyle["vertical-align"] is not CssKeyword { Value: "top" }
                            || Edges(cellStyle, "margin", 0) != default
                            || cellStyle["max-height"] is not CssKeyword { Value: "none" })
                        { throw new UnsupportedLayoutException("Table cells require top alignment, zero margins and no maximum height."); }
                        positionedCells.Add(new(cell, column, columnSpan));
                        column += columnSpan;
                    }
                    columnCount = Math.Max(columnCount, column);
                    rows.Add(new(row, positionedCells.AsReadOnly()));
                }
                groups.Add(new(group, rows.AsReadOnly()));
            }
            if (groups.Count(g => Display(g.Element) == "table-header-group") > 1
                || groups.Count(g => Display(g.Element) == "table-footer-group") > 1)
            { throw new UnsupportedLayoutException("Only one table header group and footer group are supported."); }
            groups = groups.OrderBy(g => Display(g.Element) switch
            {
                "table-header-group" => 0,
                "table-footer-group" => 2,
                _ => 1
            }).ToList();
            var minimum = new double[columnCount];
            var maximum = new double[columnCount];
            Dictionary<int, List<(int Column, double Minimum, double Maximum)>>? spanningConstraints = null;
            List<int>? spanOrder = null;
            foreach (var row in groups.SelectMany(g => g.Rows))
            {
                foreach (var cell in row.Cells)
                {
                    var intrinsic = Intrinsic(cell.Element, depth + 3, cell: true);
                    if (cell.ColumnSpan == 1)
                    {
                        minimum[cell.Column] = Math.Max(minimum[cell.Column], intrinsic.Minimum);
                        maximum[cell.Column] = Math.Max(maximum[cell.Column], intrinsic.Maximum);
                    }
                    else
                    {
                        spanningConstraints ??= [];
                        spanOrder ??= [];
                        if (!spanningConstraints.TryGetValue(cell.ColumnSpan, out var constraints))
                        {
                            constraints = [];
                            spanningConstraints.Add(cell.ColumnSpan, constraints);
                            spanOrder.Add(cell.ColumnSpan);
                        }
                        constraints.Add((cell.Column, intrinsic.Minimum, intrinsic.Maximum));
                    }
                }
            }
            if (spanOrder is not null && spanningConstraints is not null)
            {
                cancellation.ThrowIfCancellationRequested();
                spanOrder.Sort();
                foreach (var span in spanOrder)
                {
                    cancellation.ThrowIfCancellationRequested();
                    foreach (var constraint in spanningConstraints[span])
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var spanGaps = Finite((span - 1.0) * spacing);
                        var requiredMinimum = Math.Max(0, constraint.Minimum - spanGaps);
                        var minimumDeficit = Math.Max(0, requiredMinimum - SumColumns(minimum, constraint.Column, span));
                        var minimumShare = minimumDeficit / span;
                        var requiredMaximum = Math.Max(requiredMinimum, constraint.Maximum - spanGaps);
                        var maximumDeficit = Math.Max(0, requiredMaximum - SumColumns(maximum, constraint.Column, span));
                        var maximumShare = maximumDeficit / span;
                        ChargeTableColumnWork(span);
                        for (var index = constraint.Column; index < constraint.Column + span; index++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            minimum[index] = Finite(minimum[index] + minimumShare);
                            maximum[index] = Finite(Math.Max(minimum[index], maximum[index] + maximumShare));
                        }
                    }
                }
            }
            var gaps = columnCount == 0 ? 0 : Finite((columnCount + 1.0) * spacing);
            var min = Finite(minimum.Sum() + gaps);
            var max = Finite(maximum.Sum() + gaps);
            var available = ContentWidth(style, containingWidth);
            var width = style["width"] is CssKeyword { Value: "auto" }
                ? Math.Min(Math.Max(min, available), max) : Math.Max(min, available);
            var flexibility = max - min;
            var ratio = flexibility > 0 ? Math.Min(1, (width - min) / flexibility) : 0;
            var extra = columnCount > 0 ? Math.Max(0, width - max) / columnCount : 0;
            ChargeTableColumnWork(columnCount);
            for (var index = 0; index < columnCount; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                minimum[index] = Finite(minimum[index] + ratio * (maximum[index] - minimum[index]) + extra);
            }
            return new(groups.AsReadOnly(), minimum, Finite(width), spacing);
        }

        private int ParseColumnSpan(string? raw)
        {
            if (raw is null) { return 1; }
            ChargeCharacters(raw.Length);
            var index = 0;
            while (index < raw.Length && raw[index] is ' ' or '\t' or '\n' or '\f' or '\r')
            { cancellation.ThrowIfCancellationRequested(); index++; }
            var negative = index < raw.Length && raw[index] == '-';
            if (index < raw.Length && raw[index] is '+' or '-') { index++; }
            if (index == raw.Length || !char.IsAsciiDigit(raw[index])) { return 1; }
            var value = 0;
            while (index < raw.Length && char.IsAsciiDigit(raw[index]))
            {
                cancellation.ThrowIfCancellationRequested();
                value = Math.Min(1001, value * 10 + raw[index++] - '0');
            }
            return negative || value == 0 ? 1 : Math.Min(value, MaxHtmlColumnSpan);
        }

        private double SumColumns(double[] columns, int start, int count)
        {
            var sum = 0.0;
            ChargeTableColumnWork(count);
            for (var index = start; index < start + count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                sum = Finite(sum + columns[index]);
            }
            return sum;
        }

        private IEnumerable<DomElement> TableChildren(DomElement parent)
        {
            foreach (var node in parent.ChildNodes)
            {
                Visit();
                if (node is DomElement element)
                {
                    if (Display(element) != "none") { yield return element; }
                }
                else if (node is DomText text && !IsCollapsibleWhitespace(text.Data))
                { throw new UnsupportedLayoutException("Non-whitespace content requires unsupported anonymous table boxes."); }
            }
        }

        private void CheckTablePart(DomElement element, int depth)
        {
            Check(element, depth, block: true, tableContext: true);
            RejectTableAttributes(element);
            var style = Style(element);
            RequireAutoHeight(style);
            if (style["width"] is not CssKeyword { Value: "auto" }
                || style["min-width"] is not CssKeyword { Value: "auto" }
                || style["max-width"] is not CssKeyword { Value: "none" }
                || Edges(style, "margin", 0) != default || Edges(style, "padding", 0) != default
                || Edges(style, "border", 0, "-width") != default)
            { throw new UnsupportedLayoutException("Row groups and rows require automatic sizing without margins, padding or borders."); }
        }

        private static void RejectTableAttributes(DomElement element)
        {
            if (new[] { "width", "height", "border", "cellpadding", "cellspacing", "valign", "nowrap" }
                    .Any(attribute => element.GetAttribute(attribute) is not null)
                || element.GetAttribute("align") is not null
                    && !CssStyleEngine.HasSupportedTableCenterAlignmentHint(element)
                    && !CssStyleEngine.HasSupportedTableCellAlignmentHint(element))
            { throw new UnsupportedLayoutException("Legacy table presentation attributes require unsupported presentational hints; use supported CSS."); }
        }

        private static void RequireAutoHeight(CssComputedStyle style)
        {
            if (style["height"] is not CssKeyword { Value: "auto" }
                || style["min-height"] is not CssKeyword { Value: "auto" } && style["min-height"] is not CssLength { Value: 0 }
                || style["max-height"] is not CssKeyword { Value: "none" })
            { throw new UnsupportedLayoutException("Table and row-group/row heights must remain automatic."); }
        }

        private (double Minimum, double Maximum) Intrinsic(DomElement element, int depth, bool cell = false)
        {
            Check(element, depth, block: true, tableContext: cell);
            Visit();
            var style = Style(element);
            if (Display(element) == "table" || element.LocalName is "input" or "button" or "textarea" or "select")
            { throw new UnsupportedLayoutException("Nested tables and form widgets in intrinsic table cells are unsupported."); }
            foreach (var (name, value) in style.Properties)
            {
                if (value is CssLength { Unit: "%" } && (name.Contains("width", StringComparison.Ordinal)
                    || name.StartsWith("padding-", StringComparison.Ordinal) || name.StartsWith("margin-", StringComparison.Ordinal)
                    || name.Contains("height", StringComparison.Ordinal)))
                { throw new UnsupportedLayoutException("Percentage dimensions inside intrinsic table cells are unsupported."); }
            }
            var inline = new List<InlineUnit>();
            var min = 0.0;
            var max = 0.0;
            foreach (var child in element.ChildNodes)
            {
                cancellation.ThrowIfCancellationRequested();
                if (child is DomElement hidden && Display(hidden) == "none") { continue; }
                if (child is DomElement block && IsBlock(block))
                {
                    FlushIntrinsic();
                    var intrinsic = Intrinsic(block, depth + 1);
                    min = Math.Max(min, intrinsic.Minimum);
                    max = Math.Max(max, intrinsic.Maximum);
                }
                else { Gather(child, style, inline, depth + 1); }
            }
            FlushIntrinsic();
            var padding = Edges(style, "padding", 0);
            var border = Edges(style, "border", 0, "-width");
            var margin = Edges(style, "margin", 0);
            var edges = padding.Left + padding.Right + border.Left + border.Right;
            var specified = Dimension(style["width"], 0);
            if (specified is { } width && style["box-sizing"] is CssKeyword { Value: "border-box" })
            { specified = Math.Max(0, width - edges); }
            if (specified is { } fixedWidth)
            {
                min = cell ? Math.Max(min, fixedWidth) : fixedWidth;
                max = cell ? Math.Max(max, fixedWidth) : fixedWidth;
            }
            var minWidth = Dimension(style["min-width"], 0) ?? 0;
            var maxWidth = Dimension(style["max-width"], 0) ?? double.PositiveInfinity;
            if (style["box-sizing"] is CssKeyword { Value: "border-box" })
            {
                minWidth = Math.Max(0, minWidth - edges);
                maxWidth = Math.Max(0, maxWidth - edges);
            }
            min = Math.Max(minWidth, Math.Min(min, maxWidth));
            max = Math.Max(min, Math.Max(minWidth, Math.Min(max, maxWidth)));
            return (Finite(Math.Max(0, min + edges + margin.Left + margin.Right)),
                Finite(Math.Max(0, max + edges + margin.Left + margin.Right)));

            void FlushIntrinsic()
            {
                if (inline.Count == 0) { return; }
                if (inline.Any(unit => unit.Kind == UnitKind.Widget))
                { throw new UnsupportedLayoutException("Form widgets in intrinsic table cells are unsupported."); }
                min = Math.Max(min, WidthOf(Inline(inline, style, 0, 0, 0)));
                max = Math.Max(max, WidthOf(Inline(inline, style, 0, 0, double.MaxValue / 4)));
                inline.Clear();
            }
            static double WidthOf(IEnumerable<LayoutLine> lines) => lines.Select(line =>
                line.Fragments.Sum(fragment => fragment.Run.Width) + line.Widgets.Sum(widget => widget.Bounds.Width))
                .DefaultIfEmpty().Max();
        }

        private (IReadOnlyList<LayoutBox> Groups, double Height) ArrangeTable(TablePlan plan, double x, double y, int depth)
        {
            var groups = new List<LayoutBox>();
            var cursor = y + (plan.Columns.Length > 0 ? plan.Spacing : 0);
            foreach (var group in plan.Groups)
            {
                var groupY = cursor;
                var rows = new List<LayoutBox>();
                foreach (var row in group.Rows)
                {
                    var cells = new List<LayoutBox>();
                    var cellX = x + plan.Spacing;
                    foreach (var cell in row.Cells)
                    {
                        var width = Finite(SumColumns(plan.Columns, cell.Column, cell.ColumnSpan)
                            + (cell.ColumnSpan - 1) * plan.Spacing);
                        var style = Style(cell.Element);
                        var padding = Edges(style, "padding", width);
                        var border = Edges(style, "border", width, "-width");
                        var contentWidth = Math.Max(0, width - padding.Left - padding.Right - border.Left - border.Right);
                        cells.Add(Block(cell.Element, cellX, cursor, width, null, depth + 3, cellWidth: contentWidth));
                        cellX = Finite(cellX + width + plan.Spacing);
                    }
                    var height = cells.Select(cell => cell.BorderBox.Height).DefaultIfEmpty().Max();
                    for (var index = 0; index < cells.Count; index++)
                    {
                        var box = cells[index];
                        var extra = height - box.BorderBox.Height;
                        cells[index] = box with
                        {
                            Content = box.Content with { Height = box.Content.Height + extra },
                            PaddingBox = box.PaddingBox with { Height = box.PaddingBox.Height + extra },
                            BorderBox = box.BorderBox with { Height = height }
                        };
                    }
                    rows.Add(TableBox(row.Element, Rect(x + plan.Spacing, cursor, Math.Max(0, plan.Width - 2 * plan.Spacing), height),
                        cells, depth + 2));
                    cursor = Finite(cursor + height + (plan.Columns.Length > 0 ? plan.Spacing : 0));
                }
                var groupHeight = Math.Max(0, cursor - groupY - (rows.Count > 0 && plan.Columns.Length > 0 ? plan.Spacing : 0));
                groups.Add(TableBox(group.Element, Rect(x + plan.Spacing, groupY, Math.Max(0, plan.Width - 2 * plan.Spacing), groupHeight),
                    rows, depth + 1));
            }
            return (groups.AsReadOnly(), Finite(cursor - y));
        }

        private LayoutBox TableBox(DomElement element, LayoutRect bounds, List<LayoutBox> children, int depth)
        {
            CheckTablePart(element, depth);
            Visit();
            if (++boxes > options.MaxBoxes) { throw new LayoutLimitException("Layout box limit exceeded."); }
            return new(element, bounds, bounds, bounds, default, children.AsReadOnly(), Array.Empty<LayoutLine>())
            { Flow = children.Select(child => (LayoutFlowItem)new LayoutBlockItem(child)).ToList().AsReadOnly() };
        }
    }
}
