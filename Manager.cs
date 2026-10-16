using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace FolderBloom
{
    internal sealed class ManagerWindow : Window
    {
        private readonly Controller controller;
        private readonly StackPanel list;
        internal ManagerWindow(Controller controller)
        {
            this.controller = controller; Ui.SetupWindow(this, "FolderBloom · Your desktop, in color", 930, 690); MinWidth = 740; MinHeight = 510;
            var root = new Grid { Background = Ui.Back, Margin = new Thickness(30, 26, 30, 22) }; Content = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new Grid { Margin = new Thickness(0, 0, 0, 24) }; root.Children.Add(header);
            var brand = new StackPanel(); header.Children.Add(brand);
            brand.Children.Add(Ui.Text("FOLDERBLOOM", 12, Ui.Accent));
            var title = Ui.Text("A little room for everything.", 29, Ui.TextColor); title.Margin = new Thickness(0, 9, 0, 6); title.FontWeight = FontWeights.SemiBold; brand.Children.Add(title);
            brand.Children.Add(Ui.Text("Your Windows folders. A color of their own. Right where you want them.", 14, Ui.Muted));
            var headerActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top }; header.Children.Add(headerActions);
            headerActions.Children.Add(Ui.Button("Settings", controller.ShowSettings, false)); headerActions.Children.Add(Ui.Button("+  Add folder", controller.ChooseFolder, true));
            var hint = new StackPanel(); hint.Children.Add(Ui.Text("Make the desktop yours", 16, Ui.TextColor));
            var steps = Ui.Text("1  Choose a folder     ·     2  Pick a color     ·     3  Drag its panel anywhere", 13, Ui.Muted); steps.Margin = new Thickness(0, 8, 0, 0); hint.Children.Add(steps);
            var hintBox = Ui.Box(hint, 18); hintBox.Margin = new Thickness(0, 0, 0, 22); Grid.SetRow(hintBox, 1); root.Children.Add(hintBox);
            list = new StackPanel(); var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(scroll, 2); root.Children.Add(scroll);
            var footer = new Grid { Margin = new Thickness(0, 18, 0, 0) }; Grid.SetRow(footer, 3); root.Children.Add(footer);
            var note = Ui.Text("Live folder views · Saved colors & positions\nClosing this window keeps your folders on the desktop.", 12, Ui.Muted); note.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(note);
            var done = Ui.Button("Back to desktop", () => Hide(), false); done.HorizontalAlignment = HorizontalAlignment.Right; footer.Children.Add(done);
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!controller.Quitting) { e.Cancel = true; Hide(); } };
            Refresh();
        }
        internal void Refresh()
        {
            list.Children.Clear();
            var label = Ui.Text("YOUR FOLDERS  ·  " + controller.Settings.Folders.Count, 11, Ui.Muted); label.Margin = new Thickness(0, 0, 0, 12); list.Children.Add(label);
            if (controller.Settings.Folders.Count == 0)
            {
                var empty = new StackPanel { Margin = new Thickness(20, 25, 20, 28), HorizontalAlignment = HorizontalAlignment.Center };
                empty.Children.Add(Ui.FolderImage("default", 88));
                var t = Ui.Text("Start with a folder you use every day", 19, Ui.TextColor); t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, 14, 0, 7); empty.Children.Add(t);
                empty.Children.Add(Ui.Text("Documents, a project, or your downloads—all within reach.", 14, Ui.Muted)); list.Children.Add(Ui.Box(empty, 12)); return;
            }
            foreach (var f in controller.Settings.Folders)
            {
                var folder = f; var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var icon = Ui.FolderImage(folder.Color, 62); row.Children.Add(icon);
                var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 12, 0) }; Grid.SetColumn(details, 1); row.Children.Add(details);
                details.Children.Add(Ui.Text(folder.Name, 18, Ui.TextColor));
                var path = Ui.Text(folder.Path, 12, Ui.Muted); path.TextWrapping = TextWrapping.NoWrap; path.TextTrimming = TextTrimming.CharacterEllipsis; path.ToolTip = folder.Path; path.Margin = new Thickness(0, 4, 0, 6); details.Children.Add(path);
                details.Children.Add(Ui.Text((controller.HoverEnabled(folder) ? "Hover or click to expand" : "Click to expand") + "  ·  Drag panel to position", 11, Ui.Accent));
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(buttons, 2); row.Children.Add(buttons);
                buttons.Children.Add(Ui.Button("Open", delegate { FolderWidget w; if (controller.Widgets.TryGetValue(folder.Id, out w)) { Hide(); w.Expand(); } }, false));
                buttons.Children.Add(Ui.Button("Customize", () => controller.Edit(folder), false));
                var more = Ui.Button("···", delegate { }, false); var menu = new ContextMenu();
                menu.Items.Add(Ui.Menu("Open in Explorer", () => controller.OpenPath(folder.Path)));
                menu.Items.Add(Ui.Menu("Rename folder…", () => { Hide(); controller.PromptRename(folder); }));
                menu.Items.Add(Ui.Menu("Bring icon here", delegate { FolderWidget w; if (controller.Widgets.TryGetValue(folder.Id, out w)) w.ResetLocation(); }));
                menu.Items.Add(new Separator()); menu.Items.Add(Ui.Menu("Remove desktop view (keeps your files)", () => controller.Remove(folder)));
                more.ContextMenu = menu; more.Click += delegate { menu.PlacementTarget = more; menu.IsOpen = true; }; buttons.Children.Add(more);
                var box = Ui.Box(row, 14); box.Margin = new Thickness(0, 0, 0, 10); list.Children.Add(box);
            }
        }
    }
    internal sealed class FolderNameDialog : Window
    {
        internal FolderNameDialog(string title, string name, string description, Action<string> save)
        {
            Ui.SetupWindow(this, title, 450, 220); ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var body = new StackPanel { Margin = new Thickness(24) }; Content = body;
            body.Children.Add(Ui.Text(description, 13, Ui.Muted)); var input = new TextBox { Text = name, MaxLength = 255, Margin = new Thickness(0, 16, 0, 12) }; body.Children.Add(input);
            System.Windows.Automation.AutomationProperties.SetName(input, "Folder name");
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; body.Children.Add(buttons);
            var cancel = Ui.Button("Cancel", () => DialogResult = false, false); cancel.IsCancel = true; buttons.Children.Add(cancel);
            var confirm = Ui.Button(title.StartsWith("New") ? "Create folder" : "Rename", delegate { try { save(input.Text); DialogResult = true; } catch (Exception ex) { Ui.Error(this, ex.Message); input.Focus(); input.SelectAll(); } }, true); confirm.IsDefault = true; buttons.Children.Add(confirm);
            Loaded += delegate { Activate(); input.Focus(); input.SelectAll(); };
        }
    }
    internal sealed class FolderEditor : Window
    {
        internal static readonly string[] Colors = { "default", "#E58D73", "#E7B867", "#9AC477", "#67B99C", "#73ACE3", "#AA92DC", "#D88EBA" };
        private string selected;
        private readonly Image preview;
        private readonly TextBox hex;
        private readonly StackPanel swatches;
        internal FolderEditor(FolderSettings folder, Window owner, bool adding, AppPreferences preferences = null)
        {
            bool inherited = !folder.UseCustomBehavior && preferences != null;
            Ui.SetupWindow(this, adding ? "Add a desktop folder" : "Customize folder", 560, Math.Min(830, SystemParameters.WorkArea.Height - 40)); Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResize; MinWidth = 510; MinHeight = 500;
            selected = folder.Color;
            var root = new StackPanel { Background = Ui.Back, Margin = new Thickness(28) }; Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var h = Ui.Text(adding ? "Meet your new desktop folder." : "Give this folder its own look.", 23, Ui.TextColor); root.Children.Add(h);
            var path = Ui.Text(folder.Path, 12, Ui.Muted); path.Margin = new Thickness(0, 9, 0, 16); path.MaxHeight = 36; root.Children.Add(path);
            preview = Ui.FolderImage(selected, 86); preview.HorizontalAlignment = HorizontalAlignment.Left; root.Children.Add(preview);
            var nameLabel = Ui.Text("Desktop label", 12, Ui.Muted); nameLabel.Margin = new Thickness(0, 14, 0, 5); root.Children.Add(nameLabel);
            var name = new TextBox { Text = folder.Name, MaxLength = 80 }; root.Children.Add(name);
            var colorLabel = Ui.Text("Folder color", 12, Ui.Muted); colorLabel.Margin = new Thickness(0, 17, 0, 8); root.Children.Add(colorLabel);
            swatches = new StackPanel { Orientation = Orientation.Horizontal }; root.Children.Add(swatches);
            var custom = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 12) }; root.Children.Add(custom);
            hex = new TextBox { Text = selected == "default" ? "" : selected, Width = 128, MaxLength = 7, ToolTip = "Custom color, for example #73ACE3", Margin = new Thickness(0, 0, 8, 0) }; custom.Children.Add(hex);
            custom.Children.Add(Ui.Button("Apply hex", delegate { var value = hex.Text.Trim(); if (value.Length == 7 && value[0] == '#') { try { ColorConverter.ConvertFromString(value); Choose(value); return; } catch { } } Ui.Error(this, "Enter a color as #RRGGBB, for example #73ACE3."); }, false));
            custom.Children.Add(Ui.Button("Color picker…", delegate
            {
                using (var dialog = new Forms.ColorDialog { FullOpen = true }) { if (dialog.ShowDialog() == Forms.DialogResult.OK) Choose("#" + dialog.Color.R.ToString("X2") + dialog.Color.G.ToString("X2") + dialog.Color.B.ToString("X2")); }
            }, false));
            var useApp = new CheckBox { Content = "Use app opening and closing settings", IsChecked = !folder.UseCustomBehavior, Foreground = Ui.TextColor, Margin = new Thickness(0, 5, 0, 12) }; root.Children.Add(useApp);
            var hover = new CheckBox { Content = "Also expand when I hover over the icon", IsChecked = inherited ? preferences.OpenOnHover : folder.OpenOnHover, Foreground = Ui.TextColor, Margin = new Thickness(0, 5, 0, 12), IsEnabled = folder.UseCustomBehavior }; root.Children.Add(hover);
            var styleLabel = Ui.Text("Expansion style", 12, Ui.Muted); styleLabel.Margin = new Thickness(0, 4, 0, 7); root.Children.Add(styleLabel);
            var layout = new ComboBox { ItemsSource = ExpansionLayout.Styles, SelectedItem = folder.ExpandStyle ?? "Grid", Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 12) }; root.Children.Add(layout);
            var directionRow = new StackPanel { Margin = new Thickness(0, 0, 0, 12), Visibility = folder.ExpandStyle == "Fan" ? Visibility.Visible : Visibility.Collapsed }; root.Children.Add(directionRow);
            var directionLabel = Ui.Text("Fan direction", 12, Ui.Muted); directionLabel.Margin = new Thickness(0, 0, 0, 7); directionRow.Children.Add(directionLabel);
            var fanDirection = new ComboBox { ItemsSource = ExpansionLayout.FanDirections, SelectedItem = folder.FanDirection ?? "Auto", Padding = new Thickness(10, 6, 10, 6) }; directionRow.Children.Add(fanDirection);
            var directionHelp = Ui.Text("Choose which side of the folder the fan opens toward. Auto switches above or below to fit the screen.", 11, Ui.Muted); directionHelp.Margin = new Thickness(0, 6, 0, 0); directionRow.Children.Add(directionHelp);
            layout.SelectionChanged += delegate { directionRow.Visibility = (string)layout.SelectedItem == "Fan" ? Visibility.Visible : Visibility.Collapsed; };
            var animate = new CheckBox { Content = "Animate opening and closing", IsChecked = folder.Animate, Foreground = Ui.TextColor, Margin = new Thickness(0, 0, 0, 12) }; root.Children.Add(animate);
            var leave = new CheckBox { Content = "Close when the cursor leaves the folder and its panel", IsChecked = inherited ? preferences.CloseOnMouseLeave : folder.CloseOnMouseLeave, Foreground = Ui.TextColor, Margin = new Thickness(0, 0, 0, 10) }; root.Children.Add(leave);
            var delayRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 12) }; root.Children.Add(delayRow);
            var delayLabel = Ui.Text("Time to cross to the panel", 12, Ui.Muted); delayLabel.VerticalAlignment = VerticalAlignment.Center; delayLabel.Margin = new Thickness(0, 0, 10, 0); delayRow.Children.Add(delayLabel);
            int[] delayValues = { 150, 250, 350, 650, 1000, 1500, 3000 }; int selectedDelay = Array.FindIndex(delayValues, value => value >= (inherited ? preferences.CloseDelay : folder.MouseLeaveDelay));
            var delay = new ComboBox { ItemsSource = new[] { "150 ms", "250 ms", "350 ms", "650 ms", "1 second", "1.5 seconds", "3 seconds" }, SelectedIndex = Math.Max(0, selectedDelay), Padding = new Thickness(8, 4, 8, 4), Width = 120 }; delayRow.Children.Add(delay);
            leave.IsEnabled = delay.IsEnabled = folder.UseCustomBehavior;
            useApp.Checked += delegate { hover.IsEnabled = leave.IsEnabled = delay.IsEnabled = false; };
            useApp.Unchecked += delegate { hover.IsEnabled = leave.IsEnabled = delay.IsEnabled = true; };
            bool canReplace = DesktopReplacement.IsDesktopFolder(folder.Path);
            var replace = new CheckBox { Content = "Replace this folder's original Desktop icon", IsChecked = folder.ReplaceDesktopIcon, IsEnabled = canReplace, Foreground = Ui.TextColor, Margin = new Thickness(0, 4, 0, 7) }; root.Children.Add(replace);
            var replaceNote = Ui.Text(canReplace ? "Keeps the same folder and files. The original is hidden in Explorer while FolderBloom runs and restored on quit or removal. If Show hidden items is enabled, Windows may still show it faintly." : "Available for folders directly on your Desktop. This folder can be added as a live expanding view.", 11, Ui.Muted); replaceNote.Margin = new Thickness(20, 0, 0, 14); root.Children.Add(replaceNote);
            root.Children.Add(Ui.Text("Drag an empty part of the popup, or hold Alt and drag, to place it anywhere. Right-click for style, size, position and keep-open options.\nIts position is remembered relative to the folder icon.", 12, Ui.Muted));
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) }; root.Children.Add(actions);
            var cancel = Ui.Button("Cancel", () => { DialogResult = false; }, false); cancel.IsCancel = true; actions.Children.Add(cancel);
            var save = Ui.Button(adding ? "Add folder" : "Save changes", delegate
            {
                folder.Name = String.IsNullOrWhiteSpace(name.Text) ? new DirectoryInfo(folder.Path).Name : name.Text.Trim(); folder.Color = selected; folder.OpenOnHover = hover.IsChecked == true;
                folder.ExpandStyle = (string)layout.SelectedItem ?? "Grid"; folder.CloseOnMouseLeave = leave.IsChecked == true; folder.Animate = animate.IsChecked == true;
                folder.FanDirection = (string)fanDirection.SelectedItem ?? "Auto";
                folder.UseCustomBehavior = useApp.IsChecked != true;
                folder.MouseLeaveDelay = delayValues[Math.Max(0, delay.SelectedIndex)]; folder.ReplaceDesktopIcon = canReplace && replace.IsChecked == true; DialogResult = true;
            }, true); save.IsDefault = true; actions.Children.Add(save);
            DrawSwatches();
        }
        private void Choose(string value) { selected = value; preview.Source = Icons.Folder(value); hex.Text = value == "default" ? "" : value; DrawSwatches(); }
        private void DrawSwatches()
        {
            swatches.Children.Clear();
            foreach (var c in Colors)
            {
                var color = c; var b = Ui.Button("", () => Choose(color), false); b.Width = 43; b.Height = 40; b.Padding = new Thickness(3); b.Margin = new Thickness(0, 0, 8, 0); b.Content = Ui.FolderImage(color, 29); b.ToolTip = color == "default" ? "Windows default" : color; b.BorderBrush = selected == color ? Ui.Accent : Ui.BorderColor; b.BorderThickness = new Thickness(selected == color ? 2 : 1); swatches.Children.Add(b);
            }
        }
    }
}
