using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace FolderBloom
{
    internal static class Appearance
    {
        internal static bool Light(AppPreferences preferences)
        {
            if (preferences.PanelTheme != "Auto") return preferences.PanelTheme == "Light";
            try { using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 0)) != 0; }
            catch { return false; }
        }
        internal static Brush Surface(AppPreferences preferences)
        {
            return Light(preferences) ? new LinearGradientBrush(Color.FromRgb(250, 250, 250), Color.FromRgb(229, 232, 237), 90)
                : new LinearGradientBrush(Color.FromRgb(43, 43, 43), Color.FromRgb(30, 30, 30), 90);
        }
    }
    internal static class DragTiming
    {
        internal static bool Ready(TimeSpan held, int delay, Vector distance)
        {
            return held.TotalMilliseconds >= delay && (Math.Abs(distance.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(distance.Y) >= SystemParameters.MinimumVerticalDragDistance);
        }
    }
    internal sealed class SettingsWindow : Window
    {
        private readonly Controller controller;
        private readonly AppPreferences draft;
        private bool applyToAll;
        internal SettingsWindow(Controller controller)
        {
            this.controller = controller; draft = controller.Preferences.Clone();
            Ui.SetupWindow(this, "FolderBloom · Settings", 840, Math.Min(960, SystemParameters.WorkArea.Height - 40)); MinWidth = 670; MinHeight = 500;
            Background = Ui.Brush("#101010");
            InstallControlStyles();
            var dock = new DockPanel { Margin = new Thickness(28, 22, 28, 20), Background = Background }; Content = dock;
            var top = new StackPanel { Margin = new Thickness(0, 0, 0, 18) }; DockPanel.SetDock(top, Dock.Top); dock.Children.Add(top);
            top.Children.Add(Ui.Text("Settings", 26, Brushes.White)); var subtitle = Ui.Text("Appearance, timing and desktop behavior for your folders.", 12, Ui.Muted); subtitle.Margin = new Thickness(0, 6, 0, 0); top.Children.Add(subtitle);
            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); dock.Children.Add(footer);
            footer.Children.Add(Ui.Button("Cancel", Close, false)); footer.Children.Add(Ui.Button("Save settings", delegate { try { controller.ApplyPreferences(draft, applyToAll); Close(); } catch (Exception ex) { Ui.Error(this, "Settings could not be saved.\n\n" + ex.Message); } }, true));
            var body = new StackPanel(); dock.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            var opening = new StackPanel(); var mode = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var hover = Radio("Hover", "opening", draft.OpenOnHover, () => draft.OpenOnHover = true); var click = Radio("Click", "opening", !draft.OpenOnHover, () => draft.OpenOnHover = false); mode.Children.Add(hover); mode.Children.Add(click);
            opening.Children.Add(Row("Open folders with", mode)); opening.Children.Add(SliderRow("Delay before opening", 0, 1500, 10, draft.OpenDelay, v => draft.OpenDelay = (int)v, Milliseconds, "Instant", "Slow")); body.Children.Add(Card(opening));
            var appearance = new StackPanel(); appearance.Children.Add(SliderRow("Panel background", 0, 100, 1, draft.PanelOpacity * 100, v => draft.PanelOpacity = v / 100, v => Math.Round(v) + "%", "Clear", "Solid"));
            var themes = new StackPanel(); themes.Children.Add(Radio("Match my Windows theme", "theme", draft.PanelTheme == "Auto", () => draft.PanelTheme = "Auto")); themes.Children.Add(Radio("Dark", "theme", draft.PanelTheme == "Dark", () => draft.PanelTheme = "Dark")); themes.Children.Add(Radio("Light", "theme", draft.PanelTheme == "Light", () => draft.PanelTheme = "Light")); appearance.Children.Add(Row("Panel theme", themes)); body.Children.Add(Card(appearance));
            var timing = new StackPanel(); timing.Children.Add(Toggle("Close when the cursor leaves", draft.CloseOnMouseLeave, v => draft.CloseOnMouseLeave = v, "Keeps the popup open while the pointer is over the folder or its expanded area."));
            timing.Children.Add(SliderRow("Delay before closing", 0, 3000, 10, draft.CloseDelay, v => draft.CloseDelay = (int)v, Milliseconds, "Instant", "Slow"));
            timing.Children.Add(SliderRow("Hold before dragging", 0, 2000, 25, draft.DragHoldDelay, v => draft.DragHoldDelay = (int)v, Milliseconds, "Instant", "Long"));
            timing.Children.Add(Note("How long to hold before dragging a folder or an item from its popup. Clicks still work either way."));
            timing.Children.Add(SliderRow("Animation speed", 0, 1000, 10, draft.AnimationDuration, v => draft.AnimationDuration = (int)v, Milliseconds, "Instant / Fast", "Slow")); body.Children.Add(Section("Timing & animation", timing, true));
            var grid = new StackPanel(); grid.Children.Add(SliderRow("Max columns", 1, 12, 1, draft.GridColumns, v => draft.GridColumns = (int)v, v => ((int)v).ToString(), null, null)); grid.Children.Add(SliderRow("Max rows", 1, 12, 1, draft.GridRows, v => draft.GridRows = (int)v, v => ((int)v).ToString(), null, null)); grid.Children.Add(Note("Applies to Grid folders. Use the mouse wheel to see entries beyond these limits.")); body.Children.Add(Section("Grid style layout", grid, true));
            var desktop = new StackPanel(); desktop.Children.Add(Toggle("Highlight folder icons on hover", draft.HighlightOnHover, v => draft.HighlightOnHover = v, "Shows a subtle highlight behind the folder, like Windows desktop icons."));
            var previews = new StackPanel(); previews.Children.Add(Toggle("Show folder content previews", draft.ShowFolderPreviews, v => draft.ShowFolderPreviews = v, "Shows up to four item icons over each desktop folder. The folder keeps its normal Windows size."));
            previews.Children.Add(Toggle("Cycle preview icons", draft.CycleFolderPreviews, v => draft.CycleFolderPreviews = v, "For folders with more than four items, shows the next group every four seconds. Turn this off to keep the first four icons.")); body.Children.Add(Section("Folder previews", previews, true));
            desktop.Children.Add(Toggle("Snap folders to the desktop grid", draft.SnapToDesktopGrid, v => draft.SnapToDesktopGrid = v, "Uses Windows icon spacing for new folders and after dragging. Keeps FolderBloom folders in separate grid spaces."));
            desktop.Children.Add(Toggle("Move desktop icons out of the way", draft.MoveDesktopIcons, v => draft.MoveDesktopIcons = v, "Keeps FolderBloom folders in place and moves overlapping shortcuts to the nearest free grid spot, including newly added shortcuts. Turn off Windows Auto arrange icons to keep folder positions fixed; otherwise FolderBloom uses a free spot."));
            desktop.Children.Add(Toggle("Restore expanded folders after restart", draft.RestoreExpanded, v => draft.RestoreExpanded = v, "Reopens the folders that were expanded when FolderBloom closed.")); body.Children.Add(Section("Desktop behavior", desktop, true));
            body.Children.Add(Card(Toggle("Start FolderBloom when you sign in to Windows", draft.StartOnLogin, v => draft.StartOnLogin = v, "Starts about 10 seconds after sign-in. Keep the app in the same location after enabling startup.")));
            var privacy = new StackPanel(); privacy.Children.Add(Ui.Text("No usage reports are sent", 14, Brushes.White)); privacy.Children.Add(Note("FolderBloom has no telemetry service or installation identifier. Your folders and settings stay on this PC. You can copy a local diagnostic summary without file names or folder paths."));
            var copied = Note(""); privacy.Children.Add(Ui.Button("Copy diagnostic summary", delegate { try { Clipboard.SetText(DiagnosticSummary(controller.Settings)); copied.Text = "Diagnostic summary copied. Nothing was uploaded."; } catch (Exception ex) { copied.Text = ex.Message; } }, false)); privacy.Children.Add(copied); body.Children.Add(Section("Privacy & diagnostics", privacy, false));
            body.Children.Add(Toggle("Apply opening and closing settings to every folder", false, v => applyToAll = v, "Also replaces individual timing overrides. Otherwise, folders set to use their own opening settings keep them."));
        }
        internal static string DiagnosticSummary(Settings settings)
        {
            return "FolderBloom " + Assembly.GetExecutingAssembly().GetName().Version + "\r\n.NET " + Environment.Version + "\r\nWindows " + Environment.OSVersion.Version + "\r\nFolders: " + settings.Folders.Count + "\r\nStyles: " + String.Join(", ", settings.Folders.GroupBy(f => f.ExpandStyle).Select(g => g.Key + "=" + g.Count())) + "\r\nAnimation: " + settings.Preferences.AnimationDuration + " ms\r\nTelemetry: none\r\n";
        }
        private void InstallControlStyles()
        {
            const string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='RadioButton'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'><Grid Background='Transparent'><Grid.ColumnDefinitions><ColumnDefinition Width='18'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Ellipse x:Name='ring' Width='15' Height='15' Stroke='#999999' StrokeThickness='1' Fill='Transparent'/><Ellipse x:Name='dot' Width='6' Height='6' Fill='White' Visibility='Collapsed'/><ContentPresenter Grid.Column='1' Margin='7,0,0,0' VerticalAlignment='Center'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='ring' Property='Fill' Value='#007ACC'/><Setter TargetName='ring' Property='Stroke' Value='#007ACC'/><Setter TargetName='dot' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='ring' Property='Stroke' Value='White'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='Slider'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'><Grid Height='22' Background='Transparent'><Border Height='4' Background='#4A4A4A' CornerRadius='2'/><Track x:Name='PART_Track'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='4' Background='#007ACC' CornerRadius='2'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='16' Height='16'><Thumb.Template><ControlTemplate TargetType='Thumb'><Ellipse Fill='#007ACC' Stroke='#666666' StrokeThickness='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
            Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml));
        }
        private static string Milliseconds(double value) { return value < 1 ? "Instant" : (value / 1000).ToString("0.##") + " s"; }
        private static RadioButton Radio(string text, string group, bool selected, Action changed)
        {
            var control = new RadioButton { Content = text, GroupName = group, IsChecked = selected, Foreground = Brushes.White, Margin = new Thickness(0, 3, 16, 4), FontSize = 13 }; control.Checked += delegate { changed(); }; return control;
        }
        private static TextBlock Note(string text) { var note = Ui.Text(text, 11, Ui.Muted); note.Margin = new Thickness(0, 6, 0, 8); return note; }
        private static Border Card(UIElement child) { return new Border { Background = Ui.Brush("#202020"), BorderBrush = Ui.Brush("#2C2C2C"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 0, 7, 10), Child = child }; }
        private static Border Section(string title, UIElement child, bool expanded) { return Card(new Expander { Header = title, IsExpanded = expanded, Content = child, Foreground = Brushes.White, FontSize = 14, Padding = new Thickness(0, 7, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch }); }
        private static Grid Row(string label, UIElement control)
        {
            var row = new Grid { Margin = new Thickness(0, 8, 0, 8) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(175) }); row.ColumnDefinitions.Add(new ColumnDefinition());
            var text = Ui.Text(label, 13, Brushes.White); text.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(text); Grid.SetColumn(control, 1); row.Children.Add(control); return row;
        }
        private static Grid SliderRow(string label, double min, double max, double step, double value, Action<double> changed, Func<double, string> format, string start, string end)
        {
            var area = new Grid(); area.ColumnDefinitions.Add(new ColumnDefinition()); area.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) }); area.RowDefinitions.Add(new RowDefinition()); area.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var number = Ui.Text(format(value), 12, Brushes.White); number.HorizontalAlignment = HorizontalAlignment.Right; number.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(number, 1); area.Children.Add(number);
            var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true, Value = value, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) }; System.Windows.Automation.AutomationProperties.SetName(slider, label); slider.ValueChanged += delegate { changed(slider.Value); number.Text = format(slider.Value); }; area.Children.Add(slider);
            if (start != null) { var left = Note(start); left.FontSize = 10; Grid.SetRow(left, 1); area.Children.Add(left); var right = Note(end); right.FontSize = 10; right.HorizontalAlignment = HorizontalAlignment.Right; right.Margin = new Thickness(0, 6, 10, 8); Grid.SetRow(right, 1); area.Children.Add(right); }
            return Row(label, area);
        }
        private static Grid Toggle(string label, bool enabled, Action<bool> changed, string description)
        {
            var row = new Grid { Margin = new Thickness(0, 9, 0, 9) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            var text = new StackPanel(); text.Children.Add(Ui.Text(label, 13, Brushes.White)); if (!String.IsNullOrEmpty(description)) text.Children.Add(Note(description)); row.Children.Add(text);
            var toggle = new CheckBox { IsChecked = enabled, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Width = 42, Height = 22, ToolTip = label }; System.Windows.Automation.AutomationProperties.SetName(toggle, label);
            var template = new ControlTemplate(typeof(CheckBox)); var track = new FrameworkElementFactory(typeof(Border)); track.Name = "track"; track.SetValue(Border.CornerRadiusProperty, new CornerRadius(11)); track.SetValue(Border.BackgroundProperty, Ui.Brush("#555555"));
            var dot = new FrameworkElementFactory(typeof(Border)); dot.Name = "dot"; dot.SetValue(Border.BackgroundProperty, Brushes.White); dot.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); dot.SetValue(FrameworkElement.WidthProperty, 16.0); dot.SetValue(FrameworkElement.HeightProperty, 16.0); dot.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left); dot.SetValue(FrameworkElement.MarginProperty, new Thickness(3)); track.AppendChild(dot); template.VisualTree = track;
            var on = new Trigger { Property = CheckBox.IsCheckedProperty, Value = true }; on.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Brush("#007ACC"), "track")); on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "dot")); template.Triggers.Add(on);
            var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true }; focus.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.White, "track")); focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1), "track")); template.Triggers.Add(focus); toggle.Template = template;
            toggle.Checked += delegate { changed(true); }; toggle.Unchecked += delegate { changed(false); }; Grid.SetColumn(toggle, 1); row.Children.Add(toggle); return row;
        }
    }
}
