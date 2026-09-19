using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        // Use the stable visible pose, not the oversized transparent animation
        // canvas or a changing breath/tilt frame, as the official placement anchor.
        private RectangleF PlacementFootprint()
        {
            if (variant != "official" || dockSide != 0) return new RectangleF(0, 0, Width, Height);
            return GetOfficialRestBounds();
        }

        private RectangleF VisiblePlacementBounds(Rectangle windowBounds)
        {
            RectangleF visible = PlacementFootprint();
            visible.Offset(windowBounds.Location);
            return visible;
        }

        private Rectangle PlacementWorkingArea()
        {
            if (variant != "official" || dockSide != 0) return Screen.FromRectangle(Bounds).WorkingArea;
            return Screen.FromRectangle(Rectangle.Ceiling(VisiblePlacementBounds(Bounds))).WorkingArea;
        }

        private int PlacementLeftLimit(Rectangle area)
        {
            return (int)Math.Ceiling(area.Left - PlacementFootprint().Left);
        }

        private int PlacementRightLimit(Rectangle area)
        {
            return (int)Math.Floor(area.Right - PlacementFootprint().Right);
        }

        private int PlacementFloor(Rectangle area)
        {
            // All official action rectangles share this exact foot anchor. Do not
            // derive it by adding float Top + Height: action sizes can round their
            // sum across an integer boundary and make a landed window jump 1px.
            if (variant == "official" && dockSide == 0) return area.Bottom - (canvas.Height - 7);
            return (int)Math.Floor(area.Bottom - PlacementFootprint().Bottom);
        }

        private Point ClampPlacement(Point desired, Rectangle area)
        {
            RectangleF visible = PlacementFootprint();
            return new Point(Clamp(desired.X, PlacementLeftLimit(area), PlacementRightLimit(area)),
                Clamp(desired.Y, (int)Math.Ceiling(area.Top - visible.Top), PlacementFloor(area)));
        }

        private Point CornerPlacement(Rectangle area)
        {
            return new Point(Math.Max(PlacementLeftLimit(area), PlacementRightLimit(area) - 18), PlacementFloor(area));
        }

        // Capture PlacementFootprint() before changing official pose/outfit state,
        // then call this after the change. A silhouette touching an edge stays at
        // that edge even when pickup, sitting or greeting changes its width.
        // preserveVertical is required during StepFall, whose integrator exclusively
        // owns windowY; it also prevents a pose change from becoming an upward step.
        private void ReconcileOfficialPosePlacement(RectangleF previousFootprint, Rectangle area, bool preserveVertical)
        {
            if (variant != "official" || dockSide != 0) return;
            RectangleF current = PlacementFootprint();
            if (current == previousFootprint) return;
            bool touchesLeft = Math.Abs(Left + previousFootprint.Left - area.Left) <= 1.1;
            bool touchesRight = Math.Abs(Left + previousFootprint.Right - area.Right) <= 1.1;
            int desiredLeft = touchesLeft ? PlacementLeftLimit(area) : touchesRight ? PlacementRightLimit(area) : Left;
            Point adjusted = ClampPlacement(new Point(desiredLeft, Top), area);
            Left = adjusted.X;
            if (!preserveVertical)
            {
                Top = adjusted.Y;
                windowY = Top;
            }
        }

        private void ReconcileOfficialPosePlacement(RectangleF previousFootprint, bool preserveVertical)
        {
            if (variant != "official" || dockSide != 0) return;
            RectangleF previousScreenBounds = previousFootprint;
            previousScreenBounds.Offset(Location);
            Rectangle area = Screen.FromRectangle(Rectangle.Ceiling(previousScreenBounds)).WorkingArea;
            ReconcileOfficialPosePlacement(previousFootprint, area, preserveVertical);
        }

        // Invoked by the smoke-test entry point; never shows or starts another pet.
        internal void SmokeOfficialPlacement(StringBuilder results)
        {
            if (!bank.HasOfficial) return;
            foreach (string clothing in Outfits)
            foreach (int size in new int[] { 160, 240, 360 })
            foreach (Rectangle area in new Rectangle[] { new Rectangle(0, 0, 1920, 1080), new Rectangle(-1920, -1080, 1920, 1080) })
            {
                variant = "official"; outfit = clothing; petSize = size;
                ResetInteractionCheck(); breathing = false;
                Point corner = CornerPlacement(area);
                RectangleF footprint = PlacementFootprint();
                CheckInteraction(Math.Abs(area.Right - (corner.X + footprint.Right) - 18) < 1.1,
                    "official corner uses visible right edge");
                CheckInteraction(Math.Abs(corner.Y + footprint.Bottom - area.Bottom) < .001f, "official visible feet reach floor");
                foreach (int side in new int[] { -1, 1 })
                {
                    dockSide = 0; ResizeCanvas(); autoDockArmed = true;
                    Location = ClampPlacement(new Point(area.Left + area.Width / 2 - Width / 2, area.Top + 200), area);
                    downWindow = Location;
                    downCursor = lastCursor = new Point(Left + Width / 2, Top + Height - 7 - petSize / 2);
                    pressed = true; dragging = false;
                    Point edgeCursor = new Point(side < 0 ? area.Left : area.Right - 1, downCursor.Y);
                    MovePointer(edgeCursor, area);
                    RectangleF visible = VisiblePlacementBounds(Bounds);
                    CheckInteraction(visible.Left >= area.Left - .01f && visible.Right <= area.Right + .01f,
                        "official dragged silhouette remains inside work area");
                    CheckInteraction(side < 0 ? visible.Left - area.Left < 1.1 : area.Right - visible.Right < 1.1,
                        "official pointer-coordinate drag reaches visible screen edge");
                    CheckInteraction(side < 0 ? Left < area.Left : Right > area.Right,
                        "official transparent window may extend past screen edge");
                    ReleasePointer(edgeCursor, area);
                    CheckInteraction(dockSide == side && !falling, "official pointer release docks at both edges");

                    pressed = true; dragging = false;
                    downWindow = Location;
                    downCursor = lastCursor = new Point(side < 0 ? Left + 2 : Right - 2, Top + Height / 2);
                    int beforeTop = Top;
                    Point slidCursor = new Point(downCursor.X, downCursor.Y + 60);
                    MovePointer(slidCursor, area);
                    CheckInteraction(dockSide == side && Top == beforeTop + 60,
                        "official dock still slides vertically");
                    ReleasePointer(slidCursor, area);
                    ExpandDock(false, slidCursor);
                    visible = VisiblePlacementBounds(Bounds);
                    CheckInteraction(side < 0 ? Math.Abs(visible.Left - area.Left) < 1.1 : Math.Abs(visible.Right - area.Right) < 1.1,
                        "official click expansion preserves visible edge anchor");
                    BeginFall(area);
                    int frames = 0;
                    while (falling && frames++ < 240) StepFall(1.0 / 60);
                    RectangleF landed = VisiblePlacementBounds(Bounds);
                    CheckInteraction(!falling && (side < 0 ? Math.Abs(landed.Left-area.Left)<1.1 : Math.Abs(landed.Right-area.Right)<1.1) && Top == PlacementFloor(area),
                        "official fall preserves side placement and visible floor");
                    CheckInteraction(AutoDockSide(new Point(area.Left + area.Width / 2, area.Top + 500), area, Bounds) == 0,
                        "official center cursor does not dock");
                }
                results.AppendLine("PASS: official placement " + clothing + "/" + size + (area.Left < 0 ? " negative monitor" : " primary monitor") +
                    ": visible corner, pointer-coordinate both-edge docking, transparent overflow, vertical slide, expansion and fall");
            }
            foreach (string type in new string[] { "standard", "big-head" })
            {
                variant = type; outfit = "maid"; petSize = 240; ResetInteractionCheck();
                Rectangle area = new Rectangle(-1920, -1080, 1920, 1080);
                CheckInteraction(PlacementFootprint() == new RectangleF(0, 0, Width, Height), "Q placement still uses full window");
                CheckInteraction(CornerPlacement(area) == new Point(Math.Max(area.Left, area.Right - Width - 18), area.Bottom - Height),
                    "Q corner placement remains unchanged");
            }
            SmokeOfficialPosePlacement(results);
            variant = "standard"; outfit = "maid"; petSize = 240; ResetInteractionCheck();
        }

        private void SmokeOfficialPosePlacement(StringBuilder results)
        {
            if (!bank.HasOfficialMotions) return;
            foreach (string clothing in Outfits)
            foreach (int size in new int[] { 160, 240, 360 })
            foreach (Rectangle area in new Rectangle[] { new Rectangle(0, 0, 1920, 1080), new Rectangle(-1920, -1080, 1920, 1080) })
            foreach (int side in new int[] { -1, 1 })
            {
                variant = "official"; outfit = clothing; petSize = size;
                ResetInteractionCheck(); breathing = false;
                Location = new Point(side < 0 ? PlacementLeftLimit(area) : PlacementRightLimit(area), PlacementFloor(area));
                int fixedFloor = Top;
                foreach (string action in new string[] { "sit", "idle", "wave", "idle", "pickup", "idle" })
                {
                    RectangleF previous = PlacementFootprint();
                    seated = action == "sit";
                    dragging = action == "pickup";
                    feedbackKind = action == "wave" ? "hair" : "";
                    feedbackUntil = action == "wave" ? simulationTime + 10 : 0;
                    ReconcileOfficialPosePlacement(previous, area, false);
                    RectangleF visible = VisiblePlacementBounds(Bounds);
                    CheckInteraction(side < 0 ? Math.Abs(visible.Left - area.Left) < 1.1 : Math.Abs(visible.Right - area.Right) < 1.1,
                        "official pose change preserves visible side anchor: " + action);
                    CheckInteraction(Top == fixedFloor && windowY == fixedFloor && PlacementFloor(area) == fixedFloor,
                        "official pose change preserves exact foot anchor: " + action);
                }
                // Clothing changes can also change the standing silhouette width.
                RectangleF oldOutfit = PlacementFootprint();
                outfit = Outfits[(Array.IndexOf(Outfits, clothing) + 1) % Outfits.Length];
                ReconcileOfficialPosePlacement(oldOutfit, area, false);
                RectangleF changed = VisiblePlacementBounds(Bounds);
                CheckInteraction(side < 0 ? Math.Abs(changed.Left - area.Left) < 1.1 : Math.Abs(changed.Right - area.Right) < 1.1,
                    "official outfit change preserves visible side anchor");
                outfit = clothing;
                falling = true; dragging = seated = false; feedbackUntil = 0;
                landingArea = area; windowY = fixedFloor - 120; Top = (int)windowY; verticalSpeed = 0;
                int frames = 0;
                while (falling && frames++ < 240)
                {
                    double before = windowY;
                    StepFall(1.0 / 60);
                    CheckInteraction(windowY >= before && windowY <= fixedFloor,
                        "official pickup-to-standing fall never jumps upward or through floor");
                }
                CheckInteraction(!falling && Top == fixedFloor, "official action fall finishes at fixed foot anchor");
                for (int frame = 0; frame < 60; frame++)
                    CheckInteraction(!StepFall(1.0 / 60) && Top == fixedFloor, "official grounded position stays fixed after pickup ends");
            }
            results.AppendLine("PASS: official pose placement: all outfits/sizes, both edges and signed monitors retain visible side/foot anchors through sit, stand, wave, pickup and outfit changes; fall is monotonic and remains fixed after landing");
        }
    }
}
