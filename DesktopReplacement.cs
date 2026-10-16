using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace FolderBloom
{
    public sealed class ReplacementRecord
    {
        public string Path { get; set; }
        public bool WasHidden { get; set; }
    }
    public sealed class ReplacementJournal
    {
        public string Owner { get; set; }
        public List<ReplacementRecord> Folders { get; set; }
        public ReplacementJournal() { Folders = new List<ReplacementRecord>(); }
    }
    internal sealed class DesktopReplacement : IDisposable
    {
        private readonly string file, owner, allowedTestDesktop;
        private readonly Mutex gate;
        private bool guardianStarted;
        internal DesktopReplacement(string dataDirectory, string testDesktop)
        {
            Directory.CreateDirectory(dataDirectory); file = Path.Combine(dataDirectory, "desktop-recovery.json"); allowedTestDesktop = testDesktop;
            owner = Process.GetCurrentProcess().Id + ":" + Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
            using (var sha = SHA256.Create()) gate = new Mutex(false, "Local\\FolderBloom.Recovery." + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(file).ToUpperInvariant()))).Replace("-", "").Substring(0, 24));
        }
        internal static bool IsDesktopFolder(string path)
        {
            try
            {
                string parent = Path.GetDirectoryName(Path.GetFullPath(path).TrimEnd('\\'));
                return String.Equals(parent, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), StringComparison.OrdinalIgnoreCase) || String.Equals(parent, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        private void Locked(Action action)
        {
            bool locked = false;
            try { try { locked = gate.WaitOne(TimeSpan.FromSeconds(10)); } catch (AbandonedMutexException) { locked = true; } if (!locked) throw new IOException("Folder recovery is busy. Please try again."); action(); }
            finally { if (locked) gate.ReleaseMutex(); }
        }
        private ReplacementJournal Read()
        {
            if (!File.Exists(file)) return new ReplacementJournal();
            var value = new JavaScriptSerializer().Deserialize<ReplacementJournal>(File.ReadAllText(file));
            if (value == null || value.Folders == null) throw new InvalidDataException("Folder recovery information could not be read. The original folders were left unchanged.");
            return value;
        }
        private void Write(ReplacementJournal journal)
        {
            File.WriteAllText(file + ".tmp", new JavaScriptSerializer().Serialize(journal), new UTF8Encoding(false));
            if (File.Exists(file)) File.Replace(file + ".tmp", file, file + ".bak"); else File.Move(file + ".tmp", file);
        }
        private static bool OwnerAlive(string identity)
        {
            try { string[] parts = identity.Split(':'); using (var process = Process.GetProcessById(Int32.Parse(parts[0]))) return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == Int64.Parse(parts[1]); }
            catch { return false; }
        }
        internal void RecoverPrevious()
        {
            Locked(delegate
            {
                var journal = Read(); if (journal.Owner == owner || !OwnerAlive(journal.Owner)) Restore(journal, null);
                else throw new IOException("Another FolderBloom process is still managing these desktop folders.");
            });
        }
        internal void EnsureGuardian(bool forceTest)
        {
            if (guardianStarted || allowedTestDesktop != null && !forceTest) return;
            // A killed helper cannot run after a power loss. Register independent next-sign-in recovery before hiding any real folder.
            if (allowedTestDesktop == null) StartupIntegration.EnsureRecoveryShortcut(Path.GetDirectoryName(file), Environment.GetFolderPath(Environment.SpecialFolder.Startup));
            string arguments = "--restore-guard \"" + Path.GetDirectoryName(file) + "\" " + owner;
            using (var process = Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, arguments) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
            {
                if (process == null) throw new IOException("Could not start desktop folder recovery.");
            }
            guardianStarted = true;
        }
        internal void Apply(FolderSettings folder)
        {
            if (!folder.ReplaceDesktopIcon) { RestoreOne(folder.Path); return; }
            bool eligible = IsDesktopFolder(folder.Path) || allowedTestDesktop != null && String.Equals(Path.GetDirectoryName(Path.GetFullPath(folder.Path)), Path.GetFullPath(allowedTestDesktop), StringComparison.OrdinalIgnoreCase);
            if (!eligible) throw new InvalidOperationException("Replacement is available for folders directly on your Desktop. Other folders can still be added as expanding views.");
            if ((File.GetAttributes(folder.Path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Desktop links and junctions cannot be replaced. Select a normal desktop folder.");
            EnsureGuardian(false);
            Locked(delegate
            {
                var journal = Read();
                if (journal.Owner != owner && journal.Folders.Count > 0) throw new IOException("Recover the previous desktop folder session before replacing another icon.");
                journal.Owner = owner;
                if (!journal.Folders.Any(f => String.Equals(f.Path, folder.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    journal.Folders.Add(new ReplacementRecord { Path = folder.Path, WasHidden = (File.GetAttributes(folder.Path) & FileAttributes.Hidden) != 0 });
                    Write(journal); // Recovery is durable before any folder attributes change.
                }
                File.SetAttributes(folder.Path, File.GetAttributes(folder.Path) | FileAttributes.Hidden); Native.NotifyAttributes(folder.Path);
            });
        }
        private void Restore(ReplacementJournal journal, string onlyPath)
        {
            var errors = new List<string>();
            foreach (var record in journal.Folders.ToList())
            {
                if (onlyPath != null && !String.Equals(record.Path, onlyPath, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    if (Directory.Exists(record.Path))
                    {
                        var attributes = File.GetAttributes(record.Path);
                        File.SetAttributes(record.Path, record.WasHidden ? attributes | FileAttributes.Hidden : attributes & ~FileAttributes.Hidden); Native.NotifyAttributes(record.Path);
                    }
                    journal.Folders.Remove(record);
                }
                catch (Exception ex) { errors.Add(record.Path + ": " + ex.Message); }
            }
            Write(journal); if (errors.Count > 0) throw new IOException("Some original folder icons could not be restored:\n" + String.Join("\n", errors));
        }
        internal void RestoreOne(string path) { Locked(delegate { var journal = Read(); if (journal.Owner == owner || !OwnerAlive(journal.Owner)) Restore(journal, path); }); }
        internal void RestoreAll() { Locked(delegate { var journal = Read(); if (journal.Owner == owner || !OwnerAlive(journal.Owner)) Restore(journal, null); }); }
        internal static int RecoverAtSignIn(string directory)
        {
            try
            {
                using (var recovery = new DesktopReplacement(directory, null))
                {
                    recovery.Locked(delegate
                    {
                        var journal = recovery.Read();
                        // The normal app may already be active. Never unhide originals belonging to a live session.
                        if (OwnerAlive(journal.Owner)) { StartupLog.Write(directory, "Folder recovery skipped; FolderBloom is running."); return; }
                        int count = journal.Folders.Count; recovery.Restore(journal, null);
                        StartupLog.Write(directory, "Sign-in recovery restored " + count + " original desktop folders.");
                    });
                }
                return 0;
            }
            catch (Exception ex) { StartupLog.Write(directory, "Sign-in folder recovery failed: " + ex); return 1; }
        }
        internal static int Guard(string directory, string watchedOwner)
        {
            try
            {
                string[] parts = watchedOwner.Split(':');
                try { using (var process = Process.GetProcessById(Int32.Parse(parts[0]))) { if (process.StartTime.ToUniversalTime().Ticks == Int64.Parse(parts[1])) process.WaitForExit(); } } catch (ArgumentException) { }
                using (var recovery = new DesktopReplacement(directory, null))
                {
                    recovery.Locked(delegate { var journal = recovery.Read(); if (journal.Owner == watchedOwner) recovery.Restore(journal, null); });
                }
                return 0;
            }
            catch (Exception ex) { try { File.WriteAllText(Path.Combine(directory, "recovery-error.txt"), ex.Message); } catch { } return 1; }
        }
        public void Dispose() { gate.Dispose(); }
    }
}
