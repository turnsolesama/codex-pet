using System;
using System.Drawing;
using System.Text;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        private void SmokeFreePlacement(StringBuilder results)
        {
            CheckInteraction(!gravityEnabled, "gravity defaults off");
            if (menu == null) BuildMenu();
            string[] variants = bank.HasOfficialMotions ? new[] { "standard", "big-head", "official" } : new[] { "standard", "big-head" };
            foreach (string type in variants)
            foreach (string clothing in Outfits)
            foreach (string pose in type == "official" ? new[] { "stand", "sit", "wave", "pickup" } : new[] { "stand" })
            {
                variant = type; outfit = clothing; petSize = SizeForPreset(240);
                ResetInteractionCheck(); gravityEnabled = false; breathing = false;
                Rectangle area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                if (type == "official") PinOfficialPose(pose);
                else if (CanSit()) ToggleSit();
                Location = new Point(area.Left + 300, area.Top + 100); windowY = Top;
                bool beforeSeated = seated;
                Point before = Location;
                pressed = true; dragging = false;
                downWindow = Location;
                downCursor = lastCursor = new Point(area.Left + 450, area.Top + 300);
                Point release = new Point(downCursor.X + 40, downCursor.Y + 30);
                MovePointer(release, area);
                CheckInteraction(seated == beforeSeated, "free drag keeps sitting state");
                ReleasePointer(release, area);
                CheckInteraction(Location == new Point(before.X + 40, before.Y + 30), "free drag stays at release position");
                CheckInteraction(!falling && verticalSpeed == 0, "free release has no gravity");
                Point held = Location;
                for (int i = 0; i < 80; i++) StepFall(.05);
                CheckInteraction(Location == held, "free placement remains stable over time");
                if (type == "official")
                {
                    CheckInteraction(OfficialAction() == (pose == "stand" ? null : pose), "resting pose survives drag release");
                    if (pose == "sit")
                    {
                        RectangleF shape = GetOfficialRestBounds();
                        pressedPart = HitPart(new Point((int)(shape.Left + shape.Width / 2), (int)(shape.Top + petSize * .13)));
                        CheckInteraction(pressedPart == "face", "pinned sitting uses face hit region instead of generic pose");
                        pressed = true;
                        ReleasePointer(release, area);
                    }
                    else ReactToPart("face");
                    CheckInteraction(OfficialAction() == null && CurrentExpression() == "shy", "resting pose allows temporary face reaction");
                    simulationTime = feedbackUntil + .1;
                    DrawFrame();
                    CheckInteraction(OfficialAction() == (pose == "stand" ? null : pose), "resting pose returns after reaction");
                    if (pose == "sit")
                    {
                        RefreshMenu();
                        CheckInteraction(commands["sit"].Checked && commands["rest-sit"].Checked, "sitting menu remains aligned after reaction");
                        commands["sit"].PerformClick();
                        CheckInteraction(!IsSittingRestPose() && OfficialAction() == null, "sitting menu stands up in one click");
                        PinOfficialPose("sit");
                    }
                }
                float oldBottom = Top + PlacementFootprint().Bottom;
                ChangeSize(SizeForPreset(160));
                CheckInteraction(Math.Abs(Top + PlacementFootprint().Bottom - oldBottom) < 1.1, "free resize keeps foot anchor above floor");
                // Capture cancellation uses the same hold path as release.
                BeginFall(area);
                CheckInteraction(!falling, "capture-loss fall request respects gravity off");
                results.AppendLine("PASS: free placement " + type + "/" + clothing + "/" + pose + " drag, hold, pose, reaction, resize and interrupted drag");
            }
            variant = "standard"; outfit = "maid"; petSize = 240;
            ResetInteractionCheck(); gravityEnabled = false;
            Location = new Point(200, 100); windowY = Top;
            commands["gravity"].PerformClick();
            CheckInteraction(gravityEnabled && falling, "extra gravity can start falling");
            StepFall(.05);
            int stoppedTop = Top;
            commands["gravity"].PerformClick();
            CheckInteraction(!gravityEnabled && !falling && Top == stoppedTop, "turning gravity off holds current position");
            RefreshMenu();
            CheckInteraction(!commands["gravity"].Checked && commands["gravity"].OwnerItem == commands["extras"], "gravity lives in extras and is off");
            ResetInteractionCheck();
            results.AppendLine("PASS: optional gravity on/off transition and menu placement");
        }
    }
}
