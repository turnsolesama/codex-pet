using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CodexPet
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool smoke = Array.IndexOf(args, "--smoke-test") >= 0;
            LayeredForm.Inspectable = Array.IndexOf(args, "--ui-test") >= 0;
            string diagnostics = null;
            string initialOutfit = "maid";
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "--diagnostics") diagnostics = Path.GetFullPath(args[i + 1]);
                if (args[i] == "--outfit") initialOutfit = args[i + 1];
            }
            if (smoke && diagnostics == null) return 2;
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (smoke)
                {
                    Directory.CreateDirectory(diagnostics);
                    using (SpriteBank bank = new SpriteBank())
                    using (DesktopPet pet = new DesktopPet(bank, true, initialOutfit))
                        pet.SmokeTest(diagnostics);
                    return 0;
                }
                bool created;
                using (Mutex mutex = new Mutex(true, "Local\\CodexNatsumePet", out created))
                {
                    if (!created) return 0;
                    try
                    {
                        using (SpriteBank bank = new SpriteBank())
                        using (DesktopPet pet = new DesktopPet(bank, false, initialOutfit))
                            Application.Run(pet);
                    }
                    finally { mutex.ReleaseMutex(); }
                }
                return 0;
            }
            catch (Exception error)
            {
                if (smoke && diagnostics != null)
                {
                    Directory.CreateDirectory(diagnostics);
                    File.WriteAllText(Path.Combine(diagnostics, "error.txt"), error.ToString(), Encoding.UTF8);
                }
                else MessageBox.Show("桌宠启动失败：\r\n" + error.Message, "枣子姐桌宠", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    internal sealed class DesktopPet : LayeredForm
    {
        private readonly SpriteBank bank;
        private readonly bool diagnosticsMode;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Dictionary<string, Rectangle> crops = new Dictionary<string, Rectangle>();
        private readonly Dictionary<string, Rectangle> poseCrops = new Dictionary<string, Rectangle>();
        private readonly Dictionary<string, Rectangle> gripCrops = new Dictionary<string, Rectangle>();
        private readonly Queue<double> clicks = new Queue<double>();
        private System.Windows.Forms.Timer timer;
        private ContextMenuStrip menu;
        private NotifyIcon tray;
        private Icon trayIcon;
        private Bitmap canvas;
        private string variant = "standard";
        private string outfit = "maid";
        private static readonly string[] Outfits = new string[] { "maid", "casual", "pink-waitress", "winter-coat", "red-cheongsam" };
        private static readonly string[] OutfitNames = new string[] { "女仆装", "日常私服", "粉色服务员", "冬日外套", "红金旗袍" };
        private int petSize = 280;
        private bool breathing = true, sleeping, paused, dragging, pressed, falling;
        private bool seated, suppressPoses;
        private double dragPoseStarted, landPoseUntil, dragMagnitude;
        private double simulationTime, lastTick, annoyedUntil, jumpTime = -10, landingTime = -10;
        private double windowY, verticalSpeed, tilt, dragSpeed, nextBlink = 3.1, blinkUntil;
        private double lastDragTime;
        private Point downCursor, downWindow, lastCursor;
        private Rectangle landingArea;
        private int dockSide, dockCenterY;
        private Rectangle dockArea;
        private double dockStarted;
        private bool dockTransitionPending;
        private bool autoDockArmed = true;
        private int detachedEdgeX;
        private Matrix inverseCharacterTransform;
        private string pressedPart = "body", feedbackExpression = "idle", feedbackKind = "", bubbleText = "";
        private double feedbackUntil, feedbackStarted;
        private Rectangle bubbleBounds;
        private readonly Font bubbleFont = new Font("Microsoft YaHei UI", 10, FontStyle.Regular, GraphicsUnit.Pixel);

        public DesktopPet(SpriteBank sprites, bool diagnostic)
            : this(sprites, diagnostic, "maid") { }

        public DesktopPet(SpriteBank sprites, bool diagnostic, string initialOutfit)
        {
            bank = sprites;
            diagnosticsMode = diagnostic;
            if (Array.IndexOf(Outfits, initialOutfit) < 0) throw new ArgumentException("未知服装：" + initialOutfit);
            outfit = initialOutfit;
            Text = "枣子姐桌宠";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = LayeredForm.Inspectable;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = false;
            GetCrop(variant, outfit);
            ResizeCanvas();
            if (!diagnostic)
            {
                BuildMenu();
                CreateTrayIcon();
                timer = new System.Windows.Forms.Timer();
                timer.Interval = 33;
                timer.Tick += Tick;
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaysChanged;
            }
        }

        protected override bool ShowWithoutActivation { get { return !LayeredForm.Inspectable; } }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ReturnToCorner();
            lastTick = clock.Elapsed.TotalSeconds;
            DrawFrame();
            if (timer != null) timer.Start();
        }

        private Rectangle GetCrop(string type, string clothing)
        {
            string key = type + "." + clothing;
            Rectangle bounds;
            if (!crops.TryGetValue(key, out bounds))
            {
                bounds = FindVariantBounds(type, clothing);
                crops.Add(key, bounds);
            }
            return bounds;
        }

        private Rectangle FindVariantBounds(string type, string clothing)
        {
            Rectangle bounds = Rectangle.Empty;
            foreach (string expression in new string[] { "idle", "closed", "annoyed" })
            {
                Bitmap bitmap = bank.Get(type, clothing, expression);
                Rectangle current = FindBitmapBounds(bitmap, type + "/" + clothing + "/" + expression);
                bounds = bounds.IsEmpty ? current : Rectangle.Union(bounds, current);
            }
            return bounds;
        }

        private static Rectangle FindBitmapBounds(Bitmap bitmap, string label)
        {
            return FindBitmapBounds(bitmap, label, 2);
        }

        private static Rectangle FindBitmapBounds(Bitmap bitmap, string label, int padding)
        {
                int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
                // Scan a private standardized copy: SpriteBank retains ownership of its assets.
                using (Bitmap copy = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(copy)) g.DrawImageUnscaled(bitmap, 0, 0);
                    BitmapData data = copy.LockBits(new Rectangle(0, 0, copy.Width, copy.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        byte[] row = new byte[copy.Width * 4];
                        for (int y = 0; y < copy.Height; y++)
                        {
                            Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                            for (int x = 0; x < copy.Width; x++) if (row[x * 4 + 3] > 12)
                            {
                                left = Math.Min(left, x); right = Math.Max(right, x);
                                top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                            }
                        }
                    }
                    finally { copy.UnlockBits(data); }
                }
                if (right < left) throw new InvalidDataException("角色素材没有可见像素：" + label);
                return Rectangle.FromLTRB(Math.Max(0, left - padding), Math.Max(0, top - padding), Math.Min(bitmap.Width, right + 1 + padding), Math.Min(bitmap.Height, bottom + 1 + padding));
        }

        private Rectangle GetGripCrop()
        {
            string key = variant + "." + outfit;
            Rectangle bounds;
            if (!gripCrops.TryGetValue(key, out bounds))
            {
                bounds = FindBitmapBounds(bank.GetGrip(variant, outfit), "grip/" + key, 0);
                // Match the outer fingertips of both hands, not a ribbon or headdress extending farther left.
                // These source-pixel anchors belong to edge-grip-v2; the screen naturally occludes anything outside.
                int contactX;
                if (variant == "standard")
                    contactX = outfit == "maid" ? 81 : outfit == "casual" ? 145 : outfit == "pink-waitress" ? 165 : outfit == "winter-coat" ? 208 : 89;
                else
                    contactX = outfit == "maid" ? 280 : 281;
                bounds = Rectangle.FromLTRB(Math.Max(bounds.Left, contactX), bounds.Top, bounds.Right, bounds.Bottom);
                gripCrops.Add(key, bounds);
            }
            return bounds;
        }

        private float GripHeight()
        {
            Rectangle source = GetGripCrop();
            // Size the head-and-shoulders view by width so long hair does not make the face tiny.
            float width = petSize * (variant == "standard" ? 0.40f : 0.55f);
            if (dockArea.Width > 9) width = Math.Min(width, dockArea.Width - 9);
            float height = width * source.Height / source.Width;
            if (dockArea.Height > 40) height = Math.Min(height, dockArea.Height - 40);
            return Math.Max(1, height);
        }

        private Rectangle GetPoseCrop(string pose)
        {
            Rectangle bounds;
            if (!poseCrops.TryGetValue(pose, out bounds))
            {
                bounds = FindBitmapBounds(bank.GetPose(pose), pose);
                poseCrops.Add(pose, bounds);
            }
            return bounds;
        }

        private bool SupportsPoses() { return variant == "standard" && outfit == "maid"; }

        private string CurrentPose()
        {
            if (!SupportsPoses() || dockSide != 0 || suppressPoses) return null;
            if (dragging)
                return clock.Elapsed.TotalSeconds - dragPoseStarted < 0.15 || dragMagnitude < 90 ? "pickup" : "drag";
            if (falling) return "pickup";
            if (simulationTime < landPoseUntil) return "land";
            if (sleeping) return "sleep";
            return seated ? "sit" : null;
        }

        private void StandUp()
        {
            seated = sleeping = false;
            landPoseUntil = 0;
            jumpTime = landingTime = -10;
            feedbackUntil = annoyedUntil = 0;
            DrawFrame();
        }

        private void ToggleSit()
        {
            if (!SupportsPoses()) return;
            if (dockSide != 0) ExpandDock(false, Cursor.Position);
            seated = !seated;
            sleeping = false;
            landPoseUntil = 0;
            jumpTime = landingTime = -10;
            feedbackUntil = annoyedUntil = 0;
            DrawFrame();
        }

        private void ResizeCanvas()
        {
            if (canvas != null) canvas.Dispose();
            if (dockSide == 0)
                canvas = new Bitmap((int)(petSize * 1.4), (int)(petSize * 1.5) + 12, PixelFormat.Format32bppPArgb);
            else
            {
                Rectangle source = GetGripCrop();
                canvas = new Bitmap((int)Math.Ceiling(GripHeight() * source.Width / source.Height) + 8,
                    (int)Math.Ceiling(GripHeight()) + 16, PixelFormat.Format32bppPArgb);
            }
            ClientSize = canvas.Size;
        }

        private double HeadFraction()
        {
            if (outfit == "maid") return variant == "standard" ? 0.36 : 0.54;
            if (variant == "standard") return 0.35;
            if (outfit == "casual") return 0.52;
            if (outfit == "pink-waitress") return 0.53;
            return 0.505; // Winter coat and cheongsam have lower-profile head accessories.
        }

        private void ChangeVariant(string value)
        {
            GetCrop(value, outfit);
            variant = value;
            seated = false;
            landPoseUntil = 0;
            if (dockSide != 0) { ResizeCanvas(); PositionDock(); }
            DrawFrame();
        }

        private void ChangeOutfit(string value)
        {
            if (Array.IndexOf(Outfits, value) < 0) throw new ArgumentException("未知服装：" + value);
            GetCrop(variant, value);
            outfit = value;
            seated = false;
            landPoseUntil = 0;
            feedbackUntil = annoyedUntil = 0;
            feedbackKind = bubbleText = "";
            jumpTime = landingTime = -10;
            tilt = dragSpeed = 0;
            clicks.Clear();
            // Full-body canvas and location stay fixed, preserving the horizontal center and foot anchor.
            if (dockSide != 0) { ResizeCanvas(); PositionDock(); }
            DrawFrame();
        }

        private void BuildMenu()
        {
            menu = new ContextMenuStrip();
            Add("标准 Q 版", delegate { ChangeVariant("standard"); });
            Add("大头 Q 版", delegate { ChangeVariant("big-head"); });
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem sizes = new ToolStripMenuItem("尺寸");
            foreach (int value in new int[] { 200, 280, 360 })
            {
                int captured = value;
                ToolStripMenuItem item = new ToolStripMenuItem((value == 200 ? "小" : value == 280 ? "中" : "大") + "（" + value + "）");
                item.Tag = value;
                item.Click += delegate { ChangeSize(captured); };
                sizes.DropDownItems.Add(item);
            }
            menu.Items.Add(sizes);
            Add("呼吸 / 眨眼", delegate { breathing = !breathing; DrawFrame(); });
            Add("开心轻跳", delegate { if (dockSide != 0) ExpandDock(false, Cursor.Position); paused = false; ReactToPart("feet"); });
            Add("嫌弃", delegate { paused = false; ReactToPart("body"); });
            Add("睡觉 / 唤醒", delegate { if (dockSide != 0) ExpandDock(false, Cursor.Position); sleeping = !sleeping; seated = false; landPoseUntil = 0; jumpTime = -10; annoyedUntil = 0; feedbackUntil = 0; DrawFrame(); });
            Add("暂停 / 继续", delegate { paused = !paused; lastTick = clock.Elapsed.TotalSeconds; DrawFrame(); });
            Add("置顶", delegate { TopMost = !TopMost; });
            menu.Items.Add(new ToolStripSeparator());
            Add("回到右下角", ReturnToCorner);
            Add("退出", Close);
            menu.Items.Add(new ToolStripSeparator());
            Add("收到左侧边框", delegate { DockAtSide(-1); });
            Add("收到右侧边框", delegate { DockAtSide(1); });
            Add("展开全身", delegate { ExpandDock(false, Cursor.Position); });
            Add("开心表情", delegate { paused = false; ReactToPart("hair"); });
            Add("害羞表情", delegate { paused = false; ReactToPart("face"); });
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem wardrobe = new ToolStripMenuItem("换装");
            for (int i = 0; i < Outfits.Length; i++)
            {
                string selectedOutfit = Outfits[i];
                ToolStripMenuItem clothing = new ToolStripMenuItem(OutfitNames[i]);
                clothing.Tag = selectedOutfit;
                clothing.Click += delegate { ChangeOutfit(selectedOutfit); };
                wardrobe.DropDownItems.Add(clothing);
            }
            menu.Items.Add(wardrobe);
            ToolStripMenuItem sitting = new ToolStripMenuItem("坐下 / 站起");
            sitting.Click += delegate { ToggleSit(); };
            menu.Items.Add(sitting);
            menu.Opening += delegate
            {
                ((ToolStripMenuItem)menu.Items[0]).Checked = variant == "standard";
                ((ToolStripMenuItem)menu.Items[1]).Checked = variant == "big-head";
                foreach (ToolStripMenuItem item in sizes.DropDownItems) item.Checked = (int)item.Tag == petSize;
                ((ToolStripMenuItem)menu.Items[4]).Checked = breathing;
                ((ToolStripMenuItem)menu.Items[7]).Checked = sleeping;
                ((ToolStripMenuItem)menu.Items[7]).Text = sleeping ? "唤醒" : "睡觉";
                ((ToolStripMenuItem)menu.Items[8]).Checked = paused;
                ((ToolStripMenuItem)menu.Items[8]).Text = paused ? "继续动画" : "暂停动画";
                ((ToolStripMenuItem)menu.Items[9]).Checked = TopMost;
                ((ToolStripMenuItem)menu.Items[14]).Checked = dockSide == -1;
                ((ToolStripMenuItem)menu.Items[15]).Checked = dockSide == 1;
                menu.Items[16].Enabled = dockSide != 0;
                foreach (ToolStripMenuItem clothing in wardrobe.DropDownItems) clothing.Checked = (string)clothing.Tag == outfit;
                sitting.Enabled = SupportsPoses();
                sitting.Checked = seated;
                sitting.Text = seated ? "站起" : "坐下";
            };
        }

        private void Add(string text, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += delegate { action(); };
            menu.Items.Add(item);
        }

        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

        private void CreateTrayIcon()
        {
            using (Bitmap thumbnail = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(thumbnail))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    Rectangle crop = GetCrop("big-head", outfit);
                    int headHeight = Math.Min(crop.Height, crop.Width);
                    g.DrawImage(bank.Get("big-head", outfit, "idle"), new Rectangle(0, 0, 32, 32), new Rectangle(crop.X, crop.Y, crop.Width, headHeight), GraphicsUnit.Pixel);
                }
                IntPtr handle = thumbnail.GetHicon();
                try { using (Icon borrowed = Icon.FromHandle(handle)) trayIcon = (Icon)borrowed.Clone(); }
                finally { DestroyIcon(handle); }
            }
            tray = new NotifyIcon();
            tray.Icon = trayIcon;
            tray.Text = "枣子姐桌宠 · 右键菜单";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ReturnToCorner(); };
            tray.Visible = true;
        }

        private void ChangeSize(int size)
        {
            Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
            int center = Left + Width / 2;
            petSize = size;
            ResizeCanvas();
            if (dockSide != 0) { PositionDock(); DrawFrame(); return; }
            Location = new Point(Clamp(center - Width / 2, area.Left, area.Right - Width), area.Bottom - Height);
            windowY = Top;
            falling = false;
            jumpTime = -10;
            DrawFrame();
        }

        private static int Clamp(int value, int lower, int upper) { return Math.Max(lower, Math.Min(Math.Max(lower, upper), value)); }

        private void ReturnToCorner()
        {
            dockTransitionPending = false;
            seated = false;
            landPoseUntil = 0;
            if (dockSide != 0) { dockSide = 0; ResizeCanvas(); }
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(Math.Max(area.Left, area.Right - Width - 18), area.Bottom - Height);
            windowY = Top;
            falling = false;
            pressed = dragging = false;
            Capture = false;
            verticalSpeed = 0;
            tilt = 0;
            autoDockArmed = true;
            DrawFrame();
        }

        private void DockAtSide(int side)
        {
            Rectangle area = dockSide == 0 ? Screen.FromRectangle(Bounds).WorkingArea : dockArea;
            int center = dockSide == 0 ? Top + Height - 7 - petSize + (int)(petSize * HeadFraction() / 2) : dockCenterY;
            DockAtSide(side, area, center);
        }

        private void DockAtSide(int side, Rectangle area, int centerY)
        {
            seated = sleeping = false;
            landPoseUntil = 0;
            dockArea = area;
            dockCenterY = centerY;
            dockSide = side;
            falling = false;
            pressed = dragging = false;
            Capture = false;
            verticalSpeed = tilt = dragSpeed = dragMagnitude = 0;
            jumpTime = landingTime = -10;
            feedbackUntil = annoyedUntil = 0;
            clicks.Clear();
            bank.GetGrip(variant, outfit); // A cached crop may outlive its decoded LRU frame.
            ResizeCanvas();
            PositionDock();
            // Preparing a previously unseen grip must not consume the visible entry animation.
            dockStarted = clock.Elapsed.TotalSeconds;
            dockTransitionPending = true;
            DrawFrame();
        }

        private void PositionDock()
        {
            Location = new Point(dockSide < 0 ? dockArea.Left : dockArea.Right - Width,
                Clamp(dockCenterY - Height / 2, dockArea.Top + 12, dockArea.Bottom - Height - 12));
            dockCenterY = Top + Height / 2;
            windowY = Top;
        }

        private void ExpandDock(bool forDrag, Point cursor)
        {
            if (dockSide == 0) return;
            dockTransitionPending = false;
            int oldSide = dockSide, oldCenter = dockCenterY;
            Rectangle area = dockArea;
            autoDockArmed = !forDrag;
            detachedEdgeX = oldSide < 0 ? area.Left : area.Right;
            dockSide = 0;
            ResizeCanvas();
            int x = forDrag ? cursor.X - Width / 2 : oldSide < 0 ? area.Left : area.Right - Width;
            int headCenterOffset = Height - 7 - petSize + (int)(petSize * HeadFraction() / 2);
            int y = (forDrag ? cursor.Y : oldCenter) - headCenterOffset;
            Location = new Point(Clamp(x, area.Left, area.Right - Width), Clamp(y, area.Top, area.Bottom - Height));
            windowY = Top;
            falling = false;
            jumpTime = landingTime = -10;
            feedbackUntil = annoyedUntil = 0;
            tilt = dragSpeed = 0;
            clicks.Clear();
            if (forDrag) { downCursor = lastCursor = cursor; downWindow = Location; }
            else
            {
                landingArea = area;
                verticalSpeed = 0;
                falling = Top < area.Bottom - Height;
            }
            DrawFrame();
        }

        private void DisplaysChanged(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke((Action)delegate { if (!IsDisposed) ReturnToCorner(); });
        }

        private void Tick(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.05, Math.Max(0, now - lastTick));
            lastTick = now;
            if (!paused) simulationTime += dt;
            bool fallChanged = StepFall(dt);
            if (!paused)
            {
                double targetTilt = dragging ? Math.Max(-12, Math.Min(12, dragSpeed * 0.012)) : 0;
                tilt += (targetTilt - tilt) * Math.Min(1, dt * 12);
                if (dragging) { dragSpeed *= Math.Exp(-dt * 5); dragMagnitude *= Math.Exp(-dt * 5); }
                if (simulationTime >= nextBlink)
                {
                    blinkUntil = simulationTime + 0.15;
                    nextBlink = simulationTime + 3.4 + Math.Sin(simulationTime * 1.7) * 0.8;
                }
            }
            // Even a delayed first Tick must paint the final visible grip before a paused animation can stop.
            if (!paused || fallChanged || dragging || (dockSide != 0 && dockTransitionPending)) DrawFrame();
        }

        private bool StepFall(double dt)
        {
            if (!falling || dragging) return false;
            dt = Math.Max(0, Math.Min(0.05, dt));
            double floor = landingArea.Bottom - Height;
            verticalSpeed += petSize * 7.0 * dt;
            windowY += verticalSpeed * dt;
            if (windowY >= floor)
            {
                // Ground contact is a one-way state transition, never a new upward impulse.
                windowY = floor;
                verticalSpeed = 0;
                falling = false;
                tilt = dragSpeed = dragMagnitude = 0;
                landingTime = simulationTime;
                landPoseUntil = SupportsPoses() ? simulationTime + 0.10 : 0;
            }
            Top = (int)Math.Round(windowY);
            return true;
        }

        private static double LandingCompression(double elapsed)
        {
            if (elapsed <= 0 || elapsed >= 0.20) return 0;
            double t = elapsed / 0.20;
            // A single smooth lobe: zero value and slope at both ends, peak compression 3%.
            return 0.03 * 16 * t * t * (1 - t) * (1 - t);
        }

        private string CurrentExpression()
        {
            if (sleeping) return "closed";
            if (dragging || simulationTime < annoyedUntil) return "annoyed";
            if (simulationTime < feedbackUntil) return feedbackExpression;
            if (breathing && simulationTime < blinkUntil) return "closed";
            return "idle";
        }

        private void DrawFrame()
        {
            if (canvas == null || IsDisposed) return;
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                bubbleBounds = Rectangle.Empty;
                if (inverseCharacterTransform != null) { inverseCharacterTransform.Dispose(); inverseCharacterTransform = null; }
                if (dockSide != 0)
                {
                    DrawDockedHead(g);
                }
                else if (CurrentPose() != null)
                {
                    DrawPose(g, CurrentPose());
                }
                else
                {
                double phase = simulationTime * (sleeping ? 1.4 : 2.2);
                double breath = breathing ? Math.Sin(phase) * (sleeping ? 0.012 : 0.006) : 0;
                double sway = 0; // Resting feet stay planted; explicit touch feedback may still add a brief sway.
                double feedbackAge = simulationTime - feedbackStarted;
                double reactionShake = 0;
                if (simulationTime < feedbackUntil)
                {
                    if (feedbackKind == "hair") sway += Math.Sin(feedbackAge * 8) * Math.Exp(-feedbackAge * 1.6) * 4;
                    if (feedbackKind == "face") sway += Math.Sin(feedbackAge * 5) * Math.Exp(-feedbackAge * 1.4) * 2.8;
                    if (feedbackKind == "body") reactionShake = Math.Sin(feedbackAge * 34) * Math.Exp(-feedbackAge * 4) * petSize * 0.015;
                }
                double lift = 0, squash = 0;
                double sinceJump = simulationTime - jumpTime;
                if (sinceJump >= 0 && sinceJump < 0.68)
                    lift = Math.Sin(sinceJump / 0.68 * Math.PI) * petSize * 0.23;
                squash += LandingCompression(sinceJump - 0.68);
                double landed = simulationTime - landingTime;
                squash += LandingCompression(landed);
                Rectangle source = GetCrop(variant, outfit);
                float height = petSize;
                float width = height * source.Width / source.Height;
                double angle = tilt + sway;
                double scaleX = 1 - breath * 0.35 + squash * 0.45;
                // Keep the transformed lower corners inside the layered bitmap even while tilted.
                double rotationPadding = Math.Abs(Math.Sin(angle * Math.PI / 180.0)) * width * scaleX / 2;
                g.TranslateTransform((float)(canvas.Width / 2f + reactionShake), (float)(canvas.Height - 7 - lift - rotationPadding));
                g.RotateTransform((float)angle);
                g.ScaleTransform((float)scaleX, (float)(1 + breath - squash));
                inverseCharacterTransform = g.Transform;
                inverseCharacterTransform.Invert();
                g.DrawImage(bank.Get(variant, outfit, CurrentExpression()), new RectangleF(-width / 2, -height, width, height), source, GraphicsUnit.Pixel);
                g.ResetTransform();
                if (simulationTime < feedbackUntil && bubbleText.Length > 0)
                    DrawBubble(g, (int)(canvas.Height - 7 - lift - petSize - 40));
                }
            }
            if (!diagnosticsMode && IsHandleCreated) Present(canvas);
        }

        private void DrawPose(Graphics g, string pose)
        {
            // Preserve the original head size across poses; a sitting pose is naturally shorter.
            Rectangle standing = GetCrop("standard", "maid");
            Rectangle source = GetPoseCrop(pose);
            float scale = (float)petSize / standing.Height;
            float width = source.Width * scale, height = source.Height * scale;
            double breath = breathing ? Math.Sin(simulationTime * (pose == "sleep" ? 1.4 : 2.2)) * 0.006 : 0;
            double angle = pose == "sit" || pose == "sleep" || pose == "land" ? 0 : tilt * 0.45;
            double padding = Math.Abs(Math.Sin(angle * Math.PI / 180)) * width / 2;
            g.TranslateTransform(canvas.Width / 2f, (float)(canvas.Height - 7 - padding));
            g.RotateTransform((float)angle);
            g.ScaleTransform((float)(1 - breath * 0.3), (float)(1 + breath));
            // Keep the original landing drawing brief; whole-image fades would create two displaced heads.
            g.DrawImage(bank.GetPose(pose), new RectangleF(-width / 2, -height, width, height), source, GraphicsUnit.Pixel);
            g.ResetTransform();
        }

        private void DrawDockedHead(Graphics g)
        {
            Rectangle source = GetGripCrop();
            float height = GripHeight();
            float width = height * source.Width / source.Height;
            double progress = DockProgress(clock.Elapsed.TotalSeconds);
            float retract = (float)((1 - progress) * width);
            // The grip drawing is upright. Mirroring reuses its left-edge hand contact on the right.
            g.TranslateTransform(dockSide < 0 ? -retract : canvas.Width + retract, canvas.Height / 2f);
            g.ScaleTransform(dockSide < 0 ? 1 : -1, 1);
            // A single grip bitmap stays still after entry; independent head motion requires separate artwork layers.
            g.DrawImage(bank.GetGrip(variant, outfit), new RectangleF(0, -height / 2, width, height), source, GraphicsUnit.Pixel);
            g.ResetTransform();
            if (progress >= 1) dockTransitionPending = false;
        }

        private double DockProgress(double now)
        {
            double time = Math.Max(0, Math.Min(1, (now - dockStarted) / 0.30));
            return 1 - Math.Pow(1 - time, 3);
        }

        private void DrawBubble(Graphics g, int top)
        {
            int width = Math.Min(canvas.Width - 16, 132);
            bubbleBounds = new Rectangle((canvas.Width - width) / 2, Math.Max(4, top), width, 29);
            using (GraphicsPath path = new GraphicsPath())
            {
                Rectangle r = bubbleBounds;
                path.AddArc(r.Left, r.Top, 12, 12, 180, 90);
                path.AddArc(r.Right - 12, r.Top, 12, 12, 270, 90);
                path.AddArc(r.Right - 12, r.Bottom - 12, 12, 12, 0, 90);
                path.AddArc(r.Left, r.Bottom - 12, 12, 12, 90, 90);
                path.CloseFigure();
                using (Brush fill = new SolidBrush(Color.FromArgb(235, 255, 247, 251))) g.FillPath(fill, path);
                using (Pen border = new Pen(Color.FromArgb(205, 175, 126, 148))) g.DrawPath(border, path);
            }
            using (StringFormat format = new StringFormat())
            using (Brush text = new SolidBrush(Color.FromArgb(72, 50, 66)))
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                g.DrawString(bubbleText, bubbleFont, text, bubbleBounds, format);
            }
        }

        private string HitPart(Point location)
        {
            if (CurrentPose() != null) return "pose";
            if (inverseCharacterTransform == null) return "body";
            PointF[] point = new PointF[] { new PointF(location.X, location.Y) };
            inverseCharacterTransform.TransformPoints(point);
            double y = (point[0].Y + petSize) / petSize;
            if (y < (variant == "standard" ? 0.23 : 0.30)) return "hair";
            if (y < (variant == "standard" ? 0.40 : 0.57)) return "face";
            return y < 0.80 ? "body" : "feet";
        }

        private void ReactToPart(string part)
        {
            if (dockSide != 0) ExpandDock(false, Cursor.Position);
            sleeping = false;
            seated = false;
            landPoseUntil = 0;
            feedbackKind = part;
            feedbackStarted = simulationTime;
            feedbackUntil = simulationTime + 1.8;
            annoyedUntil = 0;
            jumpTime = -10;
            if (part == "hair") { feedbackExpression = "happy"; bubbleText = "…还可以。"; }
            else if (part == "face") { feedbackExpression = "shy"; bubbleText = "别盯着看。"; }
            else if (part == "feet") { feedbackExpression = "happy"; bubbleText = "欸！"; if (dockSide == 0) jumpTime = simulationTime; }
            else { feedbackExpression = "annoyed"; bubbleText = "不许乱戳。"; annoyedUntil = feedbackUntil; }
            DrawFrame();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (bubbleBounds.Contains(e.Location)) return;
            if (e.Button == MouseButtons.Right) { menu.Show(Cursor.Position); return; }
            if (e.Button != MouseButtons.Left) return;
            pressed = true;
            pressedPart = HitPart(e.Location);
            downCursor = lastCursor = Cursor.Position;
            downWindow = Location;
            lastDragTime = clock.Elapsed.TotalSeconds;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!pressed) return;
            Point cursor = Cursor.Position;
            int dx = cursor.X - downCursor.X, dy = cursor.Y - downCursor.Y;
            if (!dragging && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
            {
                if (dockSide != 0) { ExpandDock(true, cursor); dx = dy = 0; }
                dragging = true;
                sleeping = false;
                seated = false;
                dragPoseStarted = clock.Elapsed.TotalSeconds;
                landPoseUntil = 0;
                falling = false;
                jumpTime = -10;
                feedbackUntil = 0;
                annoyedUntil = simulationTime + 1.1;
            }
            if (!dragging) return;
            double now = clock.Elapsed.TotalSeconds;
            dragSpeed = (cursor.X - lastCursor.X) / Math.Max(0.008, now - lastDragTime);
            double movedX = cursor.X - lastCursor.X, movedY = cursor.Y - lastCursor.Y;
            dragMagnitude = Math.Sqrt(movedX * movedX + movedY * movedY) / Math.Max(0.008, now - lastDragTime);
            lastDragTime = now;
            lastCursor = cursor;
            Rectangle dragArea = Screen.FromPoint(cursor).WorkingArea;
            UpdateDockArming(cursor);
            Location = new Point(Clamp(downWindow.X + dx, dragArea.Left, dragArea.Right - Width),
                Clamp(downWindow.Y + dy, dragArea.Top, dragArea.Bottom - Height));
            windowY = Top;
            DrawFrame();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !pressed) return;
            bool wasDragging = dragging;
            pressed = dragging = false;
            Capture = false;
            if (wasDragging) { Point cursor = Cursor.Position; FinishDrag(cursor, Screen.FromPoint(cursor).WorkingArea); return; }
            if (dockSide != 0) { sleeping = false; ExpandDock(false, Cursor.Position); return; }
            if (sleeping || seated || pressedPart == "pose") { StandUp(); return; }
            if (paused) return;
            double now = clock.Elapsed.TotalSeconds;
            clicks.Enqueue(now);
            while (clicks.Count > 0 && now - clicks.Peek() > 1.3) clicks.Dequeue();
            if (clicks.Count >= 3) { ReactToPart("body"); annoyedUntil = simulationTime + 2.2; clicks.Clear(); DrawFrame(); }
            else ReactToPart(pressedPart);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && pressed)
            {
                bool wasDragging = dragging;
                pressed = dragging = false;
                if (wasDragging) BeginFall();
            }
        }

        private void BeginFall()
        {
            BeginFall(Screen.FromPoint(Cursor.Position).WorkingArea);
        }

        private int AutoDockSide(Point cursor, Rectangle area, Rectangle petBounds)
        {
            if (!autoDockArmed) return 0;
            int near = Clamp((int)Math.Round(petSize * 0.17), 36, 60);
            // The caller selects this monitor from the cursor; its taskbar strip may lie below WorkingArea.
            if (Math.Abs((long)cursor.X - area.Left) <= near && petBounds.Left <= area.Left + near) return -1;
            if (Math.Abs((long)cursor.X - area.Right) <= near && petBounds.Right >= area.Right - near) return 1;
            return 0;
        }

        private void UpdateDockArming(Point cursor)
        {
            if (!autoDockArmed && Math.Abs((long)cursor.X - detachedEdgeX) >= 96) autoDockArmed = true;
        }

        private void FinishDrag(Point cursor, Rectangle area)
        {
            UpdateDockArming(cursor);
            int side = AutoDockSide(cursor, area, Bounds);
            if (side != 0) DockAtSide(side, area, cursor.Y);
            else BeginFall(area);
        }

        private void BeginFall(Rectangle area)
        {
            landingArea = area;
            Left = Clamp(Left, landingArea.Left, landingArea.Right - Width);
            windowY = Math.Min(Top, landingArea.Bottom - Height);
            Top = (int)windowY;
            verticalSpeed = 0;
            tilt = dragSpeed = dragMagnitude = 0;
            jumpTime = landingTime = -10;
            landPoseUntil = 0;
            falling = true;
            annoyedUntil = simulationTime + 0.6;
            DrawFrame();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0021 && !LayeredForm.Inspectable) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE in normal mode
            if (m.Msg == 0x0084 && canvas != null && !Capture)
            {
                long packed = m.LParam.ToInt64();
                Point p = PointToClient(new Point(unchecked((short)(packed & 0xffff)), unchecked((short)((packed >> 16) & 0xffff))));
                if (p.X < 0 || p.Y < 0 || p.X >= canvas.Width || p.Y >= canvas.Height || bubbleBounds.Contains(p) || canvas.GetPixel(p.X, p.Y).A == 0)
                { m.Result = new IntPtr(-1); return; } // Transparent alpha also passes through natively.
                m.Result = new IntPtr(1); return;
            }
            base.WndProc(ref m);
        }

        public void SmokeTest(string directory)
        {
            StringBuilder results = new StringBuilder();
            results.AppendLine("PASS: startup and SpriteBank construction");
            suppressPoses = true; // Keep the original 50-expression and inverse-mapping checks independent of pose overlays.
            outfit = "maid";
            foreach (string type in new string[] { "standard", "big-head" })
            {
                variant = type;
                foreach (int size in new int[] { 200, 280, 360 })
                {
                    petSize = size;
                    ResizeCanvas();
                    foreach (string expression in new string[] { "idle", "closed", "annoyed" })
                    {
                        simulationTime = 10;
                        sleeping = expression == "closed";
                        annoyedUntil = expression == "annoyed" ? 12 : 0;
                        blinkUntil = 0;
                        DrawFrame();
                        if (canvas.GetPixel(0, 0).A != 0 || canvas.GetPixel(canvas.Width - 1, canvas.Height - 1).A != 0)
                            throw new InvalidDataException("Frame corners must remain transparent.");
                        canvas.Save(Path.Combine(directory, type + "-" + size + "-" + expression + ".png"), ImageFormat.Png);
                        results.AppendLine("PASS: " + type + " " + size + " " + expression + " transparent render");
                    }
                }
                sleeping = false; annoyedUntil = 0; petSize = 280; ResizeCanvas();
                jumpTime = simulationTime - 0.34;
                DrawFrame();
                canvas.Save(Path.Combine(directory, type + "-jump.png"), ImageFormat.Png);
                jumpTime = -10;
                dragging = true; tilt = 10;
                DrawFrame();
                canvas.Save(Path.Combine(directory, type + "-drag.png"), ImageFormat.Png);
                dragging = false; tilt = 0;
                foreach (string part in new string[] { "hair", "face", "body", "feet" })
                {
                    ReactToPart(part);
                    simulationTime += 0.12;
                    DrawFrame();
                    canvas.Save(Path.Combine(directory, type + "-touch-" + part + ".png"), ImageFormat.Png);
                    // Exercise the actual hit mapper under a live rotated/jumping render transform.
                    double fraction = part == "hair" ? 0.12 : part == "face" ? (type == "standard" ? 0.32 : 0.44) : part == "body" ? 0.68 : 0.94;
                    PointF[] mapped = new PointF[] { new PointF(0, (float)((fraction - 1) * petSize)) };
                    using (Matrix forward = inverseCharacterTransform.Clone())
                    {
                        forward.Invert();
                        forward.TransformPoints(mapped);
                    }
                    if (HitPart(Point.Round(mapped[0])) != part) throw new InvalidOperationException("Inverse click mapping failed: " + type + "/" + part);
                    results.AppendLine("PASS: " + type + " transformed click region " + part);
                }
                feedbackUntil = annoyedUntil = 0; jumpTime = -10;
                foreach (int side in new int[] { -1, 1 })
                {
                    dockSide = side;
                    dockArea = new Rectangle(-1920, 0, 1920, 1080);
                    dockCenterY = 540;
                    dockStarted = clock.Elapsed.TotalSeconds - 1;
                    ResizeCanvas(); PositionDock(); DrawFrame();
                    if (!dockArea.Contains(Bounds)) throw new InvalidOperationException("Dock escaped working area.");
                    int edge = side < 0 ? 0 : canvas.Width - 1;
                    int edgePixels = 0;
                    for (int y = 0; y < canvas.Height; y++) if (canvas.GetPixel(edge, y).A > 12) edgePixels++;
                    if (edgePixels == 0) throw new InvalidOperationException("Dock head is detached from the desktop edge.");
                    AssertGripCanvasComplete(type);
                    canvas.Save(Path.Combine(directory, type + (side < 0 ? "-dock-left.png" : "-dock-right.png")), ImageFormat.Png);
                    ExpandDock(true, new Point(side < 0 ? dockArea.Left + 5 : dockArea.Right - 5, 540));
                    if (!dockArea.Contains(Bounds)) throw new InvalidOperationException("Expanded drag escaped working area.");
                    results.AppendLine("PASS: " + type + (side < 0 ? " left" : " right") + " upright grip dock, edge contact and expanded drag bounds");
                }
                dockSide = 0;
            }
            SmokeWardrobe(directory, results);
            SmokePoses(directory, results);
            SmokeAutoDock(results);
            SmokeDockAnimation(directory, results);
            SmokeGripStability(results);
            SmokeStableLanding(results);
            if (Clamp(-1800, -1920, -400) != -1800 || Clamp(-2200, -1920, -400) != -1920)
                throw new InvalidOperationException("Negative multi-monitor bounds failed.");
            results.AppendLine("PASS: negative monitor coordinate clamping");
            results.AppendLine("NOTE: smoke tests render frames without showing GUI; native focus, tray, capture and desktop composition require interactive validation.");
            File.WriteAllText(Path.Combine(directory, "results.txt"), results.ToString(), Encoding.UTF8);
        }

        private void SmokeWardrobe(string directory, StringBuilder results)
        {
            foreach (string type in new string[] { "standard", "big-head" })
            foreach (string clothing in Outfits)
            {
                variant = type;
                dockSide = 0;
                sleeping = paused = dragging = pressed = falling = false;
                petSize = 280;
                simulationTime = 20;
                blinkUntil = 0;
                ResizeCanvas();
                Rectangle beforeChange = Bounds;
                feedbackUntil = 22;
                feedbackExpression = "happy";
                jumpTime = 19.8;
                ChangeOutfit(clothing);
                if (Bounds != beforeChange || feedbackUntil != 0 || jumpTime != -10)
                    throw new InvalidOperationException("Outfit change moved the foot anchor or retained a transient reaction.");
                foreach (string expression in new string[] { "idle", "closed", "annoyed", "happy", "shy" })
                {
                    sleeping = expression == "closed";
                    annoyedUntil = expression == "annoyed" ? 22 : 0;
                    feedbackExpression = expression;
                    feedbackUntil = expression == "happy" || expression == "shy" ? 22 : 0;
                    feedbackKind = bubbleText = "";
                    DrawFrame();
                    AssertTransparentVisibleFrame(type + "/" + clothing + "/" + expression);
                    foreach (string part in new string[] { "hair", "face", "body", "feet" })
                    {
                        double fraction = part == "hair" ? 0.12 : part == "face" ? (type == "standard" ? 0.32 : 0.44) : part == "body" ? 0.68 : 0.94;
                        PointF[] mapped = new PointF[] { new PointF(0, (float)((fraction - 1) * petSize)) };
                        using (Matrix forward = inverseCharacterTransform.Clone())
                        {
                            forward.Invert();
                            forward.TransformPoints(mapped);
                        }
                        if (HitPart(Point.Round(mapped[0])) != part)
                            throw new InvalidOperationException("Wardrobe click mapping failed: " + type + "/" + clothing + "/" + expression + "/" + part);
                    }
                    canvas.Save(Path.Combine(directory, type + "-outfit-" + clothing + "-" + expression + ".png"), ImageFormat.Png);
                    results.AppendLine("PASS: " + type + "/" + clothing + "/" + expression + " transparent visible render and four click regions");
                }
                sleeping = false;
                feedbackUntil = annoyedUntil = 0;
                foreach (int side in new int[] { -1, 1 })
                {
                    dockSide = side;
                    dockArea = new Rectangle(-1920, -1080, 1920, 1080);
                    dockCenterY = -540;
                    dockStarted = clock.Elapsed.TotalSeconds - 1;
                    ResizeCanvas(); PositionDock(); DrawFrame();
                    AssertWardrobeDock(side, type + "/" + clothing);
                    canvas.Save(Path.Combine(directory, type + "-outfit-" + clothing + (side < 0 ? "-dock-left.png" : "-dock-right.png")), ImageFormat.Png);
                    string nextClothing = Outfits[(Array.IndexOf(Outfits, clothing) + 1) % Outfits.Length];
                    ChangeOutfit(nextClothing);
                    AssertWardrobeDock(side, type + "/" + nextClothing);
                    ChangeOutfit(clothing);
                    AssertWardrobeDock(side, type + "/" + clothing);
                    ExpandDock(true, new Point(side < 0 ? dockArea.Left + 5 : dockArea.Right - 5, -540));
                    if (!dockArea.Contains(Bounds)) throw new InvalidOperationException("Wardrobe expanded drag escaped working area.");
                    results.AppendLine("PASS: " + type + "/" + clothing + (side < 0 ? " left" : " right") + " transparent grip dock, edge contact, complete hands, live outfit switch and expanded drag bounds");
                }
            }
        }

        private void SmokePoses(string directory, StringBuilder results)
        {
            suppressPoses = false;
            variant = "standard";
            outfit = "maid";
            dockSide = 0;
            petSize = 280;
            paused = false;
            simulationTime = 20;
            jumpTime = landingTime = -10;
            feedbackUntil = annoyedUntil = 0;
            tilt = 0;
            ResizeCanvas();
            foreach (string pose in new string[] { "sit", "sleep", "pickup", "drag", "land" })
            {
                seated = pose == "sit";
                sleeping = pose == "sleep";
                dragging = pose == "pickup" || pose == "drag";
                falling = false;
                landPoseUntil = pose == "land" ? 22 : 0;
                dragMagnitude = pose == "drag" ? 500 : 0;
                dragPoseStarted = clock.Elapsed.TotalSeconds - 1;
                if (CurrentPose() != pose) throw new InvalidOperationException("Pose state failed: " + pose);
                DrawFrame();
                AssertTransparentVisibleFrame("pose/" + pose);
                if (HitPart(new Point(Width / 2, Height / 2)) != "pose")
                    throw new InvalidOperationException("Pose must bypass standing click regions.");
                canvas.Save(Path.Combine(directory, "standard-maid-pose-" + pose + ".png"), ImageFormat.Png);
                Rectangle poseBounds = GetPoseCrop(pose);
                double renderedHeight = (double)petSize * poseBounds.Height / GetCrop("standard", "maid").Height;
                if ((pose == "sit" || pose == "land") && renderedHeight >= petSize * 0.94)
                    throw new InvalidOperationException("Seated pose was stretched to standing height.");
                results.AppendLine("PASS: standard/maid " + pose + " transparent original-scale pose and safe click handling");
                // A pose state may never select maid artwork for another outfit or body type.
                foreach (string clothing in Outfits)
                {
                    outfit = clothing;
                    if (clothing != "maid" && CurrentPose() != null)
                        throw new InvalidOperationException("Maid pose leaked into outfit: " + clothing);
                }
                outfit = "maid";
                variant = "big-head";
                if (CurrentPose() != null) throw new InvalidOperationException("Standard pose leaked into big-head variant.");
                variant = "standard";
                dockSide = -1;
                if (CurrentPose() != null) throw new InvalidOperationException("Dock must use dedicated grip artwork.");
                dockSide = 0;
            }
            seated = true; sleeping = dragging = falling = false; landPoseUntil = 0;
            ChangeOutfit("casual");
            if (seated || outfit != "casual" || CurrentPose() != null) throw new InvalidOperationException("Changing outfit failed to cancel sitting.");
            ChangeOutfit("maid"); seated = true;
            ChangeVariant("big-head");
            if (seated || outfit != "maid" || CurrentPose() != null) throw new InvalidOperationException("Changing type failed to cancel sitting.");
            results.AppendLine("PASS: pose states remain restricted to standard maid; switching outfit/type cancels sitting and preserves selected clothing");
        }

        private void SmokeAutoDock(StringBuilder results)
        {
            variant = "standard"; outfit = "maid"; petSize = 280;
            sleeping = seated = dragging = pressed = false;
            foreach (Rectangle area in new Rectangle[] { new Rectangle(0, 0, 1920, 1080), new Rectangle(-1920, -1080, 1920, 1080) })
            {
                foreach (int side in new int[] { -1, 1 })
                foreach (bool bottom in new bool[] { false, true })
                {
                    dockSide = 0; ResizeCanvas(); autoDockArmed = true;
                    Location = new Point(side < 0 ? area.Left : area.Right - Width, bottom ? area.Bottom - Height : area.Top);
                    Point corner = new Point(side < 0 ? area.Left + 2 : area.Right - 2, bottom ? area.Bottom - 2 : area.Top + 2);
                    FinishDrag(corner, area);
                    if (dockSide != side || falling || dockArea != area || !area.Contains(Bounds) || Top < area.Top + 12 || Bottom > area.Bottom - 12)
                        throw new InvalidOperationException("Corner auto-dock failed or escaped safe margins.");
                    dockStarted = clock.Elapsed.TotalSeconds - 1; DrawFrame();
                    AssertWardrobeDock(side, "auto-corner");
                }
                foreach (int side in new int[] { -1, 1 })
                {
                    dockSide = 0; ResizeCanvas(); autoDockArmed = true;
                    Location = new Point(side < 0 ? area.Left : area.Right - Width, area.Top + 220);
                    Point near = new Point(side < 0 ? area.Left + 24 : area.Right - 24, area.Top + 400);
                    if (AutoDockSide(near, area, Bounds) != side) throw new InvalidOperationException("Near-edge drag must dock.");
                    FinishDrag(near, area);
                    ExpandDock(true, near);
                    if (autoDockArmed) throw new InvalidOperationException("Dragging out of a grip must disarm auto-docking.");
                    int edge = side < 0 ? area.Left : area.Right;
                    UpdateDockArming(new Point(edge - side * 95, near.Y));
                    if (autoDockArmed || AutoDockSide(near, area, Bounds) != 0)
                        throw new InvalidOperationException("Grip release rearmed before the 96px hysteresis boundary.");
                    UpdateDockArming(new Point(edge - side * 96, near.Y));
                    if (!autoDockArmed || AutoDockSide(near, area, Bounds) != side)
                        throw new InvalidOperationException("Grip drag failed to rearm after leaving the edge.");
                    FinishDrag(near, area);
                    if (dockSide != side) throw new InvalidOperationException("Returning to the same edge must re-dock after rearming.");
                    ExpandDock(false, near);
                    if (dockSide != 0) throw new InvalidOperationException("Click expansion must stay expanded.");
                }
                dockSide = 0; ResizeCanvas(); autoDockArmed = true;
                Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + 200);
                Point center = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
                if (AutoDockSide(center, area, Bounds) != 0 || AutoDockSide(new Point(area.Left + 10, center.Y), area, Bounds) != 0)
                    throw new InvalidOperationException("Center placement or a cursor-only edge approach must not dock.");
                FinishDrag(center, area);
                if (dockSide != 0 || !falling) throw new InvalidOperationException("Center drop must keep normal falling behavior.");
                results.AppendLine("PASS: " + (area.Left < 0 ? "negative monitor" : "primary monitor") + " four corners, near edges, center rejection, 96px grip hysteresis and click expansion");
            }
        }

        private void SmokeDockAnimation(string directory, StringBuilder results)
        {
            Rectangle area = new Rectangle(-1920, -1080, 1920, 1080);
            foreach (string type in new string[] { "standard", "big-head" })
            foreach (int side in new int[] { -1, 1 })
            {
                variant = type; outfit = "maid"; dockSide = 0; petSize = 280;
                paused = true; breathing = false; simulationTime = 0;
                ResizeCanvas();
                DockAtSide(side, area, -540);
                string prefix = type + "-dock-entry-" + (side < 0 ? "left" : "right");
                dockStarted = 100;
                if (DockProgress(100) != 0 || Math.Abs(DockProgress(100.15) - 0.875) > 0.000001 || DockProgress(100.31) != 1)
                    throw new InvalidOperationException("Grip entry must progress once from zero to one without overshoot.");
                dockStarted = clock.Elapsed.TotalSeconds + 1;
                dockTransitionPending = true;
                DrawFrame();
                int firstPixels = CountFrameVisiblePixels();
                canvas.Save(Path.Combine(directory, prefix + "-start.png"), ImageFormat.Png);
                dockStarted = clock.Elapsed.TotalSeconds - 0.15;
                DrawFrame();
                int middlePixels = CountFrameVisiblePixels();
                canvas.Save(Path.Combine(directory, prefix + "-middle.png"), ImageFormat.Png);
                // Reproduce the original failure: paused, first queued timer delayed beyond the entry duration.
                dockStarted = clock.Elapsed.TotalSeconds + 1;
                dockTransitionPending = true;
                DrawFrame();
                dockStarted = clock.Elapsed.TotalSeconds - 1;
                double frozenTime = simulationTime;
                Tick(null, EventArgs.Empty);
                int finalPixels = CountFrameVisiblePixels();
                if (!paused || simulationTime != frozenTime || dockTransitionPending || firstPixels >= middlePixels || middlePixels >= finalPixels)
                    throw new InvalidOperationException("Paused grip entry failed to paint its visible endpoint after a delayed timer.");
                AssertWardrobeDock(side, prefix);
                canvas.Save(Path.Combine(directory, prefix + "-end.png"), ImageFormat.Png);
                int fixedLeft = Left, fixedTop = Top;
                Tick(null, EventArgs.Empty);
                if (Left != fixedLeft || Top != fixedTop || dockTransitionPending || CountFrameVisiblePixels() != finalPixels)
                    throw new InvalidOperationException("Paused grip moved or changed after completing its entry.");
                results.AppendLine("PASS: " + prefix + " first/middle/final entry frames, paused delayed-Tick completion and fixed final contact");
            }
            paused = false; breathing = true;
        }

        private int CountFrameVisiblePixels()
        {
            int count = 0;
            for (int y = 0; y < canvas.Height; y++)
                for (int x = 0; x < canvas.Width; x++)
                    if (canvas.GetPixel(x, y).A > 12) count++;
            return count;
        }

        private void SmokeGripStability(StringBuilder results)
        {
            Rectangle area = new Rectangle(-1920, -1080, 1920, 1080);
            foreach (string type in new string[] { "standard", "big-head" })
            foreach (int side in new int[] { -1, 1 })
            {
                variant = type; outfit = "casual"; dockSide = 0; petSize = 280;
                paused = false; breathing = true; simulationTime = 0;
                ResizeCanvas();
                DockAtSide(side, area, -540);
                dockStarted = clock.Elapsed.TotalSeconds - 1;
                DrawFrame();
                Rectangle fixedBounds = Bounds;
                byte[] reference = ReadFramePixels();
                // A hidden layered window exercises the real Present path without showing another pet.
                using (LayeredForm probe = new LayeredForm())
                {
                    probe.FormBorderStyle = FormBorderStyle.None;
                    probe.ShowInTaskbar = false;
                    probe.StartPosition = FormStartPosition.Manual;
                    probe.AutoScaleMode = AutoScaleMode.None;
                    probe.Location = Location;
                    probe.Size = canvas.Size;
                    probe.Present(canvas);
                    Rectangle nativeBounds = probe.GetNativeBounds();
                    for (int frame = 0; frame < 60; frame++)
                    {
                        simulationTime += 1.0 / 30;
                        DrawFrame();
                        if (Bounds != fixedBounds || falling || dockTransitionPending)
                            throw new InvalidOperationException("Dock placement changed during settled idle.");
                        byte[] current = ReadFramePixels();
                        if (current.Length != reference.Length) throw new InvalidOperationException("Grip frame size changed during idle.");
                        for (int pixel = 0; pixel < current.Length; pixel++)
                            if (current[pixel] != reference[pixel])
                                throw new InvalidOperationException("Grip pixels moved while breathing was enabled: " + type + "/" + side);
                        probe.Present(canvas);
                        if (probe.GetNativeBounds() != nativeBounds)
                            throw new InvalidOperationException("Repeated layered paints moved or resized the native grip window.");
                    }
                    probe.Left += 7;
                    Rectangle explicitlyMoved = probe.GetNativeBounds();
                    probe.Present(canvas);
                    if (probe.GetNativeBounds() != explicitlyMoved)
                        throw new InvalidOperationException("A layered repaint changed an explicitly placed window.");
                }
                AssertWardrobeDock(side, "idle-stability/" + type);
                results.AppendLine("PASS: " + type + (side < 0 ? " left" : " right") + " grip: 60 identical frames with breathing enabled, fixed managed/native bounds and stable edge pixels");
            }
        }

        private byte[] ReadFramePixels()
        {
            int rowBytes = canvas.Width * 4;
            byte[] pixels = new byte[rowBytes * canvas.Height];
            BitmapData data = canvas.LockBits(new Rectangle(0, 0, canvas.Width, canvas.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < canvas.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * rowBytes, rowBytes);
            }
            finally { canvas.UnlockBits(data); }
            return pixels;
        }

        private void SmokeStableLanding(StringBuilder results)
        {
            foreach (string type in new string[] { "standard", "big-head" })
            foreach (Rectangle area in new Rectangle[] { new Rectangle(0, 0, 1920, 1080), new Rectangle(-1920, -1080, 1920, 1080) })
            {
                variant = type; outfit = "maid"; dockSide = 0; petSize = 280;
                dragging = sleeping = seated = paused = false;
                ResizeCanvas();
                landingArea = area;
                double floor = area.Bottom - Height;
                windowY = floor - 400;
                Location = new Point(area.Left + 100, (int)windowY);
                verticalSpeed = 0;
                tilt = 12;
                falling = true;
                simulationTime = 100;
                landingTime = -10;
                landPoseUntil = 0;
                int steps = 0;
                while (falling && steps++ < 240)
                {
                    double previousY = windowY;
                    simulationTime += 1.0 / 60;
                    StepFall(1.0 / 60);
                    if (windowY < previousY || windowY > floor || Top > floor)
                        throw new InvalidOperationException("Drop moved upward or passed through floor.");
                }
                if (falling || windowY != floor || Top != floor || verticalSpeed != 0 || tilt != 0)
                    throw new InvalidOperationException("Drop failed to lock to floor.");
                double contactTime = landingTime;
                double finalPoseUntil = landPoseUntil;
                int finalTop = Top;
                for (int frame = 0; frame < 120; frame++)
                {
                    simulationTime += 1.0 / 60;
                    if (StepFall(1.0 / 60) || falling || verticalSpeed != 0 || windowY != floor || Top != finalTop || landingTime != contactTime || landPoseUntil != finalPoseUntil)
                        throw new InvalidOperationException("Resting coordinates or landing timers changed after contact.");
                }
                results.AppendLine("PASS: " + type + (area.Left < 0 ? " negative-monitor" : " primary-monitor") + " drop locks floor once and stays fixed for two simulated seconds");
            }
            double previous = 0;
            for (int i = 0; i <= 20; i++)
            {
                double compression = LandingCompression(i * 0.01);
                if (compression < 0 || compression > 0.030001 || (i <= 10 && compression < previous - 0.000001) || (i > 10 && compression > previous + 0.000001))
                    throw new InvalidOperationException("Landing compression must be a single bounded lobe.");
                previous = compression;
            }
            if (LandingCompression(0.21) != 0 || LandingCompression(2) != 0)
                throw new InvalidOperationException("Landing compression continued after settling.");
            results.AppendLine("PASS: landing compression peaks once at 3% and finishes within 0.20 seconds");
        }

        private void AssertTransparentVisibleFrame(string label)
        {
            if (canvas.GetPixel(0, 0).A != 0 || canvas.GetPixel(canvas.Width - 1, 0).A != 0 ||
                canvas.GetPixel(0, canvas.Height - 1).A != 0 || canvas.GetPixel(canvas.Width - 1, canvas.Height - 1).A != 0)
                throw new InvalidDataException("Frame corners must remain transparent: " + label);
            int visible = 0;
            for (int y = 0; y < canvas.Height; y += 3)
                for (int x = 0; x < canvas.Width; x += 3)
                    if (canvas.GetPixel(x, y).A > 12) visible++;
            if (visible < 64) throw new InvalidDataException("Rendered character is missing: " + label);
        }

        private void AssertWardrobeDock(int side, string label)
        {
            AssertTransparentVisibleFrame(label);
            if (dockSide != side || !dockArea.Contains(Bounds))
                throw new InvalidOperationException("Outfit dock escaped working area: " + label);
            if ((side < 0 && Left != dockArea.Left) || (side > 0 && Right != dockArea.Right))
                throw new InvalidOperationException("Outfit change detached the dock window: " + label);
            int edge = side < 0 ? 0 : canvas.Width - 1;
            int edgePixels = 0;
            for (int y = 0; y < canvas.Height; y++) if (canvas.GetPixel(edge, y).A > 12) edgePixels++;
            if (edgePixels == 0) throw new InvalidOperationException("Outfit head detached from edge: " + label);
            AssertGripCanvasComplete(label);
        }

        private void AssertGripCanvasComplete(string label)
        {
            Rectangle grip = GetGripCrop();
            int expectedWidth = (int)Math.Ceiling(GripHeight() * grip.Width / grip.Height) + 8;
            int expectedHeight = (int)Math.Ceiling(GripHeight()) + 16;
            if (canvas.Width != expectedWidth || canvas.Height != expectedHeight)
                throw new InvalidOperationException("Grip canvas does not preserve the full head-and-hands crop: " + label);
            for (int x = 0; x < canvas.Width; x++)
                if (canvas.GetPixel(x, 0).A != 0 || canvas.GetPixel(x, canvas.Height - 1).A != 0)
                    throw new InvalidOperationException("Grip artwork was cut at the top or bottom: " + label);
            int innerEdge = dockSide < 0 ? canvas.Width - 1 : 0;
            for (int y = 0; y < canvas.Height; y++)
                if (canvas.GetPixel(innerEdge, y).A != 0)
                    throw new InvalidOperationException("Grip artwork was cut at its inward edge: " + label);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
                if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; }
                if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
                if (trayIcon != null) { trayIcon.Dispose(); trayIcon = null; }
                if (menu != null) { menu.Dispose(); menu = null; }
                if (canvas != null) { canvas.Dispose(); canvas = null; }
                if (inverseCharacterTransform != null) { inverseCharacterTransform.Dispose(); inverseCharacterTransform = null; }
                bubbleFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
