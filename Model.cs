using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;

namespace FolderBloom
{
    public sealed class FolderSettings
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double PanelWidth { get; set; }
        public double PanelHeight { get; set; }
        public bool OpenOnHover { get; set; }
        public bool CloseOnMouseLeave { get; set; }
        public int MouseLeaveDelay { get; set; }
        public string ExpandStyle { get; set; }
        public string FanDirection { get; set; }
        public bool ReplaceDesktopIcon { get; set; }
        public bool Animate { get; set; }
        public double PopupScale { get; set; }
        public bool CustomPanelPosition { get; set; }
        public bool UseCustomBehavior { get; set; }
        public bool WasExpanded { get; set; }
        public FolderSettings()
        {
            Id = Guid.NewGuid().ToString("N"); Color = "default"; Name = "Folder";
            X = 160; Y = 140; OffsetX = 112; OffsetY = 0; PanelWidth = 440; PanelHeight = 420;
            ExpandStyle = "Grid"; FanDirection = "Auto"; MouseLeaveDelay = 250;
            Animate = true; PopupScale = 1;
        }
        public void Normalize()
        {
            if (String.IsNullOrEmpty(Id)) Id = Guid.NewGuid().ToString("N");
            if (String.IsNullOrWhiteSpace(Name)) Name = "Folder";
            if (String.IsNullOrWhiteSpace(Color)) Color = "default";
            if (!ExpansionLayout.Styles.Contains(ExpandStyle)) ExpandStyle = "Grid";
            if (!ExpansionLayout.FanDirections.Contains(FanDirection)) FanDirection = "Auto";
            PopupScale = Math.Max(0.7, Math.Min(1.5, Geometry.Finite(PopupScale, 1)));
            MouseLeaveDelay = Math.Max(150, Math.Min(3000, MouseLeaveDelay == 0 ? 250 : MouseLeaveDelay));
            X = Geometry.Finite(X, 160); Y = Geometry.Finite(Y, 140);
            OffsetX = Geometry.Finite(OffsetX, 112); OffsetY = Geometry.Finite(OffsetY, 0);
            PanelWidth = Math.Max(320, Math.Min(1200, Geometry.Finite(PanelWidth, 440)));
            PanelHeight = Math.Max(260, Math.Min(1000, Geometry.Finite(PanelHeight, 420)));
        }
    }
    public sealed class Settings
    {
        public int Version { get; set; }
        public List<FolderSettings> Folders { get; set; }
        public AppPreferences Preferences { get; set; }
        public Settings() { Version = 1; Folders = new List<FolderSettings>(); Preferences = new AppPreferences(); }
    }
    public sealed class AppPreferences
    {
        public bool OpenOnHover { get; set; }
        public int OpenDelay { get; set; }
        public bool CloseOnMouseLeave { get; set; }
        public int CloseDelay { get; set; }
        public int DragHoldDelay { get; set; }
        public int AnimationDuration { get; set; }
        public double PanelOpacity { get; set; }
        public string PanelTheme { get; set; }
        public int GridColumns { get; set; }
        public int GridRows { get; set; }
        public bool HighlightOnHover { get; set; }
        public bool ShowFolderPreviews { get; set; }
        public bool CycleFolderPreviews { get; set; }
        public bool MoveDesktopIcons { get; set; }
        public bool SnapToDesktopGrid { get; set; }
        public bool RestoreExpanded { get; set; }
        public bool StartOnLogin { get; set; }
        public AppPreferences()
        {
            OpenDelay = 120; CloseDelay = 250; AnimationDuration = 100; PanelOpacity = 0.95; PanelTheme = "Dark";
            GridColumns = 5; GridRows = 5; HighlightOnHover = true; ShowFolderPreviews = true; CycleFolderPreviews = true; SnapToDesktopGrid = true;
        }
        public void Normalize()
        {
            OpenDelay = Math.Max(0, Math.Min(3000, OpenDelay)); CloseDelay = Math.Max(0, Math.Min(3000, CloseDelay)); DragHoldDelay = Math.Max(0, Math.Min(2000, DragHoldDelay));
            AnimationDuration = Math.Max(0, Math.Min(1000, AnimationDuration)); PanelOpacity = Math.Max(0, Math.Min(1, Geometry.Finite(PanelOpacity, 0.95)));
            if (PanelTheme != "Auto" && PanelTheme != "Dark" && PanelTheme != "Light") PanelTheme = "Dark";
            GridColumns = Math.Max(1, Math.Min(12, GridColumns)); GridRows = Math.Max(1, Math.Min(12, GridRows));
        }
        public AppPreferences Clone() { return (AppPreferences)MemberwiseClone(); }
    }
    public sealed class SettingsStore
    {
        public readonly string DirectoryPath;
        public string FilePath { get { return System.IO.Path.Combine(DirectoryPath, "settings.json"); } }
        public SettingsStore(string directory) { DirectoryPath = directory; }
        public Settings Load()
        {
            if (!File.Exists(FilePath)) return new Settings();
            string json = File.ReadAllText(FilePath); var serializer = new JavaScriptSerializer();
            var result = serializer.Deserialize<Settings>(json);
            if (result == null || result.Folders == null || result.Version != 1) throw new InvalidDataException("The saved settings format is not supported.");
            var raw = serializer.DeserializeObject(json) as Dictionary<string, object>; object list;
            if (raw != null && raw.TryGetValue("Folders", out list))
            {
                int i = 0;
                foreach (object item in (System.Collections.IEnumerable)list)
                {
                    var values = item as Dictionary<string, object>; var folder = result.Folders[i++];
                    if (values != null && !values.ContainsKey("CustomPanelPosition") && values.ContainsKey("OffsetX")) folder.CustomPanelPosition = folder.OffsetX != 112 || folder.OffsetY != 0;
                }
            }
            foreach (var folder in result.Folders) folder.Normalize();
            if (result.Preferences == null) result.Preferences = new AppPreferences();
            if (raw != null && !raw.ContainsKey("Preferences") && result.Folders.Count > 0)
            {
                var first = result.Folders[0]; result.Preferences.OpenOnHover = first.OpenOnHover; result.Preferences.CloseOnMouseLeave = first.CloseOnMouseLeave; result.Preferences.CloseDelay = first.MouseLeaveDelay;
                foreach (var folder in result.Folders) folder.UseCustomBehavior = folder.OpenOnHover != first.OpenOnHover || folder.CloseOnMouseLeave != first.CloseOnMouseLeave || folder.MouseLeaveDelay != first.MouseLeaveDelay;
            }
            result.Preferences.Normalize();
            return result;
        }
        public void Save(Settings settings)
        {
            Directory.CreateDirectory(DirectoryPath);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, new JavaScriptSerializer().Serialize(settings), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
            else File.Move(temp, FilePath);
        }
    }
    public static class Geometry
    {
        public static double Finite(double value, double fallback) { return Double.IsNaN(value) || Double.IsInfinity(value) ? fallback : value; }
        public static Point PanelOrigin(FolderSettings f) { return new Point(f.X + f.OffsetX, f.Y + f.OffsetY); }
        public static Point Clamp(Point point, Size size, Rect work)
        {
            return new Point(Math.Max(work.Left, Math.Min(point.X, work.Right - size.Width)), Math.Max(work.Top, Math.Min(point.Y, work.Bottom - size.Height)));
        }
        public static void SetOffset(FolderSettings folder, Point origin) { folder.OffsetX = origin.X - folder.X; folder.OffsetY = origin.Y - folder.Y; }
    }
    public sealed class FolderEntry
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsDirectory { get; set; }
    }
    public static class FolderReader
    {
        public static Task<List<FolderEntry>> ReadAsync(string path)
        {
            return Task.Run(() => new DirectoryInfo(path).EnumerateFileSystemInfos()
                .Where(f => (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0 && !f.Name.StartsWith(".folderbloom-", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FolderEntry { Name = f.Name, Path = f.FullName, IsDirectory = (f.Attributes & FileAttributes.Directory) != 0 })
                .OrderByDescending(f => f.IsDirectory).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
        }
    }
}
