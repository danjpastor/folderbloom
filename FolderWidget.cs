using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace FolderBloom
{
    internal sealed class FolderWidget : Window
    {
        internal readonly FolderSettings Settings;
        private readonly Controller controller;
        private readonly Image image;
        internal readonly FolderPreview Preview;
        internal TextBlock CollapsedLabel { get { return label; } }
        private readonly TextBlock label;
        internal Border CollapsedLabelViewport { get { return labelViewport; } }
        private readonly Border labelViewport;
        private readonly TextBox renameBox;
        private readonly StackPanel stack;
        private bool renaming, endingRename, selected, clickedLabel, renameOnRelease;
        private DateTime selectedAt;
        internal bool IsRenaming { get { return renaming; } }
        private readonly Border surface;
        private readonly Border strokeOverlay;
        private readonly DispatcherTimer hover;
        internal FolderPanel Panel;
        private Point mouseStart, windowStart;
        private bool pressed, moved;
        private DateTime pressedAt;
        internal bool IsMoving { get { return pressed; } }
        private bool dropping;
        private double? desktopPitchWidth;
        internal bool IsBusy { get { return pressed || dropping || renaming || surface.ContextMenu.IsOpen; } }
        internal FolderWidget(Controller controller, FolderSettings settings)
        {
            this.controller = controller; Settings = settings; Title = "FolderBloom · " + settings.Name;
            Width = Native.DesktopTileSize.Width; Height = Native.DesktopTileSize.Height; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
            var location = Native.KeepVisible(new Point(settings.X, settings.Y), new Size(Width, Height)); Left = settings.X = location.X; Top = settings.Y = location.Y;
            stack = new StackPanel(); Preview = new FolderPreview(settings.Path, settings.Color, Native.DesktopIconSize); Preview.HorizontalAlignment = HorizontalAlignment.Center; image = Preview.FolderIcon; stack.Children.Add(Preview);
            double iconFontSize = Math.Max(9, SystemFonts.IconFontSize);
            label = Ui.Text(settings.Name, iconFontSize, Brushes.White); label.FontFamily = SystemFonts.IconFontFamily; label.TextAlignment = TextAlignment.Center; label.TextWrapping = TextWrapping.Wrap; label.MaxHeight = Double.PositiveInfinity; label.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 1 };
            labelViewport = new Border { ClipToBounds = true, Margin = new Thickness(0, 3, 0, 0), Child = label, HorizontalAlignment = HorizontalAlignment.Stretch }; stack.Children.Add(labelViewport);
            label.ToolTip = "Click to select, then click again or press F2 to rename.";
            renameBox = new TextBox { Visibility = Visibility.Collapsed, FontFamily = label.FontFamily, FontSize = iconFontSize, Padding = new Thickness(1), Margin = new Thickness(0, 3, 0, 0), MaxLength = 255, TextAlignment = TextAlignment.Center, Foreground = Brushes.Black, Background = Brushes.White, BorderBrush = Ui.Accent, BorderThickness = new Thickness(1), VerticalContentAlignment = VerticalAlignment.Center };
            System.Windows.Automation.AutomationProperties.SetName(renameBox, "Folder name"); stack.Children.Add(renameBox);
            renameBox.PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { FinishRename(true); e.Handled = true; } else if (e.Key == Key.Escape) { FinishRename(false); e.Handled = true; } };
            renameBox.LostKeyboardFocus += delegate { if (renaming && !endingRename) FinishRename(true); };
            var contentGrid = new Grid(); stack.Margin = new Thickness(0, 2 / Native.Scale, 0, 3); contentGrid.Children.Add(stack);
            strokeOverlay = new Border { CornerRadius = new CornerRadius(6), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1), IsHitTestVisible = false }; contentGrid.Children.Add(strokeOverlay);
            surface = new Border { CornerRadius = new CornerRadius(6), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0), Child = contentGrid, Cursor = Cursors.Hand, ToolTip = settings.Path }; Content = surface;
            UpdateCollapsedFootprint(Native.DesktopTileSize.Width);
            SourceInitialized += delegate { Native.ToolWindow(this, true); };
            hover = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, controller.Preferences.OpenDelay)) };
            hover.Tick += delegate { hover.Stop(); if (!pressed && !renaming && IsMouseOver && controller.HoverEnabled(Settings)) Expand(false); };
            surface.MouseEnter += delegate { RefreshHighlight(); if (!renaming && controller.HoverEnabled(Settings)) { if (controller.Preferences.OpenDelay == 0) Expand(false); else hover.Start(); } };
            surface.MouseLeave += delegate { surface.Background = Brushes.Transparent; SetStroke(Brushes.Transparent); hover.Stop(); };
            surface.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (renaming) return;
                clickedLabel = label.IsMouseOver; renameOnRelease = clickedLabel && selected && (DateTime.UtcNow - selectedAt).TotalMilliseconds > System.Windows.Forms.SystemInformation.DoubleClickTime;
                Native.ToolWindow(this, false); Activate(); Focus();
                controller.InvalidateDesktopArrangement(); hover.Stop(); pressed = true; moved = false; pressedAt = DateTime.UtcNow; mouseStart = PointToScreen(e.GetPosition(this)); windowStart = new Point(Left, Top); surface.CaptureMouse(); e.Handled = true;
            };
            surface.MouseMove += delegate(object sender, MouseEventArgs e)
            {
                if (!pressed || e.LeftButton != MouseButtonState.Pressed) return;
                Point current = PointToScreen(e.GetPosition(this)); double scale = Native.Scale;
                Vector delta = (current - mouseStart) / scale;
                if (!moved && !DragTiming.Ready(DateTime.UtcNow - pressedAt, controller.Preferences.DragHoldDelay, delta)) return;
                moved = true; Left = windowStart.X + delta.X; Top = windowStart.Y + delta.Y;
                Settings.X = Left; Settings.Y = Top; if (Panel != null && Panel.IsVisible) Panel.FollowFolder();
            };
            surface.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!pressed) return; pressed = false; surface.ReleaseMouseCapture();
                if (moved)
                {
                    var p = Native.KeepVisible(new Point(Left, Top), new Size(Width, Height)); Left = Settings.X = p.X; Top = Settings.Y = p.Y;
                    if (Panel != null && Panel.IsVisible) Panel.FollowFolder(); controller.SaveSoon(); controller.ArrangeDesktop(this);
                }
                else if (clickedLabel) { if (renameOnRelease) BeginRename(); else { selected = true; selectedAt = DateTime.UtcNow; RefreshHighlight(); } }
                else if (Panel != null && Panel.IsVisible && Panel.Phase != PopupPhase.Closing) Collapse(); else Expand();
                if (!renaming) Native.LowerToDesktop(this); e.Handled = true;
            };
            surface.LostMouseCapture += delegate { if (pressed) { pressed = false; controller.InvalidateDesktopArrangement(); controller.SaveSoon(); controller.ArrangeDesktop(this); } };
            BuildMenu();
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.Key == Key.F2 && !renaming) { BeginRename(); e.Handled = true; } };
            Deactivated += delegate { if (renaming && !endingRename) FinishRename(true); selected = false; RefreshHighlight(); };
            Loaded += async delegate { RefreshPreview(); if (Panel == null) Panel = new FolderPanel(controller, Settings); await Panel.PrimeAsync(); };
            AllowDrop = true;
            DragEnter += OnDragOver; DragOver += OnDragOver;
            DragLeave += delegate { dropping = false; SetStroke(Brushes.Transparent); };
            Drop += async delegate(object sender, DragEventArgs e)
            {
                e.Handled = true; e.Effects = DragDropEffects.None;
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop); dropping = true;
                try { await controller.AddDroppedItems(Settings.Path, paths); Preview.QueueRefresh(); if (Panel != null) Panel.QueueRefresh(); e.Effects = DragDropEffects.Copy; }
                finally { dropping = false; SetStroke(Brushes.Transparent); }
            };
        }
        private void OnDragOver(object sender, DragEventArgs e)
        {
            dropping = e.Data.GetDataPresent(DataFormats.FileDrop); e.Effects = dropping ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
            SetStroke(dropping ? Ui.Accent : Brushes.Transparent);
        }
        internal void UpdateSize()
        {
            double size = Native.DesktopIconSize; var footprint = Native.DesktopTileSize;
            double oldWidth = Width, oldHeight = Height, oldPreviewWidth = Preview.Width, oldFontSize = label.FontSize; FontFamily oldFontFamily = label.FontFamily;
            if (Preview.Width != size || Preview.Height != size) Preview.Width = Preview.Height = size;
            double iconFontSize = Math.Max(9, SystemFonts.IconFontSize); label.FontSize = iconFontSize; label.FontFamily = SystemFonts.IconFontFamily; renameBox.FontSize = iconFontSize; renameBox.FontFamily = label.FontFamily;
            UpdateCollapsedFootprint(desktopPitchWidth ?? footprint.Width);
            bool geometryChanged = Math.Abs(Width - oldWidth) > 0.1 || Math.Abs(Height - oldHeight) > 0.1 || Math.Abs(Preview.Width - oldPreviewWidth) > 0.1 || Math.Abs(label.FontSize - oldFontSize) > 0.1 || !Equals(label.FontFamily, oldFontFamily);
            if (geometryChanged && Panel != null && Panel.IsVisible) Panel.FollowFolder();
        }
        internal void UpdateCollapsedFootprint(double width)
        {
            UpdateCollapsedFootprint(width, Native.Scale);
        }
        internal void UpdateCollapsedFootprint(double width, double scale)
        {
            scale = Math.Max(0.1, scale); var stackMargin = new Thickness(0, 2 / scale, 0, 3); if (!stack.Margin.Equals(stackMargin)) stack.Margin = stackMargin;
            width = Math.Max(1, width); if (Math.Abs(Width - width) > 0.1) Width = width; if (Double.IsNaN(label.Width) || Math.Abs(label.Width - Width) > 0.1) label.Width = Width; if (!Double.IsNaN(label.Height)) label.Height = Double.NaN;
            label.Measure(new Size(Width, Double.PositiveInfinity)); double contentHeight = Math.Max(1, label.DesiredSize.Height);
            labelViewport.Height = Math.Min(contentHeight, TwoLineLabelHeight(label));
            double labelHeight;
            if (renaming) { renameBox.Width = Width; renameBox.Measure(new Size(Width, Double.PositiveInfinity)); labelHeight = renameBox.DesiredSize.Height; }
            else labelHeight = labelViewport.Height + labelViewport.Margin.Top + labelViewport.Margin.Bottom;
            double height = DesktopGrid.CollapsedHeight(Native.DesktopIconSize, scale, labelHeight); if (Math.Abs(Height - height) > 0.1) Height = height;
        }
        internal static double LabelLineHeight(TextBlock source)
        {
            var sample = new TextBlock { Text = "Mg", FontFamily = source.FontFamily, FontSize = source.FontSize, FontWeight = source.FontWeight, FontStyle = source.FontStyle, FontStretch = source.FontStretch };
            sample.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity)); return sample.DesiredSize.Height;
        }
        internal static double TwoLineLabelHeight(TextBlock source)
        {
            var sample = new TextBlock { Text = "Ag\ngjpqy", FontFamily = source.FontFamily, FontSize = source.FontSize, FontWeight = source.FontWeight, FontStyle = source.FontStyle, FontStretch = source.FontStretch, TextWrapping = TextWrapping.NoWrap };
            sample.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity)); return sample.DesiredSize.Height;
        }
        internal void UseFreeSize() { desktopPitchWidth = null; UpdateCollapsedFootprint(Native.DesktopTileSize.Width); if (Panel != null && Panel.IsVisible) Panel.FollowFolder(); }
        internal bool ApplyDesktopPlacement(DesktopFolder placement, bool snap)
        {
            bool changed = Math.Abs(Left - placement.Position.X) > 0.1 || Math.Abs(Top - placement.Position.Y) > 0.1;
            Size oldFootprint = new Size(Width, Height);
            desktopPitchWidth = snap ? (double?)placement.Size.Width : null;
            UpdateCollapsedFootprint(desktopPitchWidth ?? placement.Size.Width);
            bool resized = Math.Abs(Width - oldFootprint.Width) > 0.1 || Math.Abs(Height - oldFootprint.Height) > 0.1;
            Left = Settings.X = placement.Position.X; Top = Settings.Y = placement.Position.Y;
            if ((changed || resized) && Panel != null && Panel.IsVisible) Panel.FollowFolder();
            return changed;
        }
        private void BuildMenu()
        {
            var menu = new ContextMenu(); menu.Items.Add(Ui.Menu("Expand folder", Expand)); menu.Items.Add(Ui.Menu("Open in Explorer", () => controller.OpenPath(Settings.Path)));
            menu.Items.Add(Ui.Menu("Rename     F2", () => controller.PromptRename(Settings)));
            var colors = new MenuItem { Header = "Folder color" };
            foreach (var c in FolderEditor.Colors)
            {
                string color = c; var item = Ui.Menu(color == "default" ? "Windows default" : color, () => controller.SetColor(Settings, color)); item.Icon = Ui.FolderImage(color, 24); colors.Items.Add(item);
            }
            colors.Items.Add(Ui.Menu("Custom color…", () => controller.Edit(Settings))); menu.Items.Add(colors);
            menu.Items.Add(Ui.Menu("Customize…", () => controller.Edit(Settings)));
            menu.Items.Add(Ui.Menu("Manage folders…", controller.ShowManager));
            menu.Items.Add(Ui.Menu("App settings…", controller.ShowSettings));
            menu.Items.Add(new Separator()); menu.Items.Add(Ui.Menu("Remove desktop view (keeps your files)", () => controller.Remove(Settings))); surface.ContextMenu = menu;
        }
        private void RefreshHighlight() { bool highlight = selected || renaming || controller.Preferences.HighlightOnHover && surface.IsMouseOver; surface.Background = highlight ? Ui.Brush("#224E718A") : Brushes.Transparent; SetStroke(highlight ? Ui.Brush("#445B83A0") : Brushes.Transparent); }
        private void SetStroke(Brush brush) { surface.BorderBrush = brush; strokeOverlay.BorderBrush = brush; }
        private async void RefreshPreview() { await Preview.ConfigureAsync(controller.Preferences.ShowFolderPreviews, controller.Preferences.CycleFolderPreviews, Settings.Animate && controller.Preferences.AnimationDuration > 0); }
        internal void Refresh() { Title = "FolderBloom · " + Settings.Name; image.Source = Icons.Folder(Settings.Color); label.Text = Settings.Name; hover.Stop(); hover.Interval = TimeSpan.FromMilliseconds(Math.Max(1, controller.Preferences.OpenDelay)); UpdateCollapsedFootprint(desktopPitchWidth ?? Width); RefreshHighlight(); if (IsLoaded) RefreshPreview(); if (Panel != null) { Panel.RefreshAppearance(); if (Panel.IsVisible) Panel.FollowFolder(); } }
        internal void RestoreExpansion() { Expand(false); }
        internal void BeginRename()
        {
            if (renaming) return; hover.Stop(); Collapse(); renaming = true; selected = true; controller.InvalidateDesktopArrangement();
            renameBox.Text = new System.IO.DirectoryInfo(Settings.Path).Name; label.Visibility = Visibility.Collapsed; renameBox.Visibility = Visibility.Visible;
            UpdateCollapsedFootprint(Width);
            Native.ToolWindow(this, false); Activate(); renameBox.Focus(); renameBox.SelectAll(); RefreshHighlight();
        }
        internal void FinishRename(bool commit)
        {
            if (!renaming || endingRename) return; endingRename = true;
            try
            {
                if (commit) controller.RenameFolder(Settings, renameBox.Text);
                renaming = false; renameBox.Visibility = Visibility.Collapsed; label.Visibility = Visibility.Visible; UpdateCollapsedFootprint(Width); RefreshHighlight(); Native.ToolWindow(this, true);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "FolderBloom · Rename"); Dispatcher.BeginInvoke((Action)(() => { Activate(); renameBox.Focus(); renameBox.SelectAll(); })); }
            finally { endingRename = false; }
        }
        internal async void SourceRenamed()
        {
            Preview.ChangePath(Settings.Path); surface.ToolTip = Settings.Path; Refresh();
            if (Panel != null) await Panel.NavigateAsync(Settings.Path);
        }
        internal void Expand() { Expand(true); }
        private void Expand(bool activate)
        {
            hover.Stop();
            if (renaming) return;
            if (Panel == null) Panel = new FolderPanel(controller, Settings);
            Panel.Open(activate);
        }
        internal void Collapse() { if (Panel != null) Panel.Collapse(); }
        internal void ResetLocation()
        {
            controller.InvalidateDesktopArrangement();
            var area = Native.WorkArea(new Point(controller.Manager.Left, controller.Manager.Top));
            Left = Settings.X = area.Left + 140; Top = Settings.Y = area.Top + 120;
            if (Panel != null) Panel.FollowFolder(); controller.SaveSoon(); controller.ArrangeDesktop(this);
        }
        internal void Dispose() { hover.Stop(); Preview.Dispose(); if (Panel != null) Panel.Dispose(); Close(); }
    }
}
