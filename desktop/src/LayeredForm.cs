using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CodexPet
{
    internal class LayeredForm : Form
    {
        internal static bool Inspectable;
        private Size presentedSize = System.Drawing.Size.Empty;
        private IntPtr presentedHandle = IntPtr.Zero;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams p = base.CreateParams;
                p.ExStyle |= 0x80000;
                if (!Inspectable) p.ExStyle |= 0x08000000 | 0x80;
                else { p.ExStyle &= ~(0x80 | 0x08000000); p.ExStyle |= 0x40000; }
                return p;
            }
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; public NativePoint(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] private struct NativeSize { public int X, Y; public NativeSize(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { public byte Operation, Flags, Alpha, Format; }
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", SetLastError = true)] private static extern bool UpdateLayeredWindowSized(IntPtr window, IntPtr screen, IntPtr position, ref NativeSize size, IntPtr source, ref NativePoint sourcePosition, int colorKey, ref Blend blend, int flags);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);

        internal Rectangle GetNativeBounds()
        {
            NativeRect bounds;
            if (!GetWindowRect(Handle, out bounds)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        }

        public void Present(Bitmap bitmap)
        {
            IntPtr window = Handle;
            bool resize = presentedHandle != window || presentedSize != bitmap.Size;
            if (resize && Size != bitmap.Size) Size = bitmap.Size;
            window = Handle;
            IntPtr screen = GetDC(IntPtr.Zero), memory = IntPtr.Zero, dib = IntPtr.Zero, previous = IntPtr.Zero;
            try
            {
                memory = CreateCompatibleDC(screen);
                dib = bitmap.GetHbitmap(Color.FromArgb(0));
                previous = SelectObject(memory, dib);
                NativePoint origin = new NativePoint(0, 0);
                NativeSize size = new NativeSize(bitmap.Width, bitmap.Height);
                Blend blend = new Blend { Operation = 0, Flags = 0, Alpha = 255, Format = 1 };
                // Location is owned by the form's explicit placement/drag code. NULL leaves the native
                // position unchanged and avoids feeding DPI-rounded Left/Top back into every paint.
                // Always describe the submitted source surface. Under Windows DPI virtualization,
                // NULL size can retain the compositor's scaled surface instead of refreshing it.
                bool updated = UpdateLayeredWindowSized(window, screen, IntPtr.Zero, ref size, memory, ref origin, 0, ref blend, 2);
                if (!updated)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                presentedHandle = window;
                presentedSize = bitmap.Size;
            }
            finally
            {
                if (previous != IntPtr.Zero) SelectObject(memory, previous);
                if (dib != IntPtr.Zero) DeleteObject(dib);
                if (memory != IntPtr.Zero) DeleteDC(memory);
                if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
            }
        }
    }
}
