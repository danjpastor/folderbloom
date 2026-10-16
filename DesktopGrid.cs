using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace FolderBloom
{
    internal sealed class DesktopGridRegion { internal Rect Bounds; internal Point Origin; }
    internal sealed class DesktopItem { internal int Index; internal string Path; internal Point Position; }
    internal sealed class DesktopLayout
    {
        internal Size Spacing;
        internal bool AutoArrange;
        internal List<DesktopGridRegion> Regions = new List<DesktopGridRegion>();
        internal List<DesktopItem> Items = new List<DesktopItem>();
    }
    internal sealed class DesktopFolder { internal string Id, ReplacedPath; internal Point Position; internal Size Size; }
    internal sealed class DesktopPlan
    {
        internal readonly List<DesktopFolder> Folders = new List<DesktopFolder>();
        internal readonly Dictionary<int, Point> IconMoves = new Dictionary<int, Point>();
        internal bool HasRoom = true, AutoArrangeFallback;
    }
    internal static class DesktopGrid
    {
        // Snapping reserves a Shell cell; its height must not become the collapsed widget height.
        internal static double CollapsedHeight(double iconSize, double scale, double labelHeight)
        {
            return iconSize + 2 / Math.Max(0.1, scale) + labelHeight + 3;
        }
        internal static bool Overlaps(Rect a, Rect b)
        {
            var overlap = Rect.Intersect(a, b); return !overlap.IsEmpty && overlap.Width > 0.5 && overlap.Height > 0.5;
        }
        private static double Mod(double value, double spacing) { return (value % spacing + spacing) % spacing; }
        internal static double InferPhase(IEnumerable<double> positions, double spacing, double fallback)
        {
            // A freely moved icon must not shift the dominant Windows grid.
            var phases = positions.Select(p => Mod(p, spacing)).ToArray();
            if (phases.Length == 0) return Mod(fallback, spacing);
            return phases.OrderByDescending(p => phases.Count(q => Math.Min(Math.Abs(p - q), spacing - Math.Abs(p - q)) < 1.1)).ThenBy(p => p).First();
        }
        internal static Point? Nearest(Point desired, Size itemSize, Size spacing, IList<DesktopGridRegion> regions, IList<Rect> occupied)
        {
            if (spacing.Width < 8 || spacing.Height < 8) return null;
            var center = new Point(desired.X + itemSize.Width / 2, desired.Y + itemSize.Height / 2);
            // Prefer this monitor; use another only when it has no available cell.
            foreach (var group in regions.GroupBy(r => r.Bounds.Contains(center) ? 0 : 1).OrderBy(g => g.Key))
            {
                Point? best = null; double distance = Double.MaxValue;
                foreach (var region in group)
                {
                    int x0 = (int)Math.Ceiling((region.Bounds.Left - region.Origin.X) / spacing.Width), x1 = (int)Math.Floor((region.Bounds.Right - itemSize.Width - region.Origin.X + 0.01) / spacing.Width);
                    int y0 = (int)Math.Ceiling((region.Bounds.Top - region.Origin.Y) / spacing.Height), y1 = (int)Math.Floor((region.Bounds.Bottom - itemSize.Height - region.Origin.Y + 0.01) / spacing.Height);
                    for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++)
                    {
                        var candidate = new Point(region.Origin.X + x * spacing.Width, region.Origin.Y + y * spacing.Height);
                        if (occupied.Any(r => Overlaps(r, new Rect(candidate, itemSize)))) continue;
                        double score = (candidate - desired).LengthSquared;
                        if (score < distance) { best = candidate; distance = score; }
                    }
                }
                if (best.HasValue) return best;
            }
            return null;
        }
        internal static DesktopPlan Plan(DesktopLayout layout, IList<DesktopFolder> folders, bool snap, bool moveShortcuts)
        {
            var plan = new DesktopPlan { AutoArrangeFallback = moveShortcuts && layout.AutoArrange };
            bool canMove = moveShortcuts && !layout.AutoArrange;
            var replacements = new HashSet<string>(folders.Where(f => !String.IsNullOrEmpty(f.ReplacedPath)).Select(f => f.ReplacedPath.TrimEnd('\\')), StringComparer.OrdinalIgnoreCase);
            var icons = layout.Items.Where(i => String.IsNullOrEmpty(i.Path) || !replacements.Contains(i.Path.TrimEnd('\\'))).ToList();
            var blocked = canMove ? new List<Rect>() : icons.Select(i => new Rect(i.Position, layout.Spacing)).ToList();
            foreach (var folder in folders)
            {
                var size = snap ? layout.Spacing : folder.Size; Point? position = folder.Position;
                var current = new Rect(folder.Position, size);
                if (snap || !layout.Regions.Any(r => r.Bounds.Contains(current)) || blocked.Any(r => Overlaps(r, current)))
                    position = Nearest(folder.Position, size, layout.Spacing, layout.Regions, blocked);
                if (!position.HasValue) { plan.HasRoom = false; return plan; }
                plan.Folders.Add(new DesktopFolder { Id = folder.Id, Position = position.Value, Size = size, ReplacedPath = folder.ReplacedPath });
                blocked.Add(new Rect(position.Value, size));
            }
            if (!canMove) return plan;
            var folderRects = plan.Folders.Select(f => new Rect(f.Position, f.Size)).ToList();
            var collisions = icons.Where(i => folderRects.Any(r => Overlaps(r, new Rect(i.Position, layout.Spacing)))).ToList();
            var collisionIds = new HashSet<int>(collisions.Select(i => i.Index));
            blocked.AddRange(icons.Where(i => !collisionIds.Contains(i.Index)).Select(i => new Rect(i.Position, layout.Spacing)));
            foreach (var icon in collisions)
            {
                var free = Nearest(icon.Position, layout.Spacing, layout.Spacing, layout.Regions, blocked);
                if (!free.HasValue) { plan.HasRoom = false; plan.IconMoves.Clear(); return plan; }
                plan.IconMoves.Add(icon.Index, free.Value); blocked.Add(new Rect(free.Value, layout.Spacing));
            }
            return plan;
        }
    }
}
