using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ShapePath = System.Windows.Shapes.Path;

namespace FolderBloom
{
    internal sealed class FolderPanel : Window
    {
        internal readonly FolderSettings Settings;
        internal string CurrentPath { get; private set; }
        internal List<FolderEntry> Entries { get; private set; }
        internal bool Loading { get; private set; }
        internal string LastError { get; private set; }
        internal int LoadedIconCount { get; private set; }
        internal LayoutResult SurfaceLayout { get; private set; }
        internal PopupPhase Phase { get; private set; }
        internal int VisibleItemCount { get { return tiles.Count; } }
        private readonly Controller controller;
        private readonly Canvas canvas;
        private readonly ShapePath surface;
        private readonly ScaleTransform surfaceScale = new ScaleTransform(1, 1);
        private readonly TextBlock message;
        private readonly List<Button> tiles = new List<Button>();
        private readonly List<Tuple<FolderEntry, Image>> iconTargets = new List<Tuple<FolderEntry, Image>>();
        private readonly DispatcherTimer refreshTimer, leaveTimer;
        private readonly LeaveTracker leaveTracker = new LeaveTracker();
        private FileSystemWatcher watcher;
        private ContextMenu popupMenu;
        private bool disposed, dragging, dropping, pinned, primed;
        private string renderedFanDirection = "Up";
        private int readVersion, iconVersion, motionVersion, page, pageCapacity, openMenus;
        private Point dragStart, origin;
        private bool MotionEnabled { get { return Settings.Animate && SystemParameters.ClientAreaAnimation; } }

