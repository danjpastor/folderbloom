using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FolderBloom
{
    // Independent of popup navigation: a desktop preview always describes its root folder.
    internal sealed class FolderPreview : Viewbox, IDisposable
    {
        internal const int CycleSeconds = 4;
        internal readonly Image FolderIcon;
        private string path;
        private readonly Canvas overlay;
        private readonly Image[] images = new Image[4];
        private readonly DispatcherTimer timer, refreshTimer;
        private List<FolderEntry> entries = new List<FolderEntry>();
        private FileSystemWatcher watcher;
        private bool enabled, cycle, animate, disposed, refreshAgain, ticking;
        private Task readTask;
        private int first, readVersion, imageVersion;
        private DateTime lastRead;
        internal string[] DisplayedPaths { get; private set; }
        internal int EntryCount { get { return entries.Count; } }
        internal int VisibleIconCount { get { return images.Count(i => i.Visibility == Visibility.Visible); } }
        internal bool IsWatching { get { return watcher != null; } }
        internal bool IsRunning { get { return timer.IsEnabled || refreshTimer.IsEnabled; } }

        internal FolderPreview(string path, string color, double size)
        {
            this.path = path; Width = Height = size; Stretch = Stretch.Uniform; IsHitTestVisible = false;
            DisplayedPaths = new string[0];
            var layers = new Grid { Width = 48, Height = 48 };
            FolderIcon = Ui.FolderImage(color, 48); layers.Children.Add(FolderIcon);
            overlay = new Canvas { Width = 48, Height = 48, IsHitTestVisible = false };
            for (int i = 0; i < images.Length; i++)
            {
                var icon = new Image { Width = 14, Height = 14, Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed,
                    Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 2, ShadowDepth = 0.5, Opacity = 0.55 } };
                RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
                Canvas.SetLeft(icon, 9 + i % 2 * 15); Canvas.SetTop(icon, 11 + i / 2 * 14);
                overlay.Children.Add(icon); images[i] = icon;
            }
            layers.Children.Add(overlay); Child = layers;
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(CycleSeconds) };
            timer.Tick += async delegate
            {
                if (ticking) return; ticking = true;
                try
                {
                    // Watchers cover normal changes; polling also recovers unavailable/network folders.
                    if (watcher == null || DateTime.UtcNow - lastRead >= TimeSpan.FromSeconds(15)) await RefreshAsync();
                    await AdvanceAsync();
                }
                finally { ticking = false; }
            };
            refreshTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(180) };
            refreshTimer.Tick += async delegate { refreshTimer.Stop(); await RefreshAsync(); };
        }
        internal async Task ConfigureAsync(bool show, bool rotate, bool motion)
        {
            if (disposed) return;
            bool wasEnabled = enabled, wasCycling = cycle;
            enabled = show; cycle = rotate; animate = motion;
            if (!show)
            {
                readVersion++; imageVersion++; refreshAgain = false; timer.Stop(); refreshTimer.Stop(); StopWatching();
                entries.Clear(); first = 0; ClearImages(); return;
            }
            if (!wasEnabled) { first = 0; timer.Start(); await RefreshAsync(); }
            else if (!rotate && wasCycling) { first = 0; await ShowPageAsync(false); }
        }
        internal Task RefreshAsync()
        {
            if (disposed || !enabled) return Task.FromResult(false);
            // A slow folder must not spawn another read on every timer tick.
            if (readTask != null && !readTask.IsCompleted) { refreshAgain = true; return readTask; }
            refreshAgain = false; return readTask = ReadAsync();
        }
        internal void ChangePath(string next)
        {
            if (String.Equals(path, next, StringComparison.Ordinal)) return;
            path = next; readVersion++; imageVersion++; StopWatching(); entries.Clear(); first = 0; ClearImages();
            if (enabled) QueueRefresh();
        }
        private async Task ReadAsync()
        {
            if (disposed || !enabled) return;
            int version = ++readVersion; lastRead = DateTime.UtcNow; StartWatching();
            try
            {
                List<FolderEntry> result;
                try { result = await FolderReader.ReadAsync(path); }
                catch
                {
                    if (disposed || !enabled || version != readVersion) return;
                    entries.Clear(); first = 0; imageVersion++; ClearImages(); StopWatching(); return;
                }
                if (disposed || !enabled || version != readVersion) return;
                bool same = entries.Count == result.Count && entries.Zip(result, (a, b) => String.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase) && a.IsDirectory == b.IsDirectory).All(v => v);
                entries = result; if (!same || !cycle) first = 0;
                await ShowPageAsync(false);
            }
            finally { if (refreshAgain && enabled && !disposed) QueueRefresh(); }
        }
        internal async Task AdvanceAsync()
        {
            if (disposed || !enabled || !cycle || entries.Count <= 4) return;
            first = (first + 4) % entries.Count;
            await ShowPageAsync(true);
        }
        private async Task ShowPageAsync(bool transition)
        {
            int version = ++imageVersion;
            var page = Enumerable.Range(0, Math.Min(4, entries.Count)).Select(i => entries[(first + i) % entries.Count]).ToArray();
            var loaded = await Task.WhenAll(page.Select(LoadIconAsync));
            if (disposed || !enabled || version != imageVersion) return;
            // Swap the entire group together, after loading, so there are no blank or mixed pages.
            for (int i = 0; i < images.Length; i++)
            {
                images[i].Source = i < loaded.Length ? loaded[i] : null;
                images[i].Visibility = i < loaded.Length ? Visibility.Visible : Visibility.Collapsed;
            }
            DisplayedPaths = page.Select(e => e.Path).ToArray();
            overlay.BeginAnimation(OpacityProperty, null); overlay.Opacity = 1;
            if (transition && animate && SystemParameters.ClientAreaAnimation)
                overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0.45, 1, TimeSpan.FromMilliseconds(140)) { FillBehavior = FillBehavior.Stop });
        }
        private static async Task<BitmapSource> LoadIconAsync(FolderEntry entry)
        {
            try { return await ShellWorker.Icon(entry.Path); }
            catch { return entry.IsDirectory ? Icons.Folder("default") : Icons.GenericFile; }
        }
        private void ClearImages()
        {
            overlay.BeginAnimation(OpacityProperty, null); overlay.Opacity = 1; DisplayedPaths = new string[0];
            foreach (var image in images) { image.Source = null; image.Visibility = Visibility.Collapsed; }
        }
        internal void QueueRefresh()
        {
            if (disposed || !enabled) return; refreshTimer.Stop(); refreshTimer.Start();
        }
        private void DispatchRefresh(bool resetWatcher)
        {
            if (Dispatcher.HasShutdownStarted) return;
            Dispatcher.BeginInvoke((Action)delegate { if (disposed || !enabled) return; if (resetWatcher) StopWatching(); QueueRefresh(); });
        }
        private void StartWatching()
        {
            if (watcher != null) return;
            try
            {
                watcher = new FileSystemWatcher(path) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Attributes };
                FileSystemEventHandler changed = delegate(object sender, FileSystemEventArgs e) { ShellWorker.InvalidateIcon(e.FullPath); DispatchRefresh(false); };
                watcher.Created += changed; watcher.Deleted += changed; watcher.Changed += changed;
                watcher.Renamed += delegate(object sender, RenamedEventArgs e) { ShellWorker.InvalidateIcon(e.OldFullPath); ShellWorker.InvalidateIcon(e.FullPath); DispatchRefresh(false); };
                watcher.Error += delegate { DispatchRefresh(true); }; watcher.EnableRaisingEvents = true;
            }
            catch { StopWatching(); }
        }
        private void StopWatching() { if (watcher != null) { watcher.Dispose(); watcher = null; } }
        public void Dispose()
        {
            if (disposed) return; disposed = true; readVersion++; imageVersion++; timer.Stop(); refreshTimer.Stop(); StopWatching(); entries.Clear(); ClearImages();
        }
    }
}
