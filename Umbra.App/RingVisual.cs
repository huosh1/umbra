using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Umbra.App;

// Cadran circulaire façon appli Horloge de Windows (onglet Focus) - anneau
// de graduations statique + arc de progression, générés en code plutôt
// qu'à la main en XAML (des dizaines de <Line> répétitives). Pas de drag
// interactif sur l'anneau (juste l'affichage) : la valeur se règle via un
// Slider classique en dessous, pour rester simple et fiable.
public static class RingVisual
{
    public static Grid BuildFocusTimer(double diameter, double fraction, Brush trackBrush, Brush progressBrush,
        string centerValue, Brush textBrush, string style)
    {
        return style?.ToLowerInvariant() switch
        {
            "orbit" => BuildOrbit(diameter, fraction, trackBrush, progressBrush, centerValue, textBrush),
            "arc" => BuildOpenArc(diameter, fraction, trackBrush, progressBrush, centerValue, textBrush),
            "digital" => BuildDigital(diameter, fraction, trackBrush, progressBrush, centerValue, textBrush),
            _ => BuildHalo(diameter, fraction, trackBrush, progressBrush, centerValue, textBrush),
        };
    }

    private static Grid BuildHalo(double diameter, double fraction, Brush trackBrush, Brush progressBrush,
        string centerValue, Brush textBrush)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        var grid = new Grid { Width = diameter, Height = diameter };
        var thickness = Math.Max(6, diameter * 0.045);

        var innerRing = new Ellipse
        {
            Width = diameter * 0.72,
            Height = diameter * 0.72,
            Stroke = trackBrush,
            StrokeThickness = Math.Max(1, diameter * 0.006),
            Opacity = 0.32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(innerRing);
        grid.Children.Add(BuildArc(diameter, thickness, 1, trackBrush));

        if (fraction > 0.001)
        {
            var glow = BuildArc(diameter, thickness * 1.8, Math.Clamp(fraction, 0.001, 1), progressBrush);
            glow.Opacity = 0.14;
            grid.Children.Add(glow);
            grid.Children.Add(BuildArc(diameter, thickness, Math.Clamp(fraction, 0.001, 1), progressBrush));

            var angle = -90 + 360 * fraction;
            var radius = diameter / 2 - thickness / 2;
            var point = PointOnCircle(new Point(diameter / 2, diameter / 2), radius, angle);
            var dotSize = thickness * 1.5;
            var dot = new Ellipse { Width = dotSize, Height = dotSize, Fill = progressBrush };
            var dotCanvas = new Canvas { Width = diameter, Height = diameter };
            Canvas.SetLeft(dot, point.X - dotSize / 2);
            Canvas.SetTop(dot, point.Y - dotSize / 2);
            dotCanvas.Children.Add(dot);
            grid.Children.Add(dotCanvas);
        }

        grid.Children.Add(BuildTimerCenter(diameter, centerValue, textBrush));
        return grid;
    }

    private static Grid BuildOrbit(double diameter, double fraction, Brush trackBrush, Brush progressBrush,
        string centerValue, Brush textBrush)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        var grid = new Grid { Width = diameter, Height = diameter };
        var canvas = new Canvas { Width = diameter, Height = diameter };
        var center = diameter / 2;
        const int segmentCount = 24;
        var activeSegments = (int)Math.Ceiling(fraction * segmentCount);

        for (var i = 0; i < segmentCount; i++)
        {
            var angle = i * 2 * Math.PI / segmentCount;
            var major = i % 6 == 0;
            var outer = diameter / 2 - diameter * 0.045;
            var length = major ? diameter * 0.105 : diameter * 0.075;
            var active = i < activeSegments;
            var marker = active && i == activeSegments - 1;
            canvas.Children.Add(new Line
            {
                X1 = center + outer * Math.Sin(angle),
                Y1 = center - outer * Math.Cos(angle),
                X2 = center + (outer - length) * Math.Sin(angle),
                Y2 = center - (outer - length) * Math.Cos(angle),
                Stroke = active ? progressBrush : trackBrush,
                StrokeThickness = marker ? Math.Max(5, diameter * 0.038) : Math.Max(3, diameter * 0.023),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = active ? 1 : 0.42,
            });
        }

