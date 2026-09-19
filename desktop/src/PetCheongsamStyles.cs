using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        private void ChangeCheongsamHair(bool updo)
        {
            if (!bank.HasCheongsamStyles || variant != "official" || outfit != "red-cheongsam") return;
            if (bank.CheongsamUpdo == updo) { if (menu != null) RefreshMenu(); return; }
            RectangleF previous = PlacementFootprint();
            Rectangle area = dockSide == 0 ? PlacementWorkingArea() : dockArea;
            bank.CheongsamUpdo = updo;
            // Standing/grip pixels and their cached bounds are identical for
            // both action styles. Motion bounds come directly from the selected bitmap.
            if (dockSide != 0) { ResizeCanvas(); PositionDock(); }
            else ReconcileOfficialPosePlacement(previous, area, falling);
            if (menu != null) RefreshMenu();
            DrawFrame();
        }

        private void SmokeCheongsamStyles(string directory, StringBuilder results)
        {
            if (!bank.HasCheongsamStyles)
            {
                CheckInteraction(!commands.ContainsKey("cheongsam-hair"), "Q-only package has no cheongsam hair submenu");
                return;
            }
            CheckInteraction(bank.CheongsamUpdo, "cheongsam action style defaults to updo");
            variant = "official"; outfit = "red-cheongsam"; petSize = 240; ResetInteractionCheck();
            Bitmap standing = bank.Get("official", outfit, "idle");
            Bitmap grip = bank.GetOfficialMotion(outfit, "grip");
            Bitmap updo = bank.GetOfficialMotion(outfit, "sit");
            ChangeCheongsamHair(false);
            CheckInteraction(Object.ReferenceEquals(standing, bank.Get("official", outfit, "idle")), "hair style shares original standing frame");
            CheckInteraction(Object.ReferenceEquals(grip, bank.GetOfficialMotion(outfit, "grip")), "hair style shares original updo grip");
            CheckInteraction(!Object.ReferenceEquals(updo, bank.GetOfficialMotion(outfit, "sit")), "action bitmap cache separates hair styles");
            ChangeCheongsamHair(true);
            CheckInteraction(Object.ReferenceEquals(updo, bank.GetOfficialMotion(outfit, "sit")), "returning to hair style reuses correct cached bitmap");

            foreach (int size in new[] { 160, 240, 360 })
            foreach (string state in new[] { "idle", "closed", "annoyed", "happy", "shy", "sit", "wave", "pickup", "left-grip", "right-grip" })
            {
                variant = "official"; outfit = "red-cheongsam"; petSize = size; ResetInteractionCheck(); breathing = false;
                if (state == "sit") seated = true;
                else if (state == "wave") { feedbackKind = "hair"; feedbackUntil = simulationTime + 10; }
                else if (state == "pickup") dragging = true;
                else if (state.EndsWith("grip"))
                {
                    DockAtSide(state == "left-grip" ? -1 : 1, landingArea, landingArea.Top + landingArea.Height / 2);
                    dockStarted = clock.Elapsed.TotalSeconds - 1;
                }
                else { feedbackExpression = state; feedbackUntil = simulationTime + 10; }
                string expectedAction = OfficialAction();
                int expectedSide = dockSide;
                foreach (bool style in new[] { false, true, false, true })
                {
                    commands[style ? "cheongsam-updo" : "cheongsam-long-hair"].PerformClick();
                    RefreshMenu();
                    CheckInteraction(bank.CheongsamUpdo == style && commands["cheongsam-updo"].Checked == style && commands["cheongsam-long-hair"].Checked != style,
                        "hair menu switches/checks correct style");
                    CheckInteraction(commands["cheongsam-hair"].Available && variant == "official" && outfit == "red-cheongsam" && petSize == size,
                        "hair menu is scoped to official cheongsam without adding an outfit");
                    CheckInteraction(OfficialAction() == expectedAction && dockSide == expectedSide, "hair switch preserves current pose/dock state");
                    AssertTransparentVisibleFrame("cheongsam style " + state);
                    if (dockSide != 0)
                    {
                        CheckInteraction(dockSide < 0 ? Left == dockArea.Left : Right == dockArea.Right, "hair switch preserves dock side anchor");
                        AssertGripCanvasComplete("cheongsam style " + state);
                    }
                    if (size == 360)
                        canvas.Save(Path.Combine(directory, "cheongsam-" + (style ? "updo" : "long-hair") + "-" + state + ".png"), ImageFormat.Png);
                }
                results.AppendLine("PASS: cheongsam action hair styles " + size + "/" + state + ": menu roundtrips preserve selected pose, outfit, size and edge anchor");
            }
            foreach (string type in new[] { "standard", "big-head", "official" })
            foreach (string clothing in Outfits)
            {
                variant = type; outfit = clothing; petSize = 240; ResetInteractionCheck(); RefreshMenu();
                CheckInteraction(commands["cheongsam-hair"].Available == (type == "official" && clothing == "red-cheongsam"),
                    "hair submenu visibility excludes Q variants and other outfits");
            }
            CheckInteraction(Outfits.Length == 5, "hair alternatives do not expand global outfit list");
            bank.CheongsamUpdo = true; crops.Clear(); gripCrops.Clear();
            variant = "standard"; outfit = "maid"; petSize = 240; ResetInteractionCheck(); RefreshMenu();
            results.AppendLine("PASS: cheongsam updo is default; long-hair actions remain selectable, standing/grip resources shared and Q wardrobe unchanged");
        }
    }
}
