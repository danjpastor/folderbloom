using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FolderBloom
{
    internal static class Tests
    {
        internal static int LayoutProbe(string output, double? rasterScale = null)
        {
            Directory.CreateDirectory(output); var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; Ui.InstallStyles(app);
            double renderScale = rasterScale.HasValue ? rasterScale.Value : Native.Scale;
            string[] names = { "Games", "Quarterly Project Materials", "Very Long Folder Name That Needs More Than Two Lines" };
            double[] pitches = { 76, 96, 120, 160 }, sizes = { 1, 1.25, 1.5, 2 };
            var board = new System.Windows.Controls.Canvas { Width = 720, Background = new SolidColorBrush(Color.FromRgb(20, 25, 34)) };
            var log = new List<string> { "pitchDip,fontScale,fontDip,actualScale,candidateWindowWidthDip,candidateWindowHeightDip,previewX,previewY,previewWidth,labelX,labelY,labelWidth,labelViewportHeight" }; int exit = 0; double y = 8;
            try
            {
                string fixture = Path.Combine(output, "layout-folder"); Directory.CreateDirectory(fixture);
                var controller = new Controller(app, Path.Combine(output, "layout-settings"), true);
                var legacyBoard = new System.Windows.Controls.Canvas { Width = 720, Height = 224, Background = new SolidColorBrush(Color.FromRgb(20, 25, 34)) };
                for (int col = 0; col < pitches.Length; col++)
                {
                    double width = pitches[col], height = new[] { 100.0, 124, 156, 196 }[col]; var stack = new System.Windows.Controls.StackPanel();
                    var icon = Ui.FolderImage("#79B9A5", Native.DesktopIconSize); stack.Children.Add(icon);
                    var label = Ui.Text("Quarterly Project Materials", 12, Brushes.White); label.TextAlignment = TextAlignment.Center; label.MaxHeight = 34; label.Margin = new Thickness(0, 3, 0, 0); stack.Children.Add(label);
                      var oldBorder = new System.Windows.Controls.Border { Width = width, Height = height, Padding = new Thickness(3, 1, 3, 3), BorderThickness = new Thickness(1), BorderBrush = Ui.Brush("#445B83A0"), Child = stack };
                      oldBorder.Measure(new Size(width, height)); oldBorder.Arrange(new Rect(0, 0, width, height)); oldBorder.UpdateLayout();
                    System.Windows.Controls.Canvas.SetLeft(oldBorder, 8 + col * 176); System.Windows.Controls.Canvas.SetTop(oldBorder, 8); legacyBoard.Children.Add(oldBorder);
                }
                RenderAtDpi(legacyBoard, (int)legacyBoard.Width, (int)legacyBoard.Height, Path.Combine(output, "collapsed-layout-legacy-reconstruction.png"), renderScale);
                for (int row = 0; row < sizes.Length; row++)
                {
                    double font = Math.Max(9, SystemFonts.IconFontSize * sizes[row]); double rowHeight = 0;
                    for (int col = 0; col < pitches.Length; col++)
                    {
                        double width = pitches[col]; var settings = new FolderSettings { Path = fixture, Name = names[(row + col) % names.Length], X = 0, Y = 0 };
                        var widget = new FolderWidget(controller, settings); var label = widget.CollapsedLabel; label.FontSize = font; label.FontFamily = SystemFonts.IconFontFamily;
                        widget.Preview.Width = widget.Preview.Height = Native.DesktopIconSize; widget.UpdateCollapsedFootprint(width);
                          var cell = (System.Windows.Controls.Border)widget.Content; cell.Measure(new Size(width, widget.Height)); cell.Arrange(new Rect(0, 0, width, widget.Height)); cell.UpdateLayout();
                        var previewPoint = widget.Preview.TranslatePoint(new Point(), cell); var viewport = widget.CollapsedLabelViewport; var labelPoint = viewport.TranslatePoint(new Point(), cell);
                        double expectedX = (width - Native.DesktopIconSize) / 2;
                        double twoLineLimit = FolderWidget.TwoLineLabelHeight(label);
                          if (Math.Abs(previewPoint.X - expectedX) > 0.1 || Math.Abs(previewPoint.Y - 2 / Native.Scale) > 0.1 || viewport.ActualHeight > twoLineLimit + 0.1 || labelPoint.Y + viewport.ActualHeight > widget.Height - 2.9) throw new InvalidOperationException("Actual widget layout failed at pitch " + width + " and font scale " + sizes[row] + ": preview=" + previewPoint + ", label=" + labelPoint + ", viewportHeight=" + viewport.ActualHeight + ", windowHeight=" + widget.Height + ", two-line limit=" + twoLineLimit + ".");
                        var naturalProbe = Ui.Text(label.Text, font, Brushes.White); naturalProbe.FontFamily = label.FontFamily; naturalProbe.Width = width; naturalProbe.Measure(new Size(width, Double.PositiveInfinity)); double naturalHeight = naturalProbe.DesiredSize.Height;
                        var baseline = Ui.Text("M", font, Brushes.White); baseline.FontFamily = label.FontFamily; baseline.Measure(new Size(width, Double.PositiveInfinity)); int estimatedLines = Math.Max(1, (int)Math.Round((naturalHeight - label.Margin.Top) / Math.Max(1, baseline.DesiredSize.Height)));
                        log.Add(width + "," + sizes[row] + "," + font.ToString("0.##") + "," + Native.Scale.ToString("0.##") + "," + widget.Width.ToString("0.##") + "," + widget.Height.ToString("0.##") + "," + previewPoint.X.ToString("0.##") + "," + previewPoint.Y.ToString("0.##") + "," + widget.Preview.Width.ToString("0.##") + "," + labelPoint.X.ToString("0.##") + "," + labelPoint.Y.ToString("0.##") + "," + label.Width.ToString("0.##") + "," + viewport.ActualHeight.ToString("0.##") + " (natural label lines approx " + estimatedLines + ")");
                        RenderAtDpi(cell, (int)Math.Ceiling(width), (int)Math.Ceiling(widget.Height), Path.Combine(output, "widget-p" + width + "-f" + sizes[row] + (renderScale == Native.Scale ? "" : "-" + (int)(96 * renderScale) + "dpi") + ".png"), renderScale);
                        widget.Content = null; System.Windows.Controls.Canvas.SetLeft(cell, 8 + col * 176); System.Windows.Controls.Canvas.SetTop(cell, y); board.Children.Add(cell); rowHeight = Math.Max(rowHeight, widget.Height); widget.Dispose();
                    }
                    y += rowHeight + 10;
                }
                var descenderWidget = new FolderWidget(controller, new FolderSettings { Path = fixture, Name = "Folder label gjpqy continues onto subsequent rows and keeps going" });
                descenderWidget.UpdateCollapsedFootprint(76); var descenderCell = (System.Windows.Controls.Border)descenderWidget.Content;
                descenderCell.Measure(new Size(76, descenderWidget.Height)); descenderCell.Arrange(new Rect(0, 0, 76, descenderWidget.Height)); descenderCell.UpdateLayout();
                var naturalDescenderLabel = Ui.Text(descenderWidget.CollapsedLabel.Text, descenderWidget.CollapsedLabel.FontSize, Brushes.White); naturalDescenderLabel.FontFamily = descenderWidget.CollapsedLabel.FontFamily; naturalDescenderLabel.Width = 76; naturalDescenderLabel.TextWrapping = TextWrapping.Wrap; naturalDescenderLabel.Measure(new Size(76, Double.PositiveInfinity));
                double descenderCap = FolderWidget.TwoLineLabelHeight(descenderWidget.CollapsedLabel);
                if (naturalDescenderLabel.DesiredSize.Height <= descenderCap || Math.Abs(descenderWidget.CollapsedLabelViewport.ActualHeight - descenderCap) > 0.1 || descenderWidget.Height - descenderWidget.CollapsedLabelViewport.TranslatePoint(new Point(), descenderCell).Y - descenderWidget.CollapsedLabelViewport.ActualHeight < 2.9)
                    throw new InvalidOperationException("Long two-line descender fixture did not fit the measured clip viewport: natural=" + naturalDescenderLabel.DesiredSize.Height + ", two-line cap=" + descenderCap + ", viewport=" + descenderWidget.CollapsedLabelViewport.ActualHeight + ".");
                RenderAtDpi(descenderCell, 76, (int)Math.Ceiling(descenderWidget.Height), Path.Combine(output, "widget-p76-long-descenders-192dpi.png"), 2);
                descenderWidget.Content = null; descenderWidget.Dispose();
                foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
                {
                    var widget = new FolderWidget(controller, new FolderSettings { Path = fixture, Name = "Games", X = 0, Y = 0 });
                    widget.UpdateCollapsedFootprint(76, scale);
                    var cell = (System.Windows.Controls.Border)widget.Content; cell.Measure(new Size(76, widget.Height)); cell.Arrange(new Rect(0, 0, 76, widget.Height)); cell.UpdateLayout();
                    var previewPoint = widget.Preview.TranslatePoint(new Point(), cell); var labelPoint = widget.CollapsedLabelViewport.TranslatePoint(new Point(), cell);
                    double expectedHeight = Native.DesktopIconSize + 2 / scale + widget.CollapsedLabelViewport.ActualHeight + 6;
                    if (Math.Abs(previewPoint.Y * scale - 2) > 0.1 || Math.Abs(widget.Height - expectedHeight) > 0.1 || Math.Abs(widget.Height - labelPoint.Y - widget.CollapsedLabelViewport.ActualHeight - 3) > 0.1)
                        throw new InvalidOperationException("Scaled collapsed geometry failed at scale " + scale + ": previewY=" + previewPoint.Y + ", labelY=" + labelPoint.Y + ", labelHeight=" + widget.CollapsedLabelViewport.ActualHeight + ", windowHeight=" + widget.Height + ", expectedHeight=" + expectedHeight + ".");
                    log.Add("76," + scale + "," + SystemFonts.IconFontSize.ToString("0.##") + ",simulated-" + scale + ",76," + widget.Height.ToString("0.##") + "," + widget.Preview.TranslatePoint(new Point(), cell).X.ToString("0.##") + "," + previewPoint.Y.ToString("0.##") + "," + widget.Preview.Width.ToString("0.##") + "," + labelPoint.X.ToString("0.##") + "," + labelPoint.Y.ToString("0.##") + "," + widget.CollapsedLabel.Width.ToString("0.##") + "," + widget.CollapsedLabelViewport.ActualHeight.ToString("0.##") + " (simulated scale; expected physical inset 2px)");
                    RenderAtDpi(cell, 76, (int)Math.Ceiling(widget.Height), Path.Combine(output, "widget-p76-Games-scale-" + scale + "-" + (int)(96 * scale) + "dpi.png"), scale);
                    widget.Content = null; widget.Dispose();
                }
                board.Height = y + 8; RenderAtDpi(board, (int)board.Width, (int)Math.Ceiling(board.Height), Path.Combine(output, "collapsed-layout.png"), renderScale);
                log.Add("PASS: Rendered 16 actual FolderWidget collapsed surfaces at RTB " + (96 * renderScale) + " DPI across four pitches and four label font scales; additionally rendered four simulated native-scale geometries with a 2-physical-pixel image inset. Simulated geometry fixtures do not modify or emulate Windows desktop scaling.");
            }
            catch (Exception ex) { log.Add(ex.ToString()); exit = 1; }
            File.WriteAllLines(Path.Combine(output, "collapsed-layout-results.csv"), log); return exit;
        }
        internal static int DesktopPlacementProbe(string output)
        {
            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ".folderbloom-grid-check-" + Guid.NewGuid().ToString("N") + ".lnk");
            var log = new List<string>(); int exit = 0;
            try
            {
                var before = ShellWorker.Run(DesktopArrangement.Read).GetAwaiter().GetResult();
                if (before.AutoArrange) throw new InvalidOperationException("This isolated placement check requires Auto arrange to be off; it was left unchanged.");
                var bounds = before.Regions.Last().Bounds;
                var target = DesktopGrid.Nearest(new Point(bounds.Right - before.Spacing.Width * 2, bounds.Bottom - before.Spacing.Height * 2), before.Spacing, before.Spacing, before.Regions, before.Items.Select(i => new Rect(i.Position, before.Spacing)).ToList());
                if (!target.HasValue) throw new InvalidOperationException("No free space for the temporary test shortcut.");
                ShellWorker.Run(() => { ShortcutDrop.CreateLink(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), shortcut); return true; }).GetAwaiter().GetResult();
                DesktopLayout layout = null;
                for (int i = 0; i < 40; i++) { layout = ShellWorker.Run(DesktopArrangement.Read).GetAwaiter().GetResult(); if (layout.Items.Any(item => String.Equals(item.Path, shortcut, StringComparison.OrdinalIgnoreCase))) break; System.Threading.Thread.Sleep(100); }
                ShellWorker.Run(() => { DesktopArrangement.PositionTestShortcut(shortcut, target.Value); return true; }).GetAwaiter().GetResult();
                layout = ShellWorker.Run(DesktopArrangement.Read).GetAwaiter().GetResult(); var testItem = layout.Items.Single(item => String.Equals(item.Path, shortcut, StringComparison.OrdinalIgnoreCase));
                if ((testItem.Position - target.Value).Length > 1) throw new Exception("Windows icon positioning did not round-trip through the desktop grid.");
                log.Add("PASS: A temporary desktop shortcut lands exactly in the requested Windows grid cell.");
                var folders = new[] { new DesktopFolder { Id = "test", Position = target.Value, Size = layout.Spacing } };
                var expected = DesktopGrid.Plan(layout, folders, true, true);
                if (expected.IconMoves.Count != 1 || !expected.IconMoves.ContainsKey(testItem.Index)) throw new Exception("The isolated check would affect an unrelated desktop item.");
                int checks = 0;
                var cancelled = ShellWorker.Run(() => DesktopArrangement.Arrange(folders, true, true, () => ++checks == 1)).GetAwaiter().GetResult();
                var unchanged = ShellWorker.Run(DesktopArrangement.Read).GetAwaiter().GetResult().Items.Single(item => String.Equals(item.Path, shortcut, StringComparison.OrdinalIgnoreCase));
                if (cancelled != null || (unchanged.Position - target.Value).Length > 1) throw new Exception("A stale placement request moved an icon.");
                log.Add("PASS: A placement request invalidated during a drag or preference change does not move the shortcut.");
                var plan = ShellWorker.Run(() => DesktopArrangement.Arrange(folders, true, true, () => true)).GetAwaiter().GetResult();
                if (plan == null) throw new Exception("The placement check paused because a mouse button is held.");
                var after = ShellWorker.Run(DesktopArrangement.Read).GetAwaiter().GetResult();
                testItem = after.Items.Single(item => String.Equals(item.Path, shortcut, StringComparison.OrdinalIgnoreCase));
                if (DesktopGrid.Overlaps(new Rect(testItem.Position, after.Spacing), new Rect(target.Value, after.Spacing))) throw new Exception("The temporary shortcut still overlaps its reserved folder cell.");
                if ((testItem.Position - expected.IconMoves.Single().Value).Length > 1) throw new Exception("The shortcut did not reach the nearest free grid cell.");
                log.Add("PASS: The Windows Shell moves the overlapping test shortcut to the nearest free cell and keeps the folder cell fixed.");
                if (before.AutoArrange != after.AutoArrange || before.Items.Any(old => !after.Items.Any(item => String.Equals(item.Path, old.Path, StringComparison.OrdinalIgnoreCase) && (item.Position - old.Position).Length < 1))) throw new Exception("An unrelated desktop item or Windows preference changed during the check.");
                log.Add("PASS: Existing desktop icons and Windows Auto arrange settings remain unchanged.");
                var repeat = ShellWorker.Run(() => DesktopArrangement.Arrange(folders, true, true, () => true)).GetAwaiter().GetResult();
                if (repeat == null || repeat.IconMoves.Count != 0) throw new Exception("A repeated placement pass was not stable.");
                log.Add("PASS: Rechecking the desktop does not move anything again.");
            }
            catch (Exception ex) { log.Add(ex.ToString()); exit = 1; }
            finally
            {
                try { if (File.Exists(shortcut)) File.Delete(shortcut); log.Add("Temporary test shortcut removed."); }
                catch (Exception ex) { log.Add("Could not remove temporary test shortcut: " + ex.Message); exit = 1; }
                File.WriteAllLines(output, log);
            }
            return exit;
        }
        internal static int DesktopProbe(string output)
        {
            try
            {
                var layout = ShellWorker.Run(DesktopArrangement.Read).GetAwaiter().GetResult();
                File.WriteAllText(output, new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new { SpacingX = layout.Spacing.Width, SpacingY = layout.Spacing.Height, AutoArrange = layout.AutoArrange,
                    Regions = layout.Regions.Select(r => new { X = r.Bounds.X, Y = r.Bounds.Y, Width = r.Bounds.Width, Height = r.Bounds.Height, OriginX = r.Origin.X, OriginY = r.Origin.Y }).ToArray(),
                    Items = layout.Items.Select(i => new { X = i.Position.X, Y = i.Position.Y, HasFilePath = i.Path != null }).ToArray() })); return 0;
            }
            catch (Exception ex) { File.WriteAllText(output, ex.ToString()); return 1; }
        }
        internal static int RecoveryProbe(string root)
        {
            Directory.CreateDirectory(root); string desktop = Path.Combine(root, "Desktop"); Directory.CreateDirectory(desktop); string folder = Path.Combine(desktop, "Restore after exit"); Directory.CreateDirectory(folder);
            using (var replacement = new DesktopReplacement(Path.Combine(root, "Settings"), desktop))
            {
                replacement.RecoverPrevious(); replacement.EnsureGuardian(true); replacement.Apply(new FolderSettings { Path = folder, ReplaceDesktopIcon = true });
                File.WriteAllText(Path.Combine(root, "probe-ready.txt"), folder);
                // Exit without cleanup to exercise recovery after an interrupted app session.
            }
            return 0;
        }
        private static readonly List<string> results = new List<string>();
        private static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); results.Add("PASS: " + label); }
        internal static int Run(string output)
        {
            Directory.CreateDirectory(output); var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; Ui.InstallStyles(app); int exit = 0;
            app.Dispatcher.BeginInvoke((Action)(async () =>
            {
                try { await Execute(app, output); }
                catch (Exception ex) { results.Add(ex.ToString()); exit = 1; }
                finally { File.WriteAllLines(Path.Combine(output, "test-results.txt"), results); app.Shutdown(); }
            }));
            app.Run(); return exit;
        }
        private static byte[] Pixels(BitmapSource source)
        {
            var b = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); byte[] pixels = new byte[b.PixelWidth * b.PixelHeight * 4]; b.CopyPixels(pixels, b.PixelWidth * 4, 0); return pixels;
        }
        private static BitmapSource Render(FrameworkElement view, int width, int height, string path)
        {
            view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
            var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(view);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using (var stream = File.Create(path)) png.Save(stream); return image;
        }
        private static BitmapSource RenderAtDpi(FrameworkElement view, int logicalWidth, int logicalHeight, string path, double scale)
        {
            view.Measure(new Size(logicalWidth, logicalHeight)); view.Arrange(new Rect(0, 0, logicalWidth, logicalHeight)); view.UpdateLayout();
            int pixelWidth = (int)Math.Ceiling(logicalWidth * scale), pixelHeight = (int)Math.Ceiling(logicalHeight * scale);
            var image = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * scale, 96 * scale, PixelFormats.Pbgra32); image.Render(view);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using (var stream = File.Create(path)) png.Save(stream); return image;
        }
        private static async Task Until(Func<bool> predicate)
        {
            for (int i = 0; i < 400; i++) { if (predicate()) return; await Task.Delay(20); }
            throw new Exception("Timed out waiting for a live folder refresh.");
        }
        private static async Task ExplorerChecks(Application app, string root, string output)
        {
            Check(ShellRequest.Parse(new[] { "--new-desktop-folder" }).Path == Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) && ShellRequest.Parse(new[] { "--new-folder", root }).Action == "new" && ShellRequest.Parse(new[] { "--add-folder", root }).Action == "add", "Explorer and Desktop commands resolve the requested folder and action");
            Check(!new ShellRequest { Action = "delete", Path = root }.Valid && !new ShellRequest { Action = "new", Path = "relative" }.Valid, "Explorer command receiver rejects unknown actions and relative paths");
            Check(ExplorerIntegration.Command(@"C:\My Apps\FolderBloom.exe", "--new-folder", "%V") == "\"C:\\My Apps\\FolderBloom.exe\" --new-folder \"%V\"", "Explorer menu command quotes the application and selected directory");
            string pipeName = "FolderBloom.Test." + Guid.NewGuid().ToString("N"); var requests = new List<ShellRequest>();
            using (var server = new ShellCommandServer(pipeName, request => { lock (requests) requests.Add(request); }))
            {
                Check(await Task.Run(() => ShellCommandServer.Send(pipeName, new ShellRequest { Action = "new", Path = root })), "A second process command reaches the existing app through its same-user pipe");
                Check(await Task.Run(() => ShellCommandServer.Send(pipeName, new ShellRequest { Action = "add", Path = root })) && requests.Count == 2 && requests[0].Action == "new" && requests[1].Action == "add", "The running app accepts repeated Explorer commands without a duplicate session");
            }
            var controller = new Controller(app, Path.Combine(root, "Explorer settings"), true);
            string parent = Path.Combine(root, "Explorer parent"); Directory.CreateDirectory(parent);
            controller.CreateFolder(parent, "New & useful"); var folder = controller.Settings.Folders.Single();
            Check(Directory.Exists(folder.Path) && controller.Store.Load().Folders.Single().Path == folder.Path, "Creating a FolderBloom folder creates a real folder at the selected location and saves its view");
            File.WriteAllText(Path.Combine(folder.Path, "keep.txt"), "preserve contents");
            bool rejected = false; try { controller.CreateFolder(parent, "New & useful"); } catch (IOException) { rejected = true; }
            Check(rejected && controller.Settings.Folders.Count == 1 && File.ReadAllText(Path.Combine(folder.Path, "keep.txt")) == "preserve contents", "Creating a duplicate folder never adopts or overwrites the existing folder");
            foreach (string invalid in new[] { "", "..", "../outside", "bad:name", "trailing.", "trailing ", "CON", "com1.txt" })
            { rejected = false; try { FolderNames.Validate(invalid); } catch (IOException) { rejected = true; } Check(rejected, "Folder names reject invalid or reserved value: " + invalid); }
            string childPath = Path.Combine(folder.Path, "Child"); Directory.CreateDirectory(childPath); var child = new FolderSettings { Path = childPath, Name = "My child label" }; controller.Add(child);
            string oldPath = folder.Path; controller.RenameFolder(folder, "Renamed & useful");
            Check(!Directory.Exists(oldPath) && File.ReadAllText(Path.Combine(folder.Path, "keep.txt")) == "preserve contents" && folder.Name == "Renamed & useful" && controller.Store.Load().Folders[0].Path == folder.Path, "Rename changes the real folder and saved view together while preserving all contents");
            Check(child.Path == Path.Combine(folder.Path, "Child") && child.Name == "My child label", "Renaming a parent also updates tracked subfolder paths while preserving their custom labels");
            controller.RenameFolder(folder, "renamed & useful"); Check(new DirectoryInfo(folder.Path).Name == "renamed & useful" && Directory.Exists(folder.Path), "Renaming only letter casing works without losing the folder");
            string stablePath = folder.Path; Directory.CreateDirectory(Path.Combine(parent, "Occupied"));
            rejected = false; try { controller.RenameFolder(folder, "Occupied"); } catch (IOException) { rejected = true; }
            Check(rejected && folder.Path == stablePath && Directory.Exists(stablePath), "Rename rejects an existing destination without changing either folder");
            using (var lockedSettings = new FileStream(controller.Store.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            { rejected = false; try { controller.RenameFolder(folder, "Cannot save"); } catch (IOException) { rejected = true; } }
            Check(rejected && folder.Path == stablePath && Directory.Exists(stablePath) && !Directory.Exists(Path.Combine(parent, "Cannot save")), "A failed settings save rolls back the rename and restores the original path");
            var widget = new FolderWidget(controller, folder) { Opacity = 0 }; controller.Widgets.Add(folder.Id, widget);
            try
            {
                widget.Show(); await Until(() => widget.Preview.EntryCount == 2);
                widget.BeginRename(); Check(widget.IsRenaming && widget.IsBusy, "Inline rename pauses hover opening and desktop arrangement");
                widget.FinishRename(false); Check(!widget.IsRenaming && folder.Path == stablePath, "Esc or cancel leaves the folder name and path unchanged");
                widget.BeginRename(); var grid = (System.Windows.Controls.Grid)((System.Windows.Controls.Border)widget.Content).Child; var box = grid.Children.OfType<System.Windows.Controls.StackPanel>().Single().Children.OfType<System.Windows.Controls.TextBox>().Single(); box.Text = "Inline renamed"; widget.FinishRename(true);
                await Until(() => widget.Preview.DisplayedPaths.All(path => path.StartsWith(folder.Path + "\\", StringComparison.OrdinalIgnoreCase)) && widget.Preview.EntryCount == 2 && widget.Panel.CurrentPath == folder.Path);
                Check(!widget.IsRenaming && folder.Name == "Inline renamed" && File.Exists(Path.Combine(folder.Path, "keep.txt")) && widget.Preview.IsWatching, "Inline rename commits the real name and reconnects previews, popup contents, and live watching");
            }
            finally { widget.Dispose(); controller.Widgets.Remove(folder.Id); }
            var dialog = new FolderNameDialog("New FolderBloom folder", "Games", "Create a real folder here and add its expanding desktop icon.", name => { });
            Render((FrameworkElement)dialog.Content, 440, 190, Path.Combine(output, "new-folder-dialog.png")); dialog.Close();
            controller.Quitting = true; controller.Manager.Close();
        }
        private static async Task Execute(Application app, string output)
        {
            string root = Path.Combine(output, "fixtures-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string folderPath = Path.Combine(root, "Design studio"); Directory.CreateDirectory(folderPath);
            Directory.CreateDirectory(Path.Combine(folderPath, "Brand assets")); Directory.CreateDirectory(Path.Combine(folderPath, "Client projects")); Directory.CreateDirectory(Path.Combine(folderPath, "References"));
            foreach (string file in new[] { "Project notes.txt", "Website ideas.txt", "Reading list.txt", "September plan.txt", "Welcome.txt" }) File.WriteAllText(Path.Combine(folderPath, file), "FolderBloom test fixture");
            string hidden = Path.Combine(folderPath, "hidden.txt"); File.WriteAllText(hidden, "hidden"); File.SetAttributes(hidden, FileAttributes.Hidden);
            var entries = await FolderReader.ReadAsync(folderPath);
            Check(entries.Count == 8 && entries[0].IsDirectory && !entries.Any(e => e.Name == "hidden.txt"), "Folder enumeration returns actual entries, sorts folders first, and omits hidden files");
            var f = new FolderSettings { Path = folderPath, Name = "Design studio", Color = "#73ACE3", X = 200, Y = 250, OffsetX = -140, OffsetY = 95 };
            Check(Geometry.PanelOrigin(f) == new Point(60, 345), "Arbitrary panel offset works to the left and below the folder");
            f.X += 310; f.Y -= 80; Check(Geometry.PanelOrigin(f) == new Point(370, 265), "Moving the folder retains its panel offset");
            Geometry.SetOffset(f, new Point(-100, 30)); Check(f.OffsetX == -610 && f.OffsetY == -140, "Free placement records both negative offsets");
            Check(Geometry.Clamp(new Point(-1600, -100), new Size(440, 420), new Rect(-1920, 0, 1920, 1080)) == new Point(-1600, 0), "Clamping supports monitors with negative coordinates");
            Check(Geometry.Clamp(new Point(1800, 950), new Size(440, 420), new Rect(0, 0, 1920, 1080)) == new Point(1480, 660), "Panels stay within available screen space");
            var settings = new Settings(); settings.Folders.Add(f); var store = new SettingsStore(Path.Combine(root, "settings")); store.Save(settings);
            f.PanelWidth = 603; f.OpenOnHover = true; store.Save(settings); var restored = store.Load().Folders[0];
            Check(restored.Color == f.Color && restored.OffsetX == -610 && restored.PanelWidth == 603 && restored.OpenOnHover && restored.Path == folderPath, "Color, position, size, hover preference, and folder path persist across save/load");
            Check(File.Exists(store.FilePath + ".bak"), "Atomic save keeps a previous settings backup");
            f.ExpandStyle = "Ring"; f.CloseOnMouseLeave = true; f.MouseLeaveDelay = 1000; f.ReplaceDesktopIcon = true; store.Save(settings); restored = store.Load().Folders[0];
            Check(restored.ExpandStyle == "Ring" && restored.CloseOnMouseLeave && restored.MouseLeaveDelay == 1000 && restored.ReplaceDesktopIcon, "New layout, cursor leave, and replacement options persist");
            f.ExpandStyle = "Grid"; f.ReplaceDesktopIcon = false;
            var legacyPath = Path.Combine(root, "legacy"); Directory.CreateDirectory(legacyPath); File.WriteAllText(Path.Combine(legacyPath, "settings.json"), "{\"Version\":1,\"Folders\":[{\"Name\":\"Legacy\",\"Path\":\"C:\\\\test\"}]}");
            var legacy = new SettingsStore(legacyPath).Load().Folders[0]; Check(legacy.ExpandStyle == "Grid" && legacy.MouseLeaveDelay == 250 && !legacy.ReplaceDesktopIcon && legacy.Animate && legacy.PopupScale == 1, "Old settings gain animation and scale defaults without enabling replacement or auto-close");
            string legacyJson = File.ReadAllText(Path.Combine(legacyPath, "settings.json"));
            File.WriteAllText(Path.Combine(legacyPath, "settings.json"), legacyJson.Replace("\"Name\":\"Legacy\"", "\"Name\":\"Legacy\",\"OffsetX\":-240,\"OffsetY\":75"));
            legacy = new SettingsStore(legacyPath).Load().Folders[0]; Check(legacy.CustomPanelPosition && legacy.OffsetX == -240 && legacy.OffsetY == 75, "An existing manually positioned popup keeps its relative placement after upgrading");
            f.Animate = false; f.PopupScale = 1.2; f.CustomPanelPosition = true; store.Save(settings); restored = store.Load().Folders[0];
            Check(!restored.Animate && restored.PopupScale == 1.2 && restored.CustomPanelPosition, "Animation, popup scale, and free placement preferences survive restarting");
            f.Animate = true; f.PopupScale = 1; f.CustomPanelPosition = false;
            Check(legacy.FanDirection == "Auto", "Existing folders default to automatic Fan direction after upgrading");
            f.FanDirection = "Left"; f.WasExpanded = true; store.Save(settings); restored = store.Load().Folders[0];
            Check(restored.FanDirection == "Left" && restored.WasExpanded, "Fan direction and expanded state persist across restarting"); f.FanDirection = "Auto"; f.WasExpanded = false;
            var invalidDirection = new FolderSettings { FanDirection = "invalid" }; invalidDirection.Normalize(); Check(invalidDirection.FanDirection == "Auto", "Unknown saved Fan directions safely fall back to Auto");
            foreach (string direction in ExpansionLayout.FanDirections.Where(d => d != "Auto"))
            {
                bool valid = true;
                for (int n = 1; n <= 7; n++) foreach (double scale in new[] { 0.7, 1.0, 1.5 })
                {
                    var shape = ExpansionLayout.Build("Fan", n, scale, direction);
                    valid &= !shape.Surface.FillContains(shape.Anchor) && shape.Items.All(r => r.Width == 64 * scale && r.Height == 78 * scale && r.Left >= 0 && r.Top >= 0 && r.Right <= shape.Size.Width + 0.01 && r.Bottom <= shape.Size.Height + 0.01 && shape.Surface.FillContains(new Point(r.X + r.Width / 2, r.Y + r.Height / 2)));
                    for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) { var overlap = Rect.Intersect(shape.Items[i], shape.Items[j]); valid &= overlap.IsEmpty || overlap.Width < 0.5 || overlap.Height < 0.5; }
                }
                Check(valid, direction + " Fan keeps upright items in bounds, separated, and outside its transparent notch");
            }
            var preferences = new AppPreferences { OpenOnHover = true, OpenDelay = 330, CloseOnMouseLeave = true, CloseDelay = 420, DragHoldDelay = 200, AnimationDuration = 180, PanelOpacity = 0.4, PanelTheme = "Light", GridColumns = 2, GridRows = 3, HighlightOnHover = false, MoveDesktopIcons = true, RestoreExpanded = true, StartOnLogin = true };
            settings.Preferences = preferences; store.Save(settings); var reloadedPreferences = store.Load().Preferences;
            Check(reloadedPreferences.OpenOnHover && reloadedPreferences.OpenDelay == 330 && reloadedPreferences.CloseDelay == 420 && reloadedPreferences.DragHoldDelay == 200 && reloadedPreferences.AnimationDuration == 180 && reloadedPreferences.PanelOpacity == 0.4 && reloadedPreferences.PanelTheme == "Light" && reloadedPreferences.GridColumns == 2 && reloadedPreferences.GridRows == 3 && !reloadedPreferences.HighlightOnHover && reloadedPreferences.MoveDesktopIcons && reloadedPreferences.RestoreExpanded && reloadedPreferences.StartOnLogin, "All app settings persist without applying startup or desktop changes during tests");
            preferences.ShowFolderPreviews = false; preferences.CycleFolderPreviews = false; preferences.SnapToDesktopGrid = false; store.Save(settings); reloadedPreferences = store.Load().Preferences;
            Check(!reloadedPreferences.ShowFolderPreviews && !reloadedPreferences.CycleFolderPreviews, "Preview visibility and cycling can both be disabled and persist across restart");
            Check(!reloadedPreferences.SnapToDesktopGrid && reloadedPreferences.MoveDesktopIcons, "Grid alignment and shortcut displacement preferences persist independently");
            string previousVersion = File.ReadAllText(store.FilePath).Replace(",\"ShowFolderPreviews\":false", "").Replace(",\"CycleFolderPreviews\":false", "").Replace(",\"SnapToDesktopGrid\":false", ""); File.WriteAllText(store.FilePath, previousVersion);
            reloadedPreferences = store.Load().Preferences;
            Check(reloadedPreferences.ShowFolderPreviews && reloadedPreferences.CycleFolderPreviews && reloadedPreferences.StartOnLogin && reloadedPreferences.PanelOpacity == 0.4, "Upgrading adds enabled previews while retaining existing startup and appearance preferences");
            Check(reloadedPreferences.SnapToDesktopGrid && reloadedPreferences.MoveDesktopIcons, "Existing settings gain desktop grid alignment without losing their displacement preference");
            var invalid = new AppPreferences { OpenDelay = -20, CloseDelay = -1, DragHoldDelay = 10000, AnimationDuration = 9999, PanelOpacity = Double.NaN, PanelTheme = "unknown", GridColumns = 0, GridRows = 100 }; invalid.Normalize();
            Check(invalid.OpenDelay == 0 && invalid.CloseDelay == 0 && invalid.DragHoldDelay == 2000 && invalid.AnimationDuration == 1000 && invalid.PanelOpacity == 0.95 && invalid.PanelTheme == "Dark" && invalid.GridColumns == 1 && invalid.GridRows == 12, "Invalid app settings are normalized to supported ranges");
            Check(!DragTiming.Ready(TimeSpan.FromMilliseconds(99), 200, new Vector(50, 0)) && DragTiming.Ready(TimeSpan.FromMilliseconds(200), 200, new Vector(50, 0)) && !DragTiming.Ready(TimeSpan.FromSeconds(1), 0, new Vector(0, 0)), "Hold-before-drag requires both the configured hold and pointer movement");
            var available = DesktopGrid.Nearest(new Point(0, 0), new Size(80, 90), new Size(80, 90), new[] { new DesktopGridRegion { Bounds = new Rect(0, 0, 320, 270), Origin = new Point(0, 0) } }, new[] { new Rect(0, 0, 80, 90), new Rect(80, 0, 80, 90) });
            Check(available == new Point(0, 90), "Desktop collision planning chooses the nearest available slot");
            Check(!DesktopGrid.Nearest(new Point(0, 0), new Size(80, 90), new Size(80, 90), new[] { new DesktopGridRegion { Bounds = new Rect(0, 0, 80, 90), Origin = new Point(0, 0) } }, new[] { new Rect(0, 0, 80, 90) }).HasValue, "A full desktop produces no invalid or overlapping destination");
            Check(StartupIntegration.Command(@"C:\My Apps\FolderBloom.exe", @"C:\My Settings").Equals("\"C:\\My Apps\\FolderBloom.exe\" --data-dir \"C:\\My Settings\" --quiet --startup"), "Startup command quotes executable and settings paths and keeps the manager hidden");
            var taskXml = new System.Xml.XmlDocument(); taskXml.LoadXml(StartupIntegration.Definition(@"C:\My Apps & Tools\FolderBloom.exe", @"C:\My Settings & Data", StartupIntegration.UserSid));
            var namespaces = new System.Xml.XmlNamespaceManager(taskXml.NameTable); namespaces.AddNamespace("t", "http://schemas.microsoft.com/windows/2004/02/mit/task");
            Func<string, string> taskValue = path => taskXml.SelectSingleNode("/t:Task/" + path, namespaces).InnerText;
            Check(taskValue("t:Actions/t:Exec/t:Command") == @"C:\My Apps & Tools\FolderBloom.exe" && taskValue("t:Actions/t:Exec/t:Arguments") == StartupIntegration.Arguments(@"C:\My Settings & Data"), "Sign-in task preserves quoted paths containing spaces and XML characters");
            Check(taskValue("t:Triggers/t:LogonTrigger/t:UserId") == StartupIntegration.UserSid && taskValue("t:Principals/t:Principal/t:UserId") == StartupIntegration.UserSid && taskValue("t:Principals/t:Principal/t:LogonType") == "InteractiveToken" && taskValue("t:Principals/t:Principal/t:RunLevel") == "LeastPrivilege", "Startup targets the current signed-in user without elevation or a stored password");
            Check(taskValue("t:Triggers/t:LogonTrigger/t:Delay") == "PT10S" && taskValue("t:Settings/t:DisallowStartIfOnBatteries") == "false" && taskValue("t:Settings/t:StopIfGoingOnBatteries") == "false" && taskValue("t:Settings/t:ExecutionTimeLimit") == "PT0S", "Startup waits briefly for the desktop and stays running on battery without a time limit");
            Check(taskValue("t:Settings/t:MultipleInstancesPolicy") == "IgnoreNew" && taskValue("t:Settings/t:RestartOnFailure/t:Count") == "3" && taskValue("t:Settings/t:RestartOnFailure/t:Interval") == "PT1M", "Startup ignores duplicate launches and retries early failures");
            StartupIntegration.ValidateDefinition(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, root);
            Check(true, "Windows Task Scheduler validates the actual startup definition without creating or changing a task");
            string logDirectory = Path.Combine(root, "Startup log"); Directory.CreateDirectory(logDirectory);
            File.WriteAllText(Path.Combine(logDirectory, "startup.log"), new string('x', 66000)); StartupLog.Write(logDirectory, "Ready test");
            Check(new FileInfo(Path.Combine(logDirectory, "startup.log.previous")).Length == 66000 && File.ReadAllText(Path.Combine(logDirectory, "startup.log")).Contains("Ready test") && new FileInfo(Path.Combine(logDirectory, "startup.log")).Length < 1000, "Startup logging keeps the latest result and rotates oversized local logs");
            string fixtureStartup = Path.Combine(root, "Startup shortcuts"); StartupIntegration.EnsureRecoveryShortcut(root, fixtureStartup); StartupIntegration.EnsureRecoveryShortcut(root, fixtureStartup);
            object recoveryShell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); object recoveryLink = null;
            try
            {
                recoveryLink = ((dynamic)recoveryShell).CreateShortcut(Path.Combine(fixtureStartup, "FolderBloom folder recovery.lnk"));
                Check((string)((dynamic)recoveryLink).TargetPath == System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName && (string)((dynamic)recoveryLink).Arguments == "--recover-desktop \"" + root + "\"" && Directory.GetFiles(fixtureStartup).Length == 1, "Independent recovery shortcut is updated atomically and launches only folder recovery");
            }
            finally { if (recoveryLink != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(recoveryLink); System.Runtime.InteropServices.Marshal.ReleaseComObject(recoveryShell); }
            Check(!SettingsWindow.DiagnosticSummary(settings).Contains(folderPath) && !SettingsWindow.DiagnosticSummary(settings).Contains(f.Name), "Local diagnostics omit folder paths and names");
            var desktopLayout = await ShellWorker.Run(DesktopArrangement.Read); Check(desktopLayout.Spacing.Width >= 8 && desktopLayout.Spacing.Height >= 8 && desktopLayout.Regions.Count > 0, "Read-only desktop API probe obtains actual grid spacing, origin and icon positions without moving icons");
            TestDesktopGrid();
            foreach (string style in ExpansionLayout.Styles)
            {
                int count = Math.Min(20, ExpansionLayout.PageSize(style)); var layout = ExpansionLayout.Calculate(style, count, 390);
                Check(layout.Items.Count == count && layout.Size.Width > 0 && layout.Size.Height > 0 && layout.Items.All(r => r.X >= 0 && r.Y >= 0 && r.Right <= layout.Size.Width + 0.1 && r.Bottom <= layout.Size.Height + 0.1), style + " layout keeps every item inside its window");
                bool overlap = false; for (int i = 0; i < count; i++) for (int j = i + 1; j < count; j++) { var overlapRect = Rect.Intersect(layout.Items[i], layout.Items[j]); if (!overlapRect.IsEmpty && overlapRect.Width > 0.5 && overlapRect.Height > 0.5) overlap = true; }
                Check(!overlap, style + " layout keeps labels and icons from overlapping");
                bool valid = true;
                for (int n = 1; n <= ExpansionLayout.PageSize(style); n++) foreach (double scale in new[] { 0.7, 1.0, 1.5 }) foreach (bool flipped in new[] { false, true })
                {
                    var sample = ExpansionLayout.Build(style, n, scale, flipped);
                    valid &= sample.Items.All(r => sample.Surface.FillContains(new Point(r.X + r.Width / 2, r.Y + r.Height / 2)) && r.Left >= 0 && r.Top >= 0 && r.Right <= sample.Size.Width + 0.1 && r.Bottom <= sample.Size.Height + 0.1);
                }
                Check(valid, style + " surface contains every icon center at every supported item count and scale");
            }
            var fanShape = ExpansionLayout.Build("Fan", 5, 1, false); var ringShape = ExpansionLayout.Build("Ring", 6, 1, false);
            Check(!fanShape.Surface.FillContains(fanShape.Anchor) && !fanShape.Surface.FillContains(new Point(4, 4)) && fanShape.Surface.FillContains(new Point(fanShape.Anchor.X, 20)), "Fan has a crescent surface with transparent corners and an open folder notch");
            Check(!ringShape.Surface.FillContains(ringShape.Anchor) && !ringShape.Surface.FillContains(new Point(4, 4)) && ringShape.Surface.FillContains(new Point(ringShape.Anchor.X, 20)), "Ring has a transparent center and corners rather than a rectangular backdrop");
            var leave = new LeaveTracker(); var now = DateTime.UtcNow;
            Check(!leave.ShouldClose(false, now, 650) && !leave.ShouldClose(false, now.AddMilliseconds(600), 650) && !leave.ShouldClose(true, now.AddMilliseconds(620), 650) && !leave.ShouldClose(false, now.AddMilliseconds(900), 650) && leave.ShouldClose(false, now.AddMilliseconds(1600), 650), "Cursor leave waits for the grace period and resets when the pointer reaches either surface or a busy control");
            Check(Native.DesktopIconSize >= 16 && Native.DesktopIconSize <= 256 && Native.DesktopTileSize.Height >= Native.DesktopIconSize, "Desktop icon size follows the configured size or Windows' 48-pixel default");
            var baseIcon = Icons.Folder("default"); var tinted = Icons.Folder("#73ACE3"); byte[] a = Pixels(baseIcon), b = Pixels(tinted); int changed = 0; bool alpha = true;
            for (int i = 0; i < a.Length; i += 4) { if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2]) changed++; if (a[i + 3] != b[i + 3]) alpha = false; }
            Check(baseIcon.PixelWidth >= 48 && changed > 100 && alpha, "Real Windows folder artwork is recolored without changing its transparency");
            var fileIcon = await ShellWorker.Icon(Path.Combine(folderPath, "Welcome.txt"));
            Check(fileIcon != null && fileIcon.IsFrozen && Native.HasVisiblePixels(fileIcon), "The STA shell worker returns a visible file icon safe to display on the UI thread");
            Check(Object.ReferenceEquals(fileIcon, await ShellWorker.Icon(Path.Combine(folderPath, "Welcome.txt"))), "Reopening uses cached Shell artwork instead of waiting for another extraction");
            var missingIcon = await ShellWorker.Icon(Path.Combine(root, "missing-file.unknown")); Check(Native.HasVisiblePixels(missingIcon), "Unavailable icons fall back to a visible generic file icon");
            string dropTarget = Path.Combine(root, "Drop target"); Directory.CreateDirectory(dropTarget);
            string testShortcut = Path.Combine(root, "Notes.lnk"); string originalFile = Path.Combine(folderPath, "Welcome.txt");
            await ShellWorker.Run(() => { ShortcutDrop.CreateLink(originalFile, testShortcut); return true; });
            var shortcutIcon = await ShellWorker.Icon(testShortcut); Check(Native.HasVisiblePixels(shortcutIcon), "A real .lnk shortcut displays a Windows Shell icon");
            string explorerShortcut = Path.Combine(root, "Explorer.lnk");
            await ShellWorker.Run(() => { ShortcutDrop.CreateLink(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), explorerShortcut); return true; });
            var applicationIcon = await ShellWorker.Icon(explorerShortcut);
            Check(Native.HasVisiblePixels(applicationIcon) && !Pixels(applicationIcon).SequenceEqual(Pixels(missingIcon)), "An application shortcut resolves to its application artwork rather than the generic placeholder");
            string copied = await ShellWorker.Run(() => ShortcutDrop.Add(testShortcut, dropTarget));
            Check(File.ReadAllBytes(copied).SequenceEqual(File.ReadAllBytes(testShortcut)) && File.Exists(testShortcut), "Dropping a shortcut copies it unchanged and preserves the source");
            string copy2 = await ShellWorker.Run(() => ShortcutDrop.Add(testShortcut, dropTarget)); Check(copied != copy2 && File.Exists(copied) && File.Exists(copy2), "Conflicting shortcut names never overwrite an existing item");
            string noOp = await ShellWorker.Run(() => ShortcutDrop.Add(copied, dropTarget)); Check(noOp == null, "Dropping an item already in the destination does not duplicate it");
            string newLink = await ShellWorker.Run(() => ShortcutDrop.Add(originalFile, dropTarget));
            string linkTarget = await ShellWorker.Run(() => ShortcutDrop.ReadLinkTarget(newLink)); Check(File.Exists(originalFile) && linkTarget == originalFile, "Dropping a regular file creates a working shortcut instead of moving the original");
            string testUrl = Path.Combine(root, "Website.url"); File.WriteAllText(testUrl, "[InternetShortcut]\r\nURL=https://example.com\r\n");
            string urlCopy = await ShellWorker.Run(() => ShortcutDrop.Add(testUrl, dropTarget)); Check(File.ReadAllText(urlCopy) == File.ReadAllText(testUrl), "Web shortcuts retain their complete contents when dropped");
            string testDesktop = Path.Combine(root, "Test desktop"); Directory.CreateDirectory(testDesktop); string originalFolder = Path.Combine(testDesktop, "Existing folder"); Directory.CreateDirectory(originalFolder); File.WriteAllText(Path.Combine(originalFolder, "keep.txt"), "keep me");
            string recoveryPath = Path.Combine(root, "recovery");
            using (var replacement = new DesktopReplacement(recoveryPath, testDesktop))
            {
                replacement.RecoverPrevious(); var replace = new FolderSettings { Path = originalFolder, ReplaceDesktopIcon = true }; replacement.Apply(replace);
                Check((File.GetAttributes(originalFolder) & FileAttributes.Hidden) != 0 && File.ReadAllText(Path.Combine(originalFolder, "keep.txt")) == "keep me", "Replacing a desktop folder hides only the original entry and preserves its path and contents");
                File.SetAttributes(originalFolder, File.GetAttributes(originalFolder) | FileAttributes.ReadOnly); replacement.RestoreOne(originalFolder);
                Check((File.GetAttributes(originalFolder) & FileAttributes.Hidden) == 0 && (File.GetAttributes(originalFolder) & FileAttributes.ReadOnly) != 0, "Restoring a folder removes only our Hidden attribute and preserves other attribute changes");
                File.SetAttributes(originalFolder, File.GetAttributes(originalFolder) | FileAttributes.Hidden); replacement.Apply(replace); replacement.RestoreAll();
                Check((File.GetAttributes(originalFolder) & FileAttributes.Hidden) != 0, "A folder that was already hidden keeps its original hidden state");
                File.SetAttributes(originalFolder, File.GetAttributes(originalFolder) & ~FileAttributes.Hidden);
                bool rejected = false; try { replacement.Apply(new FolderSettings { Path = folderPath, ReplaceDesktopIcon = true }); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Replacement rejects folders outside the Desktop boundary");
                replacement.Apply(replace);
                Check(DesktopReplacement.RecoverAtSignIn(recoveryPath) == 0 && (File.GetAttributes(originalFolder) & FileAttributes.Hidden) != 0, "Sign-in recovery leaves original icons hidden while their FolderBloom session is still alive");
                var journal = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<ReplacementJournal>(File.ReadAllText(Path.Combine(recoveryPath, "desktop-recovery.json"))); journal.Owner = "0:0";
                File.WriteAllText(Path.Combine(recoveryPath, "desktop-recovery.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(journal)); replacement.RecoverPrevious();
                Check((File.GetAttributes(originalFolder) & FileAttributes.Hidden) == 0, "Recovery restores an interrupted session from its durable journal");
                replacement.Apply(replace);
                journal = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<ReplacementJournal>(File.ReadAllText(Path.Combine(recoveryPath, "desktop-recovery.json"))); journal.Owner = "0:0";
                File.WriteAllText(Path.Combine(recoveryPath, "desktop-recovery.json"), new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(journal));
                var recoveryInfo = new System.Diagnostics.ProcessStartInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, "--recover-desktop \"" + recoveryPath + "\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
                using (var recoveryProcess = System.Diagnostics.Process.Start(recoveryInfo)) { await Task.Run(() => recoveryProcess.WaitForExit()); Check(recoveryProcess.ExitCode == 0, "Recovery can run independently without launching the FolderBloom desktop app"); }
                Check((File.GetAttributes(originalFolder) & FileAttributes.Hidden) == 0 && File.ReadAllText(Path.Combine(originalFolder, "keep.txt")) == "keep me", "After a simulated reboot, independent recovery restores the missing original folder and preserves its contents");
                Check(DesktopReplacement.RecoverAtSignIn(recoveryPath) == 0 && (File.GetAttributes(originalFolder) & FileAttributes.ReadOnly) != 0, "Repeated sign-in recovery is harmless and preserves unrelated attributes");
            }
            string probeRoot = Path.Combine(root, "Recovery process test");
            var probeInfo = new System.Diagnostics.ProcessStartInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName, "--recovery-probe \"" + probeRoot + "\"") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden };
            using (var probe = System.Diagnostics.Process.Start(probeInfo)) { await Task.Run(() => probe.WaitForExit()); Check(probe.ExitCode == 0, "The isolated recovery probe starts and exits without normal app cleanup"); }
            string probeFolder = File.ReadAllText(Path.Combine(probeRoot, "probe-ready.txt"));
            await Until(() => (File.GetAttributes(probeFolder) & FileAttributes.Hidden) == 0);
            Check(true, "The background guardian restores the original folder after its owning process exits");
            await ExplorerChecks(app, root, output);
            var controller = new Controller(app, Path.Combine(root, "app-settings"), true); controller.Add(f);
            var second = new FolderSettings { Path = Path.Combine(root, "Documents"), Name = "Documents", Color = "#67B99C" }; Directory.CreateDirectory(second.Path); controller.Add(second);
            var third = new FolderSettings { Path = Path.Combine(root, "Downloads"), Name = "Downloads", Color = "default" }; Directory.CreateDirectory(third.Path); controller.Add(third);
            f.PanelWidth = 440; f.PanelHeight = 420;
            using (var scope = new PanelScope(new FolderPanel(controller, f)))
            {
                var panel = scope.Panel; await panel.NavigateAsync(folderPath); Check(panel.Entries.Count == 8, "Expanded panel loads the actual folder");
                string added = Path.Combine(folderPath, "Live update.txt"); File.WriteAllText(added, "live"); await Until(() => panel.Entries.Any(e => e.Name == "Live update.txt")); Check(true, "Live watcher detects a newly created file");
                string renamed = Path.Combine(folderPath, "Renamed live.txt"); File.Move(added, renamed); await Until(() => panel.Entries.Any(e => e.Name == "Renamed live.txt") && !panel.Entries.Any(e => e.Name == "Live update.txt")); Check(true, "Live watcher detects a renamed file");
                File.Delete(renamed); await Until(() => !panel.Entries.Any(e => e.Name == "Renamed live.txt")); Check(true, "Live watcher detects a deleted file");
                await panel.NavigateAsync(Path.Combine(folderPath, "Brand assets")); Check(panel.Entries.Count == 0 && panel.CurrentPath.EndsWith("Brand assets"), "Subfolder navigation updates the view");
                await panel.NavigateAsync(Path.Combine(root, "missing")); Check(panel.LastError != null && panel.Entries.Count == 0, "Missing folders show an error rather than stale contents");
                await panel.NavigateAsync(folderPath); await Until(() => panel.LoadedIconCount == 8); Check(panel.LastError == null && panel.Entries.Count == 8, "The panel recovers after navigating back from an unavailable folder");
                Check(panel.LoadedIconCount == panel.Entries.Count, "Every visible file and folder receives a loaded icon in the expanded panel");
                Render((FrameworkElement)panel.Content, (int)Math.Ceiling(panel.Width), (int)Math.Ceiling(panel.Height), Path.Combine(output, "panel-preview.png"));
                foreach (string style in ExpansionLayout.Styles)
                {
                    panel.ChangeStyle(style); await Until(() => panel.LoadedIconCount == panel.VisibleItemCount);
                    Render((FrameworkElement)panel.Content, (int)Math.Ceiling(panel.Width), (int)Math.Ceiling(panel.Height), Path.Combine(output, "layout-" + style + ".png"));
                }
                panel.ChangeStyle("Fan"); int firstPageCount = panel.VisibleItemCount;
                panel.ChangePage(1); Check(panel.VisibleItemCount == 8 - firstPageCount, "Mouse-wheel paging exposes the remaining items without popup controls");
                panel.ChangePage(-1); Check(panel.VisibleItemCount == firstPageCount, "Paging backward returns to the first set of items");
                double previousY = f.Y; var workArea = Native.WorkArea(new Point(f.X, f.Y)); f.Y = workArea.Top + 20; panel.FollowFolder(); bool pointsDown = panel.SurfaceLayout.Anchor.Y < panel.Height / 2;
                f.Y = workArea.Bottom - 100; panel.FollowFolder(); Check(pointsDown && panel.SurfaceLayout.Anchor.Y > panel.Height / 2, "Moving a folder between screen edges flips the automatic Fan orientation"); f.Y = previousY; panel.FollowFolder();
                double normalWidth = panel.Width; panel.SetScale(1.2); Check(Math.Abs(panel.Width / normalWidth - 1.2) < 0.01, "Popup scaling resizes the shaped surface and its items together"); panel.SetScale(1);
                panel.Place("Left"); Check(f.OffsetX == -panel.Width - 16 && f.OffsetY == 0, "Left placement preset sets the relative offset");
                panel.Place("Above"); Check(f.OffsetY == -panel.Height - 16, "Above placement preset sets the relative offset");
                Check(f.CustomPanelPosition, "Manual positioning disables automatic placement so the offset is retained");
                f.CustomPanelPosition = false; f.Y = workArea.Top + workArea.Height / 2; panel.ChangeFanDirection("Right");
                Check(panel.Height > panel.Width && panel.SurfaceLayout.Anchor.X < panel.Width / 2, "Selecting Right opens the Fan on the right side with the folder in its left notch");
                f.Y = workArea.Top + 10; panel.FollowFolder(); Check(panel.SurfaceLayout.Anchor.X < panel.Width / 2 && f.FanDirection == "Right", "An explicit Fan direction stays selected near screen edges");
                f.Y = workArea.Top + workArea.Height / 2; panel.ChangeFanDirection("Up");
                f.CustomPanelPosition = true; var anchorBefore = new Point(panel.Left + panel.SurfaceLayout.Anchor.X, panel.Top + panel.SurfaceLayout.Anchor.Y); Geometry.SetOffset(f, new Point(panel.Left, panel.Top));
                panel.ChangeFanDirection("Left"); var anchorAfter = new Point(panel.Left + panel.SurfaceLayout.Anchor.X, panel.Top + panel.SurfaceLayout.Anchor.Y);
                Check((anchorAfter - anchorBefore).Length < 1 && f.CustomPanelPosition, "Changing Fan direction preserves the hinge of a manually positioned popup when space permits");
                f.Y = previousY; panel.ChangeFanDirection("Auto");
                var savedPreferences = controller.Preferences.Clone(); var compactGrid = savedPreferences.Clone(); compactGrid.GridColumns = 2; compactGrid.GridRows = 1; compactGrid.PanelTheme = "Light"; compactGrid.PanelOpacity = 0.35; compactGrid.OpenOnHover = true; compactGrid.CloseDelay = 410; compactGrid.CloseOnMouseLeave = true; controller.ApplyPreferences(compactGrid, true);
                panel.ChangeStyle("Grid"); Check(panel.VisibleItemCount == 2 && panel.SurfaceLayout.Items.Select(r => r.Y).Distinct().Count() == 1, "App grid limits constrain visible rows and columns and retain paging");
                panel.ChangePage(1); Check(panel.VisibleItemCount == 2, "Grid items beyond app limits remain reachable through paging");
                Check(controller.HoverEnabled(f) && controller.LeaveEnabled(f) && controller.LeaveDelay(f) == 410, "App opening and closing defaults control folders without overrides");
                f.UseCustomBehavior = true; f.OpenOnHover = false; f.MouseLeaveDelay = 900; Check(!controller.HoverEnabled(f) && controller.LeaveDelay(f) == 900, "Folder behavior overrides remain effective until explicitly reset");
                controller.ApplyPreferences(savedPreferences, true); panel.ChangeStyle("Grid");
                panel.ChangeStyle("Grid"); f.CloseOnMouseLeave = false; panel.Opacity = 0; await panel.PrimeAsync();
                Check(PopupMotion.OpenMilliseconds == 100 && PopupMotion.CloseMilliseconds == 70 && PopupMotion.HoverDelay == 120, "Opening, closing and hover delays use the faster 100/70/120 ms settings");
                var timer = System.Diagnostics.Stopwatch.StartNew(); panel.Open(false);
                Check(panel.IsVisible && panel.VisibleItemCount == 8, "Primed contents are present immediately when the popup opens");
                Check(f.WasExpanded, "Opening records the expanded state for optional restoration");
                var firstTile = ((System.Windows.Controls.Canvas)panel.Content).Children[1];
                await Until(() => panel.Phase == PopupPhase.Open); long openMs = timer.ElapsedMilliseconds;
                Check(openMs < 700 && Object.ReferenceEquals(firstTile, ((System.Windows.Controls.Canvas)panel.Content).Children[1]), "Opening completes promptly and the background refresh preserves the animated items");
                timer.Restart(); panel.Collapse(); await Until(() => panel.Phase == PopupPhase.Hidden); long closeMs = timer.ElapsedMilliseconds;
                Check(!panel.IsVisible && closeMs < 700, "Closing completes promptly without waiting for folder or icon loading");
                Check(!f.WasExpanded, "Closing clears the recorded expanded state");
                panel.Open(false); panel.Collapse(); panel.Open(false); await Task.Delay(250);
                Check(panel.IsVisible && panel.Phase == PopupPhase.Open, "A close immediately followed by reopen cancels the stale close animation");
                f.Animate = false; panel.Collapse(); Check(!panel.IsVisible && panel.Phase == PopupPhase.Hidden, "Disabling animations closes immediately");
                panel.Open(false); Check(panel.Phase == PopupPhase.Open, "Disabling animations opens immediately"); panel.Collapse(); f.Animate = true;
                results.Add("Measured popup completion (20 ms polling): open " + openMs + " ms; close " + closeMs + " ms.");
            }
            await TestFolderPreviews(controller, root, output);
            await MakeLayoutPreview(controller, root, output);
            controller.Manager.Refresh(); Render((FrameworkElement)controller.Manager.Content, 930, 650, Path.Combine(output, "manager-preview.png"));
            var editor = new FolderEditor(f, null, false); Render((FrameworkElement)editor.Content, 560, 920, Path.Combine(output, "customize-preview.png")); editor.Close();
            var settingsWindow = new SettingsWindow(controller); Render((FrameworkElement)settingsWindow.Content, 790, 1900, Path.Combine(output, "settings-preview.png")); settingsWindow.Close();
            controller.Remove(second); Check(Directory.Exists(second.Path), "Removing a desktop view leaves the source folder untouched");
            controller.Quitting = true; controller.Manager.Close();
            results.Add("All " + results.Count(line => line.StartsWith("PASS:")) + " checks passed. Windows folder icon: " + baseIcon.PixelWidth + " × " + baseIcon.PixelHeight + ".");
        }
        internal static int GridProbe(string output)
        {
            results.Clear();
            try { TestDesktopGrid(); Directory.CreateDirectory(output); File.WriteAllLines(Path.Combine(output, "desktop-grid-results.txt"), results); return 0; }
            catch (Exception ex) { Directory.CreateDirectory(output); results.Add(ex.ToString()); File.WriteAllLines(Path.Combine(output, "desktop-grid-results.txt"), results); return 1; }
        }
        private static void TestDesktopGrid()
        {
            foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
            {
                double oneLine = DesktopGrid.CollapsedHeight(48, scale, 18);
                double twoLines = DesktopGrid.CollapsedHeight(48, scale, 33);
                Check(twoLines > oneLine && Math.Abs((oneLine - 48 - 18 - 3) - 2 / scale) < 0.01, "Collapsed layout retains the native image inset and measured label height at scale " + scale);
            }
            var layout = new DesktopLayout { Spacing = new Size(80, 100) };
            layout.Regions.Add(new DesktopGridRegion { Bounds = new Rect(0, 0, 402, 402), Origin = new Point(2, 2) });
            var folders = new[] { new DesktopFolder { Id = "one", Position = new Point(18, 113), Size = new Size(76, 92) } };
            var plan = DesktopGrid.Plan(layout, folders, true, true);
            Check(plan.HasRoom && plan.Folders[0].Position == new Point(2, 102) && plan.Folders[0].Size == layout.Spacing, "Folders snap to the real desktop origin and spacing rather than an arbitrary offset");
            layout.Items.Add(new DesktopItem { Index = 0, Position = new Point(2, 102), Path = "new-shortcut.lnk" });
            plan = DesktopGrid.Plan(layout, folders, true, true);
            Check(plan.IconMoves.Count == 1 && plan.Folders[0].Position == new Point(2, 102) && plan.IconMoves[0] == new Point(82, 102), "A shortcut added over an existing folder moves to the nearest free grid cell while the folder stays fixed");
            layout.Items.Add(new DesktopItem { Index = 1, Position = new Point(82, 102) });
            plan = DesktopGrid.Plan(layout, folders, true, true);
            Check(plan.IconMoves.Count == 1 && plan.IconMoves[0] == new Point(2, 2) && !plan.IconMoves.ContainsKey(1), "Occupied neighboring cells are skipped and unrelated desktop icons stay put");
            layout.Items[0].Position = plan.IconMoves[0];
            var again = DesktopGrid.Plan(layout, plan.Folders, true, true);
            Check(again.IconMoves.Count == 0 && again.Folders[0].Position == plan.Folders[0].Position, "Repeated background checks are stable after a collision is resolved");
            layout.Items[0].Position = new Point(2, 102); layout.AutoArrange = true;
            plan = DesktopGrid.Plan(layout, folders, true, true);
            Check(plan.AutoArrangeFallback && plan.IconMoves.Count == 0 && plan.Folders[0].Position != new Point(2, 102), "Auto arrange falls back to free folder cells without fighting Explorer or changing Windows preferences");
            layout.AutoArrange = false; plan = DesktopGrid.Plan(layout, folders, true, false);
            Check(plan.IconMoves.Count == 0 && plan.Folders[0].Position != new Point(2, 102), "Disabling shortcut movement makes the folder choose a free cell instead");
            layout.Items.Clear();
            plan = DesktopGrid.Plan(layout, new[] { folders[0], new DesktopFolder { Id = "two", Position = folders[0].Position, Size = folders[0].Size } }, true, true);
            Check(plan.Folders.Count == 2 && !DesktopGrid.Overlaps(new Rect(plan.Folders[0].Position, plan.Folders[0].Size), new Rect(plan.Folders[1].Position, plan.Folders[1].Size)), "Two FolderBloom folders cannot claim the same grid cell");
            layout.Items.Add(new DesktopItem { Index = 0, Position = new Point(2, 102), Path = @"C:\Desktop\Existing" });
            folders[0].ReplacedPath = @"c:\desktop\existing";
            plan = DesktopGrid.Plan(layout, folders, true, true);
            Check(plan.IconMoves.Count == 0 && plan.Folders[0].Position == new Point(2, 102), "The folder being replaced is never displaced while Explorer processes its hidden state");
            folders[0].ReplacedPath = null; layout.Items.Clear();
            plan = DesktopGrid.Plan(layout, folders, false, false);
            Check(plan.Folders[0].Position == folders[0].Position && plan.Folders[0].Size == folders[0].Size, "Disabling snapping retains free placement in an unoccupied location");
            Check(DesktopGrid.InferPhase(new[] { 2.0, 82, 162, 19, 242 }, 80, 0) == 2, "An off-grid native icon does not shift the inferred desktop grid");
            layout.Regions.Add(new DesktopGridRegion { Bounds = new Rect(-802, -102, 800, 500), Origin = new Point(-800, -100) });
            folders[0].Position = new Point(-749, -91); plan = DesktopGrid.Plan(layout, folders, true, true);
            Check(plan.Folders[0].Position == new Point(-720, -100), "Grid snapping supports monitors with negative coordinates and separate origins");
            layout.Regions.Clear(); layout.Regions.Add(new DesktopGridRegion { Bounds = new Rect(0, 0, 82, 102), Origin = new Point(2, 2) });
            folders[0].Position = new Point(2, 2); layout.Items.Add(new DesktopItem { Index = 0, Position = new Point(2, 2) });
            plan = DesktopGrid.Plan(layout, folders, true, true);
            Check(!plan.HasRoom && plan.IconMoves.Count == 0, "A full desktop rejects the whole collision move without placing shortcuts off-screen");
            layout.Spacing = new Size(60, 72.5); layout.Regions[0].Bounds = new Rect(0, 0, 401, 301); layout.Regions[0].Origin = new Point(1.5, 1.5); layout.Items.Clear();
            folders[0].Position = new Point(63, 142); plan = DesktopGrid.Plan(layout, folders, true, true);
            Check(plan.Folders[0].Position == new Point(61.5, 146.5), "Fractional Windows scaling retains the correct grid phase and spacing");
        }
        private static async Task TestFolderPreviews(Controller controller, string root, string output)
        {
            string folder = Path.Combine(root, "Icon preview"); Directory.CreateDirectory(folder);
            using (var preview = new FolderPreview(folder, "default", 48))
            {
                await preview.ConfigureAsync(true, true, false);
                Check(preview.EntryCount == 0 && preview.VisibleIconCount == 0, "An empty folder retains the normal folder icon without blank preview tiles");
                Task reading = preview.RefreshAsync(), repeated = preview.RefreshAsync(); Check(Object.ReferenceEquals(reading, repeated), "Overlapping refresh requests share one folder read instead of building up background work"); await reading;
                string note = Path.Combine(folder, "Notes.txt"); File.WriteAllText(note, "Preview test");
                await Until(() => preview.VisibleIconCount == 1 && preview.DisplayedPaths.Contains(note));
                Check(true, "Preview watcher adds a new item without opening the popup");
                await preview.AdvanceAsync(); Check(preview.DisplayedPaths.SequenceEqual(new[] { note }), "A one-item folder does not rotate or duplicate its icon");
                await ShellWorker.Run(() =>
                {
                    ShortcutDrop.CreateLink(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), Path.Combine(folder, "Explorer.lnk"));
                    ShortcutDrop.CreateLink(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), Path.Combine(folder, "Command.lnk"));
                    ShortcutDrop.CreateLink(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"), Path.Combine(folder, "Notepad.lnk")); return true;
                });
                await Until(() => preview.VisibleIconCount == 4); var firstFour = preview.DisplayedPaths.ToArray();
                await preview.AdvanceAsync(); Check(preview.DisplayedPaths.SequenceEqual(firstFour), "Exactly four items remain fixed with four distinct icons");
                var firstShot = Render(preview, 48, 48, Path.Combine(output, "preview-first-four.png"));
                var baseShot = Render(Ui.FolderImage("default", 48), 48, 48, Path.Combine(output, "preview-folder-base.png"));
                Check(!Pixels(firstShot).SequenceEqual(Pixels(baseShot)) && preview.Width == 48 && preview.Height == 48 && !preview.IsHitTestVisible, "Content icons overlay the original folder without resizing it or intercepting clicks");
                string fifth = Path.Combine(folder, "Photos"); Directory.CreateDirectory(fifth);
                await Until(() => preview.EntryCount == 5 && preview.DisplayedPaths.Contains(fifth));
                var seen = new HashSet<string>(preview.DisplayedPaths); bool valid = true;
                for (int i = 0; i < 5; i++) { await preview.AdvanceAsync(); valid &= preview.DisplayedPaths.Length == 4 && preview.DisplayedPaths.Distinct().Count() == 4; foreach (string entry in preview.DisplayedPaths) seen.Add(entry); }
                Check(valid && seen.Count == 5, "Five-item previews wrap through every item while keeping four unique icons visible");
                var beforeTick = preview.DisplayedPaths.ToArray(); await Until(() => !preview.DisplayedPaths.SequenceEqual(beforeTick));
                Check(true, "The live four-second timer automatically cycles preview icons");
                await preview.ConfigureAsync(true, false, false); firstFour = preview.DisplayedPaths.ToArray();
                Check(firstFour.SequenceEqual((await FolderReader.ReadAsync(folder)).Take(4).Select(e => e.Path)), "Turning cycling off returns to the first four items in folder order");
                await preview.AdvanceAsync(); Check(preview.DisplayedPaths.SequenceEqual(firstFour), "Disabled cycling keeps the preview fixed");
                var secondShot = Render(preview, 48, 48, Path.Combine(output, "preview-with-subfolder.png"));
                using (var scope = new PanelScope(new FolderPanel(controller, new FolderSettings { Path = folder })))
                {
                    await scope.Panel.NavigateAsync(fifth);
                    Check(scope.Panel.Entries.Count == 0 && preview.EntryCount == 5 && preview.DisplayedPaths.SequenceEqual(firstFour), "Popup subfolder navigation does not change the desktop root preview");
                }
                string renamed = Path.Combine(folder, "Albums"); Directory.Move(fifth, renamed);
                await Until(() => preview.DisplayedPaths.Contains(renamed) && !preview.DisplayedPaths.Contains(fifth));
                Check(true, "Renaming a previewed item removes the old path and displays the new one");
                Directory.Delete(renamed); await Until(() => preview.EntryCount == 4 && !preview.DisplayedPaths.Contains(renamed));
                Check(true, "Deleting an item clears its preview without opening the folder");
                var inFlight = preview.RefreshAsync(); await preview.ConfigureAsync(false, true, false); await inFlight;
                var disabledShot = Render(preview, 48, 48, Path.Combine(output, "preview-disabled.png"));
                Check(preview.VisibleIconCount == 0 && !preview.IsWatching && !preview.IsRunning && Pixels(disabledShot).SequenceEqual(Pixels(baseShot)), "Disabling previews cancels pending work, stops watchers and timers, and restores the original folder icon");
                string hidden = Path.Combine(folder, "Hidden.txt"); File.WriteAllText(hidden, "hidden"); File.SetAttributes(hidden, FileAttributes.Hidden);
                for (int i = 0; i < 5; i++) File.WriteAllText(Path.Combine(folder, "Item " + i + ".txt"), "more");
                await preview.ConfigureAsync(true, true, false);
                Check(preview.EntryCount == 9 && preview.VisibleIconCount == 4 && !preview.DisplayedPaths.Contains(hidden), "Re-enabling loads current contents and excludes hidden items");
                seen = new HashSet<string>(); valid = true;
                for (int i = 0; i < 9; i++) { foreach (string entry in preview.DisplayedPaths) seen.Add(entry); valid &= preview.DisplayedPaths.Distinct().Count() == 4; await preview.AdvanceAsync(); }
                Check(valid && seen.Count == 9, "A partial final group wraps safely and every item in a larger folder appears");
                preview.Width = preview.Height = 96; var large = Render(preview, 96, 96, Path.Combine(output, "preview-large.png"));
                Check(preview.ActualWidth == 96 && preview.ActualHeight == 96 && preview.VisibleIconCount == 4, "The preview scales with larger Windows desktop icons");
                var board = new System.Windows.Controls.Canvas { Width = 640, Height = 235, Background = Ui.Brush("#111621") };
                var heading = Ui.Text("FolderBloom · Folder previews", 23, Brushes.White); System.Windows.Controls.Canvas.SetLeft(heading, 24); System.Windows.Controls.Canvas.SetTop(heading, 22); board.Children.Add(heading);
                var shots = new[] { firstShot, secondShot, disabledShot, large }; var captions = new[] { "Four item icons", "Live contents", "Previews off", "Larger icon size" };
                for (int i = 0; i < shots.Length; i++)
                {
                    var icon = new System.Windows.Controls.Image { Source = shots[i], Width = shots[i].Width, Height = shots[i].Height }; System.Windows.Controls.Canvas.SetLeft(icon, 60 + i * 150 - (i == 3 ? 24 : 0)); System.Windows.Controls.Canvas.SetTop(icon, i == 3 ? 77 : 101); board.Children.Add(icon);
                    var label = Ui.Text(captions[i], 12, Ui.Muted); label.Width = 142; label.TextAlignment = TextAlignment.Center; System.Windows.Controls.Canvas.SetLeft(label, 13 + i * 150); System.Windows.Controls.Canvas.SetTop(label, 186); board.Children.Add(label);
                }
                Render(board, 640, 235, Path.Combine(output, "FolderBloom-content-previews.png"));
                preview.Dispose(); await preview.AdvanceAsync(); await preview.RefreshAsync(); Check(!preview.IsRunning && !preview.IsWatching && preview.VisibleIconCount == 0, "Removed folder previews release timers, watchers and icon references");
            }
            string unavailable = Path.Combine(root, "Preview recovery");
            using (var preview = new FolderPreview(unavailable, "#73ACE3", 48))
            {
                await preview.ConfigureAsync(true, true, false); Check(preview.VisibleIconCount == 0, "An unavailable folder shows its base icon without stale previews");
                Directory.CreateDirectory(unavailable); File.WriteAllText(Path.Combine(unavailable, "Recovered.txt"), "recovered");
                await Until(() => preview.VisibleIconCount == 1); Check(preview.IsWatching, "Preview polling recovers a folder that becomes available again");
            }
            var savedPreferences = controller.Preferences.Clone();
            var widgetSettings = new FolderSettings { Name = "Preview integration", Path = folder };
            var widget = new FolderWidget(controller, widgetSettings) { Opacity = 0 };
            controller.Widgets.Add(widgetSettings.Id, widget);
            try
            {
                widget.Show(); await Until(() => widget.Preview.VisibleIconCount == 4);
                Check(widget.Width == Native.DesktopTileSize.Width && widget.Preview.Width == Native.DesktopIconSize && widget.CollapsedLabelViewport.ActualHeight <= FolderWidget.TwoLineLabelHeight(widget.CollapsedLabel) + 0.1 && widget.Height < 100, "Desktop widgets center the native-size preview in the grid pitch and keep the collapsed label within two measured lines");
                var off = savedPreferences.Clone(); off.ShowFolderPreviews = false; controller.ApplyPreferences(off, false);
                Check(widget.Preview.VisibleIconCount == 0 && !widget.Preview.IsWatching, "Saving the preview toggle off immediately updates existing desktop widgets");
                controller.ApplyPreferences(savedPreferences, false); await Until(() => widget.Preview.VisibleIconCount == 4);
                Check(true, "Saving the preview toggle on restores existing widgets without restarting");
            }
            finally { widget.Dispose(); controller.Widgets.Remove(widgetSettings.Id); controller.ApplyPreferences(savedPreferences, false); }
        }
        private static async Task MakeLayoutPreview(Controller controller, string root, string output)
        {
            string folder = Path.Combine(root, "Preview items"); Directory.CreateDirectory(folder);
            Directory.CreateDirectory(Path.Combine(folder, "Projects")); Directory.CreateDirectory(Path.Combine(folder, "Photos"));
            await ShellWorker.Run(() => { ShortcutDrop.CreateLink(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), Path.Combine(folder, "Explorer.lnk")); ShortcutDrop.CreateLink(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), Path.Combine(folder, "Terminal.lnk")); return true; });
            File.WriteAllText(Path.Combine(folder, "Notes.txt"), "Preview only");
            var board = new System.Windows.Controls.Canvas { Background = Ui.Brush("#111621"), Width = 1120, Height = 940 };
            var heading = Ui.Text("FolderBloom 0.6", 28, Brushes.White); System.Windows.Controls.Canvas.SetLeft(heading, 32); System.Windows.Controls.Canvas.SetTop(heading, 24); board.Children.Add(heading);
            var caption = Ui.Text("Shaped popups · Windows icons · Free placement", 14, Ui.Muted); System.Windows.Controls.Canvas.SetLeft(caption, 32); System.Windows.Controls.Canvas.SetTop(caption, 65); board.Children.Add(caption);
            string[] styles = { "Grid", "Fan", "Column", "Row", "Ring" };
            Point[] origins = { new Point(40, 146), new Point(326, 146), new Point(932, 146), new Point(40, 600), new Point(486, 552) };
            for (int i = 0; i < styles.Length; i++)
            {
                var settings = new FolderSettings { Path = folder, Name = styles[i], ExpandStyle = styles[i], X = 650, Y = 800, CustomPanelPosition = true };
                using (var scope = new PanelScope(new FolderPanel(controller, settings)))
                {
                    var panel = scope.Panel; await panel.NavigateAsync(folder); await Until(() => panel.LoadedIconCount == panel.VisibleItemCount);
                    var shot = Render((FrameworkElement)panel.Content, (int)Math.Ceiling(panel.Width), (int)Math.Ceiling(panel.Height), Path.Combine(output, "preview-" + styles[i] + ".png"));
                    var label = Ui.Text(styles[i], 14, Ui.Muted); System.Windows.Controls.Canvas.SetLeft(label, origins[i].X); System.Windows.Controls.Canvas.SetTop(label, origins[i].Y - 32); board.Children.Add(label);
                    var image = new System.Windows.Controls.Image { Source = shot, Width = shot.Width, Height = shot.Height }; System.Windows.Controls.Canvas.SetLeft(image, origins[i].X); System.Windows.Controls.Canvas.SetTop(image, origins[i].Y); board.Children.Add(image);
                    var icon = Ui.FolderImage("default", 48); double anchorX = styles[i] == "Ring" || styles[i] == "Fan" ? panel.SurfaceLayout.Anchor.X : panel.Width / 2;
                    double anchorY = styles[i] == "Ring" || styles[i] == "Fan" ? panel.SurfaceLayout.Anchor.Y - 24 : panel.Height + 10;
                    System.Windows.Controls.Canvas.SetLeft(icon, origins[i].X + anchorX - 24); System.Windows.Controls.Canvas.SetTop(icon, origins[i].Y + anchorY); board.Children.Add(icon);
                    var name = Ui.Text(styles[i], 12, Brushes.White); name.Width = 80; name.TextAlignment = TextAlignment.Center; System.Windows.Controls.Canvas.SetLeft(name, origins[i].X + anchorX - 40); System.Windows.Controls.Canvas.SetTop(name, origins[i].Y + anchorY + 49); board.Children.Add(name);
                }
            }
            Render(board, 1120, 940, Path.Combine(output, "FolderBloom-layouts.png"));
            await MakeFanPreview(controller, folder, output);
        }
        private static async Task MakeFanPreview(Controller controller, string folder, string output)
        {
            var board = new System.Windows.Controls.Canvas { Background = Ui.Brush("#111621"), Width = 1120, Height = 1130 };
            var heading = Ui.Text("Choose your Fan direction", 28, Brushes.White); System.Windows.Controls.Canvas.SetLeft(heading, 40); System.Windows.Controls.Canvas.SetTop(heading, 28); board.Children.Add(heading);
            var caption = Ui.Text("Up · Down · Left · Right · Auto", 14, Ui.Muted); System.Windows.Controls.Canvas.SetLeft(caption, 40); System.Windows.Controls.Canvas.SetTop(caption, 72); board.Children.Add(caption);
            string[] directions = { "Up", "Down", "Left", "Right" }; Point[] origins = { new Point(40, 150), new Point(600, 150), new Point(165, 570), new Point(725, 570) };
            for (int i = 0; i < directions.Length; i++)
            {
                var settings = new FolderSettings { Path = folder, Name = directions[i], ExpandStyle = "Fan", FanDirection = directions[i], X = 650, Y = 550, CustomPanelPosition = true };
                using (var scope = new PanelScope(new FolderPanel(controller, settings)))
                {
                    var panel = scope.Panel; await panel.NavigateAsync(folder); await Until(() => panel.LoadedIconCount == panel.VisibleItemCount);
                    var bitmap = Render((FrameworkElement)panel.Content, (int)Math.Ceiling(panel.Width), (int)Math.Ceiling(panel.Height), Path.Combine(output, "fan-" + directions[i] + ".png"));
                    var shot = new System.Windows.Controls.Image { Source = bitmap, Width = bitmap.Width, Height = bitmap.Height }; System.Windows.Controls.Canvas.SetLeft(shot, origins[i].X); System.Windows.Controls.Canvas.SetTop(shot, origins[i].Y); board.Children.Add(shot);
                    var label = Ui.Text(directions[i], 16, Ui.Muted); System.Windows.Controls.Canvas.SetLeft(label, i % 2 == 0 ? 40 : 600); System.Windows.Controls.Canvas.SetTop(label, i < 2 ? 114 : 520); board.Children.Add(label);
                    Point anchor = panel.SurfaceLayout.Anchor; var icon = Ui.FolderImage("default", 48); System.Windows.Controls.Canvas.SetLeft(icon, origins[i].X + anchor.X - 24); System.Windows.Controls.Canvas.SetTop(icon, origins[i].Y + anchor.Y - 24); board.Children.Add(icon);
                    var name = Ui.Text("Folder", 12, Brushes.White); name.Width = 80; name.TextAlignment = TextAlignment.Center; System.Windows.Controls.Canvas.SetLeft(name, origins[i].X + anchor.X - 40); System.Windows.Controls.Canvas.SetTop(name, origins[i].Y + anchor.Y + 26); board.Children.Add(name);
                }
            }
            Render(board, 1120, 1130, Path.Combine(output, "FolderBloom-fan-directions.png"));
        }
        private sealed class PanelScope : IDisposable
        {
            internal readonly FolderPanel Panel; internal PanelScope(FolderPanel panel) { Panel = panel; } public void Dispose() { Panel.Dispose(); }
        }
    }
}
