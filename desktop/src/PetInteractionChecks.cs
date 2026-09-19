using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        private static void CheckInteraction(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Interaction regression: " + message);
        }

        private void ResetInteractionCheck()
        {
            pressed = dragging = false;
            Capture = false;
            dockSide = 0;
            dockTransitionPending = false;
            sleeping = seated = paused = falling = suppressPoses = false;
            pinnedOfficialPose = null;
            breathing = true;
            simulationTime = 100;
            nextBlink = 104;
            blinkUntil = feedbackUntil = annoyedUntil = landPoseUntil = 0;
            jumpTime = landingTime = -10;
            verticalSpeed = tilt = dragSpeed = dragMagnitude = 0;
            feedbackKind = bubbleText = "";
            feedbackExpression = "idle";
            clicks.Clear();
            autoDockArmed = true;
            ResizeCanvas();
            landingArea = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(landingArea.Left + Math.Max(0, (landingArea.Width - Width) / 2), landingArea.Bottom - Height);
            windowY = Top;
            DrawFrame();
        }

        private void CheckMenuState(string label)
        {
            RefreshMenu();
            CheckInteraction(menu.Items.Count > 0 && Object.ReferenceEquals(menu.Items[menu.Items.Count - 1], commands["exit"]), label + " exit must be the final root item");
            CheckInteraction(commands["standard"].Checked == (variant == "standard") && commands["big-head"].Checked == (variant == "big-head"), label + " shape checks");
            foreach (string clothing in Outfits)
                CheckInteraction(commands["outfit-" + clothing].Checked == (outfit == clothing), label + " outfit check " + clothing);
            foreach (int size in new int[] { 160, 240, 360, 432 })
                CheckInteraction(commands["size-" + size].Checked == (petSize == SizeForPreset(size)), label + " size check " + size);
            CheckInteraction(commands["pause"].Checked == paused && commands["resume"].Available == paused, label + " pause/resume state");
            CheckInteraction(commands["breathing"].Checked == breathing && commands["topmost"].Checked == TopMost, label + " idle/topmost checks");
            CheckInteraction(commands["sleep"].Checked == sleeping && commands["sleep"].Text == (sleeping ? "唤醒" : "睡觉"), label + " sleep toggle");
            CheckInteraction(commands["sit"].Enabled == CanSit() && commands["sit"].Checked == IsSittingRestPose(), label + " pose availability");
            CheckInteraction(commands["dock-left"].Checked == (dockSide == -1) && commands["dock-right"].Checked == (dockSide == 1), label + " edge checks");
            // Available is the item's visibility intent even while the diagnostic menu itself is hidden.
            CheckInteraction(commands["expand"].Available == (dockSide != 0), label + " expand visibility");
        }

        private void CheckReaction(string part, string label)
        {
            string expression = part == "hair" || part == "feet" ? "happy" : part == "face" ? "shy" : "annoyed";
            CheckInteraction(!paused && !falling && !dragging && !sleeping && !seated && dockSide == 0, label + " stable active state");
            CheckInteraction(CurrentPose() == null && feedbackKind == part && CurrentExpression() == expression, label + " visible reaction selected");
            CheckInteraction(feedbackUntil > simulationTime && bubbleText.Length > 0 && !bubbleBounds.IsEmpty, label + " feedback and bubble started");
            if (part == "feet") CheckInteraction(Math.Abs(jumpTime - simulationTime) < 0.000001, label + " jump starts now");
            byte[] initial = ReadFramePixels();
            simulationTime += 0.16;
            DrawFrame();
            byte[] next = ReadFramePixels();
            bool changed = false;
            for (int i = 0; i < initial.Length && !changed; i++) changed = initial[i] != next[i];
            CheckInteraction(changed && CurrentPose() == null && CurrentExpression() == expression, label + " animation advances without a pose overlay");
        }

        private Point VisiblePartPoint(string part)
        {
            double fraction = part == "hair" ? 0.12 : part == "face" ? (variant == "standard" ? 0.32 : 0.44) : part == "body" ? 0.68 : 0.94;
            PointF[] point = new PointF[] { new PointF(0, (float)((fraction - 1) * petSize)) };
            using (Matrix forward = inverseCharacterTransform.Clone())
            {
                forward.Invert();
                forward.TransformPoints(point);
            }
            Point center = Point.Round(point[0]);
            // Prefer the mapped anatomical row, then nearby rows; avoid transparent gaps between feet.
            for (int offset = 0; offset < canvas.Height; offset++)
                foreach (int direction in new int[] { -1, 1 })
                {
                    int y = center.Y + offset * direction;
                    if (y < 0 || y >= canvas.Height) continue;
                    for (int x = 0; x < canvas.Width; x++)
                    {
                        Point candidate = new Point(x, y);
                        if (canvas.GetPixel(x, y).A > 32 && !bubbleBounds.Contains(candidate) && HitPart(candidate) == part)
                            return candidate;
                    }
                }
            throw new InvalidOperationException("Interaction regression: no visible click pixel for " + variant + "/" + outfit + "/" + part);
        }

        private void SmokeInteractions(StringBuilder results)
        {
            if (menu == null) BuildMenu();
            foreach (string type in new string[] { "standard", "big-head" })
            foreach (string clothing in Outfits)
            {
                ResetInteractionCheck();
                commands[type].PerformClick();
                commands["outfit-" + clothing].PerformClick();
                CheckInteraction(variant == type && outfit == clothing, "menu selects " + type + "/" + clothing);
                foreach (int size in new int[] { 160, 240, 360, 432 })
                {
                    commands["size-" + size].PerformClick();
                    CheckInteraction(petSize == size && variant == type && outfit == clothing && !falling, "size preserves wardrobe");
                    CheckMenuState(type + "/" + clothing + "/" + size);
                }
                commands["size-240"].PerformClick();
                string alternate = type == "standard" ? "big-head" : "standard";
                commands[alternate].PerformClick();
                CheckInteraction(variant == alternate && outfit == clothing, "shape switch preserves outfit");
                commands[type].PerformClick();
                string[] actions = new string[] { "happy", "shy", "annoyed", "jump" };
                string[] parts = new string[] { "hair", "face", "body", "feet" };
                for (int i = 0; i < actions.Length; i++)
                {
                    commands[actions[i]].PerformClick();
                    CheckReaction(parts[i], type + "/" + clothing + "/menu-" + actions[i]);
                }
                commands["sleep"].PerformClick();
                CheckInteraction(sleeping && CurrentExpression() == "closed" && CurrentPose() == (SupportsPoses() ? "sleep" : null), "sleep applies to selected wardrobe");
                CheckMenuState("sleeping " + type + "/" + clothing);
                commands["sleep"].PerformClick();
                CheckInteraction(!sleeping && CurrentPose() == null, "sleep menu wakes selected wardrobe");

                foreach (string part in parts)
                {
                    ResetInteractionCheck();
                    commands["pause"].PerformClick();
                    CheckInteraction(paused, "pause menu enters paused state");
                    CheckMenuState("paused " + type + "/" + clothing);
                    Point hit = VisiblePartPoint(part);
                    OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, hit.X, hit.Y, 0));
                    CheckInteraction(pressed && pressedPart == part, "mouse down records visible " + part);
                    OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, hit.X, hit.Y, 0));
                    CheckInteraction(!pressed && !dragging && !Capture, "mouse up releases capture");
                    CheckReaction(part, type + "/" + clothing + "/paused-touch-" + part);
                }
                CheckMenuState(type + "/" + clothing + " final");
                results.AppendLine("PASS: interaction " + type + "/" + clothing + ": actual menu outfit/shape/3 sizes/4 reactions/sleep and paused mouse down/up on 4 visible regions; dynamic menu checks and exit last");
            }

            commands["standard"].PerformClick();
            commands["outfit-maid"].PerformClick();
            foreach (int side in new int[] { -1, 1 })
            foreach (string action in new string[] { "jump", "shy", "sleep", "sit" })
            {
                ResetInteractionCheck();
                Rectangle area = new Rectangle(-1920, -1080, 1920, 1080);
                DockAtSide(side, area, area.Top + 12);
                paused = true;
                CheckMenuState("upper-corner dock");
                commands[action].PerformClick();
                CheckInteraction(dockSide == 0 && !falling && !paused && windowY == Top && Bottom == area.Bottom, "upper-corner " + action + " receives stable floor");
                if (action == "jump" || action == "shy") CheckReaction(action == "jump" ? "feet" : "face", "upper-corner " + action);
                else
                {
                    CheckInteraction(CurrentPose() == action, "upper-corner " + action + " is not swallowed by pickup/land");
                    simulationTime += 0.5;
                    DrawFrame();
                    CheckInteraction(CurrentPose() == action, "upper-corner " + action + " remains selected");
                }
                CheckMenuState("upper-corner " + action);
            }
            results.AppendLine("PASS: paused standard maid at both upper dock corners: jump/shy/sleep/sit command events expand and play without falling-pose interception");

            foreach (int side in new int[] { -1, 1 })
            {
                ResetInteractionCheck();
                DockAtSide(side, landingArea, landingArea.Top + 12);
                dockStarted = clock.Elapsed.TotalSeconds - 1;
                DrawFrame();
                paused = true;
                Point hit = Point.Empty;
                bool found = false;
                for (int y = 0; y < canvas.Height && !found; y++)
                    for (int x = 0; x < canvas.Width && !found; x++)
                        if (canvas.GetPixel(x, y).A > 32) { hit = new Point(x, y); found = true; }
                CheckInteraction(found, "docked head has a visible click point");
                OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, hit.X, hit.Y, 0));
                OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, hit.X, hit.Y, 0));
                CheckInteraction(dockSide == 0 && !paused && !pressed && !Capture, "paused dock click expands and resumes");
            }
            results.AppendLine("PASS: paused left/right head click events expand and resume automatic animation");

            ResetInteractionCheck();
            Point dragHit = VisiblePartPoint("hair");
            commands["pause"].PerformClick();
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, dragHit.X, dragHit.Y, 0));
            // Exercise the real threshold handler without changing the user's physical cursor.
            Point cursor = Cursor.Position;
            downCursor = new Point(cursor.X - 10, cursor.Y);
            OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, dragHit.X + 10, dragHit.Y, 0));
            CheckInteraction(dragging && !paused, "dragging out of pause resumes animation");
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, dragHit.X + 10, dragHit.Y, 0));
            CheckInteraction(!dragging && !pressed && !Capture, "drag release clears capture state");
            results.AppendLine("PASS: paused real mouse drag threshold resumes animation; mouse up releases capture without moving the physical cursor");

            foreach (Rectangle area in new Rectangle[] { new Rectangle(0, 0, 1920, 1080), new Rectangle(-1920, -1080, 1920, 1080) })
            {
                ResetInteractionCheck();
                Top = area.Bottom - Height - 300;
                BeginFall(area);
                commands["pause"].PerformClick();
                double frozen = simulationTime;
                for (int frame = 0; frame < 180 && falling; frame++) StepFall(1.0 / 30);
                CheckInteraction(paused && simulationTime == frozen && !falling && windowY == area.Bottom - Height && verticalSpeed == 0, "paused drop settles on floor");
                for (int frame = 0; frame < 60; frame++)
                {
                    StepFall(1.0 / 30);
                    DrawFrame();
                    CheckInteraction(CurrentPose() != "land" && Top == area.Bottom - Height && windowY == Top, "paused landing cannot retain a timed land pose for two seconds");
                }
            }
            results.AppendLine("PASS: pausing during a drop settles on primary/negative monitor without an immortal land pose or coordinate drift for two simulated seconds");
            ResetInteractionCheck();
            CheckMenuState("finished interactions");
        }
    }
}
