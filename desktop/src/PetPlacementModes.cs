using System;
using System.Drawing;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        // Gravity is an optional extra. Placement and a resting pose are persistent.
        private bool gravityEnabled;
        private string pinnedOfficialPose;
        private bool IsSittingRestPose() { return seated || pinnedOfficialPose == "sit"; }

        private void HoldPlacement(Rectangle area)
        {
            falling = false;
            verticalSpeed = tilt = dragSpeed = dragMagnitude = 0;
            jumpTime = landingTime = -10;
            landPoseUntil = 0;
            annoyedUntil = feedbackUntil = 0;
            Location = ClampPlacement(Location, area);
            windowY = Top;
            DrawFrame();
        }

        private void ToggleGravity()
        {
            gravityEnabled = !gravityEnabled;
            if (dockSide != 0) return;
            if (gravityEnabled) BeginFall(PlacementWorkingArea());
            else HoldPlacement(PlacementWorkingArea());
        }

        private void PinOfficialPose(string pose)
        {
            if (variant != "official" || !bank.HasOfficialMotions) return;
            PrepareInteraction();
            RectangleF previous = PlacementFootprint();
            pinnedOfficialPose = pose == "sit" || pose == "wave" || pose == "pickup" ? pose : null;
            seated = pose == "sit";
            sleeping = false;
            feedbackUntil = annoyedUntil = 0;
            jumpTime = landingTime = -10;
            ReconcileOfficialPosePlacement(previous, false);
            DrawFrame();
        }
    }
}
