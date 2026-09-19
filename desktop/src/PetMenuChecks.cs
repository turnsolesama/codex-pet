using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        private static void CheckMenuLayout(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Menu layout regression: " + message);
        }

        private Size LayoutHiddenMenu(ToolStripDropDown strip, string label, bool root, StringBuilder results)
        {
            CheckMenuLayout(!strip.Visible, label + " must remain hidden");
            strip.PerformLayout();
            Size preferred = strip.GetPreferredSize(Size.Empty);
            CheckMenuLayout(preferred.Width > 0 && preferred.Height > 0, label + " has no preferred size");
            strip.Size = preferred;
            strip.PerformLayout();
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            CheckMenuLayout(strip.Width <= area.Width && strip.Height <= area.Height, label + " does not fit the primary working area");

            List<ToolStripItem> available = new List<ToolStripItem>();
            int commandCount = 0;
            int occupiedHeight = 0;
            foreach (ToolStripItem item in strip.Items)
            {
                // Visible also depends on the hidden owner. Available is the intended row visibility.
                if (!item.Available) continue;
                available.Add(item);
                if (!(item is ToolStripSeparator)) commandCount++;
                Rectangle bounds = item.Bounds;
                CheckMenuLayout(bounds.Width > 0 && bounds.Height > 0, label + "/" + item.Name + " has an empty row");
                CheckMenuLayout(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= strip.ClientSize.Width && bounds.Bottom <= strip.ClientSize.Height,
                    label + "/" + item.Name + " row is clipped");
                occupiedHeight += bounds.Height + item.Margin.Vertical;
            }
            CheckMenuLayout(available.Count > 0, label + " has no available rows");
            for (int i = 0; i < available.Count; i++)
            {
                if (i > 0) CheckMenuLayout(available[i].Bounds.Top >= available[i - 1].Bounds.Bottom, label + " rows are out of order or overlap");
                for (int j = i + 1; j < available.Count; j++)
                    CheckMenuLayout(!available[i].Bounds.IntersectsWith(available[j].Bounds), label + " rows overlap: " + available[i].Name + "/" + available[j].Name);
            }
            // A compact root exposes groups instead of all expression/outfit/size choices at once.
            if (root)
            {
                CheckMenuLayout(commandCount <= 12, label + " root has too many ungrouped commands");
                CheckMenuLayout(Object.ReferenceEquals(available[available.Count - 1], commands["exit"]), label + " exit is not the final available row");
                CheckMenuLayout(!(available[0] is ToolStripSeparator), label + " starts with a separator");
                for (int i = 1; i < available.Count; i++)
                    CheckMenuLayout(!(available[i] is ToolStripSeparator && available[i - 1] is ToolStripSeparator), label + " has adjacent separators");
            }
            int slack = strip.Height - occupiedHeight;
            CheckMenuLayout(slack <= strip.Padding.Vertical + Math.Max(12, strip.Font.Height), label + " has excessive vertical blank space");
            CheckMenuLayout(!strip.Visible, label + " unexpectedly became visible during layout");
            results.AppendLine("PASS: hidden WinForms " + label + " " + strip.Width + "x" + strip.Height + ", " + commandCount + " commands; rows fit without overlap or clipping");
            return strip.Size;
        }

        private void SaveHiddenMenu(string path, string label)
        {
            using (Bitmap image = new Bitmap(menu.Width, menu.Height, PixelFormat.Format32bppArgb))
            {
                // DrawToBitmap paints the real control and its renderer; no Show, popup, or input calls.
                menu.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                int background = image.GetPixel(image.Width / 2, Math.Min(2, image.Height - 1)).ToArgb();
                int ink = 0;
                for (int y = 5; y < image.Height - 5; y++)
                    for (int x = Math.Min(40, image.Width / 3); x < image.Width - 12; x++)
                    {
                        Color pixel = image.GetPixel(x, y);
                        if (pixel.A > 0 && pixel.ToArgb() != background && pixel.R + pixel.G + pixel.B < 450) ink++;
                    }
                CheckMenuLayout(ink > 100, label + " DrawToBitmap produced a blank preview");
                image.Save(path, ImageFormat.Png);
            }
            CheckMenuLayout(!menu.Visible, label + " preview must not display the menu");
        }

        private void SmokeMenuLayout(string directory, StringBuilder results)
        {
            if (menu == null) BuildMenu();
            CheckMenuLayout(!menu.Visible, "diagnostic menu is already visible");
            int savedSide = dockSide;
            bool savedPaused = paused;
            Size savedSize = menu.Size;
            try
            {
                Size expandedSize = Size.Empty;
                Size dockedPausedSize = Size.Empty;
                foreach (int side in new int[] { 0, -1 })
                foreach (bool isPaused in new bool[] { false, true })
                {
                    dockSide = side;
                    paused = isPaused;
                    RefreshMenu();
                    string label = (side == 0 ? "expanded" : "docked") + (isPaused ? "-paused" : "-active");
                    CheckMenuLayout(commands["expand"].Available == (side != 0), label + " expand row visibility");
                    CheckMenuLayout(commands["resume"].Available == isPaused, label + " resume row visibility");
                    Size actual = LayoutHiddenMenu(menu, label, true, results);
                    if (side == 0 && !isPaused)
                    {
                        expandedSize = actual;
                        SaveHiddenMenu(Path.Combine(directory, "menu-expanded.png"), label);
                    }
                    if (side != 0 && isPaused)
                    {
                        dockedPausedSize = actual;
                        SaveHiddenMenu(Path.Combine(directory, "menu-docked-paused.png"), label);
                    }
                }
                CheckMenuLayout(dockedPausedSize.Height > expandedSize.Height, "context rows did not expand the actual layout");
                foreach (string id in new string[] { "actions", "wardrobe", "shape", "edge" })
                    LayoutHiddenMenu(commands[id].DropDown, "submenu-" + id, false, results);
                results.AppendLine("PASS: all four dock/pause combinations have contextual expand/resume rows, compact root and exit last; two real control previews exported without showing a window or moving the cursor");
            }
            finally
            {
                dockSide = savedSide;
                paused = savedPaused;
                RefreshMenu();
                menu.Size = savedSize;
                menu.PerformLayout();
            }
        }
    }
}
