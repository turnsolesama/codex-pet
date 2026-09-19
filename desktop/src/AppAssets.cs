using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace CodexPet
{
    internal static class AppAssets
    {
        internal static Icon LoadIcon()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("App.Icon"))
            {
                if (stream == null) throw new InvalidDataException("缺少内置应用图标。");
                using (Icon icon = new Icon(stream)) return (Icon)icon.Clone();
            }
        }
    }
}
