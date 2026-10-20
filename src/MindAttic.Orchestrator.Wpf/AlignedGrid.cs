using System.Windows;
using System.Windows.Controls;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// Lays its children out in equal columns that fill the width exactly (M.ORCH-T192): every column is the same width,
/// every gap is <see cref="Gap"/>, the first column starts on the left edge and the last one ends on the right edge, so
/// nothing leaves a ragged empty strip and a resize reflows the whole grid in step. A row is as tall as its tallest
/// child (or every row as tall as the tallest, with <see cref="UniformRows"/>). With <see cref="Balanced"/>, the rows
/// are filled evenly instead (8 tiles that don't fit on one row become 2 rows of 4, never 7 and 1).
/// </summary>
public sealed class AlignedGrid : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(AlignedGrid),
        new FrameworkPropertyMetadata(200d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(AlignedGrid),
        new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty RowGapProperty = DependencyProperty.Register(
        nameof(RowGap), typeof(double), typeof(AlignedGrid),
        new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty BalancedProperty = DependencyProperty.Register(
        nameof(Balanced), typeof(bool), typeof(AlignedGrid),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty UniformRowsProperty = DependencyProperty.Register(
        nameof(UniformRows), typeof(bool), typeof(AlignedGrid),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>The narrowest a column may get before the grid drops to fewer columns.</summary>
    public double MinItemWidth { get => (double)GetValue(MinItemWidthProperty); set => SetValue(MinItemWidthProperty, value); }

    /// <summary>The space between two columns.</summary>
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>The space between two rows.</summary>
    public double RowGap { get => (double)GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>Spread the children evenly over the fewest rows, instead of filling each row before the next.</summary>
    public bool Balanced { get => (bool)GetValue(BalancedProperty); set => SetValue(BalancedProperty, value); }

    /// <summary>Make every row as tall as the tallest child, so every cell is the same size.</summary>
    public bool UniformRows { get => (bool)GetValue(UniformRowsProperty); set => SetValue(UniformRowsProperty, value); }

    /// <summary>How many columns fit <paramref name="count"/> children in <paramref name="width"/>.</summary>
    public static int ColumnsFor(double width, int count, double minItemWidth, double gap, bool balanced)
    {
        if (count <= 0) return 1;
        var fit = double.IsInfinity(width) || double.IsNaN(width)
            ? count
            : (int)Math.Floor((width + gap) / (Math.Max(1, minItemWidth) + gap));
        fit = Math.Max(1, fit);
        if (!balanced) return fit;
        var columns = Math.Min(fit, count);
        var rows = (count + columns - 1) / columns;
        return (count + rows - 1) / rows;
    }

    /// <summary>The width of one column when <paramref name="columns"/> of them and their gaps fill <paramref name="width"/>.</summary>
    public static double ItemWidth(double width, int columns, double gap) => Math.Max(0, (width - (columns - 1) * gap) / columns);

    private List<UIElement> Visible() => InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToList();

    protected override Size MeasureOverride(Size available)
    {
        var children = Visible();
        foreach (UIElement hidden in InternalChildren) if (hidden.Visibility == Visibility.Collapsed) hidden.Measure(default);
        if (children.Count == 0) return new Size(0, 0);

        if (double.IsInfinity(available.Width))
        {
            // Nothing to fill: one row at the children's own widths.
            double w = 0, h = 0;
            foreach (var child in children)
            {
                child.Measure(new Size(double.PositiveInfinity, available.Height));
                w += child.DesiredSize.Width;
                h = Math.Max(h, child.DesiredSize.Height);
            }
            return new Size(w + (children.Count - 1) * Gap, h);
        }

        var columns = ColumnsFor(available.Width, children.Count, MinItemWidth, Gap, Balanced);
        var itemWidth = ItemWidth(available.Width, columns, Gap);
        foreach (var child in children) child.Measure(new Size(itemWidth, double.PositiveInfinity));
        var heights = RowHeights(children, columns);
        return new Size(available.Width, heights.Sum() + Math.Max(0, heights.Count - 1) * RowGap);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var children = Visible();
        if (children.Count == 0) return final;

        if (double.IsInfinity(final.Width) || final.Width <= 0)
        {
            double x = 0;
            foreach (var child in children)
            {
                child.Arrange(new Rect(x, 0, child.DesiredSize.Width, final.Height));
                x += child.DesiredSize.Width + Gap;
            }
            return final;
        }

        var columns = ColumnsFor(final.Width, children.Count, MinItemWidth, Gap, Balanced);
        var itemWidth = ItemWidth(final.Width, columns, Gap);
        var heights = RowHeights(children, columns);
        double y = 0;
        for (var i = 0; i < children.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            if (column == 0 && row > 0) y += heights[row - 1] + RowGap;
            // The last column ends exactly on the right edge, so rounding never leaves a sliver.
            var x = column * (itemWidth + Gap);
            var width = column == columns - 1 ? final.Width - x : itemWidth;
            children[i].Arrange(new Rect(x, y, Math.Max(0, width), heights[row]));
        }
        return final;
    }

    private List<double> RowHeights(List<UIElement> children, int columns)
    {
        var heights = new List<double>();
        for (var i = 0; i < children.Count; i++)
        {
            if (i % columns == 0) heights.Add(0);
            heights[^1] = Math.Max(heights[^1], children[i].DesiredSize.Height);
        }
        if (UniformRows && heights.Count > 0)
        {
            var tallest = heights.Max();
            for (var r = 0; r < heights.Count; r++) heights[r] = tallest;
        }
        return heights;
    }
}

/// <summary>The window's one left/right gutter, kept beside a scroll bar too (M.ORCH-T192).</summary>
public static class Gutters
{
    /// <summary>Every row of the dashboard starts and ends this far from the window's edges.</summary>
    public const double Side = 24;

    /// <summary>
    /// A body view's padding while its vertical scroll bar shows: the bar sits inside the right gutter instead of pushing
    /// the content in, so the cards still end where the tiles, chart and toolbar end.
    /// </summary>
    public static Thickness BesideScrollBar => new(Side, 0, Math.Max(0, Side - ScrollBarWidth), 16);

    /// <summary>A vertical scroll bar's width: the theme's slim McScrollBar, which every ScrollViewer takes (M.ORCH-T531).</summary>
    public const double ScrollBarWidth = 10;
}