        grid.Children.Add(canvas);
        grid.Children.Add(new Ellipse
        {
            Width = diameter * 0.62,
            Height = diameter * 0.62,
            Stroke = trackBrush,
            StrokeThickness = Math.Max(1, diameter * 0.006),
            Opacity = 0.25,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        grid.Children.Add(BuildTimerCenter(diameter, centerValue, textBrush));
        return grid;
    }

    private static Grid BuildOpenArc(double diameter, double fraction, Brush trackBrush, Brush progressBrush,
        string centerValue, Brush textBrush)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        const double startAngle = -230;
        const double sweepAngle = 280;
        var grid = new Grid { Width = diameter, Height = diameter };
        var thickness = Math.Max(6, diameter * 0.045);

        grid.Children.Add(BuildSweepArc(diameter, thickness, 1, trackBrush, startAngle, sweepAngle));
        if (fraction > 0.001)
        {
            grid.Children.Add(BuildSweepArc(diameter, thickness, fraction, progressBrush, startAngle, sweepAngle));
            var radius = diameter / 2 - thickness / 2;
            var point = PointOnCircle(
                new Point(diameter / 2, diameter / 2),
                radius,
                startAngle + sweepAngle * fraction);
            var dotSize = thickness * 1.4;
            var dot = new Ellipse { Width = dotSize, Height = dotSize, Fill = progressBrush };
            var dotCanvas = new Canvas { Width = diameter, Height = diameter };
            Canvas.SetLeft(dot, point.X - dotSize / 2);
            Canvas.SetTop(dot, point.Y - dotSize / 2);
            dotCanvas.Children.Add(dot);
            grid.Children.Add(dotCanvas);
        }

        grid.Children.Add(BuildTimerCenter(diameter, centerValue, textBrush));
        return grid;
    }

