using System;
using Avalonia;
using Avalonia.Controls;

namespace ESPWebBridge.Controls;

/// <summary>
/// Lays children out in one horizontal row, shrinking them proportionally
/// when their preferred widths do not fit in the available width.
/// </summary>
public sealed class ShrinkToFitPanel : Panel
{
    private double[] _childWidths = [];
    private double _layoutSpacing;

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<ShrinkToFitPanel, double>(nameof(Spacing));

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var childrenCount = Children.Count;
        if (childrenCount == 0)
        {
            _childWidths = [];
            _layoutSpacing = 0;
            return new Size(0, 0);
        }

        var naturalWidths = new double[childrenCount];
        var naturalTotal = 0d;

        for (var i = 0; i < childrenCount; i++)
        {
            var child = Children[i];
            // First measure at natural width so the panel can decide which
            // children need to share the constrained space.
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            naturalWidths[i] = child.DesiredSize.Width;
            naturalTotal += naturalWidths[i];
        }

        _layoutSpacing = childrenCount > 1
            ? Math.Min(Spacing, availableSize.Width / (childrenCount - 1))
            : 0;
        var spacingTotal = _layoutSpacing * (childrenCount - 1);
        var availableForChildren = double.IsPositiveInfinity(availableSize.Width)
            ? naturalTotal
            : Math.Max(0, availableSize.Width - spacingTotal);
        var widthForLargeChild = availableForChildren / childrenCount;

        var isSmall = new bool[childrenCount];
        var totalWidthSmall = 0d;
        while (true)
        {
            var foundNewSmall = false;
            var countLarge = 0;

            for (var i = 0; i < childrenCount; i++)
            {
                if (isSmall[i])
                    continue;

                if (naturalWidths[i] < widthForLargeChild)
                {
                    isSmall[i] = true;
                    totalWidthSmall += naturalWidths[i];
                    foundNewSmall = true;
                }
                else
                {
                    countLarge++;
                }
            }

            // If every item is small, leave the unused space empty rather
            // than dividing by zero or stretching those items.
            if (!foundNewSmall || countLarge == 0)
                break;

            widthForLargeChild = Math.Max(
                widthForLargeChild,
                (availableForChildren - totalWidthSmall) / countLarge);
        }

        // Measure each child again at the width it will actually receive.
        // This is important for controls such as TextBox whose internal
        // scrolling viewport depends on its measured width.
        _childWidths = new double[childrenCount];
        var desiredWidth = spacingTotal;
        var maxHeight = 0d;
        for (var i = 0; i < childrenCount; i++)
        {
            var width = Math.Min(naturalWidths[i], widthForLargeChild);
            _childWidths[i] = width;
            Children[i].Measure(new Size(width, availableSize.Height));
            desiredWidth += width;
            maxHeight = Math.Max(maxHeight, Children[i].DesiredSize.Height);
        }

        return new Size(Math.Min(desiredWidth, availableSize.Width), maxHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0d;
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(new Rect(x, 0, _childWidths[i], finalSize.Height));
            x += _childWidths[i] + _layoutSpacing;
        }

        return finalSize;
    }
}
