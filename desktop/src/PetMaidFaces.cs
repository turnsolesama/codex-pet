using System.Drawing;
using System.Text;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        private void ChangeMaidFace(bool cute)
        {
            if (!bank.HasMaidFaceStyles || variant != "official" || outfit != "maid") return;
            RectangleF before = PlacementFootprint();
            Rectangle area = dockSide == 0 ? PlacementWorkingArea() : dockArea;
            bank.MaidCuteFace = cute;
            if (dockSide == 0) ReconcileOfficialPosePlacement(before, area, falling);
            if (menu != null) RefreshMenu();
            DrawFrame();
        }

        private void SmokeMaidFaces(StringBuilder results)
        {
            if (!bank.HasMaidFaceStyles) return;
            CheckInteraction(!bank.MaidCuteFace, "maid defaults to classic face");
            variant = "official"; outfit = "maid"; petSize = SizeForPreset(360);
            ResetInteractionCheck(); gravityEnabled = false;
            Bitmap original = bank.Get("official", "maid", "idle");
            Bitmap grip = bank.GetOfficialMotion("maid", "grip");
            foreach (string pose in new[] { "sit", "wave", "pickup" })
            {
                PinOfficialPose(pose);
                Point location = Location;
                Bitmap classic = bank.GetOfficialMotion("maid", pose);
                commands["maid-cute"].PerformClick();
                CheckInteraction(bank.MaidCuteFace && !object.ReferenceEquals(classic, bank.GetOfficialMotion("maid", pose)), "maid face changes selected action resource");
                CheckInteraction(OfficialAction() == pose && pinnedOfficialPose == pose && !falling, "maid face preserves pinned action");
                CheckInteraction(Location == location, "maid face switch preserves placement");
                commands["maid-classic"].PerformClick();
                CheckInteraction(object.ReferenceEquals(classic, bank.GetOfficialMotion("maid", pose)), "maid classic frame returns from separate cache");
                CheckInteraction(object.ReferenceEquals(original, bank.Get("official", "maid", "idle")) && object.ReferenceEquals(grip, bank.GetOfficialMotion("maid", "grip")), "maid original standing and grip stay unchanged");
                results.AppendLine("PASS: maid " + pose + " classic/cute menu switch preserves pose, placement, standing and grip");
            }
            bank.MaidCuteFace = false;
            variant = "standard"; outfit = "maid"; petSize = 240;
            ResetInteractionCheck();
        }
    }
}