    private static Grid BuildDigital(double diameter, double fraction, Brush trackBrush, Brush progressBrush,
        string centerValue, Brush textBrush)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        var grid = new Grid { Width = diameter, Height = diameter };
        var parts = centerValue.Split(':', 2);
        var minutes = parts.Length > 0 ? parts[0] : "--";
        var seconds = parts.Length > 1 ? parts[1] : "--";
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(BuildDigitalTile(diameter, minutes, fraction, trackBrush, progressBrush, textBrush));
        panel.Children.Add(new TextBlock
        {
            Text = ":",
            FontSize = diameter * 0.145,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(diameter * 0.025, 0, diameter * 0.025, diameter * 0.018),
        });
        panel.Children.Add(BuildDigitalTile(diameter, seconds, fraction, trackBrush, progressBrush, textBrush));
        grid.Children.Add(panel);
        return grid;
    }

    private static Grid BuildDigitalTile(double diameter, string value, double fraction, Brush trackBrush,
        Brush progressBrush, Brush textBrush)
    {
        var width = diameter * 0.31;
        var height = diameter * 0.29;
        var radius = diameter * 0.045;
        var tile = new Grid
        {
            Width = width,
            Height = height,
            ClipToBounds = true,
            Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius),
        };
        tile.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(radius),
            Background = trackBrush,
            Opacity = 0.22,
        });
        if (fraction > 0.001)
        {
            tile.Children.Add(new Border
            {
                Height = height * fraction,
                Background = progressBrush,
                Opacity = 0.18,
                VerticalAlignment = VerticalAlignment.Bottom,
                CornerRadius = new CornerRadius(0, 0, radius, radius),
            });
        }
        tile.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(radius),
            BorderBrush = trackBrush,
            BorderThickness = new Thickness(Math.Max(1, diameter * 0.006)),
            Opacity = 0.55,
        });
        tile.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = diameter * 0.145,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return tile;
    }

    private static UIElement BuildSweepArc(double diameter, double thickness, double fraction, Brush brush,
        double startAngle, double sweepAngle)
    {
        fraction = Math.Clamp(fraction, 0.001, 1);
        var radius = diameter / 2 - thickness / 2;
        var center = new Point(diameter / 2, diameter / 2);
        var startPoint = PointOnCircle(center, radius, startAngle);
        var actualSweep = sweepAngle * fraction;
        var endPoint = PointOnCircle(center, radius, startAngle + actualSweep);
        var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = endPoint,
            Size = new Size(radius, radius),
            IsLargeArc = actualSweep > 180,
            SweepDirection = SweepDirection.Clockwise,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Path
        {
            Data = geometry,
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
    }

    private static StackPanel BuildTimerCenter(double diameter, string centerValue, Brush textBrush)
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(new TextBlock
        {
            Text = centerValue,
            FontSize = diameter * 0.16,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        return panel;
    }

    public static Grid BuildClock(double diameter, double fraction, Brush tickBrush, Brush accentBrush, string centerValue, string centerUnit, Brush textBrush)
    {
        var grid = new Grid { Width = diameter, Height = diameter };
        var center = diameter / 2;
        var ring = new Ellipse
        {
            Width = diameter - diameter * 0.13, Height = diameter - diameter * 0.13,
            Stroke = tickBrush, StrokeThickness = Math.Max(1.2, diameter * 0.007), Opacity = 0.42,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(ring);

        const int count = 32;
        var marker = (int)Math.Round((1 - Math.Clamp(fraction, 0, 1)) * (count - 1));
        var ticks = new Canvas { Width = diameter, Height = diameter };
        for (var i = 0; i < count; i++)
        {
            var angle = i * 2 * Math.PI / count;
            var highlighted = Math.Abs(i - marker) <= 1;
            var outer = diameter / 2 - diameter * 0.055;
            var length = highlighted ? diameter * 0.075 : diameter * 0.055;
            ticks.Children.Add(new Line
            {
                X1 = center + outer * Math.Sin(angle), Y1 = center - outer * Math.Cos(angle),
                X2 = center + (outer - length) * Math.Sin(angle), Y2 = center - (outer - length) * Math.Cos(angle),
                Stroke = highlighted ? accentBrush : tickBrush,
                StrokeThickness = highlighted ? Math.Max(4, diameter * 0.023) : Math.Max(3, diameter * 0.018),
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                Opacity = highlighted ? 1 : 0.62,
            });
        }
        grid.Children.Add(ticks);

        var valuePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        valuePanel.Children.Add(new TextBlock { Text = centerValue, FontSize = diameter * 0.155, FontWeight = FontWeights.SemiBold, Foreground = textBrush, VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrWhiteSpace(centerUnit)) valuePanel.Children.Add(new TextBlock { Text = centerUnit, FontSize = diameter * 0.06, Opacity = 0.62, Foreground = textBrush, Margin = new Thickness(5, 7, 0, 0) });
        grid.Children.Add(valuePanel);
        return grid;
    }

    public static Grid BuildMinimal(double diameter, double fraction, Brush trackBrush, Brush progressBrush, string centerValue, string centerUnit, Brush textBrush)
    {
        var grid = new Grid { Width = diameter, Height = diameter };
        var thickness = Math.Max(5, diameter * 0.028);
        grid.Children.Add(BuildArc(diameter, thickness, 1, trackBrush));
        if (fraction > 0.001) grid.Children.Add(BuildArc(diameter, thickness, Math.Clamp(fraction, 0.001, 1), progressBrush));
        var center = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        center.Children.Add(new TextBlock { Text = centerValue, FontSize = diameter * 0.17, FontWeight = FontWeights.SemiBold, Foreground = textBrush, VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrWhiteSpace(centerUnit)) center.Children.Add(new TextBlock { Text = centerUnit, FontSize = diameter * 0.065, Opacity = 0.6, Foreground = textBrush, Margin = new Thickness(5, 7, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        grid.Children.Add(center);
        return grid;
    }

    public static Grid Build(double diameter, double fraction, Brush trackBrush, Brush progressBrush, string centerValue, string centerUnit, Brush textBrush)
    {
        var grid = new Grid { Width = diameter, Height = diameter };

        grid.Children.Add(BuildTicks(diameter, trackBrush));

        var thickness = diameter * 0.045;
        grid.Children.Add(BuildArc(diameter, thickness, 1.0, new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))));
        if (fraction > 0.001)
        {
            grid.Children.Add(BuildArc(diameter, thickness, Math.Clamp(fraction, 0.001, 1.0), progressBrush));
        }

        var centerPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Orientation = Orientation.Horizontal };
        centerPanel.Children.Add(new TextBlock { Text = centerValue, FontSize = diameter * 0.16, FontWeight = FontWeights.Bold, Foreground = textBrush, VerticalAlignment = VerticalAlignment.Center });
        if (!string.IsNullOrEmpty(centerUnit))
        {
            centerPanel.Children.Add(new TextBlock { Text = centerUnit, FontSize = diameter * 0.07, Opacity = 0.6, Foreground = textBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 6, 0, 0) });
        }
        grid.Children.Add(centerPanel);

        return grid;
    }

    private static Canvas BuildTicks(double diameter, Brush brush)
    {
        const int count = 36;
        var canvas = new Canvas { Width = diameter, Height = diameter };
        var center = diameter / 2;
        var outerR = diameter / 2 - 2;
        var innerR = outerR - diameter * 0.05;

        for (var i = 0; i < count; i++)
        {
            var rad = i * 2 * Math.PI / count;
            var p1 = new Point(center + outerR * Math.Sin(rad), center - outerR * Math.Cos(rad));
            var p2 = new Point(center + innerR * Math.Sin(rad), center - innerR * Math.Cos(rad));
            canvas.Children.Add(new Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = brush,
                StrokeThickness = 1.6,
                Opacity = 0.35,
            });
        }
        return canvas;
    }

    // fraction=1.0 => cercle complet (le track de fond) ; ArcSegment ne sait
    // pas dessiner un cercle plein à 360°, donc dans ce cas précis on dessine
    // deux demi-arcs de 180° bout à bout à la place.
    private static UIElement BuildArc(double diameter, double thickness, double fraction, Brush brush)
    {
        var radius = diameter / 2 - thickness / 2;
        var center = new Point(diameter / 2, diameter / 2);

        if (fraction >= 0.999)
        {
            var canvas = new Canvas { Width = diameter, Height = diameter };
            canvas.Children.Add(new Ellipse
            {
                Width = diameter - thickness,
                Height = diameter - thickness,
                Stroke = brush,
                StrokeThickness = thickness,
            });
            Canvas.SetLeft(canvas.Children[0], thickness / 2);
            Canvas.SetTop(canvas.Children[0], thickness / 2);
            return canvas;
        }

        var startAngle = -90.0;
        var endAngle = startAngle + 360.0 * fraction;
        var startPoint = PointOnCircle(center, radius, startAngle);
        var endPoint = PointOnCircle(center, radius, endAngle);

        var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = endPoint,
            Size = new Size(radius, radius),
            IsLargeArc = fraction > 0.5,
            SweepDirection = SweepDirection.Clockwise,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Path
        {
            Data = geometry,
            Stroke = brush,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var rad = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
    }
}
