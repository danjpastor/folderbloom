using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace FolderBloom
{
    internal sealed class LayoutResult
    {
        internal readonly List<Rect> Items = new List<Rect>();
        internal Size Size;
        internal System.Windows.Media.Geometry Surface;
        internal Point Anchor;
    }
    internal static class ExpansionLayout
    {
        internal static readonly string[] Styles = { "Grid", "Row", "Column", "Fan", "Ring" };
        internal static readonly string[] FanDirections = { "Auto", "Up", "Down", "Left", "Right" };
        internal static int PageSize(string style) { return style == "Fan" ? 7 : style == "Ring" ? 10 : style == "Row" || style == "Column" ? 8 : 24; }
        internal static LayoutResult Calculate(string style, int count, double availableWidth) { return Build(style, count, 1, false); }
        internal static LayoutResult Build(string style, int count, double scale, bool flipFan)
        {
            return Build(style, count, scale, flipFan ? "Down" : "Up");
        }
        internal static LayoutResult Build(string style, int count, double scale, string fanDirection)
        {
            return Build(style, count, scale, fanDirection, Math.Min(5, Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, count))))));
        }
        internal static LayoutResult Build(string style, int count, double scale, string fanDirection, int gridColumns)
        {
            var result = new LayoutResult();
            const double w = 64, h = 78, padding = 10;
            int slots = Math.Max(1, count);
            if (style == "Fan" || style == "Ring")
            {
                double span = style == "Fan" ? Math.PI * 140 / 180 : Math.PI * 2;
                double step = style == "Fan" ? span / Math.Max(1, slots - 1) : span / slots;
                double radius = Math.Max(style == "Fan" ? 148 : 115, slots <= 1 ? 0 : 106 / (2 * Math.Sin(Math.Min(Math.PI / 2, step / 2))));
                double halfBand = Math.Sqrt(w * w + h * h) / 2 + 10;
                double outer = radius + halfBand, inner = Math.Max(50, radius - halfBand), cx = outer + 3, cy = outer + 3;
                result.Size = new Size(outer * 2 + 6, style == "Fan" ? outer + 20 : outer * 2 + 6);
                result.Anchor = new Point(cx, cy);
                if (style == "Ring")
                {
                    result.Surface = new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Point(cx, cy), outer, outer), new EllipseGeometry(new Point(cx, cy), inner, inner));
                }
                else
                {
                    var shape = new StreamGeometry();
                    using (var context = shape.Open())
                    {
                        context.BeginFigure(new Point(cx - outer, cy), true, true);
                        context.ArcTo(new Point(cx + outer, cy), new Size(outer, outer), 0, false, SweepDirection.Clockwise, true, false);
                        context.BezierTo(new Point(cx + outer, cy + 16), new Point(cx + inner, cy + 16), new Point(cx + inner, cy), true, false);
                        context.ArcTo(new Point(cx - inner, cy), new Size(inner, inner), 0, false, SweepDirection.Counterclockwise, true, false);
                        context.BezierTo(new Point(cx - inner, cy + 16), new Point(cx - outer, cy + 16), new Point(cx - outer, cy), true, false);
                    }
                    result.Surface = shape;
                }
                for (int i = 0; i < count; i++)
                {
                    double angle = slots == 1 ? -Math.PI / 2 : style == "Fan" ? -Math.PI / 2 - span / 2 + i * step : -Math.PI / 2 + i * step;
                    result.Items.Add(new Rect(cx + radius * Math.Cos(angle) - w / 2, cy + radius * Math.Sin(angle) - h / 2, w, h));
                }
                if (style == "Fan" && (fanDirection == "Down" || fanDirection == "Left" || fanDirection == "Right"))
                {
                    // Move the crescent and icon centers, leaving each icon and label upright.
                    var matrix = fanDirection == "Down" ? new Matrix(1, 0, 0, -1, 0, result.Size.Height)
                        : fanDirection == "Left" ? new Matrix(0, 1, 1, 0, 0, 0)
                        : new Matrix(0, 1, -1, 0, result.Size.Height, 0);
                    var transform = new MatrixTransform(matrix); result.Surface.Transform = transform;
                    result.Anchor = transform.Transform(result.Anchor);
                    for (int i = 0; i < result.Items.Count; i++)
                    {
                        var r = result.Items[i]; var center = transform.Transform(new Point(r.X + r.Width / 2, r.Y + r.Height / 2));
                        result.Items[i] = new Rect(center.X - w / 2, center.Y - h / 2, w, h);
                    }
                    if (fanDirection == "Left" || fanDirection == "Right") result.Size = new Size(result.Size.Height, result.Size.Width);
                }
            }
            else
            {
                int columns = style == "Column" ? 1 : style == "Row" ? slots : Math.Min(slots, Math.Max(1, gridColumns));
                int rows = (int)Math.Ceiling(slots / (double)columns);
                result.Size = new Size(columns * w + padding * 2, rows * h + padding * 2);
                result.Surface = new RectangleGeometry(new Rect(1, 1, result.Size.Width - 2, result.Size.Height - 2), 12, 12);
                result.Anchor = new Point(result.Size.Width / 2, result.Size.Height + 12);
                for (int i = 0; i < count; i++) result.Items.Add(new Rect(padding + i % columns * w, padding + i / columns * h, w, h));
            }
            if (Math.Abs(scale - 1) > 0.001)
            {
                var transforms = new TransformGroup(); if (result.Surface.Transform != null) transforms.Children.Add(result.Surface.Transform); transforms.Children.Add(new ScaleTransform(scale, scale)); result.Surface.Transform = transforms;
                result.Anchor = new Point(result.Anchor.X * scale, result.Anchor.Y * scale); result.Size = new Size(result.Size.Width * scale, result.Size.Height * scale);
                for (int i = 0; i < result.Items.Count; i++) { var r = result.Items[i]; result.Items[i] = new Rect(r.X * scale, r.Y * scale, r.Width * scale, r.Height * scale); }
            }
            result.Surface.Freeze(); return result;
        }
    }
    internal sealed class LeaveTracker
    {
        private DateTime? outsideSince;
        internal void Reset() { outsideSince = null; }
        internal bool ShouldClose(bool insideOrBusy, DateTime now, int delay)
        {
            if (insideOrBusy) { Reset(); return false; }
            if (!outsideSince.HasValue) outsideSince = now;
            return (now - outsideSince.Value).TotalMilliseconds >= delay;
        }
    }
    internal enum PopupPhase { Hidden, Opening, Open, Closing }
    internal static class PopupMotion
    {
        internal const int HoverDelay = 120;
        internal const int OpenMilliseconds = 100;
        internal const int CloseMilliseconds = 70;
        internal static int Duration(bool opening, bool enabled) { return enabled ? opening ? OpenMilliseconds : CloseMilliseconds : 0; }
    }
}
