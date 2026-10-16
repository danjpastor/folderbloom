using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace FolderBloom
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct StockInfo { public uint Size; public IntPtr Icon; public int ImageIndex; public int IconIndex; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Path; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct FileInfo { public IntPtr Icon; public int IconIndex; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName; }
        [DllImport("shell32.dll")] internal static extern int SHGetStockIconInfo(int id, uint flags, ref StockInfo info);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr SHGetFileInfo(string path, uint attributes, ref FileInfo info, uint size, uint flags);
        [DllImport("shell32.dll")] private static extern int SHGetImageList(int size, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IImageList list);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X, Y; }
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint point);
        internal static Point CursorLocation { get { CursorPoint point; GetCursorPos(out point); double scale = Scale; return new Point(point.X / scale, point.Y / scale); } }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr hwnd, StringBuilder name, int length);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr data);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLong(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        internal static bool PointerButtonDown { get { return (GetAsyncKeyState(1) & 0x8000) != 0 || (GetAsyncKeyState(2) & 0x8000) != 0; } }
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr dc, int index);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IImageFactory factory);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern void SHChangeNotify(uint eventId, uint flags, string item1, IntPtr item2);
        [StructLayout(LayoutKind.Sequential)] private struct ImageSize { public int Width, Height; }
        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IImageFactory { [PreserveSig] int GetImage(ImageSize size, uint flags, out IntPtr bitmap); }
        internal static void NotifyAttributes(string path) { SHChangeNotify(0x800, 0x5 | 0x2000, path, IntPtr.Zero); }
        internal static double DesktopIconSize
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Bags\1\Desktop"))
                    {
                        if (key != null) { object value = key.GetValue("IconSize"); if (value is int && (int)value >= 16 && (int)value <= 256) return (int)value; }
                    }
                }
                catch (System.Security.SecurityException) { }
                return 48; // Windows' standard medium desktop icon size, in device-independent pixels.
            }
        }
        internal static Size DesktopTileSize { get { double size = DesktopIconSize; return new Size(Math.Max(76, size + 20), size + 44); } }
        [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IImageList
        {
            [PreserveSig] int Add(IntPtr a, IntPtr b, out int c);
            [PreserveSig] int ReplaceIcon(int a, IntPtr b, out int c);
            [PreserveSig] int SetOverlayImage(int a, int b);
            [PreserveSig] int Replace(int a, IntPtr b, IntPtr c);
            [PreserveSig] int AddMasked(IntPtr a, uint b, out int c);
            [PreserveSig] int Draw(IntPtr a);
            [PreserveSig] int Remove(int a);
            [PreserveSig] int GetIcon(int index, int flags, out IntPtr icon);
        }
        internal static double Scale
        {
            get { var dc = GetDC(IntPtr.Zero); try { return GetDeviceCaps(dc, 88) / 96.0; } finally { ReleaseDC(IntPtr.Zero, dc); } }
        }
        internal static Rect WorkArea(Point point)
        {
            double scale = Scale;
            var area = Forms.Screen.FromPoint(new System.Drawing.Point((int)(point.X * scale), (int)(point.Y * scale))).WorkingArea;
            return new Rect(area.X / scale, area.Y / scale, area.Width / scale, area.Height / scale);
        }
        internal static Point KeepVisible(Point p, Size size) { return Geometry.Clamp(p, size, WorkArea(p)); }
        internal static BitmapSource StockFolder()
        {
            var stock = new StockInfo { Size = (uint)Marshal.SizeOf(typeof(StockInfo)) };
            Marshal.ThrowExceptionForHR(SHGetStockIconInfo(3, 0x100 | 0x4000, ref stock));
            IntPtr large = IntPtr.Zero; IImageList list = null;
            try
            {
                Guid iid = typeof(IImageList).GUID;
                if (SHGetImageList(4, ref iid, out list) == 0) list.GetIcon(stock.ImageIndex, 1, out large);
                return BitmapFromIcon(large != IntPtr.Zero ? large : stock.Icon);
            }
            finally
            {
                if (large != IntPtr.Zero) DestroyIcon(large);
                if (stock.Icon != IntPtr.Zero) DestroyIcon(stock.Icon);
                if (list != null) Marshal.ReleaseComObject(list);
            }
        }
        internal static BitmapSource BitmapFromIcon(IntPtr icon)
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); source.Freeze(); return source;
        }
        internal static BitmapSource FileIcon(string path)
        {
            IImageFactory factory = null; IntPtr bitmap = IntPtr.Zero;
            try
            {
                Guid iid = typeof(IImageFactory).GUID;
                if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory) == 0 && factory.GetImage(new ImageSize { Width = 64, Height = 64 }, 0x4 | 0x1, out bitmap) == 0 && bitmap != IntPtr.Zero)
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); source.Freeze();
                    if (HasVisiblePixels(source)) return source;
                }
            }
            catch (COMException) { }
            finally { if (bitmap != IntPtr.Zero) DeleteObject(bitmap); if (factory != null) Marshal.ReleaseComObject(factory); }
            var info = new FileInfo();
            if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(typeof(FileInfo)), 0x100 | 0x20) != IntPtr.Zero && info.Icon != IntPtr.Zero)
            {
                try { var source = BitmapFromIcon(info.Icon); if (HasVisiblePixels(source)) return source; } finally { DestroyIcon(info.Icon); }
            }
            info = new FileInfo();
            if (SHGetFileInfo(path, 0x80, ref info, (uint)Marshal.SizeOf(typeof(FileInfo)), 0x100 | 0x10) != IntPtr.Zero && info.Icon != IntPtr.Zero)
            { try { return BitmapFromIcon(info.Icon); } finally { DestroyIcon(info.Icon); } }
            return GenericFileIcon();
        }
        internal static BitmapSource GenericFileIcon()
        {
            var stock = new StockInfo { Size = (uint)Marshal.SizeOf(typeof(StockInfo)) };
            Marshal.ThrowExceptionForHR(SHGetStockIconInfo(0, 0x100, ref stock));
            try { return BitmapFromIcon(stock.Icon); } finally { if (stock.Icon != IntPtr.Zero) DestroyIcon(stock.Icon); }
        }
        internal static bool HasVisiblePixels(BitmapSource source)
        {
            var rgba = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); byte[] data = new byte[rgba.PixelWidth * rgba.PixelHeight * 4]; rgba.CopyPixels(data, rgba.PixelWidth * 4, 0);
            for (int i = 3; i < data.Length; i += 4) if (data[i] > 20) return true;
            return false;
        }
        internal static void ToolWindow(Window window, bool noActivate)
        {
            var handle = new WindowInteropHelper(window).Handle;
            long style = GetWindowLong(handle, -20).ToInt64();
            SetWindowLong(handle, -20, new IntPtr(((style & ~0x08000000L) | 0x80L | (noActivate ? 0x08000000L : 0)) & ~0x40000L));
        }
        internal static string ClassName(IntPtr hwnd) { var b = new StringBuilder(256); GetClassName(hwnd, b, b.Capacity); return b.ToString(); }
        internal static bool DesktopForeground()
        {
            string c = ClassName(GetForegroundWindow()); return c == "Progman" || c == "WorkerW";
        }
        // Keep a separate top-level window immediately above Explorer's desktop surface.
        // No Explorer injection, desktop file edits, or undocumented shell messages.
        internal static void LowerToDesktop(Window window)
        {
            if (!window.IsVisible) return;
            IntPtr desktop = IntPtr.Zero;
            EnumWindows(delegate(IntPtr hwnd, IntPtr data)
            {
                if (FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero) { desktop = hwnd; return false; }
                return true;
            }, IntPtr.Zero);
            if (desktop == IntPtr.Zero) return;
            var own = new WindowInteropHelper(window).Handle;
            var previous = GetWindow(desktop, 3);
            if (previous == own || previous == IntPtr.Zero) return;
            SetWindowPos(own, previous, 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002 | 0x0200);
        }
    }
    internal static class Icons
    {
        private static BitmapSource stock;
        private static BitmapSource genericFile;
        internal static BitmapSource GenericFile { get { if (genericFile == null) genericFile = Native.GenericFileIcon(); return genericFile; } }
        private static readonly Dictionary<string, BitmapSource> cache = new Dictionary<string, BitmapSource>();
        internal static BitmapSource Folder(string hex)
        {
            if (stock == null) stock = Native.StockFolder();
            if (String.IsNullOrEmpty(hex) || hex == "default") return stock;
            BitmapSource cached;
            if (cache.TryGetValue(hex, out cached)) return cached;
            Color color; try { color = (Color)ColorConverter.ConvertFromString(hex); } catch { return stock; }
            var source = new FormatConvertedBitmap(stock, PixelFormats.Bgra32, null, 0);
            int stride = source.PixelWidth * 4; byte[] pixels = new byte[stride * source.PixelHeight]; source.CopyPixels(pixels, stride, 0);
            double hue, saturation, value; ToHsv(color.R, color.G, color.B, out hue, out saturation, out value);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i + 3] == 0) continue;
                double h, s, v; ToHsv(pixels[i + 2], pixels[i + 1], pixels[i], out h, out s, out v);
                // Recolor the yellow folder material, preserving neutral highlights and shadows.
                if (s < 0.10 || h > 85 || h < 12) continue;
                var tinted = FromHsv(hue, Math.Min(1, s * saturation / 0.72), Math.Min(1, v * (0.55 + value * 0.45)));
                pixels[i] = tinted.B; pixels[i + 1] = tinted.G; pixels[i + 2] = tinted.R;
            }
            var result = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride); result.Freeze();
            if (cache.Count > 64) cache.Clear(); cache[hex] = result; return result;
        }
        private static void ToHsv(byte red, byte green, byte blue, out double h, out double s, out double v)
        {
            double r = red / 255.0, g = green / 255.0, b = blue / 255.0, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            v = max; s = max == 0 ? 0 : d / max; h = d == 0 ? 0 : max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4); if (h < 0) h += 360;
        }
        private static Color FromHsv(double h, double s, double v)
        {
            double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c, r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; } else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }
    }
}
