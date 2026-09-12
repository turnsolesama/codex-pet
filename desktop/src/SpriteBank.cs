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
        private const int CacheLimit = 10;
        public SpriteBank()
        {
        }
        public Bitmap Get(string variant, string expression) { return Get(variant, "maid", expression); }
        public Bitmap Get(string variant, string outfit, string expression)
        {
            string key = variant + "." + outfit + "." + expression;
            string resource = outfit == "maid" ? "Sprite." + variant + "." + expression : "Wardrobe." + key;
            return Load(key, resource);
        }
        public Bitmap GetPose(string pose) { return Load("pose." + pose, "Pose." + pose); }
        public Bitmap GetGrip(string variant, string outfit) { return Load("grip." + variant + "." + outfit, "Grip." + variant + "." + outfit); }
        private Bitmap Load(string key, string resource)
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
                using (Bitmap original = new Bitmap(stream)) bitmap = Prepare(original);
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
