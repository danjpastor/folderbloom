using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace FolderBloom
{
    internal static class DesktopArrangement
    {
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; internal NativePoint(Point p) { X = (int)Math.Round(p.X); Y = (int)Math.Round(p.Y); } }
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);
        [DllImport("shell32.dll")] private static extern int SHCreateItemWithParent(IntPtr parentId, IShellFolder parent, IntPtr childId, ref Guid iid, out IShellItem item);
        [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellFolder { void ParseDisplayName(); }
        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem { void BindToHandler(); void GetParent(); void GetDisplayName(uint kind, out IntPtr name); }
        [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
        private interface IShellWindows
        {
            void Count(); void Item(); void NewEnum(); void Register(); void RegisterPending(); void Revoke(); void OnNavigate(); void OnActivated();
            [return: MarshalAs(UnmanagedType.IDispatch)] object FindWindowSW([In] ref object location, [In] ref object root, int windowClass, out int hwnd, int options);
        }
        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IServiceProvider { void QueryService(ref Guid service, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result); }
        [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellBrowser
        {
            void GetWindow(out IntPtr hwnd); void ContextSensitiveHelp(); void InsertMenusSB(); void SetMenuSB(); void RemoveMenusSB(); void SetStatusTextSB(); void EnableModelessSB(); void TranslateAcceleratorSB(); void BrowseObject(); void GetViewStateStream(); void GetControlWindow(); void SendControlMsg();
            void QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out object view);
        }
        [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellViewWindow { void GetWindow(out IntPtr hwnd); void ContextSensitiveHelp(); }
        [ComImport, Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFolderView
        {
            void GetCurrentViewMode(); void SetCurrentViewMode(); void GetFolder(ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object folder);
            void Item(int index, out IntPtr pidl); void ItemCount(uint flags, out int count); void Items(); void GetSelectionMarkedItem(); void GetFocusedItem();
            void GetItemPosition(IntPtr pidl, out NativePoint point); void GetSpacing(out NativePoint spacing); void GetDefaultSpacing();
            [PreserveSig] int GetAutoArrange(); void SelectItem();
            void SelectAndPositionItems(uint count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] pidls, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] NativePoint[] positions, uint flags);
        }
        private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        // Reacquire the documented Shell desktop view each pass, including after Explorer restarts.
        private sealed class Session : IDisposable
        {
            private object windows, desktop, browser, shellView, folder;
            private IFolderView view;
            private readonly List<IntPtr> pidls = new List<IntPtr>();
            private NativePoint origin;
            private double scale;
            private Point iconInset;
            internal readonly DesktopLayout Layout = new DesktopLayout();
            internal Session()
            {
                try
                {
                    windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));
                    object location = 0, root = null; int unused;
                    desktop = ((IShellWindows)windows).FindWindowSW(ref location, ref root, 8, out unused, 1);
                    if (desktop == null) throw new InvalidOperationException("The Windows desktop is unavailable.");
                    var service = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"); var browserId = typeof(IShellBrowser).GUID;
                    ((IServiceProvider)desktop).QueryService(ref service, ref browserId, out browser); ((IShellBrowser)browser).QueryActiveShellView(out shellView);
                    view = (IFolderView)shellView;
                    int arranged = view.GetAutoArrange(); Marshal.ThrowExceptionForHR(arranged); Layout.AutoArrange = arranged == 0;
                    IntPtr hwnd; ((IShellViewWindow)shellView).GetWindow(out hwnd);
                    if (!ClientToScreen(hwnd, ref origin)) throw new InvalidOperationException("The desktop position is unavailable.");
                    NativePoint spacing; view.GetSpacing(out spacing); if (spacing.X < 8 || spacing.Y < 8) throw new InvalidOperationException("Windows returned an invalid icon spacing.");
                    scale = Native.Scale; Layout.Spacing = new Size(spacing.X / scale, spacing.Y / scale);
                    // Shell positions refer to the icon image. Layout plans use the enclosing
                    // grid cell, so align the image's center and its two-pixel top inset.
                    iconInset = new Point((Layout.Spacing.Width - Native.DesktopIconSize) / 2, 2 / scale);
                    var folderId = new Guid("000214E6-0000-0000-C000-000000000046"); view.GetFolder(ref folderId, out folder);
                    int count; view.ItemCount(2, out count);
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr pidl; view.Item(i, out pidl); pidls.Add(pidl); NativePoint point; view.GetItemPosition(pidl, out point);
                        Layout.Items.Add(new DesktopItem { Index = i, Path = ItemPath(pidl), Position = new Point((point.X + origin.X) / scale - iconInset.X, (point.Y + origin.Y) / scale - iconInset.Y) });
                    }
                    foreach (var screen in Forms.Screen.AllScreens)
                    {
                        var area = screen.WorkingArea; var bounds = new Rect(area.Left / scale, area.Top / scale, area.Width / scale, area.Height / scale);
                        var points = Layout.Items.Where(i => bounds.Contains(i.Position)).Select(i => i.Position).ToList();
                        double x = DesktopGrid.InferPhase(points.Select(p => p.X - bounds.Left), Layout.Spacing.Width, 0);
                        double y = DesktopGrid.InferPhase(points.Select(p => p.Y - bounds.Top), Layout.Spacing.Height, 0);
                        Layout.Regions.Add(new DesktopGridRegion { Bounds = bounds, Origin = new Point(bounds.Left + x, bounds.Top + y) });
                    }
                }
                catch { Dispose(); throw; }
            }
            private string ItemPath(IntPtr pidl)
            {
                IShellItem item = null; IntPtr text = IntPtr.Zero;
                try
                {
                    var iid = typeof(IShellItem).GUID; Marshal.ThrowExceptionForHR(SHCreateItemWithParent(IntPtr.Zero, (IShellFolder)folder, pidl, ref iid, out item));
                    item.GetDisplayName(0x80058000, out text); return Marshal.PtrToStringUni(text);
                }
                catch (COMException) { return null; } // Recycle Bin and other virtual desktop items.
                catch (ArgumentException) { return null; }
                finally { if (text != IntPtr.Zero) Marshal.FreeCoTaskMem(text); Release(item); }
            }
            internal void Apply(IDictionary<int, Point> moves)
            {
                if (moves.Count == 0) return;
                if (view.GetAutoArrange() != 1) throw new InvalidOperationException("Windows Auto arrange icons prevents individual shortcut placement.");
                var ordered = moves.ToArray();
                view.SelectAndPositionItems((uint)ordered.Length, ordered.Select(m => pidls[m.Key]).ToArray(), ordered.Select(m => new NativePoint(new Point((m.Value.X + iconInset.X) * scale - origin.X, (m.Value.Y + iconInset.Y) * scale - origin.Y))).ToArray(), 0x80);
            }
            public void Dispose()
            {
                foreach (var pidl in pidls) if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); pidls.Clear();
                Release(folder); Release(shellView); Release(browser); Release(desktop); Release(windows);
                folder = shellView = browser = desktop = windows = null;
            }
        }
        internal static DesktopLayout Read() { using (var session = new Session()) return session.Layout; }
        internal static void PositionTestShortcut(string path, Point position)
        {
            string name = Path.GetFileName(path);
            if (!name.StartsWith(".folderbloom-grid-check-", StringComparison.Ordinal) || !name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || !String.Equals(Path.GetDirectoryName(path), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a temporary FolderBloom desktop test shortcut can be positioned by this check.");
            using (var session = new Session())
            {
                var item = session.Layout.Items.Single(i => String.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase));
                session.Apply(new Dictionary<int, Point> { { item.Index, position } });
            }
        }
        internal static DesktopPlan Arrange(IList<DesktopFolder> folders, bool snap, bool moveShortcuts, Func<bool> stillCurrent)
        {
            if (!stillCurrent()) return null;
            using (var session = new Session())
            {
                var plan = DesktopGrid.Plan(session.Layout, folders, snap, moveShortcuts);
                if (!plan.HasRoom) throw new InvalidOperationException("There is no free desktop grid space for the overlapping icons.");
                if (!stillCurrent() || Native.PointerButtonDown) return null;
                session.Apply(plan.IconMoves); return plan;
            }
        }
    }
}
