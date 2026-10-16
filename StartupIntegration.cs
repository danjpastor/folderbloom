using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32;

namespace FolderBloom
{
    internal static class StartupIntegration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static string UserSid { get { using (var identity = WindowsIdentity.GetCurrent()) return identity.User.Value; } }
        internal static string TaskName { get { return "FolderBloom-" + UserSid; } }
        internal static string Arguments(string dataDirectory) { return "--data-dir \"" + Path.GetFullPath(dataDirectory).TrimEnd('\\') + "\" --quiet --startup"; }
        internal static string Command(string executable, string dataDirectory) { return "\"" + executable + "\" " + Arguments(dataDirectory); }
        internal static string Definition(string executable, string dataDirectory, string userSid)
        {
            executable = Path.GetFullPath(executable);
            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                "<RegistrationInfo><Description>Starts FolderBloom when you sign in to Windows.</Description></RegistrationInfo>" +
                "<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + SecurityElement.Escape(userSid) + "</UserId><Delay>PT10S</Delay></LogonTrigger></Triggers>" +
                "<Principals><Principal id=\"User\"><UserId>" + SecurityElement.Escape(userSid) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>" +
                "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>" +
                "<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable>" +
                "<RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>" +
                "<AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden><RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun>" +
                "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority><RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure></Settings>" +
                "<Actions Context=\"User\"><Exec><Command>" + SecurityElement.Escape(executable) + "</Command><Arguments>" + SecurityElement.Escape(Arguments(dataDirectory)) +
                "</Arguments><WorkingDirectory>" + SecurityElement.Escape(Path.GetDirectoryName(executable)) + "</WorkingDirectory></Exec></Actions></Task>";
        }
        internal static void ValidateDefinition(string executable, string dataDirectory) { Register(executable, dataDirectory, true); }
        private static void Register(string executable, string dataDirectory, bool validateOnly)
        {
            object service = null, root = null, task = null;
            try
            {
                service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")); ((dynamic)service).Connect(); root = ((dynamic)service).GetFolder("\\");
                // TASK_VALIDATE_ONLY does not create a task; TASK_CREATE_OR_UPDATE uses the interactive user, never elevation or a stored password.
                task = ((dynamic)root).RegisterTask(TaskName + (validateOnly ? "-validation" : ""), Definition(executable, dataDirectory, UserSid), validateOnly ? 1 : 6, UserSid, null, 3, null);
                if (!validateOnly && !(bool)((dynamic)task).Enabled) throw new IOException("Windows did not enable FolderBloom startup.");
            }
            finally { Release(task); Release(root); Release(service); }
        }
        internal static void SetEnabled(bool enabled, string dataDirectory)
        {
            if (enabled) Register(Process.GetCurrentProcess().MainModule.FileName, dataDirectory, false);
            else
            {
                object service = null, root = null;
                try
                {
                    service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")); ((dynamic)service).Connect(); root = ((dynamic)service).GetFolder("\\");
                    try { ((dynamic)root).DeleteTask(TaskName, 0); }
                    catch (FileNotFoundException) { }
                    catch (COMException ex) { if (ex.ErrorCode != unchecked((int)0x80070002)) throw; }
                }
                finally { Release(root); Release(service); }
            }
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true)) if (key != null) key.DeleteValue("FolderBloom", false);
        }
        internal static void EnsureRecoveryShortcut(string dataDirectory, string startupDirectory)
        {
            Directory.CreateDirectory(startupDirectory);
            string path = Path.Combine(startupDirectory, "FolderBloom folder recovery.lnk");
            string temporary = Path.Combine(startupDirectory, "FolderBloom folder recovery.tmp.lnk");
            object shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); shortcut = ((dynamic)shell).CreateShortcut(temporary);
                string executable = Process.GetCurrentProcess().MainModule.FileName;
                ((dynamic)shortcut).TargetPath = executable;
                ((dynamic)shortcut).Arguments = "--recover-desktop \"" + Path.GetFullPath(dataDirectory).TrimEnd('\\') + "\"";
                ((dynamic)shortcut).WorkingDirectory = Path.GetDirectoryName(executable);
                ((dynamic)shortcut).Description = "Restores original desktop folders if FolderBloom did not start.";
                ((dynamic)shortcut).WindowStyle = 7; ((dynamic)shortcut).Save();
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { Release(shortcut); Release(shell); if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    }
    internal static class StartupLog
    {
        internal static void Write(string directory, string message)
        {
            try
            {
                Directory.CreateDirectory(directory); string path = Path.Combine(directory, "startup.log");
                if (File.Exists(path) && new FileInfo(path).Length > 65536) { File.Copy(path, path + ".previous", true); File.WriteAllText(path, ""); }
                File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " [" + Process.GetCurrentProcess().Id + "] " + message + Environment.NewLine);
            }
            catch { /* Logging must never prevent startup or folder recovery. */ }
        }
    }
}
