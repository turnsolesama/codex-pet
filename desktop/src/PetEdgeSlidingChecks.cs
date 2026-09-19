using System;
using System.Drawing;
using System.Text;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        private void BeginEdgeCheckDrag(Point cursor)
        {
            pressed = true;
            dragging = false;
            downCursor = lastCursor = cursor;
            downWindow = Location;
            lastDragTime = clock.Elapsed.TotalSeconds;
        }

        private void SmokeEdgeSliding(StringBuilder results)
        {
            foreach (string type in bank.HasOfficial ? new string[] { "standard", "big-head", "official" } : new string[] { "standard", "big-head" })
            foreach (int size in new int[] { 160, 240, 360, 432 })
            foreach (Rectangle area in new Rectangle[] { new Rectangle(0, 0, 1920, 1080), new Rectangle(-1920, -1080, 1920, 1080) })
            foreach (int side in new int[] { -1, 1 })
            {
                ResetInteractionCheck();
                variant = type; outfit = "maid"; petSize = SizeForPreset(size);
                DockAtSide(side, area, area.Top + area.Height / 2);
                dockStarted = clock.Elapsed.TotalSeconds - 1;
                DrawFrame();
                paused = true;
                Point start = new Point(Left + Width / 2, Top + Height / 2);
                int initialTop = Top, initialLeft = Left;
                BeginEdgeCheckDrag(start);
                MovePointer(new Point(start.X, start.Y + 4), area);
                CheckInteraction(!dragging && paused && Top == initialTop, "edge click tolerance preserves pause and placement");
                MovePointer(new Point(start.X, start.Y + 80), area);
                CheckInteraction(dragging && !paused && dockSide == side && !falling && Top == initialTop + 80 && Left == initialLeft,
                    "vertical dock drag slides and resumes without expansion");
                MovePointer(new Point(start.X + side * 300, start.Y - 70), area);
                CheckInteraction(dockSide == side && Top == initialTop - 70 && Left == initialLeft, "outward movement never detaches");
                // Even a pointer on another monitor keeps the grip on its original edge.
                Rectangle otherArea = new Rectangle(area.Right, area.Top, area.Width, area.Height);
                MovePointer(new Point(start.X, area.Top - 2000), otherArea);
                CheckInteraction(dockSide == side && dockArea == area && Top == area.Top + 12 && Left == initialLeft, "upper edge clamp and monitor ownership");
                MovePointer(new Point(start.X, area.Bottom + 2000), otherArea);
                CheckInteraction(Bottom == area.Bottom - 12 && dockArea == area && Left == initialLeft, "lower edge clamp");
                MovePointer(new Point(start.X, start.Y + 45), area);
                Rectangle releasedBounds = Bounds;
                double entryStarted = dockStarted;
                ReleasePointer(new Point(start.X, start.Y + 45), area);
                CheckInteraction(dockSide == side && !dragging && !pressed && !Capture && !falling && Bounds == releasedBounds &&
                    !dockTransitionPending && dockStarted == entryStarted, "slide release preserves position without entry replay or falling");
                for (int frame = 0; frame < 3; frame++) Tick(null, EventArgs.Empty);
                CheckInteraction(Bounds == releasedBounds && !falling, "released grip remains fixed across timer updates");

                start = new Point(Left + Width / 2, Top + Height / 2);
                BeginEdgeCheckDrag(start);
                MovePointer(new Point(start.X, start.Y - 25), area);
                Rectangle captureLostBounds = Bounds;
                // Invoke the same cancellation handler, without acquiring or moving the user's cursor.
                OnMouseCaptureChanged(EventArgs.Empty);
                CheckInteraction(!pressed && !dragging && !falling && dockSide == side && Bounds == captureLostBounds, "capture loss retains edge position");

                start = new Point(Left + Width / 2, Top + Height / 2);
                BeginEdgeCheckDrag(start);
                int threshold = Clamp((int)Math.Round(petSize * 0.14), 32, 52);
                MovePointer(new Point(start.X - side * (threshold - 1), start.Y), area);
                CheckInteraction(dockSide == side, "below inward threshold remains docked");
                Point detach = new Point(start.X - side * threshold, start.Y);
                MovePointer(detach, area);
                CheckInteraction(dockSide == 0 && dragging && pressed && !falling && !paused && downCursor == detach && downWindow == Location,
                    "inward displacement unfolds into continuous full-body drag");
                Rectangle expanded = Bounds;
                Point interior = new Point(detach.X - side * 160, detach.Y + 35);
                MovePointer(interior, area);
                Point expected = ClampPlacement(new Point(expanded.Left-side*160,expanded.Top+35),area);
                CheckInteraction(dockSide == 0 && dragging && Location == expected, "full-body drag continues after detachment");
                ReleasePointer(interior, area);
                CheckInteraction(dockSide == 0 && !dragging && !pressed && !Capture && falling, "detached release restores normal falling");

                DockAtSide(side, area, area.Top + area.Height / 2);
                paused = true;
                start = new Point(Left + Width / 2, Top + Height / 2);
                BeginEdgeCheckDrag(start);
                ReleasePointer(start, area);
                CheckInteraction(dockSide == 0 && !paused && !pressed && !dragging, "single dock click still expands and resumes");
                results.AppendLine("PASS: edge sliding " + type + "/" + size + (area.Left < 0 ? " negative" : " primary") +
                    (side < 0 ? " left" : " right") + ": pause, vertical/outward travel, both bounds, monitor retention, release, capture cancellation, inward threshold, continued drag and single-click expansion");
            }
            petSize = 240;
            ResetInteractionCheck();
        }
    }
}