        internal FolderPanel(Controller controller, FolderSettings settings)
        {
            this.controller = controller; Settings = settings; settings.Normalize(); CurrentPath = settings.Path;
            Entries = new List<FolderEntry>(); Phase = PopupPhase.Hidden;
            Title = "FolderBloom · " + settings.Name; WindowStyle = WindowStyle.None; AllowsTransparency = true;
            Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
            FontFamily = new FontFamily("Segoe UI"); UseLayoutRounding = true; SnapsToDevicePixels = true;
            canvas = new Canvas { Background = null, ClipToBounds = false }; Content = canvas;
            surface = new ShapePath { Fill = new LinearGradientBrush(Color.FromArgb(243, 43, 43, 43), Color.FromArgb(245, 30, 30, 30), 90), Stroke = Ui.Brush("#88575757"), StrokeThickness = 1.1, RenderTransform = surfaceScale };
            canvas.Children.Add(surface);
            message = Ui.Text("…", 11, Ui.Muted); message.IsHitTestVisible = false; message.TextAlignment = TextAlignment.Center;
            surface.MouseLeftButtonDown += BeginDrag;
            canvas.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) BeginDrag(sender, e); };
            canvas.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
                var delta = (PointToScreen(e.GetPosition(this)) - dragStart) / Native.Scale;
                Left = origin.X + delta.X; Top = origin.Y + delta.Y;
            };
            canvas.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e) { if (dragging) { dragging = false; canvas.ReleaseMouseCapture(); RememberPosition(); e.Handled = true; } };
            canvas.LostMouseCapture += delegate { if (dragging) { dragging = false; RememberPosition(); } };
            canvas.MouseWheel += delegate(object sender, MouseWheelEventArgs e)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) SetScale(Settings.PopupScale + (e.Delta > 0 ? 0.1 : -0.1));
                else ChangePage(e.Delta > 0 ? -1 : 1);
                e.Handled = true;
            };
            canvas.PreviewMouseRightButtonDown += delegate { BuildMenu(); };
            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            refreshTimer.Tick += async delegate { refreshTimer.Stop(); await RefreshContentsAsync(); };
            leaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            leaveTimer.Tick += delegate
            {
                if (!controller.LeaveEnabled(Settings) || pinned || !IsVisible || Phase == PopupPhase.Closing) { leaveTracker.Reset(); return; }
                if (leaveTracker.ShouldClose(CursorInsideOrBusy(), DateTime.UtcNow, controller.LeaveDelay(Settings))) Collapse();
            };
            AllowDrop = true; DragEnter += OnDragOver; DragOver += OnDragOver;
            DragLeave += delegate { dropping = false; leaveTracker.Reset(); };
            Drop += async delegate(object sender, DragEventArgs e)
            {
                e.Handled = true; e.Effects = DragDropEffects.None;
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                string target = CurrentPath; string[] sources = (string[])e.Data.GetData(DataFormats.FileDrop); dropping = true;
                try { await controller.AddDroppedItems(target, sources); QueueRefresh(); e.Effects = DragDropEffects.Copy; }
                finally { dropping = false; leaveTracker.Reset(); }
            };
            SourceInitialized += delegate
            {
                Native.ToolWindow(this, false);
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WindowMessage);
            };
            PreviewKeyDown += OnKey;
            Deactivated += delegate
            {
                Dispatcher.BeginInvoke((Action)delegate { if (!disposed && IsVisible && !IsActive && !pinned && !CursorInsideOrBusy()) Collapse(); }, DispatcherPriority.Background);
            };
            BuildMenu(); RenderEntries();
        }
        private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0084 && SurfaceLayout != null && !dragging)
            {
                long packed = lParam.ToInt64(); var location = PointFromScreen(new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
                if (!SurfaceLayout.Surface.FillContains(location)) { handled = true; return new IntPtr(-1); }
            }
            return IntPtr.Zero;
        }
        internal bool HitSurface(Point local) { return SurfaceLayout != null && SurfaceLayout.Surface.FillContains(local); }
        private void BeginDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            dragging = true; dragStart = PointToScreen(e.GetPosition(this)); origin = new Point(Left, Top); canvas.CaptureMouse(); e.Handled = true;
        }
        private bool CursorInsideOrBusy()
        {
            if (dragging || dropping || openMenus > 0 || Mouse.Captured != null) return true;
            Point cursor = Native.CursorLocation;
            if (HitSurface(new Point(cursor.X - Left, cursor.Y - Top))) return true;
            FolderWidget widget;
            if (controller.Widgets.TryGetValue(Settings.Id, out widget)) return widget.IsBusy || new Rect(widget.Left, widget.Top, widget.Width, widget.Height).Contains(cursor);
            return new Rect(Settings.X, Settings.Y, Native.DesktopTileSize.Width, Native.DesktopTileSize.Height).Contains(cursor);
        }
        private void OnDragOver(object sender, DragEventArgs e) { dropping = e.Data.GetDataPresent(DataFormats.FileDrop); e.Effects = dropping ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
        private void TrackMenu(ContextMenu menu) { menu.Opened += delegate { openMenus++; }; menu.Closed += delegate { openMenus = Math.Max(0, openMenus - 1); leaveTracker.Reset(); }; }
        private void BuildMenu()
        {
            if (popupMenu != null && popupMenu.IsOpen) return;
            var menu = new ContextMenu();
            if (CurrentPath.TrimEnd('\\') != Settings.Path.TrimEnd('\\')) menu.Items.Add(Ui.Menu("Up one folder     Backspace", NavigateUp));
            int pages = Math.Max(1, (int)Math.Ceiling(Entries.Count / (double)Math.Max(1, pageCapacity)));
            if (pages > 1)
            {
                var note = new MenuItem { Header = "Page " + (page + 1) + " of " + pages + " · mouse wheel to browse", IsEnabled = false }; menu.Items.Add(note);
                var prev = Ui.Menu("Previous page", () => ChangePage(-1)); prev.IsEnabled = page > 0; menu.Items.Add(prev);
                var next = Ui.Menu("Next page", () => ChangePage(1)); next.IsEnabled = page + 1 < pages; menu.Items.Add(next);
                menu.Items.Add(new Separator());
            }
            var styles = new MenuItem { Header = "Expansion style" };
            foreach (string name in ExpansionLayout.Styles) { string style = name; var item = Ui.Menu(style, () => ChangeStyle(style)); item.IsCheckable = true; item.IsChecked = Settings.ExpandStyle == style; styles.Items.Add(item); } menu.Items.Add(styles);
            if (Settings.ExpandStyle == "Fan")
            {
                var directions = new MenuItem { Header = "Fan direction" };
                foreach (string name in ExpansionLayout.FanDirections)
                {
                    string direction = name; var item = Ui.Menu(direction, () => ChangeFanDirection(direction)); item.IsCheckable = true; item.IsChecked = Settings.FanDirection == direction; directions.Items.Add(item);
                }
                menu.Items.Add(directions);
            }
            var placement = new MenuItem { Header = "Position" };
            placement.Items.Add(Ui.Menu("Automatic · align with folder", delegate { Settings.CustomPanelPosition = false; RenderEntries(); controller.SaveSoon(); }));
            foreach (string direction in new[] { "Above", "Below", "Left", "Right" }) { string d = direction; placement.Items.Add(Ui.Menu(d + " folder", () => Place(d))); } menu.Items.Add(placement);
            var size = new MenuItem { Header = "Popup size" };
            foreach (double scale in new[] { 0.8, 1.0, 1.2, 1.4 }) { double s = scale; size.Items.Add(Ui.Menu((int)(scale * 100) + "%", () => SetScale(s))); } menu.Items.Add(size);
            var keep = Ui.Menu("Keep open", delegate { pinned = !pinned; }); keep.IsCheckable = true; keep.IsChecked = pinned; menu.Items.Add(keep);
            menu.Items.Add(Ui.Menu("Customize…", () => { Collapse(); controller.Edit(Settings); }));
            menu.Items.Add(Ui.Menu("Rename folder…", () => { Collapse(); controller.PromptRename(Settings); }));
            menu.Items.Add(Ui.Menu("App settings…", controller.ShowSettings));
            menu.Items.Add(new Separator());
            menu.Items.Add(Ui.Menu("Open in Explorer", () => controller.OpenPath(CurrentPath)));
            menu.Items.Add(Ui.Menu("Refresh     F5", delegate { foreach (var entry in Entries) ShellWorker.InvalidateIcon(entry.Path); QueueRefresh(); }));
            menu.Items.Add(Ui.Menu("Close     Esc", Collapse));
            TrackMenu(menu); popupMenu = menu; canvas.ContextMenu = menu;
        }
        private void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Collapse(); e.Handled = true; }
            else if (e.Key == Key.F5) { QueueRefresh(); e.Handled = true; }
            else if (e.Key == Key.Back) { NavigateUp(); e.Handled = true; }
            else if (e.Key == Key.PageDown || e.Key == Key.PageUp) { ChangePage(e.Key == Key.PageDown ? 1 : -1); e.Handled = true; }
            else if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
            {
                int index = tiles.FindIndex(b => b.IsKeyboardFocusWithin); int delta = e.Key == Key.Left || e.Key == Key.Up ? -1 : 1;
                if (tiles.Count > 0) tiles[(index < 0 ? 0 : index + delta + tiles.Count) % tiles.Count].Focus(); e.Handled = true;
            }
        }
        internal void ChangePage(int step)
        {
            int next = Math.Max(0, Math.Min(page + step, Math.Max(0, (Entries.Count - 1) / Math.Max(1, pageCapacity))));
            if (next == page) return; page = next; RenderEntries();
        }
        internal void ChangeStyle(string style)
        {
            if (!ExpansionLayout.Styles.Contains(style)) return; Settings.ExpandStyle = style; page = 0; Settings.CustomPanelPosition = false;
            RenderEntries(); if (IsVisible) AnimateOpen(); controller.SaveSoon();
        }
        internal void SetScale(double scale) { Settings.PopupScale = Math.Max(0.7, Math.Min(1.5, scale)); RenderEntries(); controller.SaveSoon(); }
        internal void ChangeFanDirection(string direction)
        {
            if (!ExpansionLayout.FanDirections.Contains(direction)) return;
            Settings.FanDirection = direction; RenderKeepingAnchor(); if (IsVisible) AnimateOpen(); controller.SaveSoon();
        }
        private void RenderKeepingAnchor()
        {
            bool keep = Settings.CustomPanelPosition && Settings.ExpandStyle == "Fan" && SurfaceLayout != null && SurfaceLayout.Items.Count > 0;
            var anchor = keep ? new Point(Left + SurfaceLayout.Anchor.X, Top + SurfaceLayout.Anchor.Y) : new Point();
            RenderEntries();
            if (keep) { Geometry.SetOffset(Settings, new Point(anchor.X - SurfaceLayout.Anchor.X, anchor.Y - SurfaceLayout.Anchor.Y)); FollowFolder(); }
        }
        internal void RefreshAppearance() { Title = "FolderBloom · " + Settings.Name; RenderKeepingAnchor(); leaveTracker.Reset(); }
        internal async Task PrimeAsync()
        {
            if (disposed) return;
            new WindowInteropHelper(this).EnsureHandle();
            if (!primed && !Loading) await RefreshContentsAsync();
            if (!disposed) { canvas.Measure(new Size(Width, Height)); canvas.Arrange(new Rect(0, 0, Width, Height)); canvas.UpdateLayout(); }
        }
        internal void Open() { Open(true); }
        internal async void Open(bool activate)
        {
            if (disposed) return;
            if (IsVisible && Phase != PopupPhase.Closing) return;
            FollowFolder(); Show(); if (activate) Activate(); AnimateOpen();
            Settings.WasExpanded = true; controller.SaveSoon();
            leaveTracker.Reset(); leaveTimer.Start(); StartWatching();
            if (!Loading) await RefreshContentsAsync(true);
        }
        private double FolderWidth { get { FolderWidget widget; return controller.Widgets.TryGetValue(Settings.Id, out widget) ? widget.Width : Native.DesktopTileSize.Width; } }
        private Point FolderCenter { get { return new Point(Settings.X + FolderWidth / 2, Settings.Y + 2 / Native.Scale + Native.DesktopIconSize / 2); } }
        private double FolderHeight { get { FolderWidget widget; return controller.Widgets.TryGetValue(Settings.Id, out widget) ? widget.Height : Native.DesktopTileSize.Height; } }
        private string ResolveFanDirection(int count, Rect work)
        {
            if (Settings.FanDirection != "Auto") return Settings.FanDirection;
            if (Settings.CustomPanelPosition) return "Up";
            var upper = ExpansionLayout.Build("Fan", count, Settings.PopupScale, "Up");
            return FolderCenter.Y - upper.Anchor.Y < work.Top && work.Bottom - FolderCenter.Y > FolderCenter.Y - work.Top ? "Down" : "Up";
        }
        internal void FollowFolder()
        {
            if (SurfaceLayout == null) return;
            var center = FolderCenter;
            var work = Native.WorkArea(center);
            if (!Settings.CustomPanelPosition && Settings.ExpandStyle == "Fan" && Settings.FanDirection == "Auto" && Entries.Count > 0)
            {
                if (ResolveFanDirection(SurfaceLayout.Items.Count, work) != renderedFanDirection) { RenderEntries(); return; }
            }
            var ideal = Settings.CustomPanelPosition ? Geometry.PanelOrigin(Settings) : Settings.ExpandStyle == "Fan" || Settings.ExpandStyle == "Ring" ? new Point(center.X - SurfaceLayout.Anchor.X, center.Y - SurfaceLayout.Anchor.Y) : new Point(center.X - Width / 2, Settings.Y - Height - 7);
            if (!Settings.CustomPanelPosition && Settings.ExpandStyle != "Fan" && Settings.ExpandStyle != "Ring" && ideal.Y < work.Top) ideal.Y = Settings.Y + FolderHeight + 7;
            Point p = Geometry.Clamp(ideal, new Size(Width, Height), work); Left = p.X; Top = p.Y;
            surfaceScale.CenterX = center.X - Left; surfaceScale.CenterY = center.Y - Top;
        }
        internal void RememberPosition() { var p = Native.KeepVisible(new Point(Left, Top), new Size(Width, Height)); Left = p.X; Top = p.Y; Geometry.SetOffset(Settings, p); Settings.CustomPanelPosition = true; controller.SaveSoon(); leaveTracker.Reset(); }
        internal void Place(string direction)
        {
            Settings.CustomPanelPosition = true;
            if (direction == "Left") { Settings.OffsetX = -Width - 16; Settings.OffsetY = 0; }
            else if (direction == "Above") { Settings.OffsetX = (FolderWidth - Width) / 2; Settings.OffsetY = -Height - 16; }
            else if (direction == "Below") { Settings.OffsetX = (FolderWidth - Width) / 2; Settings.OffsetY = FolderHeight + 16; }
            else { Settings.OffsetX = FolderWidth + 16; Settings.OffsetY = 0; }
            FollowFolder(); controller.SaveSoon();
        }
        private DoubleAnimation Motion(double from, double to, int milliseconds, bool openingMotion)
        {
            return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = new CubicEase { EasingMode = openingMotion ? EasingMode.EaseOut : EasingMode.EaseIn }, FillBehavior = FillBehavior.HoldEnd };
        }
        private void AnimateOpen()
        {
            bool reversing = Phase == PopupPhase.Closing;
            double startOpacity = reversing ? canvas.Opacity : 0.12, startScale = reversing ? surfaceScale.ScaleX : 0.90;
            int version = ++motionVersion; int duration = MotionEnabled ? controller.Preferences.AnimationDuration : 0; Phase = duration == 0 ? PopupPhase.Open : PopupPhase.Opening; canvas.IsHitTestVisible = true;
            canvas.BeginAnimation(UIElement.OpacityProperty, null); canvas.Opacity = 1;
            surfaceScale.BeginAnimation(ScaleTransform.ScaleXProperty, null); surfaceScale.BeginAnimation(ScaleTransform.ScaleYProperty, null); surfaceScale.ScaleX = surfaceScale.ScaleY = 1;
            for (int i = 0; i < tiles.Count; i++)
            {
                var rect = SurfaceLayout.Items[i]; var movement = (TranslateTransform)tiles[i].RenderTransform;
                double dx = reversing ? movement.X : (FolderCenter.X - Left - rect.X - rect.Width / 2) * 0.38, dy = reversing ? movement.Y : (FolderCenter.Y - Top - rect.Y - rect.Height / 2) * 0.38;
                movement.BeginAnimation(TranslateTransform.XProperty, null); movement.BeginAnimation(TranslateTransform.YProperty, null); movement.X = movement.Y = 0;
                if (duration > 0) { movement.BeginAnimation(TranslateTransform.XProperty, Motion(dx, 0, duration, true)); movement.BeginAnimation(TranslateTransform.YProperty, Motion(dy, 0, duration, true)); }
            }
            if (duration == 0) return;
            var opacity = Motion(startOpacity, 1, duration, true); opacity.Completed += delegate { if (version == motionVersion && Phase == PopupPhase.Opening) Phase = PopupPhase.Open; };
            canvas.BeginAnimation(UIElement.OpacityProperty, opacity);
            surfaceScale.BeginAnimation(ScaleTransform.ScaleXProperty, Motion(startScale, 1, duration, true)); surfaceScale.BeginAnimation(ScaleTransform.ScaleYProperty, Motion(startScale, 1, duration, true));
        }
        internal void Collapse()
        {
            if (disposed || Phase == PopupPhase.Closing || (!IsVisible && Phase == PopupPhase.Hidden)) return;
            StopWatching(); refreshTimer.Stop(); leaveTimer.Stop(); leaveTracker.Reset(); int version = ++motionVersion; Phase = PopupPhase.Closing; canvas.IsHitTestVisible = false;
            Settings.WasExpanded = false; controller.SaveSoon();
            int duration = MotionEnabled ? (int)Math.Round(controller.Preferences.AnimationDuration * 0.7) : 0;
            if (duration == 0 || !IsVisible) { FinishClose(version); return; }
            var fade = Motion(canvas.Opacity, 0, duration, false); fade.Completed += delegate { FinishClose(version); }; canvas.BeginAnimation(UIElement.OpacityProperty, fade);
            surfaceScale.BeginAnimation(ScaleTransform.ScaleXProperty, Motion(surfaceScale.ScaleX, 0.94, duration, false)); surfaceScale.BeginAnimation(ScaleTransform.ScaleYProperty, Motion(surfaceScale.ScaleY, 0.94, duration, false));
            for (int i = 0; i < tiles.Count; i++)
            {
                var rect = SurfaceLayout.Items[i]; var movement = (TranslateTransform)tiles[i].RenderTransform;
                movement.BeginAnimation(TranslateTransform.XProperty, Motion(movement.X, (FolderCenter.X - Left - rect.X - rect.Width / 2) * 0.20, duration, false));
                movement.BeginAnimation(TranslateTransform.YProperty, Motion(movement.Y, (FolderCenter.Y - Top - rect.Y - rect.Height / 2) * 0.20, duration, false));
            }
        }
        private void FinishClose(int version) { if (version != motionVersion || Phase != PopupPhase.Closing || disposed) return; Hide(); Phase = PopupPhase.Hidden; canvas.BeginAnimation(UIElement.OpacityProperty, null); canvas.Opacity = 1; canvas.IsHitTestVisible = true; }
        internal async Task NavigateAsync(string path) { StopWatching(); CurrentPath = path; page = 0; if (!disposed && (IsVisible || controller.IsTest)) StartWatching(); await RefreshContentsAsync(); }
        private async void NavigateUp() { if (String.Equals(CurrentPath.TrimEnd('\\'), Settings.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return; var parent = Directory.GetParent(CurrentPath); if (parent != null) await NavigateAsync(parent.FullName); }
        private async void Activate(FolderEntry entry) { if (entry.IsDirectory) await NavigateAsync(entry.Path); else { controller.OpenPath(entry.Path); if (!pinned) Collapse(); } }
        internal async Task RefreshContentsAsync(bool preserveUnchanged = false)
        {
            if (disposed) return; int version = ++readVersion; string path = CurrentPath; Loading = true;
            try
            {
                var result = await FolderReader.ReadAsync(path); if (disposed || version != readVersion) return;
                bool unchanged = primed && LastError == null && Entries.Count == result.Count && Entries.Zip(result, (a, b) => a.Path == b.Path && a.IsDirectory == b.IsDirectory).All(same => same);
                Entries = result; LastError = null; Loading = false; primed = true;
                if (preserveUnchanged && unchanged) await LoadIconsAsync(iconVersion, iconTargets.ToArray()); else RenderEntries();
            }
            catch (Exception ex) { if (disposed || version != readVersion) return; LastError = ex.Message; Entries = new List<FolderEntry>(); Loading = false; primed = true; RenderEntries(); }
        }
        private Style TileStyle()
        {
            var style = new Style(typeof(Button)); var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border)); border.Name = "tile"; border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5)); border.SetValue(Border.BackgroundProperty, Brushes.Transparent); border.SetValue(Border.BorderBrushProperty, Brushes.Transparent); border.SetValue(Border.BorderThicknessProperty, new Thickness(1)); border.SetValue(Border.PaddingProperty, new Thickness(3));
            var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content); template.VisualTree = border;
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Brush(Appearance.Light(controller.Preferences) ? "#18000000" : "#22FFFFFF"), "tile")); hover.Setters.Add(new Setter(Border.BorderBrushProperty, Ui.Brush("#33777777"), "tile")); template.Triggers.Add(hover);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true }; focus.Setters.Add(new Setter(Border.BorderBrushProperty, Ui.Accent, "tile")); template.Triggers.Add(focus); style.Setters.Add(new Setter(Control.TemplateProperty, template)); return style;
        }
        private async void RenderEntries()
        {
            if (disposed) return; int version = ++iconVersion; LoadedIconCount = 0; tiles.Clear(); iconTargets.Clear(); canvas.Children.Clear(); canvas.Children.Add(surface);
            var preferences = controller.Preferences; bool light = Appearance.Light(preferences);
            surface.Fill = Appearance.Surface(preferences); surface.Stroke = Ui.Brush(light ? "#887A7A7A" : "#88575757"); surface.Opacity = Math.Max(1.0 / 255, preferences.PanelOpacity); message.Foreground = light ? Ui.Brush("#505050") : Ui.Muted;
            var work = Native.WorkArea(FolderCenter); int maximum = Settings.ExpandStyle == "Grid" ? preferences.GridColumns * preferences.GridRows : ExpansionLayout.PageSize(Settings.ExpandStyle); pageCapacity = Math.Min(maximum, Math.Max(1, Entries.Count));
            while (pageCapacity > 1)
            {
                var candidate = ExpansionLayout.Build(Settings.ExpandStyle, pageCapacity, Settings.PopupScale, Settings.ExpandStyle == "Fan" ? ResolveFanDirection(pageCapacity, work) : "Up", GridColumnCount(pageCapacity));
                if (candidate.Size.Width <= work.Width - 12 && candidate.Size.Height <= work.Height - 12) break; pageCapacity--;
            }
            page = Math.Max(0, Math.Min(page, Math.Max(0, (Entries.Count - 1) / pageCapacity)));
            var visible = Entries.Skip(page * pageCapacity).Take(pageCapacity).ToList();
            string direction = Settings.ExpandStyle == "Fan" ? ResolveFanDirection(visible.Count, work) : "Up";
            var layout = ExpansionLayout.Build(Settings.ExpandStyle, visible.Count, Settings.PopupScale, direction, GridColumnCount(visible.Count));
            if (visible.Count == 0)
            {
                layout = new LayoutResult { Size = new Size(176, 84), Surface = new RectangleGeometry(new Rect(1, 1, 174, 82), 12, 12), Anchor = new Point(88, 96) };
                message.Text = LastError != null ? "Folder unavailable\nRight-click to refresh" : primed ? "This folder is empty" : "…";
                message.ToolTip = LastError; message.Width = 152; Canvas.SetLeft(message, 12); Canvas.SetTop(message, 25); canvas.Children.Add(message);
            }
            renderedFanDirection = direction; SurfaceLayout = layout; surface.Data = layout.Surface; Width = canvas.Width = layout.Size.Width; Height = canvas.Height = layout.Size.Height; FollowFolder();
            var tileStyle = TileStyle();
            for (int i = 0; i < visible.Count; i++)
            {
                var entry = visible[i]; var rect = layout.Items[i]; double scale = Settings.PopupScale;
                var tile = new StackPanel { Width = 56 * scale, Orientation = Orientation.Vertical };
                var icon = new Image { Width = 36 * scale, Height = 36 * scale, Source = entry.IsDirectory ? Icons.Folder("default") : Icons.GenericFile, Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality); tile.Children.Add(icon);
                string label = ShortcutDrop.IsShortcut(entry.Path) ? Path.GetFileNameWithoutExtension(entry.Name) : entry.Name;
                var text = Ui.Text(label, 11 * scale, light ? Ui.Brush("#202020") : Brushes.White); text.Height = 28 * scale; text.TextAlignment = TextAlignment.Center; text.TextTrimming = TextTrimming.CharacterEllipsis; text.Margin = new Thickness(0, 4 * scale, 0, 0); if (!light) text.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 2, ShadowDepth = 1, Opacity = 0.9 }; tile.Children.Add(text);
                var button = new Button { Style = tileStyle, Content = tile, Width = rect.Width, Height = rect.Height, Padding = new Thickness(0), Margin = new Thickness(0), ToolTip = entry.Path, Cursor = Cursors.Hand, RenderTransform = new TranslateTransform() };
                System.Windows.Automation.AutomationProperties.SetName(button, label); var selected = entry; bool suppressClick = false; DateTime heldAt = DateTime.MinValue; Point heldPoint = new Point();
                button.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { suppressClick = false; heldAt = DateTime.UtcNow; heldPoint = e.GetPosition(button); };
                button.PreviewMouseLeftButtonUp += delegate { heldAt = DateTime.MinValue; };
                button.LostMouseCapture += delegate { heldAt = DateTime.MinValue; };
                button.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
                {
                    if (e.LeftButton != MouseButtonState.Pressed || heldAt == DateTime.MinValue || suppressClick || !DragTiming.Ready(DateTime.UtcNow - heldAt, controller.Preferences.DragHoldDelay, e.GetPosition(button) - heldPoint)) return;
                    suppressClick = true; dropping = true;
                    try { DragDrop.DoDragDrop(button, new DataObject(DataFormats.FileDrop, new[] { selected.Path }), DragDropEffects.Copy | DragDropEffects.Link); }
                    finally { dropping = false; heldAt = DateTime.MinValue; leaveTracker.Reset(); }
                    e.Handled = true;
                };
                button.Click += delegate { if (!suppressClick) Activate(selected); };
                var itemMenu = new ContextMenu(); itemMenu.Items.Add(Ui.Menu("Open", () => Activate(selected))); itemMenu.Items.Add(Ui.Menu("Open containing folder", () => controller.OpenPath(CurrentPath))); TrackMenu(itemMenu); button.ContextMenu = itemMenu;
                Canvas.SetLeft(button, rect.X); Canvas.SetTop(button, rect.Y); canvas.Children.Add(button); tiles.Add(button); iconTargets.Add(Tuple.Create(entry, icon));
            }
            BuildMenu();
            await LoadIconsAsync(version, iconTargets.ToArray());
        }
        private int GridColumnCount(int count)
        {
            return Math.Min(controller.Preferences.GridColumns, Math.Max((int)Math.Ceiling(Math.Sqrt(Math.Max(1, count))), (int)Math.Ceiling(count / (double)controller.Preferences.GridRows)));
        }
        private async Task LoadIconsAsync(int version, IEnumerable<Tuple<FolderEntry, Image>> targets)
        {
            int loaded = 0;
            foreach (var target in targets)
            {
                if (version != iconVersion || disposed) break;
                try { var icon = await ShellWorker.Icon(target.Item1.Path); if (version == iconVersion && !disposed) { target.Item2.Source = icon; LoadedIconCount = ++loaded; } }
                catch { }
            }
        }
        internal void QueueRefresh() { if (disposed) return; refreshTimer.Stop(); refreshTimer.Start(); }
        internal void StartWatching()
        {
            StopWatching(); if (disposed) return;
            try
            {
                watcher = new FileSystemWatcher(CurrentPath) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Attributes };
                FileSystemEventHandler changed = delegate(object sender, FileSystemEventArgs e) { ShellWorker.InvalidateIcon(e.FullPath); DispatchRefresh(); };
                watcher.Created += changed; watcher.Deleted += changed; watcher.Changed += changed; watcher.Renamed += delegate(object sender, RenamedEventArgs e) { ShellWorker.InvalidateIcon(e.OldFullPath); ShellWorker.InvalidateIcon(e.FullPath); DispatchRefresh(); }; watcher.Error += delegate { DispatchRefresh(); }; watcher.EnableRaisingEvents = true;
            }
            catch { }
        }
        private void DispatchRefresh() { if (!disposed && !Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke((Action)QueueRefresh); }
        private void StopWatching() { if (watcher != null) { watcher.EnableRaisingEvents = false; watcher.Dispose(); watcher = null; } }
        internal void Dispose() { disposed = true; readVersion++; iconVersion++; motionVersion++; StopWatching(); refreshTimer.Stop(); leaveTimer.Stop(); Close(); }
    }
}
