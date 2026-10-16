using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FolderBloom
{
    internal static class ShellWorker
    {
        private static readonly Dispatcher dispatcher;
        static ShellWorker()
        {
            var ready = new ManualResetEventSlim(); Dispatcher worker = null;
            var thread = new Thread(delegate()
            {
                worker = Dispatcher.CurrentDispatcher; ready.Set();
                int initialized = OleInitialize(IntPtr.Zero);
                try { Dispatcher.Run(); }
                finally { if (initialized >= 0) OleUninitialize(); }
            });
            thread.Name = "FolderBloom Windows Shell"; thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start(); ready.Wait(); dispatcher = worker; ready.Dispose();
        }
        [DllImport("ole32.dll")] private static extern int OleInitialize(IntPtr reserved);
        [DllImport("ole32.dll")] private static extern void OleUninitialize();
        internal static Task<T> Run<T>(Func<T> work)
        {
            var completion = new TaskCompletionSource<T>();
            dispatcher.BeginInvoke((Action)delegate { try { completion.SetResult(work()); } catch (Exception e) { completion.SetException(e); } });
            return completion.Task;
        }
        private sealed class CachedIcon { internal Task<BitmapSource> Task; internal DateTime Time; }
        private static readonly Dictionary<string, CachedIcon> icons = new Dictionary<string, CachedIcon>(StringComparer.OrdinalIgnoreCase);
        private static readonly object iconLock = new object();
        internal static void InvalidateIcon(string path) { lock (iconLock) { icons.Remove(path); } }
        internal static Task<BitmapSource> Icon(string path)
        {
            lock (iconLock)
            {
                CachedIcon cached;
                if (icons.TryGetValue(path, out cached) && !cached.Task.IsFaulted && DateTime.UtcNow - cached.Time < TimeSpan.FromSeconds(15)) return cached.Task;
                if (icons.Count >= 512) icons.Clear();
                var task = Run(() => Native.FileIcon(path)); icons[path] = new CachedIcon { Task = task, Time = DateTime.UtcNow }; return task;
            }
        }
    }
    internal static class ShortcutDrop
    {
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")] private class ShellLink { }
        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int max, IntPtr data, uint flags);
            void GetIDList(out IntPtr pidl); void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int max);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int max);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string path);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int max);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string text);
            void GetHotkey(out short hotkey); void SetHotkey(short hotkey); void GetShowCmd(out int cmd); void SetShowCmd(int cmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int max, out int index);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
            void Resolve(IntPtr hwnd, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }
        internal static bool IsShortcut(string path)
        {
            string ext = Path.GetExtension(path); return ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || ext.Equals(".url", StringComparison.OrdinalIgnoreCase) || ext.Equals(".appref-ms", StringComparison.OrdinalIgnoreCase) || ext.Equals(".website", StringComparison.OrdinalIgnoreCase);
        }
        internal static string Add(string source, string targetDirectory)
        {
            source = Path.GetFullPath(source); targetDirectory = Path.GetFullPath(targetDirectory);
            if (!Directory.Exists(targetDirectory)) throw new DirectoryNotFoundException("The destination folder is unavailable.");
            bool directory = Directory.Exists(source);
            if (!directory && !File.Exists(source)) throw new FileNotFoundException("The dropped item is unavailable.", source);
            if (String.Equals(Path.GetDirectoryName(source), targetDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return null;
            if (String.Equals(source.TrimEnd('\\'), targetDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return null;
            bool copy = !directory && IsShortcut(source);
            string filename = copy ? Path.GetFileName(source) : Path.GetFileName(source.TrimEnd('\\')) + ".lnk";
            string temporary = Path.Combine(targetDirectory, ".folderbloom-" + Guid.NewGuid().ToString("N") + (copy ? ".tmp" : ".lnk"));
            try
            {
                if (copy) File.Copy(source, temporary, false);
                else CreateLink(source, temporary);
                for (int i = 0; i < 10000; i++)
                {
                    string name = i == 0 ? filename : Path.GetFileNameWithoutExtension(filename) + " (" + (i + 1) + ")" + Path.GetExtension(filename);
                    string destination = Path.Combine(targetDirectory, name);
                    try { File.Move(temporary, destination); return destination; }
                    catch (IOException) { if (!File.Exists(destination) && !Directory.Exists(destination)) throw; }
                }
                throw new IOException("Too many shortcuts have the same name.");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        internal static void CreateLink(string target, string destination)
        {
            var link = (IShellLink)new ShellLink();
            try
            {
                link.SetPath(target); string working = Directory.Exists(target) ? target : Path.GetDirectoryName(target); if (!String.IsNullOrEmpty(working)) link.SetWorkingDirectory(working);
                ((IPersistFile)link).Save(destination, true);
            }
            finally { Marshal.FinalReleaseComObject(link); }
        }
        internal static string ReadLinkTarget(string path)
        {
            var link = (IShellLink)new ShellLink();
            try { ((IPersistFile)link).Load(path, 0); var result = new StringBuilder(32768); link.GetPath(result, result.Capacity, IntPtr.Zero, 4); return result.ToString(); }
            finally { Marshal.FinalReleaseComObject(link); }
        }
    }
}
