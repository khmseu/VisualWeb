using VisualWeb.Engine.Css;
using VisualWeb.Engine.Dom;

namespace VisualWeb.Engine.Layout;

public static partial class StaticLayout
{
    private sealed partial class Builder
    {
        private const int MaxHtmlColumnSpan = 1000;
        private const int MaxHtmlRowSpan = 65534;
        private sealed record PendingTableCell(DomElement Element, int ColumnSpan, int RowSpan);
        private sealed record PendingTableRow(DomElement Element, IReadOnlyList<PendingTableCell> Cells);
        private sealed record TableCell(DomElement Element, int Column, int ColumnSpan, int RowSpan);
        private sealed record TableRow(DomElement Element, IReadOnlyList<TableCell> Cells);
        private sealed record TableGroup(DomElement Element, IReadOnlyList<TableRow> Rows);
        private sealed record ColumnGroupWidthConstraint(int Start, int Span, double Width);
        private sealed record TablePlan(IReadOnlyList<TableGroup> Groups, double[] Columns, double Width, double Spacing,
            DomElement? Caption);
        private sealed record ArrangedTable(IReadOnlyList<LayoutBox> Children, double Height,
            LayoutRect? WrapperBounds, LayoutTableBoxGeometry? TableBoxGeometry);

        // css2-tables: automatic layout permits UA-defined distribution between intrinsic constraints.
        private TablePlan PlanTable(DomElement table, double containingWidth, int depth)
        {
            var style = Style(table);
            RejectTableAttributes(table);
            RequireAutoHeight(style);
            var spacing = ((CssLength)style["border-spacing"]).Value;
            var groups = new List<TableGroup>();
            var columnGroupElements = new List<DomElement>();
            var tableChildren = new List<DomElement>();
            DomElement? caption = null;
            var captionSeen = false;
            var elementSeen = false;
            var rowGroupsStarted = false;
            foreach (var node in table.ChildNodes)
            {
                Visit();
                if (node is DomElement child)
                {
                    var display = Display(child);
                    if (child.LocalName == "caption")
                    {
                        if (captionSeen || elementSeen)
                        { throw new UnsupportedLayoutException("Only one first-child HTML caption is supported."); }
                        captionSeen = true;
                        elementSeen = true;
                        if (display != "none")
                        {
                            if (display != "table-caption")
                            { throw new UnsupportedLayoutException("HTML captions require display:table-caption."); }
                            Check(child, depth + 1, block: true, tableContext: true);
                            caption = child;
                        }
                    }
                    else if (display != "none" && child.LocalName == "colgroup")
                    {
                        if (rowGroupsStarted)
                        { throw new UnsupportedLayoutException("Column groups must precede table row groups."); }
                        if (display != "table-column-group")
                        { throw new UnsupportedLayoutException("HTML colgroup elements require display:table-column-group."); }
                        columnGroupElements.Add(child);
                        elementSeen = true;
                    }
                    else
                    {
                        elementSeen = true;
                        if (display != "none")
                        {
                            rowGroupsStarted = true;
                            tableChildren.Add(child);
                        }
                    }
                }
                else if (node is DomText text && !IsCollapsibleWhitespace(text.Data))
                { throw new UnsupportedLayoutException("Non-whitespace content requires unsupported anonymous table boxes."); }
            }
            var columnWidths = new List<double?>();
            var columnGroupWidths = new List<ColumnGroupWidthConstraint>();
            foreach (var columnGroup in columnGroupElements)
            {
                CheckTableColumn(columnGroup, depth + 1);
                var groupStart = columnWidths.Count;
                var columnChildren = new List<DomElement>();
                var hasElementChildren = false;
                foreach (var node in columnGroup.ChildNodes)
                {
                    Visit();
                    if (node is DomElement child)
                    {
                        hasElementChildren = true;
                        if (Display(child) != "none") { columnChildren.Add(child); }
                    }
                    else if (node is DomText text && !IsCollapsibleWhitespace(text.Data))
                    { throw new UnsupportedLayoutException("Column groups accept only col elements."); }
                }
                if (hasElementChildren)
                {
                    if (columnGroup.GetAttribute("span") is not null)
                    { throw new UnsupportedLayoutException("A colgroup with col children cannot also specify span."); }
                    foreach (var column in columnChildren)
                    {
                        if (column.LocalName != "col" || Display(column) != "table-column")
                        { throw new UnsupportedLayoutException("Column groups require col children with display:table-column."); }
                        CheckTableColumn(column, depth + 2);
                        CheckEmptyTableColumn(column);
                        AddColumnTracks(columnWidths, ParseColumnSpan(column.GetAttribute("span")),
                            TableColumnWidth(column));
                    }
                }
                else
                {
                    AddColumnTracks(columnWidths, ParseColumnSpan(columnGroup.GetAttribute("span")), null);
                }
                var groupSpan = columnWidths.Count - groupStart;
                if (groupSpan > 0 && TableColumnWidth(columnGroup) is { } groupWidth)
                { columnGroupWidths.Add(new(groupStart, groupSpan, groupWidth)); }
            }
            var columnCount = columnWidths.Count;
            foreach (var group in tableChildren)
            {
                var display = Display(group);
                if (group.LocalName is not ("thead" or "tbody" or "tfoot")
                    || display is not ("table-header-group" or "table-row-group" or "table-footer-group"))
                { throw new UnsupportedLayoutException("Tables require explicit HTML row groups; anonymous table boxes are unsupported."); }
                CheckTablePart(group, depth + 1);
                var pendingRows = new List<PendingTableRow>();
                foreach (var row in TableChildren(group))
                {
                    if (row.LocalName != "tr" || Display(row) != "table-row")
                    { throw new UnsupportedLayoutException("Table row groups require tr rows."); }
                    CheckTablePart(row, depth + 2);
                    var pendingCells = new List<PendingTableCell>();
                    foreach (var cell in TableChildren(row))
                    {
                        if (cell.LocalName is not ("td" or "th") || Display(cell) != "table-cell")
                        { throw new UnsupportedLayoutException("Table rows require td/th cells."); }
                        var columnSpan = ParseColumnSpan(cell.GetAttribute("colspan"));
                        var rowSpan = ParseRowSpan(cell.GetAttribute("rowspan"));
                        RejectTableAttributes(cell);
                        var cellStyle = Style(cell);
                        if (cellStyle["vertical-align"] is not CssKeyword { Value: "top" }
                            || Edges(cellStyle, "margin", 0) != default
                            || cellStyle["max-height"] is not CssKeyword { Value: "none" })
                        { throw new UnsupportedLayoutException("Table cells require top alignment, zero margins and no maximum height."); }
                        pendingCells.Add(new(cell, columnSpan, rowSpan));
                    }
                    pendingRows.Add(new(row, pendingCells.AsReadOnly()));
                }
                var rows = PositionTableCells(pendingRows, out var groupColumnCount);
                columnCount = Math.Max(columnCount, groupColumnCount);
                groups.Add(new(group, rows));
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
            for (var index = 0; index < columnWidths.Count; index++)
            {
                if (columnWidths[index] is not { } specifiedWidth) { continue; }
                minimum[index] = specifiedWidth;
                maximum[index] = specifiedWidth;
            }
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
            foreach (var constraint in columnGroupWidths)
            {
                cancellation.ThrowIfCancellationRequested();
                var deficit = Math.Max(0, constraint.Width - SumColumns(minimum, constraint.Start, constraint.Span));
                if (deficit == 0) { continue; }
                var share = deficit / constraint.Span;
                ChargeTableColumnWork(constraint.Span);
                for (var index = constraint.Start; index < constraint.Start + constraint.Span; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    minimum[index] = Finite(minimum[index] + share);
                    maximum[index] = Math.Max(maximum[index], minimum[index]);
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
            return new(groups.AsReadOnly(), minimum, Finite(width), spacing, caption);
        }

        private List<TableRow> PositionTableCells(IReadOnlyList<PendingTableRow> sourceRows, out int columnCount)
        {
            var rows = new List<TableRow>(sourceRows.Count);
            var occupied = new Dictionary<int, HashSet<int>>();
            columnCount = 0;
            for (var rowIndex = 0; rowIndex < sourceRows.Count; rowIndex++)
            {
                cancellation.ThrowIfCancellationRequested();
                var cells = new List<TableCell>(sourceRows[rowIndex].Cells.Count);
                var column = 0;
                foreach (var sourceCell in sourceRows[rowIndex].Cells)
                {
                    var rowSpan = sourceCell.RowSpan == 0
                        ? sourceRows.Count - rowIndex
                        : Math.Min(sourceCell.RowSpan, sourceRows.Count - rowIndex);
                    while (column < options.MaxTableColumns)
                    {
                        if (sourceCell.ColumnSpan > options.MaxTableColumns - column)
                        { throw new LayoutLimitException("Table column limit exceeded."); }
                        var overlaps = false;
                        if (occupied.TryGetValue(rowIndex, out var currentRow))
                        {
                            for (var candidate = column; candidate < column + sourceCell.ColumnSpan; candidate++)
                            {
                                ChargeTableRowSpanOperation();
                                if (currentRow.Contains(candidate)) { overlaps = true; break; }
                            }
                        }
                        if (!overlaps) { break; }
                        column++;
                    }
                    if (column >= options.MaxTableColumns)
                    { throw new LayoutLimitException("Table column limit exceeded."); }

                    if (rowSpan > 1)
                    {
                        for (var coveredRow = rowIndex; coveredRow < rowIndex + rowSpan; coveredRow++)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            for (var coveredColumn = column; coveredColumn < column + sourceCell.ColumnSpan; coveredColumn++)
                            {
                                ChargeTableRowSpanOperation();
                                if (!occupied.TryGetValue(coveredRow, out var occupiedColumns))
                                {
                                    occupiedColumns = [];
                                    occupied.Add(coveredRow, occupiedColumns);
                                }
                                if (!occupiedColumns.Add(coveredColumn))
                                { throw new UnsupportedLayoutException("Table cells overlap in the row-span grid."); }
                            }
                        }
                    }
                    cells.Add(new(sourceCell.Element, column, sourceCell.ColumnSpan, rowSpan));
                    column += sourceCell.ColumnSpan;
                    columnCount = Math.Max(columnCount, column);
                }
                rows.Add(new(sourceRows[rowIndex].Element, cells.AsReadOnly()));
                occupied.Remove(rowIndex);
            }
            return rows;
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

        private void CheckTableColumn(DomElement element, int depth)
        {
            Check(element, depth, block: true, tableContext: true);
            RejectTableAttributes(element);
            if (Style(element)["background-color"] is CssColor { Alpha: > 0 })
            { throw new UnsupportedLayoutException("Column and column-group backgrounds are unsupported."); }
        }

        private void CheckEmptyTableColumn(DomElement element)
        {
            foreach (var node in element.ChildNodes)
            {
                Visit();
                if (node is DomElement || node is DomText text && !IsCollapsibleWhitespace(text.Data))
                { throw new UnsupportedLayoutException("HTML col elements cannot have content."); }
            }
        }

        private double? TableColumnWidth(DomElement element) => Style(element)["width"] switch
        {
            CssKeyword { Value: "auto" } => null,
            CssLength { Unit: "px", Value: >= 0 } width => Finite(width.Value),
            CssLength { Unit: "%" } => throw new UnsupportedLayoutException("Percentage column widths are unsupported."),
            _ => throw new UnsupportedLayoutException("Columns and column groups require auto or nonnegative absolute CSS widths.")
        };

        private void AddColumnTracks(List<double?> columns, int span, double? width)
        {
            if (span > options.MaxTableColumns - columns.Count)
            { throw new LayoutLimitException("Table column limit exceeded."); }
            if (width is not null) { ChargeTableColumnWork(span); }
            for (var index = 0; index < span; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                columns.Add(width);
            }
        }

        private int ParseRowSpan(string? raw)
        {
            if (raw is null) { return 1; }
            ChargeCharacters(raw.Length);
            var index = 0;
            while (index < raw.Length && raw[index] is ' ' or '\t' or '\n' or '\f' or '\r')
            { cancellation.ThrowIfCancellationRequested(); index++; }
            if (index == raw.Length || !char.IsAsciiDigit(raw[index])) { return 1; }
            var value = 0;
            while (index < raw.Length && char.IsAsciiDigit(raw[index]))
            {
                cancellation.ThrowIfCancellationRequested();
                value = Math.Min(MaxHtmlRowSpan + 1, value * 10 + raw[index++] - '0');
            }
            return Math.Min(value, MaxHtmlRowSpan);
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

        private ArrangedTable ArrangeTable(TablePlan plan, double x, double y, int depth,
            double wrapperX, double wrapperY, double wrapperWidth, LayoutEdges tablePadding, LayoutEdges tableBorder)
        {
            var groups = new List<LayoutBox>();
            LayoutBox? caption = null;
            if (plan.Caption is { } captionElement)
            {
                caption = Block(captionElement, wrapperX, wrapperY, wrapperWidth, null, depth + 1, tableContext: true);
                var captionTableBorderY = Finite(caption.BorderBox.Y + caption.BorderBox.Height + caption.Margin.Bottom);
                y = Finite(captionTableBorderY + tableBorder.Top + tablePadding.Top);
            }
            var rowSpacing = plan.Columns.Length > 0 ? plan.Spacing : 0;
            var cursor = y + rowSpacing;
            foreach (var group in plan.Groups)
            {
                var groupY = cursor;
                var rows = new List<LayoutBox>();
                var rowHeights = new double[group.Rows.Count];
                var rowCells = new List<(TableCell Cell, LayoutBox Box)>[group.Rows.Count];
                for (var rowIndex = 0; rowIndex < group.Rows.Count; rowIndex++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var row = group.Rows[rowIndex];
                    var cells = new List<(TableCell Cell, LayoutBox Box)>(row.Cells.Count);
                    foreach (var cell in row.Cells)
                    {
                        var width = Finite(SumColumns(plan.Columns, cell.Column, cell.ColumnSpan)
                            + (cell.ColumnSpan - 1) * plan.Spacing);
                        var cellX = Finite(x + plan.Spacing + SumColumns(plan.Columns, 0, cell.Column)
                            + cell.Column * plan.Spacing);
                        var style = Style(cell.Element);
                        var padding = Edges(style, "padding", width);
                        var border = Edges(style, "border", width, "-width");
                        var contentWidth = Math.Max(0, width - padding.Left - padding.Right - border.Left - border.Right);
                        var box = Block(cell.Element, cellX, groupY, width, null, depth + 3, cellWidth: contentWidth);
                        cells.Add((cell, box));
                        if (cell.RowSpan == 1)
                        { rowHeights[rowIndex] = Math.Max(rowHeights[rowIndex], box.BorderBox.Height); }
                    }
                    rowCells[rowIndex] = cells;
                }
                for (var rowIndex = 0; rowIndex < group.Rows.Count; rowIndex++)
                {
                    foreach (var (cell, box) in rowCells[rowIndex])
                    {
                        if (cell.RowSpan == 1) { continue; }
                        var spanHeight = SumRowHeights(rowHeights, rowIndex, cell.RowSpan)
                            + rowSpacing * (cell.RowSpan - 1);
                        var deficit = Math.Max(0, box.BorderBox.Height - spanHeight);
                        var share = deficit / cell.RowSpan;
                        for (var coveredRow = rowIndex; coveredRow < rowIndex + cell.RowSpan; coveredRow++)
                        {
                            ChargeTableRowSpanOperation();
                            rowHeights[coveredRow] = Finite(rowHeights[coveredRow] + share);
                        }
                    }
                }
                for (var rowIndex = 0; rowIndex < group.Rows.Count; rowIndex++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var row = group.Rows[rowIndex];
                    var cells = new List<LayoutBox>(rowCells[rowIndex].Count);
                    foreach (var (cell, originalBox) in rowCells[rowIndex])
                    {
                        var height = cell.RowSpan == 1
                            ? rowHeights[rowIndex]
                            : SumRowHeights(rowHeights, rowIndex, cell.RowSpan) + rowSpacing * (cell.RowSpan - 1);
                        var extra = Math.Max(0, height - originalBox.BorderBox.Height);
                        var box = originalBox with
                        {
                            Content = originalBox.Content with { Height = originalBox.Content.Height + extra },
                            PaddingBox = originalBox.PaddingBox with { Height = originalBox.PaddingBox.Height + extra },
                            BorderBox = originalBox.BorderBox with { Height = height }
                        };
                        cells.Add(TranslateTableBox(box, cursor - groupY));
                    }
                    var rowHeight = rowHeights[rowIndex];
                    rows.Add(TableBox(row.Element, Rect(x + plan.Spacing, cursor,
                        Math.Max(0, plan.Width - 2 * plan.Spacing), rowHeight), cells, depth + 2));
                    cursor = Finite(cursor + rowHeight + rowSpacing);
                }
                var groupHeight = Math.Max(0, cursor - groupY - (rows.Count > 0 && plan.Columns.Length > 0 ? plan.Spacing : 0));
                groups.Add(TableBox(group.Element, Rect(x + plan.Spacing, groupY, Math.Max(0, plan.Width - 2 * plan.Spacing), groupHeight),
                    rows, depth + 1));
            }
            var tableHeight = Finite(cursor - y);
            if (caption is null) { return new(groups.AsReadOnly(), tableHeight, null, null); }

            var tableBorderY = Finite(y - tablePadding.Top - tableBorder.Top);
            var tablePaddingBox = Rect(x - tablePadding.Left, y - tablePadding.Top,
                plan.Width + tablePadding.Left + tablePadding.Right,
                tableHeight + tablePadding.Top + tablePadding.Bottom);
            var tableBorderBox = Rect(wrapperX, tableBorderY, wrapperWidth,
                tableHeight + tablePadding.Top + tablePadding.Bottom + tableBorder.Top + tableBorder.Bottom);
            var tableGeometry = new LayoutTableBoxGeometry(
                Rect(x, y, plan.Width, tableHeight), tablePaddingBox, tableBorderBox);
            var wrapperHeight = Finite(Math.Max(0, tableBorderBox.Y + tableBorderBox.Height - wrapperY));
            var children = new LayoutBox[groups.Count + 1];
            children[0] = caption;
            for (var index = 0; index < groups.Count; index++) { children[index + 1] = groups[index]; }
            return new(children, wrapperHeight, Rect(wrapperX, wrapperY, wrapperWidth, wrapperHeight), tableGeometry);
        }

        private LayoutBox TableBox(DomElement element, LayoutRect bounds, List<LayoutBox> children, int depth)
        {
            CheckTablePart(element, depth);
            Visit();
            if (++boxes > options.MaxBoxes) { throw new LayoutLimitException("Layout box limit exceeded."); }
            return new(element, bounds, bounds, bounds, default, children.AsReadOnly(), Array.Empty<LayoutLine>())
            { Flow = children.Select(child => (LayoutFlowItem)new LayoutBlockItem(child)).ToList().AsReadOnly() };
        }

        private double SumRowHeights(double[] heights, int start, int count)
        {
            var sum = 0.0;
            for (var index = start; index < start + count; index++)
            {
                ChargeTableRowSpanOperation();
                sum = Finite(sum + heights[index]);
            }
            return sum;
        }

        private LayoutBox TranslateTableBox(LayoutBox box, double offsetY)
        {
            cancellation.ThrowIfCancellationRequested();
            if (offsetY == 0) { return box; }
            LayoutRect ShiftRect(LayoutRect rect) => rect with { Y = Finite(rect.Y + offsetY) };
            LayoutLine ShiftLine(LayoutLine line)
            {
                var fragments = new LayoutTextFragment[line.Fragments.Count];
                for (var index = 0; index < fragments.Length; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var fragment = line.Fragments[index];
                    fragments[index] = fragment with { Baseline = Finite(fragment.Baseline + offsetY) };
                }
                var widgets = new LayoutInlineWidget[line.Widgets.Count];
                for (var index = 0; index < widgets.Length; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    widgets[index] = line.Widgets[index] with { Bounds = ShiftRect(line.Widgets[index].Bounds) };
                }
                var backgrounds = new LayoutInlineBackground[line.InlineBackgrounds.Count];
                for (var index = 0; index < backgrounds.Length; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    backgrounds[index] = line.InlineBackgrounds[index] with
                    { Bounds = ShiftRect(line.InlineBackgrounds[index].Bounds) };
                }
                return line with
                {
                    Bounds = ShiftRect(line.Bounds),
                    Baseline = Finite(line.Baseline + offsetY),
                    Fragments = fragments,
                    Widgets = widgets,
                    InlineBackgrounds = backgrounds
                };
            }

            var children = new LayoutBox[box.Children.Count];
            var childMap = new Dictionary<LayoutBox, LayoutBox>(ReferenceEqualityComparer.Instance);
            for (var index = 0; index < box.Children.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                children[index] = TranslateTableBox(box.Children[index], offsetY);
                childMap.Add(box.Children[index], children[index]);
            }
            var lines = new LayoutLine[box.Lines.Count];
            var lineMap = new Dictionary<LayoutLine, LayoutLine>(ReferenceEqualityComparer.Instance);
            for (var index = 0; index < box.Lines.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                lines[index] = ShiftLine(box.Lines[index]);
                lineMap.Add(box.Lines[index], lines[index]);
            }
            var flow = new LayoutFlowItem[box.Flow.Count];
            for (var index = 0; index < box.Flow.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                flow[index] = box.Flow[index] switch
                {
                    LayoutBlockItem child => new LayoutBlockItem(childMap[child.Box]),
                    LayoutLineItem line => new LayoutLineItem(lineMap[line.Line]),
                    _ => throw new InvalidOperationException("Unknown layout flow item.")
                };
            }
            return box with
            {
                Content = ShiftRect(box.Content),
                PaddingBox = ShiftRect(box.PaddingBox),
                BorderBox = ShiftRect(box.BorderBox),
                TableBoxGeometry = box.TableBoxGeometry is { } tableGeometry
                    ? tableGeometry with
                    {
                        Content = ShiftRect(tableGeometry.Content),
                        PaddingBox = ShiftRect(tableGeometry.PaddingBox),
                        BorderBox = ShiftRect(tableGeometry.BorderBox)
                    }
                    : null,
                Children = children,
                Lines = lines,
                Flow = flow,
                DiscMarker = box.DiscMarker is { } disc ? ShiftRect(disc) : null,
                CircleMarker = box.CircleMarker is { } circle ? ShiftRect(circle) : null,
                SquareMarker = box.SquareMarker is { } square ? ShiftRect(square) : null,
                TextMarker = box.TextMarker is { } marker
                    ? marker with { Baseline = Finite(marker.Baseline + offsetY) } : null
            };
        }
    }
}
