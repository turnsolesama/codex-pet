using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        private bool CanSit() { return SupportsPoses() || (variant == "official" && bank.HasOfficialMotions); }
        private string OfficialAction()
        {
            if (variant != "official" || !bank.HasOfficialMotions || dockSide != 0 || suppressPoses) return null;
            if (dragging && !gravityEnabled)
            {
                if (seated) return "sit";
                if (pinnedOfficialPose != null) return pinnedOfficialPose;
                if (sleeping) return null;
            }
            if (dragging || falling) return "pickup";
            if (sleeping) return null;
            if (seated) return "sit";
            if (simulationTime < feedbackUntil) return feedbackKind == "hair" ? "wave" : null;
            return pinnedOfficialPose;
        }
        private float OfficialActionScale(string pose)
        {
            // Waving is an upright full-body pose: changing to it must not shrink the character.
            // Seated, lifted and edge poses still use their authored head calibration.
            if (pose == "wave") return (float)petSize / bank.GetOfficialMotion(outfit, pose).Height;
            return petSize * .18f / bank.OfficialMotionHead(outfit,pose);
        }
        private RectangleF GetOfficialRestBounds()
        {
            string pose = OfficialAction();
            float width, height;
            if (pose != null)
            {
                Bitmap image = bank.GetOfficialMotion(outfit,pose);
                float scale = OfficialActionScale(pose);
                width = image.Width * scale; height = image.Height * scale;
            }
            else
            {
                Rectangle source = GetCrop("official",outfit);
                height = petSize; width = height * source.Width / source.Height;
            }
            return new RectangleF((canvas.Width-width)/2f,canvas.Height-7-height,width,height);
        }
        private string OfficialActionHitPart(PointF point)
        {
            RectangleF shape = GetOfficialRestBounds();
            float fromTop = point.Y + shape.Height;
            string pose = OfficialAction();
            float headHeight = bank.OfficialMotionHead(outfit, pose) * OfficialActionScale(pose);
            if (fromTop < headHeight * (.085f / .18f)) return "hair";
            if (fromTop < headHeight) return "face";
            return fromTop < shape.Height * .85f ? "body" : "feet";
        }
        private void DrawOfficialAction(Graphics g, string pose)
        {
            Bitmap sprite = bank.GetOfficialMotion(outfit,pose);
            RectangleF shape = GetOfficialRestBounds();
            double age = simulationTime-feedbackStarted;
            double breath = breathing ? Math.Sin(simulationTime*2.2)*.004 : 0;
            double angle = pose == "pickup" ? tilt*.5 : pose == "wave" ? Math.Sin(age*5)*Math.Exp(-age*2)*1.5 : 0;
            double padding = Math.Abs(Math.Sin(angle*Math.PI/180))*shape.Width/2;
            g.TranslateTransform(canvas.Width/2f,(float)(canvas.Height-7-padding));
            g.RotateTransform((float)angle);
            g.ScaleTransform(1,(float)(1+breath));
            inverseCharacterTransform=g.Transform;inverseCharacterTransform.Invert();
            g.DrawImage(sprite,new RectangleF(-shape.Width/2,-shape.Height,shape.Width,shape.Height));
            g.ResetTransform();
            if (simulationTime < feedbackUntil && bubbleText.Length > 0) DrawBubble(g,(int)(shape.Top-40));
        }
        private void SmokeOfficialActions(string directory,StringBuilder results)
        {
            if (!bank.HasOfficialMotions) return;
            foreach (string clothing in Outfits)
            {
                variant="official";outfit=clothing;petSize=360;ResetInteractionCheck();breathing=false;
                CheckInteraction(CanSit(),"official sitting is available");
                ToggleSit();CheckInteraction(seated && OfficialAction()=="sit","official dedicated sitting selected");
                DrawFrame();canvas.Save(Path.Combine(directory,"official-"+clothing+"-sit.png"),ImageFormat.Png);
                StandUp();ReactToPart("hair");
                CheckInteraction(OfficialAction()=="wave","official head touch selects greeting pose");
                canvas.Save(Path.Combine(directory,"official-"+clothing+"-wave.png"),ImageFormat.Png);
                ResetInteractionCheck();dragging=true;
                CheckInteraction(OfficialAction()=="pickup","official drag selects lifted pose");
                DrawFrame();canvas.Save(Path.Combine(directory,"official-"+clothing+"-pickup.png"),ImageFormat.Png);
                ResetInteractionCheck();
                results.AppendLine("PASS: official motion "+clothing+": sitting, standing, greeting, pickup and alpha-rendered action frames");
            }
            variant="standard";outfit="maid";petSize=240;ResetInteractionCheck();
        }
    }
}
