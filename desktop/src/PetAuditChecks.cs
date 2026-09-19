using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        private void SmokeInterruptedPlacement(StringBuilder results)
        {
            if (!bank.HasOfficialMotions) return;
            foreach (string clothing in Outfits)
            foreach (int preset in new[] { 160, 240, 360, 432 })
            foreach (int side in new[] { -1, 1 })
            {
                variant = "official"; outfit = clothing; petSize = SizeForPreset(preset);
                ResetInteractionCheck(); gravityEnabled = false; breathing = false;
                Rectangle area = Screen.PrimaryScreen.WorkingArea;
                PinOfficialPose("sit");
                Location = CornerPlacement(area); windowY = Top;
                RectangleF sit = GetOfficialRestBounds();
                int expectedCenter = (int)Math.Round(Top + sit.Top + petSize * .09f);
                DockAtSide(side);
                int clampedCenter = Clamp(expectedCenter - Height / 2, area.Top + 12, area.Bottom - Height - 12) + Height / 2;
                CheckInteraction(dockCenterY == clampedCenter, "menu dock follows seated head instead of standing height");
                ExpandDock(false, Point.Empty);
                CheckInteraction(OfficialAction() == "sit", "expanding restores sitting artwork");

                ResetInteractionCheck(); gravityEnabled = false;
                pressed = dragging = true;
                Location = new Point(side < 0 ? PlacementLeftLimit(area) : PlacementRightLimit(area), PlacementFloor(area));
                windowY = Top;
                OnMouseCaptureChanged(EventArgs.Empty);
                RectangleF after = VisiblePlacementBounds(Bounds);
                CheckInteraction(!pressed && !dragging && !falling && !Capture, "interrupted free drag clears capture and motion");
                CheckInteraction(Math.Abs(side < 0 ? after.Left - area.Left : after.Right - area.Right) <= 1.1,
                    "interrupted drag retains visible edge after pickup becomes standing");
                results.AppendLine("PASS: interrupted placement " + clothing + "/" + preset + "/" + side + ": seated menu dock and lost-capture edge stable");
            }
            variant = "standard"; outfit = "maid"; petSize = 240;
            ResetInteractionCheck();
        }

        private void SmokeSleepPlacement(StringBuilder results)
        {
            if (!bank.HasOfficialMotions) return;
            foreach (string clothing in Outfits)
            foreach (int preset in new[] { 160, 240, 360, 432 })
            {
                variant = "official"; outfit = clothing; petSize = SizeForPreset(preset);
                foreach (string pose in new[] { "sit", "wave", "pickup" })
                foreach (int side in new[] { -1, 1 })
                {
                    ResetInteractionCheck(); gravityEnabled = false; breathing = false;
                    PinOfficialPose(pose);
                    Rectangle area = Screen.PrimaryScreen.WorkingArea;
                    RectangleF shape = PlacementFootprint();
                    // Put the shorter pose at the top corner: sleeping is taller,
                    // so retaining the old window location would clip its head.
                    Location = new Point(side < 0 ? PlacementLeftLimit(area) : PlacementRightLimit(area),
                        (int)Math.Ceiling(area.Top - shape.Top));
                    windowY = Top;
                    foreach (bool asleep in new[] { true, false })
                    {
                        commands["sleep"].PerformClick();
                        RectangleF visible = VisiblePlacementBounds(Bounds);
                        CheckInteraction(sleeping == asleep && !falling && dockSide == 0, "sleep toggles without falling");
                        CheckInteraction(visible.Top >= area.Top - 1 && visible.Bottom <= area.Bottom + 1,
                            "sleep and wake keep the entire character in the work area");
                        CheckInteraction(Math.Abs((side < 0 ? visible.Left - area.Left : visible.Right - area.Right)) <= 1.1,
                            "sleep and wake preserve the visible screen edge");
                        CheckInteraction(Math.Abs(windowY - Top) < .01, "sleep placement keeps integrator in sync");
                        CheckMenuState("sleep placement " + clothing + "/" + preset);
                    }
                }
                results.AppendLine("PASS: sleep placement " + clothing + "/" + preset + ": sit/wave/pickup at both upper corners stay on-screen, aligned, and wake normally");
            }
            variant = "standard"; outfit = "maid"; petSize = 240;
            ResetInteractionCheck();
        }
    }
}
