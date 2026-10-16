using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace FolderBloom
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length >= 3 && args[0] == "--restore-guard") return DesktopReplacement.Guard(args[1], args[2]);
            if (args.Length >= 2 && args[0] == "--recovery-probe") return Tests.RecoveryProbe(args[1]);
            if (args.Length >= 2 && args[0] == "--self-test") return Tests.Run(args[1]);
            if (args.Length >= 2 && args[0] == "--layout-probe") return Tests.LayoutProbe(args[1], args.Length >= 3 ? (double?)Double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : null);
            if (args.Length >= 2 && args[0] == "--grid-probe") return Tests.GridProbe(args[1]);
            if (args.Length >= 2 && args[0] == "--desktop-probe") return Tests.DesktopProbe(args[1]);
            if (args.Length >= 2 && args[0] == "--desktop-placement-test") return Tests.DesktopPlacementProbe(args[1]);
            if (args.Length >= 2 && args[0] == "--recover-desktop") return DesktopReplacement.RecoverAtSignIn(args[1]);
            if (args.Length >= 1 && args[0] == "--register-explorer")
            {
                try { ExplorerIntegration.Register(); return 0; } catch (Exception ex) { StartupLog.Write(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderBloom"), "Explorer registration failed: " + ex); return 1; }
            }
            if (args.Length >= 2 && args[0] == "--register-startup")
            {
                try { StartupIntegration.SetEnabled(true, args[1]); StartupLog.Write(args[1], "Sign-in task registered."); return 0; }
                catch (Exception ex) { StartupLog.Write(args[1], "Could not register sign-in task: " + ex); return 1; }
            }
            string data = args.Length >= 2 && args[0] == "--data-dir" ? Path.GetFullPath(args[1]) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderBloom");
            bool startup = args.Contains("--startup");
            var request = ShellRequest.Parse(args);
            StartupLog.Write(data, "Starting " + typeof(Program).Assembly.GetName().Version + (startup ? " at sign-in." : " manually."));
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { StartupLog.Write(data, "Unhandled error: " + e.ExceptionObject); };
            try
            {
                bool created;
                using (var mutex = new Mutex(true, "Local\\FolderBloom.Desktop.01", out created))
                {
                    if (!created)
                    {
                        if (request != null)
                        {
                            if (ShellCommandServer.Send(ShellCommandServer.PipeName, request)) return 0;
                            MessageBox.Show("FolderBloom could not receive the Explorer command. Please reopen FolderBloom and try again.", "FolderBloom"); return 1;
                        }
                        StartupLog.Write(data, "Already running; duplicate launch finished.");
                        if (!args.Contains("--quiet")) MessageBox.Show("FolderBloom is already running. Open it from the folder icon in the notification area.", "FolderBloom");
                        return 0;
                    }
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; Ui.InstallStyles(app);
                    var controller = new Controller(app, data, false); controller.Start(!args.Contains("--quiet") && request == null);
                    StartupLog.Write(data, "Ready; desktop folders: " + controller.Widgets.Count);
                    using (var commands = new ShellCommandServer(ShellCommandServer.PipeName, r => app.Dispatcher.BeginInvoke((Action)(() => controller.HandleShellRequest(r)))))
                    {
                        if (request != null) app.Dispatcher.BeginInvoke((Action)(() => controller.HandleShellRequest(request)));
                        int result = app.Run(); StartupLog.Write(data, "Exited with code " + result + "."); return result;
                    }
                }
            }
            catch (Exception ex)
            {
                StartupLog.Write(data, "Could not start: " + ex);
                if (!startup) MessageBox.Show("FolderBloom could not start. Your saved settings have been preserved.\n\n" + ex.Message, "FolderBloom");
                return 1;
            }
        }
    }
    internal sealed class Controller
    {
        internal readonly Application App;
        internal readonly SettingsStore Store;
        internal readonly Settings Settings;
        internal readonly Dictionary<string, FolderWidget> Widgets = new Dictionary<string, FolderWidget>();
        internal readonly ManagerWindow Manager;
        internal readonly bool IsTest;
        internal readonly DesktopReplacement Replacement;
        internal bool Quitting;
        private readonly DispatcherTimer saveTimer, desktopTimer;
        private Forms.NotifyIcon tray;
        private bool saveErrorShown;
        private SettingsWindow preferencesWindow;
        private bool lastLightTheme;
        private int desktopArrangeVersion;
        private bool desktopArranging;
        private string desktopProblem, lastDroppedFolder;
        internal AppPreferences Preferences { get { return Settings.Preferences; } }
        internal bool HoverEnabled(FolderSettings folder) { return folder.UseCustomBehavior ? folder.OpenOnHover : Preferences.OpenOnHover; }
        internal bool LeaveEnabled(FolderSettings folder) { return folder.UseCustomBehavior ? folder.CloseOnMouseLeave : Preferences.CloseOnMouseLeave; }
        internal int LeaveDelay(FolderSettings folder) { return folder.UseCustomBehavior ? folder.MouseLeaveDelay : Preferences.CloseDelay; }
        internal Controller(Application app, string dataDirectory, bool isTest)
        {
            App = app; IsTest = isTest; Store = new SettingsStore(dataDirectory); Settings = Store.Load();
            Replacement = new DesktopReplacement(dataDirectory, null);
            if (!isTest)
            {
                Replacement.RecoverPrevious();
                app.SessionEnding += delegate
                {
                    try { Store.Save(Settings); } catch (Exception ex) { StartupLog.Write(Store.DirectoryPath, "Could not save at sign-out: " + ex); }
                    try { Replacement.RestoreAll(); StartupLog.Write(Store.DirectoryPath, "Original folders restored for Windows sign-out."); }
                    catch (Exception ex) { StartupLog.Write(Store.DirectoryPath, "Sign-out recovery will retry at next sign-in: " + ex); }
                };
                app.Exit += delegate { try { Replacement.RestoreAll(); } catch (Exception ex) { MessageBox.Show(ex.Message, "FolderBloom · Restore folders"); } };
            }
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            saveTimer.Tick += delegate { saveTimer.Stop(); SaveNow(); };
            desktopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            desktopTimer.Tick += delegate
            {
                bool light = Appearance.Light(Preferences); if (light != lastLightTheme) { lastLightTheme = light; foreach (var widget in Widgets.Values) widget.Refresh(); }
                foreach (var w in Widgets.Values)
                {
                    if (Native.DesktopForeground() && w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
                    if (!w.IsBusy) Native.LowerToDesktop(w);
                    if (!w.IsBusy) w.UpdateSize();
                }
                ArrangeDesktop();
            };
            Manager = new ManagerWindow(this);
        }
        internal void Start(bool showManager = true)
        {
            foreach (var folder in Settings.Folders)
            {
                var w = new FolderWidget(this, folder); Widgets.Add(folder.Id, w); w.Show(); Native.LowerToDesktop(w); ApplyReplacement(folder);
                if (Preferences.RestoreExpanded && folder.WasExpanded) App.Dispatcher.BeginInvoke((Action)(() => w.RestoreExpansion()), DispatcherPriority.ApplicationIdle);
            }
            tray = new Forms.NotifyIcon { Text = "FolderBloom", Icon = System.Drawing.SystemIcons.Application, Visible = true };
            var s = new Native.StockInfo { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.StockInfo)) };
            if (Native.SHGetStockIconInfo(3, 0x100, ref s) == 0 && s.Icon != IntPtr.Zero)
            {
                try { tray.Icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(s.Icon).Clone(); } finally { Native.DestroyIcon(s.Icon); }
            }
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Manage folders", null, delegate { App.Dispatcher.Invoke((Action)ShowManager); });
            menu.Items.Add("Settings…", null, delegate { App.Dispatcher.Invoke((Action)ShowSettings); });
            menu.Items.Add("Add a folder…", null, delegate { App.Dispatcher.Invoke((Action)ChooseFolder); });
            menu.Items.Add("Collapse all panels", null, delegate { App.Dispatcher.Invoke((Action)(() => { foreach (var w in Widgets.Values) w.Collapse(); })); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Quit FolderBloom", null, delegate { App.Dispatcher.Invoke((Action)Quit); });
            tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { ShowManager(); };
            desktopTimer.Start(); ArrangeDesktop(); if (showManager) ShowManager();
        }
        internal void ShowManager() { Manager.Refresh(); Manager.Show(); Manager.WindowState = WindowState.Normal; Manager.Activate(); }
        internal void ShowSettings()
        {
            if (preferencesWindow == null) { preferencesWindow = new SettingsWindow(this); preferencesWindow.Closed += delegate { preferencesWindow = null; }; }
            preferencesWindow.Show(); preferencesWindow.WindowState = WindowState.Normal; preferencesWindow.Activate();
        }
        internal void ApplyPreferences(AppPreferences next, bool applyToAll)
        {
            Interlocked.Increment(ref desktopArrangeVersion);
            next.Normalize(); var previous = Preferences; var overrides = Settings.Folders.Select(f => f.UseCustomBehavior).ToArray();
            if (!IsTest) StartupIntegration.SetEnabled(next.StartOnLogin, Store.DirectoryPath);
            Settings.Preferences = next;
            if (applyToAll) foreach (var folder in Settings.Folders) folder.UseCustomBehavior = false;
            try { Store.Save(Settings); }
            catch
            {
                Settings.Preferences = previous; for (int i = 0; i < overrides.Length; i++) Settings.Folders[i].UseCustomBehavior = overrides[i];
                if (!IsTest) StartupIntegration.SetEnabled(previous.StartOnLogin, Store.DirectoryPath); throw;
            }
            foreach (var widget in Widgets.Values) { widget.Refresh(); if (!Preferences.SnapToDesktopGrid) widget.UseFreeSize(); } Manager.Refresh(); ArrangeDesktop();
        }
        internal void InvalidateDesktopArrangement() { Interlocked.Increment(ref desktopArrangeVersion); }
        internal async void ArrangeDesktop(FolderWidget dropped = null)
        {
            if (dropped != null) lastDroppedFolder = dropped.Settings.Id;
            if (IsTest || Quitting || desktopArranging || Widgets.Count == 0 || (!Preferences.SnapToDesktopGrid && !Preferences.MoveDesktopIcons) || Widgets.Values.Any(w => w.IsBusy) || Native.PointerButtonDown) return;
            var folders = Widgets.Values.OrderBy(w => w.Settings.Id == lastDroppedFolder ? 1 : 0).Select(w => new DesktopFolder { Id = w.Settings.Id, Position = new Point(w.Left, w.Top), Size = new Size(w.Width, w.Height), ReplacedPath = w.Settings.ReplaceDesktopIcon ? w.Settings.Path : null }).ToList();
            int version = Volatile.Read(ref desktopArrangeVersion); bool snap = Preferences.SnapToDesktopGrid, move = Preferences.MoveDesktopIcons;
            desktopArranging = true;
            try
            {
                var plan = await ShellWorker.Run(() => DesktopArrangement.Arrange(folders, snap, move, () => Volatile.Read(ref desktopArrangeVersion) == version && !Quitting));
                if (plan == null || Quitting || version != Volatile.Read(ref desktopArrangeVersion) || Widgets.Values.Any(w => w.IsBusy) || Native.PointerButtonDown) return;
                bool changed = false;
                foreach (var placed in plan.Folders) { FolderWidget widget; if (Widgets.TryGetValue(placed.Id, out widget)) changed |= widget.ApplyDesktopPlacement(placed, snap); }
                if (changed) SaveSoon(); lastDroppedFolder = null;
                ReportDesktopProblem(plan.AutoArrangeFallback ? "Windows Auto arrange icons is on. FolderBloom will use free spaces. Turn Auto arrange off to keep folders fixed and move overlapping shortcuts instead." : null);
            }
            catch (Exception ex) { ReportDesktopProblem("Desktop placement will retry automatically. " + ex.Message); }
            finally { desktopArranging = false; }
        }
        private void ReportDesktopProblem(string problem)
        {
            if (problem == desktopProblem) return; desktopProblem = problem;
            if (problem != null && tray != null) tray.ShowBalloonTip(5000, "FolderBloom · Desktop layout", problem, Forms.ToolTipIcon.Info);
        }
        internal void ChooseFolder()
        {
            using (var picker = new Forms.FolderBrowserDialog { Description = "Choose a folder to put on your desktop", ShowNewFolderButton = false })
            {
                if (picker.ShowDialog() != Forms.DialogResult.OK) return;
                string path = Path.GetFullPath(picker.SelectedPath);
                if (Settings.Folders.Any(existing => String.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase))) { Ui.Error(Manager, "That folder is already on your desktop."); return; }
                var f = new FolderSettings { Path = path, Name = new DirectoryInfo(path).Name, ReplaceDesktopIcon = DesktopReplacement.IsDesktopFolder(path), X = 160 + Settings.Folders.Count % 6 * 110, Y = 140 + Settings.Folders.Count / 6 * 120 };
                f.OpenOnHover = Preferences.OpenOnHover; f.CloseOnMouseLeave = Preferences.CloseOnMouseLeave; f.MouseLeaveDelay = Preferences.CloseDelay;
                var edit = new FolderEditor(f, Manager, true, Preferences);
                if (edit.ShowDialog() == true) Add(f);
            }
        }
        internal void HandleShellRequest(ShellRequest request)
        {
            try
            {
                if (!request.Valid || !Directory.Exists(request.Path)) throw new IOException("The selected folder is unavailable. Open a normal file-system folder and try again.");
                if (request.Action == "new")
                {
                    var dialog = new FolderNameDialog("New FolderBloom folder", "New folder", "Create a real folder here and add its expanding desktop icon.", name => CreateFolder(request.Path, name));
                    dialog.ShowDialog();
                }
                else
                {
                    string path = Path.GetFullPath(request.Path).TrimEnd('\\'); var existing = Settings.Folders.FirstOrDefault(f => String.Equals(f.Path.TrimEnd('\\'), path, StringComparison.OrdinalIgnoreCase));
                    if (existing != null) { FolderWidget widget; if (Widgets.TryGetValue(existing.Id, out widget)) widget.Expand(); return; }
                    ShowManager(); var folder = NewFolderSettings(path); var editor = new FolderEditor(folder, Manager, true, Preferences); if (editor.ShowDialog() == true) { Add(folder); Store.Save(Settings); }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "FolderBloom"); }
        }
        private FolderSettings NewFolderSettings(string path)
        {
            return new FolderSettings { Path = path, Name = new DirectoryInfo(path).Name, ReplaceDesktopIcon = DesktopReplacement.IsDesktopFolder(path), X = 160 + Settings.Folders.Count % 6 * 110, Y = 140 + Settings.Folders.Count / 6 * 120, OpenOnHover = Preferences.OpenOnHover, CloseOnMouseLeave = Preferences.CloseOnMouseLeave, MouseLeaveDelay = Preferences.CloseDelay };
        }
        internal void CreateFolder(string parent, string name)
        {
            FolderNames.Validate(name); parent = Path.GetFullPath(parent);
            if (!Directory.Exists(parent)) throw new IOException("The parent folder is unavailable.");
            string path = Path.Combine(parent, name);
            if (File.Exists(path) || Directory.Exists(path)) throw new IOException("A file or folder with that name already exists. Choose a different name.");
            FolderNames.Create(path); var folder = NewFolderSettings(path);
            try { Add(folder); Store.Save(Settings); }
            catch
            {
                Replacement.RestoreOne(path); FolderWidget widget; if (Widgets.TryGetValue(folder.Id, out widget)) { widget.Dispose(); Widgets.Remove(folder.Id); }
                Settings.Folders.Remove(folder); Manager.Refresh();
                // Never delete the new real folder: another application may already have added contents.
                throw;
            }
        }
        internal void PromptRename(FolderSettings folder)
        {
            FolderWidget widget;
            if (Widgets.TryGetValue(folder.Id, out widget)) App.Dispatcher.BeginInvoke((Action)widget.BeginRename, DispatcherPriority.Input);
            else new FolderNameDialog("Rename folder", new DirectoryInfo(folder.Path).Name, "Renames the Windows folder and keeps its contents.", name => RenameFolder(folder, name)).ShowDialog();
        }
        internal void RenameFolder(FolderSettings folder, string name)
        {
            string oldPath = folder.Path, oldName = folder.Name, destination = FolderNames.Destination(oldPath, name);
            if (String.Equals(oldPath, destination, StringComparison.Ordinal) && oldName == name) return;
            var affected = Settings.Folders.Where(f => String.Equals(f.Path, oldPath, StringComparison.OrdinalIgnoreCase) || f.Path.StartsWith(oldPath.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)).ToDictionary(f => f, f => f.Path);
            InvalidateDesktopArrangement(); saveTimer.Stop(); Replacement.RestoreOne(oldPath);
            bool moved = false;
            try
            {
                if (!String.Equals(oldPath, destination, StringComparison.Ordinal)) { FolderNames.Rename(oldPath, destination); moved = true; }
                foreach (var item in affected) item.Key.Path = destination + item.Value.Substring(oldPath.Length);
                folder.Name = name; Store.Save(Settings);
            }
            catch (Exception error)
            {
                if (moved)
                {
                    try { FolderNames.Rename(destination, oldPath); }
                    catch (Exception rollback)
                    {
                        StartupLog.Write(Store.DirectoryPath, "Folder rename could not be rolled back: " + rollback);
                        RefreshRenamedFolders(affected.Keys); SaveSoon();
                        throw new IOException("The folder was renamed, but its saved path could not be updated. It remains accessible at " + destination + ". " + error.Message, error);
                    }
                }
                foreach (var item in affected) item.Key.Path = item.Value; folder.Name = oldName;
                if (!IsTest) ApplyReplacement(folder); throw;
            }
            RefreshRenamedFolders(affected.Keys);
            if (!IsTest) ApplyReplacement(folder);
        }
        private void RefreshRenamedFolders(IEnumerable<FolderSettings> folders)
        {
            foreach (var folder in folders) { FolderWidget widget; if (Widgets.TryGetValue(folder.Id, out widget)) widget.SourceRenamed(); }
            Manager.Refresh();
        }
        internal void Add(FolderSettings folder)
        {
            InvalidateDesktopArrangement();
            Settings.Folders.Add(folder);
            if (!IsTest) { var w = new FolderWidget(this, folder); Widgets.Add(folder.Id, w); w.Show(); Native.LowerToDesktop(w); ApplyReplacement(folder); }
            SaveSoon(); Manager.Refresh(); if (!IsTest) ArrangeDesktop(Widgets[folder.Id]);
        }
        internal void Edit(FolderSettings folder)
        {
            ShowManager(); var editor = new FolderEditor(folder, Manager, false, Preferences);
            if (editor.ShowDialog() != true) return;
            if (!IsTest) ApplyReplacement(folder);
            FolderWidget w; if (Widgets.TryGetValue(folder.Id, out w)) w.Refresh();
            SaveSoon(); Manager.Refresh();
        }
        internal void Remove(FolderSettings folder)
        {
            InvalidateDesktopArrangement();
            if (!IsTest) { try { Replacement.RestoreOne(folder.Path); } catch (Exception ex) { Ui.Error(Manager, ex.Message); return; } }
            FolderWidget w;
            if (Widgets.TryGetValue(folder.Id, out w)) { w.Dispose(); Widgets.Remove(folder.Id); }
            Settings.Folders.Remove(folder); SaveSoon(); Manager.Refresh();
        }
        private void ApplyReplacement(FolderSettings folder)
        {
            try { Replacement.Apply(folder); }
            catch (Exception ex) { folder.ReplaceDesktopIcon = false; Ui.Error(Manager, "The folder was added as a normal expanding view.\n\n" + ex.Message); }
        }
        internal async System.Threading.Tasks.Task<int> AddDroppedItems(string target, string[] sources)
        {
            int count = 0; var failures = new List<string>();
            foreach (string source in sources)
            {
                try { string added = await ShellWorker.Run(() => ShortcutDrop.Add(source, target)); if (added != null) count++; }
                catch (Exception ex) { failures.Add(Path.GetFileName(source) + ": " + ex.Message); }
            }
            if (failures.Count > 0) Ui.Error(Manager, "Some items could not be added:\n\n" + String.Join("\n", failures));
            return count;
        }
        internal void SetColor(FolderSettings folder, string color)
        {
            folder.Color = color; FolderWidget w; if (Widgets.TryGetValue(folder.Id, out w)) w.Refresh(); SaveSoon(); Manager.Refresh();
        }
        internal void SaveSoon() { if (IsTest) return; saveTimer.Stop(); saveTimer.Start(); }
        internal void SaveNow()
        {
            try { Store.Save(Settings); saveErrorShown = false; }
            catch (Exception ex) { if (!saveErrorShown) { saveErrorShown = true; Ui.Error(Manager, "Your latest layout could not be saved.\n\n" + ex.Message); } }
        }
        internal void OpenPath(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Directory.Exists(path) ? path : Path.GetDirectoryName(path) }); }
            catch (Exception ex) { Ui.Error(Manager, "Windows could not open this item.\n\n" + ex.Message); }
        }
        internal void Quit()
        {
            try { Replacement.RestoreAll(); } catch (Exception ex) { Ui.Error(Manager, ex.Message); return; }
            Quitting = true; InvalidateDesktopArrangement(); saveTimer.Stop(); desktopTimer.Stop(); SaveNow();
            foreach (var w in Widgets.Values.ToList()) w.Dispose();
            if (preferencesWindow != null) preferencesWindow.Close();
            if (tray != null) { var icon = tray.Icon; tray.Visible = false; tray.Dispose(); if (icon != null) icon.Dispose(); }
            Manager.Close(); App.Shutdown();
        }
    }
}
