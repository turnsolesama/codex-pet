using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        internal void SetInitialVariant(string value)
        {
            ChangeVariant(value);
            if (value == "official") { petSize = SizeForPreset(360); ResizeCanvas(); }
        }

        private int SizeForPreset(int preset)
        {
            return variant == "official" ? (int)Math.Round(preset * 4.0 / 3.0) : preset;
        }

        private double OfficialHeadFraction() { return 0.18; }
        private string OfficialHitPart(double y)
        {
            if (y < 0.085) return "hair";
            if (y < 0.18) return "face";
            return y < 0.85 ? "body" : "feet";
        }

        private Rectangle OfficialPeekCrop()
        {
            if (bank.HasOfficialMotions) { Bitmap motion=bank.GetOfficialMotion(outfit,"grip"); return new Rectangle(0,0,motion.Width,motion.Height); }
            string key = "official/" + outfit;
            Rectangle cached;
            if (gripCrops.TryGetValue(key, out cached)) return cached;
            Rectangle body = GetCrop("official", outfit);
            // Retain the original neck and shoulders. No generated hands or Q artwork are used.
            int height = (int)Math.Ceiling(body.Height * 0.30);
            int width = Math.Min(body.Width, (int)Math.Ceiling(body.Height * 0.23));
            Rectangle peek = new Rectangle(body.Left + (body.Width - width) / 2, body.Top, width, height);
            Bitmap sprite = bank.Get("official", outfit, "idle");
            int left = peek.Right, right = peek.Left;
            for (int y = peek.Top; y < peek.Bottom; y++)
                for (int x = peek.Left; x < peek.Right; x++)
                    if (sprite.GetPixel(x,y).A > 12) { left = Math.Min(left,x); right = Math.Max(right,x); }
            // The visible shoulder/hair contour, rather than transparent canvas, touches the edge.
            cached = new Rectangle(left,peek.Top,right-left+1,peek.Height);
            gripCrops[key] = cached;
            return cached;
        }

        private void DrawOfficialPeek(Graphics g)
        {
            if (bank.HasOfficialMotions)
            {
                Bitmap motion=bank.GetOfficialMotion(outfit,"grip");
                float scale=OfficialActionScale("grip"), w=motion.Width*scale, h=motion.Height*scale;
                double entry=DockProgress(clock.Elapsed.TotalSeconds);
                float offset=(float)((1-entry)*w);
                g.TranslateTransform(dockSide<0 ? -offset : canvas.Width+offset,canvas.Height/2f);
                g.ScaleTransform(dockSide<0 ? 1 : -1,1);
                g.DrawImage(motion,new RectangleF(0,-h/2,w,h));
                g.ResetTransform();
                if(entry>=1) dockTransitionPending=false;
                return;
            }
            Rectangle source = OfficialPeekCrop();
            float height = GripHeight();
            float width = height * source.Width / source.Height;
            double progress = DockProgress(clock.Elapsed.TotalSeconds);
            float retract = (float)((1 - progress) * width);
            float x = dockSide < 0 ? -retract : canvas.Width - width + retract;
            // Keep the official asymmetric hair accessory in its original orientation on both sides.
            g.DrawImage(bank.Get("official", outfit, CurrentExpression()),
                new RectangleF(x, (canvas.Height - height) / 2f, width, height), source, GraphicsUnit.Pixel);
            if (progress >= 1) dockTransitionPending = false;
        }

        private void SmokeOfficial(string directory, StringBuilder results)
        {
            if (!bank.HasOfficial) { results.AppendLine("NOTE: official assets absent; public Q-only build"); return; }
            foreach (string clothing in Outfits)
            {
                variant = "official"; outfit = clothing; petSize = 360;
                ResetInteractionCheck(); breathing = false;
                Rectangle common = GetCrop(variant, outfit);
                Size sourceSize = Size.Empty;
                foreach (string expression in new string[] { "idle", "closed", "annoyed", "happy", "shy" })
                {
                    Bitmap frame = bank.Get(variant, outfit, expression);
                    if (sourceSize.IsEmpty) sourceSize = frame.Size;
                    CheckInteraction(frame.Size == sourceSize, "official expression canvas alignment");
                    CheckInteraction(frame.GetPixel(0,0).A == 0, "official original transparent alpha");
                    CheckInteraction(common.Contains(FindBitmapBounds(frame,"official",0)), "official all-expression crop contains artwork");
                    feedbackExpression = expression; feedbackUntil = simulationTime + 10;
                    DrawFrame();
                    canvas.Save(Path.Combine(directory,"official-"+clothing+"-"+expression+".png"),ImageFormat.Png);
                }
                feedbackUntil = 0;
                foreach (string part in new string[] { "hair", "face", "body", "feet" })
                {
                    ResetInteractionCheck();
                    Point point = VisiblePartPoint(part);
                    CheckInteraction(HitPart(point) == part, "official visible click region " + part);
                    ReactToPart(part); CheckReaction(part,"official/"+clothing+"/"+part);
                }
                ResetInteractionCheck(); ToggleSleep();
                CheckInteraction(sleeping && CurrentExpression()=="closed" && CurrentPose()==null,"official sleeping uses official eyes");
                ToggleSleep();
                foreach (int side in new int[] { -1,1 })
                {
                    DockAtSide(side,new Rectangle(-1920,0,1920,1080),540);
                    dockStarted=clock.Elapsed.TotalSeconds-1; paused=true;
                    Rectangle large=Rectangle.Empty;
                    foreach(int size in new int[] {480,213,320,576,480})
                    {
                        ChangeSize(size);
                        Rectangle visible=FindBitmapBounds(canvas,"official peek",0);
                        CheckInteraction(side<0 ? Left==dockArea.Left : Right==dockArea.Right,"official edge anchored");
                        bool touchesEdge = false;
                        int edgeX = side < 0 ? 0 : canvas.Width - 1;
                        for (int y = 0; y < canvas.Height; y++) if (canvas.GetPixel(edgeX,y).A > 12) { touchesEdge=true; break; }
                        CheckInteraction(touchesEdge,"official visible pixels touch desktop edge");
                        if(large.IsEmpty) large=visible;
                        else CheckInteraction(Math.Abs(visible.Height-large.Height*size/480.0)<=2 && Math.Abs(visible.Width-large.Width*size/480.0)<=2,"official peek size proportional");
                    }
                    canvas.Save(Path.Combine(directory,"official-"+clothing+"-edge-"+side+".png"),ImageFormat.Png);
                    ChangeVariant("standard"); ChangeVariant("big-head"); ChangeVariant("official");
                    CheckInteraction(dockSide==side && variant=="official" && outfit==clothing,"official variant roundtrip preserves outfit/side");
                    ExpandDock(false,Point.Empty);
                }
                results.AppendLine("PASS: official "+clothing+" alpha, five expressions, body hit regions, reactions, sleep, edge size roundtrip and variant switching");
            }
            variant="standard";outfit="maid";petSize=240;ResetInteractionCheck();
        }
    }
}
