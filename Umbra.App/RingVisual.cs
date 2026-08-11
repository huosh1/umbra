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
