using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace FolderBloom
{
    internal sealed class ShellRequest
    {
        public string Action { get; set; }
        public string Path { get; set; }
        internal bool Valid { get { return (Action == "new" || Action == "add") && !String.IsNullOrWhiteSpace(Path) && Path.Length < 32000 && System.IO.Path.IsPathRooted(Path); } }
        internal static ShellRequest Parse(string[] args)
        {
            if (args.Length == 1 && args[0] == "--new-desktop-folder") return new ShellRequest { Action = "new", Path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
            if (args.Length >= 2 && (args[0] == "--new-folder" || args[0] == "--add-folder")) return new ShellRequest { Action = args[0] == "--new-folder" ? "new" : "add", Path = args[1] };
            return null;
        }
    }
    internal static class ExplorerIntegration
    {
        internal static string Command(string executable, string action, string path) { return "\"" + executable + "\" " + action + (path == null ? "" : " \"" + path + "\""); }
        internal static void Register()
        {
            string executable = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            AddVerb(@"Directory\Background\shell\FolderBloom.New", "New FolderBloom folder", Command(executable, "--new-folder", @"%V\."), executable);
            AddVerb(@"DesktopBackground\Shell\FolderBloom.New", "New FolderBloom folder", Command(executable, "--new-desktop-folder", null), executable);
            AddVerb(@"Directory\shell\FolderBloom.Add", "Use as FolderBloom folder", Command(executable, "--add-folder", "%1"), executable);
        }
        private static void AddVerb(string path, string label, string command, string executable)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + path))
            {
                key.SetValue("", label); key.SetValue("Icon", "\"" + executable + "\",0"); key.SetValue("MultiSelectModel", "Single");
                using (var action = key.CreateSubKey("command")) action.SetValue("", command);
            }
        }
    }
    // Same-user IPC forwards Explorer commands to the existing UI instead of starting a second folder session.
    internal sealed class ShellCommandServer : IDisposable
    {
        internal static string PipeName { get { return "FolderBloom.Commands." + StartupIntegration.UserSid; } }
        private readonly NamedPipeServerStream pipe;
        private bool disposed;
        internal ShellCommandServer(string name, Action<ShellRequest> received)
        {
            var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(StartupIntegration.UserSid), PipeAccessRights.FullControl, AccessControlType.Allow));
            pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536, security);
            Listen(received);
        }
        private async void Listen(Action<ShellRequest> received)
        {
            while (!disposed)
            {
                try
                {
                    await pipe.WaitForConnectionAsync().ConfigureAwait(false);
                    var bytes = new byte[4]; await ReadBytes(pipe, bytes).ConfigureAwait(false); int count = BitConverter.ToInt32(bytes, 0);
                    if (count < 1 || count > 65536) throw new InvalidDataException("Invalid Explorer request size.");
                    bytes = new byte[count]; await ReadBytes(pipe, bytes).ConfigureAwait(false);
                    var request = new JavaScriptSerializer().Deserialize<ShellRequest>(Encoding.UTF8.GetString(bytes));
                    if (request == null || !request.Valid) throw new InvalidDataException("Invalid Explorer request.");
                    received(request); await pipe.WriteAsync(new byte[] { 1 }, 0, 1).ConfigureAwait(false);
                    // Keep the pipe connected until the client has consumed the acknowledgement.
                    await ReadBytes(pipe, new byte[1]).ConfigureAwait(false);
                }
                catch (Exception) { if (disposed) return; }
                finally { try { if (!disposed && pipe.IsConnected) pipe.Disconnect(); } catch (ObjectDisposedException) { } catch (IOException) { } }
            }
        }
        private static async Task ReadBytes(Stream stream, byte[] bytes)
        {
            int offset = 0;
            while (offset < bytes.Length)
            {
                var read = stream.ReadAsync(bytes, offset, bytes.Length - offset);
                if (await Task.WhenAny(read, Task.Delay(3000)).ConfigureAwait(false) != read) throw new IOException("Explorer request timed out.");
                int count = await read.ConfigureAwait(false); if (count == 0) throw new EndOfStreamException(); offset += count;
            }
        }
        internal static bool Send(string name, ShellRequest request)
        {
            if (request == null || !request.Valid) return false;
            try
            {
                using (var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
                {
                    client.Connect(5000); byte[] bytes = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(request));
                    if (bytes.Length > 65536) return false;
                    byte[] size = BitConverter.GetBytes(bytes.Length); client.Write(size, 0, 4); client.Write(bytes, 0, bytes.Length); client.Flush();
                    var response = new byte[1]; ReadBytes(client, response).GetAwaiter().GetResult(); client.WriteByte(1); client.Flush(); return response[0] == 1;
                }
            }
            catch { return false; }
        }
        public void Dispose() { disposed = true; pipe.Dispose(); }
    }
    internal static class FolderNames
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateDirectory(string path, IntPtr security);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string source, string destination, uint flags);
        internal static void Rename(string source, string destination)
        {
            // No replace or cross-volume-copy flags: an existing destination is never overwritten.
            if (!MoveFileEx(source, destination, 0)) throw new IOException(new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error()).Message);
        }
        internal static void Create(string path)
        {
            if (!CreateDirectory(path, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }
        internal static string Validate(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.Length > 255 || name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".") || name.EndsWith(" ")) throw new IOException("Enter a valid folder name without slashes, special characters, or a trailing dot or space.");
            string first = name.Split('.')[0].ToUpperInvariant();
            if (first == "CON" || first == "PRN" || first == "AUX" || first == "NUL" || System.Text.RegularExpressions.Regex.IsMatch(first, @"^(COM|LPT)[1-9¹²³]$")) throw new IOException("That name is reserved by Windows. Choose another name.");
            return name;
        }
        internal static string Destination(string current, string name)
        {
            Validate(name); string full = System.IO.Path.GetFullPath(current).TrimEnd('\\'); string parent = System.IO.Path.GetDirectoryName(full);
            if (String.IsNullOrEmpty(parent) || !Directory.Exists(full)) throw new IOException("This folder cannot be renamed here.");
            if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new IOException("Rename linked or cloud-placeholder folders in Explorer.");
            string destination = System.IO.Path.Combine(parent, name);
            if (!String.Equals(full, destination, StringComparison.OrdinalIgnoreCase) && (File.Exists(destination) || Directory.Exists(destination))) throw new IOException("A file or folder with that name already exists.");
            return destination;
        }
    }
}
