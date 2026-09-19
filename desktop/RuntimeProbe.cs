using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Diagnostic host: uses the shipping form and real WinForms message loop, no injected mouse input.
internal static class RuntimeProbe
{
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect rect, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sx, int sy, uint operation);
    private static object Field(object value, string name) { return value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value); }
    [STAThread] private static void Main(string[] args)
    {
        string output = Path.GetFullPath(args[1]);
        bool edgeMode = args.Length > 2 && args[2] == "edge";
        bool edgeSizeMode = args.Length > 2 && (args[2] == "edge-size" || args[2] == "edge-size-big" || args[2] == "official-size");
        Directory.CreateDirectory(output);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Assembly app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        object bank = Activator.CreateInstance(app.GetType("CodexPet.SpriteBank"), true);
        Form pet = (Form)Activator.CreateInstance(app.GetType("CodexPet.DesktopPet"), new object[] { bank, false });
        string mode = args.Length > 2 ? args[2] : "actions";
        if (mode == "official-free" || mode == "official-audit") pet.Enabled = false; // Method-driven probe must not receive live pointer input.
        bool official = mode.StartsWith("official-");
        if (official)
            pet.GetType().GetMethod("SetInitialVariant",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{"official"});
        StringBuilder log = new StringBuilder();
        Timer monitor = new Timer();
        monitor.Interval = 1000;
        int count = 0;
        monitor.Tick += delegate {
            // Loading an original-resolution outfit may take longer than the sampling interval.
            // Schedule the next sample after the action so entry animation receives real timer ticks.
            monitor.Stop();
            count++;
            Timer animation = (Timer)Field(pet, "timer");
            log.AppendLine(String.Format("sample={0} visible={1} nativeVisible={2} handle={3} dpi={4} timer={5} time={6} paused={7} bounds={8}", count, pet.Visible, IsWindowVisible(pet.Handle), pet.Handle, GetDpiForWindow(pet.Handle), animation.Enabled, Field(pet,"simulationTime"), Field(pet,"paused"), pet.Bounds));
            ((Bitmap)Field(pet, "canvas")).Save(Path.Combine(output, "frame-" + count + ".png"), ImageFormat.Png);
            Rect physical;
            if (DwmGetWindowAttribute(pet.Handle, 9, out physical, 16) == 0 && count % 2 == 0)
            {
                log.AppendLine("DWM bounds=" + physical.Left + "," + physical.Top + " " + (physical.Right-physical.Left) + "x" + (physical.Bottom-physical.Top));
                using (Bitmap screen = new Bitmap(physical.Right-physical.Left, physical.Bottom-physical.Top))
                {
                    DwmFlush();
                    using(Graphics graphics = Graphics.FromImage(screen))
                    {
                        IntPtr source = GetDC(IntPtr.Zero), target = graphics.GetHdc();
                        try { BitBlt(target,0,0,screen.Width,screen.Height,source,physical.Left,physical.Top,0x40CC0020); }
                        finally { graphics.ReleaseHdc(target); ReleaseDC(IntPtr.Zero,source); }
                    }
                    screen.Save(Path.Combine(output,"screen-"+count+".png"),ImageFormat.Png);
                }
            }
            File.WriteAllText(Path.Combine(output,"runtime.txt"), log.ToString());
            if (mode == "official-cheongsam")
            {
                if (count == 1) pet.GetType().GetMethod("ChangeOutfit",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{"red-cheongsam"});
                if (count == 3) pet.GetType().GetMethod("ChangeCheongsamHair",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{false});
                if (count == 5) {
                    pet.GetType().GetMethod("ChangeCheongsamHair",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{true});
                    pet.GetType().GetMethod("ReactToPart",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{"hair"});
                }
                if (count == 7) pet.GetType().GetMethod("DockAtSide",BindingFlags.NonPublic|BindingFlags.Instance,null,new Type[]{typeof(int)},null).Invoke(pet,new object[]{1});
            }
            else if (mode == "official-audit")
            {
                BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                if (count == 1 || count == 5)
                {
                    pet.GetType().GetMethod("ChangeSize",flags).Invoke(pet,new object[]{576});
                    pet.GetType().GetMethod("PinOfficialPose",flags).Invoke(pet,new object[]{"sit"});
                    Rectangle area = Screen.PrimaryScreen.WorkingArea;
                    RectangleF shape = (RectangleF)pet.GetType().GetMethod("PlacementFootprint",flags).Invoke(pet,null);
                    int right = (int)pet.GetType().GetMethod("PlacementRightLimit",flags).Invoke(pet,new object[]{area});
                    pet.Location = new Point(right, count == 1 ? (int)Math.Ceiling(area.Top-shape.Top) : (int)Math.Floor(area.Bottom-shape.Bottom-120));
                    pet.GetType().GetField("windowY",flags).SetValue(pet,(double)pet.Top);
                    pet.GetType().GetMethod("DrawFrame",flags).Invoke(pet,null);
                }
                if (count == 3) pet.GetType().GetMethod("ToggleSleep",flags).Invoke(pet,null);
                if (count == 7) pet.GetType().GetMethod("DockAtSide",flags,null,new Type[]{typeof(int)},null).Invoke(pet,new object[]{1});
            }
            else if (mode == "official-wave-size")
            {
                if (count == 1 || count == 5)
                {
                    pet.GetType().GetMethod("PinOfficialPose",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{"stand"});
                    pet.GetType().GetMethod("ChangeSize",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{count == 1 ? 480 : 576});
                }
                if (count == 3 || count == 7)
                    pet.GetType().GetMethod("ReactToPart",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{"hair"});
            }
            else if (mode == "official-free")
            {
                if (count % 2 == 1)
                {
                    pet.GetType().GetMethod("PinOfficialPose",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{count==3 ? "wave" : count==5 ? "pickup" : "sit"});
                    Rectangle area=Screen.PrimaryScreen.WorkingArea;
                    pet.Location=new Point(area.Left+300,area.Top+30);
                    Point start=new Point(area.Left+500,area.Top+250), end=new Point(area.Left+540,area.Top+270);
                    foreach(string name in new[]{"downCursor","lastCursor"}) pet.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,start);
                    pet.GetType().GetField("downWindow",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,pet.Location);
                    pet.GetType().GetField("pressed",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,true);
                    pet.GetType().GetMethod("MovePointer",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{end,area});
                    pet.GetType().GetMethod("ReleasePointer",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{end,area});
                    log.AppendLine("FREE pose="+Field(pet,"pinnedOfficialPose")+" gravity="+Field(pet,"gravityEnabled")+" falling="+Field(pet,"falling")+" held="+pet.Location);
                }
            }
            else if (mode == "official-motion")
            {
                if (count == 1) {
                    pet.GetType().GetMethod("StandUp",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);
                    pet.GetType().GetMethod("ToggleSit",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);
                }
                if (count == 3) pet.GetType().GetMethod("ReactToPart",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{"hair"});
                if (count == 5) {
                    pet.GetType().GetField("dragging",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,true);
                    pet.GetType().GetMethod("DrawFrame",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);
                }
                if (count == 7) pet.GetType().GetMethod("DockAtSide",BindingFlags.NonPublic|BindingFlags.Instance,null,new Type[]{typeof(int)},null).Invoke(pet,new object[]{1});
            }
            else if (mode == "official-wardrobe")
            {
                if (count % 2 == 1)
                    pet.GetType().GetMethod("ChangeOutfit",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{new string[]{"casual","pink-waitress","winter-coat","red-cheongsam"}[(count-1)/2]});
            }
            else if (mode == "official-edge")
            {
                if (count % 2 == 1)
                {
                    if (count == 5)
                        pet.GetType().GetMethod("ChangeOutfit",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{"casual"});
                    pet.GetType().GetMethod("DockAtSide",BindingFlags.NonPublic|BindingFlags.Instance,null,new Type[]{typeof(int)},null).Invoke(pet,new object[]{count == 1 || count == 5 ? -1 : 1});
                }
            }
            else if (edgeSizeMode)
            {
                if (count == 1 && args[2] == "edge-size-big")
                    pet.GetType().GetMethod("ChangeVariant",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{"big-head"});
                if (count == 1)
                    pet.GetType().GetMethod("DockAtSide",BindingFlags.NonPublic|BindingFlags.Instance,null,new Type[]{typeof(int)},null).Invoke(pet,new object[]{1});
                if (count == 1 || count == 3 || count == 5 || count == 7)
                {
                    int preset = count == 3 ? 160 : count == 5 ? 240 : count == 7 ? 432 : 360;
                    int size = (int)pet.GetType().GetMethod("SizeForPreset",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{preset});
                    pet.GetType().GetMethod("ChangeSize",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{size});
                    log.AppendLine("size preset="+preset+" renderedHeight="+size);
                }
            }
            else if (edgeMode)
            {
                if (count == 1 || count == 3 || count == 7)
                    pet.GetType().GetMethod("DockAtSide",BindingFlags.NonPublic|BindingFlags.Instance,null,new Type[]{typeof(int)},null).Invoke(pet,new object[]{count == 3 ? 1 : -1});
                if (count == 5) pet.GetType().GetMethod("ChangeVariant",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{"big-head"});
            }
            else
            {
            if (count == 1 || count == 7)
                pet.GetType().GetMethod("ReactToPart",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{ count == 1 ? "hair" : "body" });
            if (count == 3) pet.GetType().GetMethod("ToggleSleep",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);
            if (count == 5)
            {
                if (official) {
                    pet.GetType().GetMethod("ToggleSleep",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);
                    pet.GetType().GetMethod("ReactToPart",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,new object[]{"face"});
                }
                else pet.GetType().GetMethod("ToggleSit",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);
            }
            }
            if (count == 1 && (mode == "official-motion" || mode == "official-cheongsam"))
            {
                Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
                pet.Location = new Point(work.Left + (work.Width-pet.Width)/2, work.Top + 30);
                pet.GetType().GetField("windowY",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,(double)pet.Top);
                pet.GetType().GetMethod("DrawFrame",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(pet,null);
            }
            if (count >= 8) { monitor.Stop(); pet.Close(); }
            else monitor.Start();
        };
        pet.Shown += delegate {
            log.AppendLine("EVENT: Shown");
            Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
            pet.Location = new Point(work.Left + (work.Width-pet.Width)/2, work.Top + 30);
            pet.GetType().GetField("windowY",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(pet,(double)pet.Top);
        };
        pet.VisibleChanged += delegate { log.AppendLine("EVENT: VisibleChanged=" + pet.Visible); };
        pet.HandleCreated += delegate { log.AppendLine("EVENT: HandleCreated=" + pet.Handle); };
        pet.HandleDestroyed += delegate { log.AppendLine("EVENT: HandleDestroyed"); };
        monitor.Start();
        Application.Run(pet);
        monitor.Dispose(); pet.Dispose(); ((IDisposable)bank).Dispose();
        File.WriteAllText(Path.Combine(output,"runtime.txt"), log.ToString());
    }
}
