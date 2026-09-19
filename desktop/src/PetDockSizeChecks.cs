using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        private void SmokeDockSizes(string directory, StringBuilder results)
        {
            foreach (string type in new string[] { "standard", "big-head" })
            foreach (string clothing in Outfits)
            foreach (int side in new int[] { -1, 1 })
            {
                ResetInteractionCheck();
                ChangeVariant(type); ChangeOutfit(clothing);
                Rectangle area = new Rectangle(-1920,-1080,1920,1080);
                DockAtSide(side,area,-540);
                dockStarted = clock.Elapsed.TotalSeconds - 1;
                DrawFrame();
                paused = true;
                Rectangle largest = Rectangle.Empty;
                int originalCenter = dockCenterY;
                foreach (int size in new int[] { 360,160,240,432,360 })
                {
                    commands["size-"+size].PerformClick();
                    Rectangle visible = FindBitmapBounds(canvas,"size check",0);
                    CheckInteraction(dockSide == side && paused && !falling && dockCenterY == originalCenter,
                        "dock resize preserves side, pause, center and stable placement");
                    CheckInteraction(side < 0 ? Left == area.Left : Right == area.Right, "dock resize stays flush to its edge");
                    if (largest.IsEmpty) largest = visible;
                    else
                    {
                        double scale = size / 360.0;
                        CheckInteraction(Math.Abs(visible.Width-largest.Width*scale) <= 2 && Math.Abs(visible.Height-largest.Height*scale) <= 2,
                            "visible grip pixels scale proportionally and restore on size roundtrip");
                    }
                    if (clothing == "maid") canvas.Save(Path.Combine(directory,type+"-size-"+size+(side<0?"-left":"-right")+".png"),ImageFormat.Png);
                }
                results.AppendLine("PASS: dock size menu "+type+"/"+clothing+"/"+side+": 360-160-240-432-360 visible bounds proportional, edge and vertical center fixed while paused");
            }
            petSize=240; ResetInteractionCheck();
        }
    }
}
