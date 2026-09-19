using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CodexPet
{
    internal sealed class SpriteBank : IDisposable
    {
        private readonly Dictionary<string, Bitmap> sprites = new Dictionary<string, Bitmap>();
        private readonly LinkedList<string> recentlyUsed = new LinkedList<string>();
        private readonly bool hasOfficial;
        private const int CacheLimit = 10;
        public bool HasOfficial { get { return hasOfficial; } }
        public bool HasOfficialMotions { get; private set; }
        public bool HasCheongsamStyles { get; private set; }
        public bool CheongsamUpdo { get; set; }
        public bool HasMaidFaceStyles { get; private set; }
        public bool MaidCuteFace { get; set; }
        private readonly Dictionary<string, float> motionHeads = new Dictionary<string, float>();
        public SpriteBank()
        {
            CheongsamUpdo = true;
            HashSet<string> resources = new HashSet<string>(Assembly.GetExecutingAssembly().GetManifestResourceNames());
            List<string> missing = new List<string>();
            int found = 0;
            foreach (string outfit in new[] { "maid", "casual", "pink-waitress", "winter-coat", "red-cheongsam" })
                foreach (string expression in new[] { "idle", "closed", "annoyed", "happy", "shy" })
                {
                    string resource = "Official." + outfit + "." + expression;
                    if (resources.Contains(resource)) found++;
                    else missing.Add(resource);
                }
            if (found > 0 && missing.Count > 0)
                throw new InvalidDataException("官方等身素材不完整，缺少：" + string.Join("、", missing.ToArray()));
            hasOfficial = found == 25;
            int motions = 0;
            foreach (string outfit in new[] { "maid", "casual", "pink-waitress", "winter-coat", "red-cheongsam" })
                foreach (string pose in new[] { "sit", "wave", "pickup", "grip" })
                    if (resources.Contains("OfficialMotion." + outfit + "." + pose)) motions++;
            if (motions != 0 && motions != 20) throw new InvalidDataException("官方衍生动作素材不完整。");
            HasOfficialMotions = motions == 20;
            List<string> missingUpdo = new List<string>();
            int updoMotions = 0;
            foreach (string pose in new[] { "sit", "wave", "pickup" })
            {
                string resource = "OfficialMotion.red-cheongsam-updo." + pose;
                if (resources.Contains(resource)) updoMotions++;
                else missingUpdo.Add(resource);
            }
            if ((HasOfficialMotions || updoMotions > 0) && updoMotions != 3)
                throw new InvalidDataException("旗袍盘发动作不完整，缺少：" + string.Join("、", missingUpdo.ToArray()));
            HasCheongsamStyles = HasOfficialMotions && updoMotions == 3;
            int classicMotions = 0;
            foreach (string pose in new[] { "sit", "wave", "pickup" })
                if (resources.Contains("OfficialMotion.maid-classic." + pose)) classicMotions++;
            if ((HasOfficialMotions || classicMotions > 0) && classicMotions != 3)
                throw new InvalidDataException("女仆常态脸动作不完整。");
            HasMaidFaceStyles = HasOfficialMotions && classicMotions == 3;
            if (HasOfficialMotions)
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OfficialMotion.Meta"))
                {
                    if (stream == null) throw new InvalidDataException("缺少官方动作尺寸标定：OfficialMotion.Meta");
                    using (StreamReader reader = new StreamReader(stream))
                    {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] fields = line.Split('|');
                        if (fields.Length == 2) motionHeads.Add(fields[0], float.Parse(fields[1],System.Globalization.CultureInfo.InvariantCulture));
                    }
                    }
                    foreach (string clothing in new[] { "maid", "casual", "pink-waitress", "winter-coat", "red-cheongsam", "red-cheongsam-updo", "maid-classic" })
                        foreach (string pose in clothing == "red-cheongsam-updo" || clothing == "maid-classic" ? new[] { "sit", "wave", "pickup" } : new[] { "sit", "wave", "pickup", "grip" })
                        {
                            float head;
                            if (!motionHeads.TryGetValue(clothing + "." + pose, out head) || head <= 0 || float.IsNaN(head) || float.IsInfinity(head))
                                throw new InvalidDataException("缺少或无效的官方动作尺寸标定：" + clothing + "." + pose);
                        }
                }
        }
        public Bitmap Get(string variant, string expression) { return Get(variant, "maid", expression); }
        public Bitmap Get(string variant, string outfit, string expression)
        {
            string key = variant + "." + outfit + "." + expression;
            if (variant == "official")
            {
                if (!HasOfficial) throw new InvalidDataException("当前版本未包含官方等身素材，请使用本地官方资源版。");
                return Load(key, "Official." + outfit + "." + expression, true);
            }
            string resource = outfit == "maid" ? "Sprite." + variant + "." + expression : "Wardrobe." + key;
            return Load(key, resource);
        }
        public Bitmap GetPose(string pose) { return Load("pose." + pose, "Pose." + pose); }
        private string OfficialMotionOutfit(string outfit, string pose)
        {
            if (outfit == "maid" && pose != "grip" && HasMaidFaceStyles && !MaidCuteFace) return "maid-classic";
            // Original standing expressions and grip already use the correct updo.
            // Only the three alternate action sprites have a selectable hair style.
            return outfit == "red-cheongsam" && pose != "grip" && CheongsamUpdo ? "red-cheongsam-updo" : outfit;
        }
        public Bitmap GetOfficialMotion(string outfit, string pose)
        {
            string resourceOutfit = OfficialMotionOutfit(outfit, pose);
            return Load("motion." + resourceOutfit + "." + pose, "OfficialMotion." + resourceOutfit + "." + pose, true);
        }
        public float OfficialMotionHead(string outfit, string pose) { return motionHeads[OfficialMotionOutfit(outfit, pose) + "." + pose]; }
        public Bitmap GetGrip(string variant, string outfit) { return Load("grip." + variant + "." + outfit, "Grip." + variant + "." + outfit); }
        private Bitmap Load(string key, string resource, bool preserveAlpha = false)
        {
            Bitmap bitmap;
            if (sprites.TryGetValue(key, out bitmap))
            {
                recentlyUsed.Remove(key);
                recentlyUsed.AddLast(key);
                return bitmap;
            }
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream == null) throw new InvalidDataException("缺少素材：" + key);
                using (Bitmap original = new Bitmap(stream))
                {
                    // Keep the full HD resource in the package. Standing canvases can
                    // exceed 7K; decode a display-sized copy instead of caching 92 MiB
                    // per expression. Motion geometry must retain its metadata scale.
                    if (preserveAlpha && resource.StartsWith("Official.") && original.Height > 2048)
                    {
                        int height = 2048;
                        int width = (int)Math.Round(original.Width * (double)height / original.Height);
                        bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                        using (Graphics graphics = Graphics.FromImage(bitmap))
                        using (ImageAttributes attributes = new ImageAttributes())
                        {
                            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                            graphics.DrawImage(original, new Rectangle(0,0,width,height), 0,0,original.Width,original.Height,GraphicsUnit.Pixel,attributes);
                        }
                    }
                    else bitmap = preserveAlpha ? original.Clone(new Rectangle(0, 0, original.Width, original.Height), PixelFormat.Format32bppArgb) : Prepare(original);
                }
            }
            // Cache only the most recent frames so 50 embedded outfits/expressions do not remain decoded in RAM.
            while (sprites.Count >= CacheLimit)
            {
                string expired = recentlyUsed.First.Value;
                recentlyUsed.RemoveFirst();
                sprites[expired].Dispose();
                sprites.Remove(expired);
            }
            sprites.Add(key, bitmap);
            recentlyUsed.AddLast(key);
            return bitmap;
        }

        // Remove only near-white background connected to the border, preserving white costume details.
        // This is an in-memory rendering mask; original artwork is never overwritten.
        private static Bitmap Prepare(Bitmap original)
        {
            int width = original.Width, height = original.Height, count = width * height;
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result)) g.DrawImageUnscaled(original, 0, 0);
            BitmapData data = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                byte[] pixels = new byte[count * 4];
                for (int y = 0; y < height; y++) Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * width * 4, width * 4);
                bool[] background = new bool[count];
                int[] queue = new int[count];
                int head = 0, tail = 0;
                Action<int> enqueue = delegate(int index)
                {
                    if (background[index]) return;
                    int p = index * 4;
                    if (pixels[p + 3] == 0 || (pixels[p] >= 242 && pixels[p + 1] >= 242 && pixels[p + 2] >= 242))
                    { background[index] = true; queue[tail++] = index; }
                };
                for (int x = 0; x < width; x++) { enqueue(x); enqueue((height - 1) * width + x); }
                for (int y = 1; y < height - 1; y++) { enqueue(y * width); enqueue(y * width + width - 1); }
                while (head < tail)
                {
                    int index = queue[head++], x = index % width, y = index / width;
                    if (x > 0) enqueue(index - 1);
                    if (x + 1 < width) enqueue(index + 1);
                    if (y > 0) enqueue(index - width);
                    if (y + 1 < height) enqueue(index + width);
                }
                for (int index = 0; index < count; index++)
                    if (background[index]) { int p = index * 4; pixels[p] = pixels[p + 1] = pixels[p + 2] = pixels[p + 3] = 0; }
                for (int y = 0; y < height; y++) Marshal.Copy(pixels, y * width * 4, IntPtr.Add(data.Scan0, y * data.Stride), width * 4);
            }
            finally { result.UnlockBits(data); }
            return result;
        }
        public void Dispose() { foreach (Bitmap sprite in sprites.Values) sprite.Dispose(); sprites.Clear(); recentlyUsed.Clear(); }
    }
}
