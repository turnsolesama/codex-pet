using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CodexPet
{
    internal sealed partial class DesktopPet
    {
        private readonly Dictionary<string, ToolStripMenuItem> commands = new Dictionary<string, ToolStripMenuItem>();

        private ToolStripMenuItem Command(ToolStripItemCollection items, string id, string label, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Name = id;
            if (action != null) item.Click += delegate { action(); };
            commands.Add(id, item);
            items.Add(item);
            return item;
        }

        private void BuildMenu()
        {
            menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.Font = new Font("Microsoft YaHei UI", 9);
            Command(menu.Items, "resume", "继续互动", delegate { ResumeAnimation(); DrawFrame(); });
            ToolStripMenuItem actions = Command(menu.Items, "actions", "表情与动作", null);
            Command(actions.DropDownItems, "happy", "摸摸头 · 开心", delegate { ReactToPart("hair"); });
            Command(actions.DropDownItems, "shy", "碰脸 · 害羞", delegate { ReactToPart("face"); });
            Command(actions.DropDownItems, "annoyed", "戳衣服 · 嫌弃", delegate { ReactToPart("body"); });
            Command(actions.DropDownItems, "jump", "开心轻跳", delegate { ReactToPart("feet"); });
            actions.DropDownItems.Add(new ToolStripSeparator());
            Command(actions.DropDownItems, "sit", "坐下", ToggleSit);
            Command(actions.DropDownItems, "sleep", "睡觉", ToggleSleep);
            if (bank.HasOfficialMotions)
            {
                ToolStripMenuItem resting = Command(actions.DropDownItems, "resting", "常驻姿势", null);
                Command(resting.DropDownItems, "rest-stand", "站立", delegate { PinOfficialPose("stand"); });
                Command(resting.DropDownItems, "rest-sit", "坐下", delegate { PinOfficialPose("sit"); });
                Command(resting.DropDownItems, "rest-wave", "招手", delegate { PinOfficialPose("wave"); });
                Command(resting.DropDownItems, "rest-pickup", "拎起", delegate { PinOfficialPose("pickup"); });
            }

            ToolStripMenuItem wardrobe = Command(menu.Items, "wardrobe", "换装", null);
            for (int i = 0; i < Outfits.Length; i++)
            {
                string value = Outfits[i];
                Command(wardrobe.DropDownItems, "outfit-" + value, OutfitNames[i], delegate { ChangeOutfit(value); });
            }
            if (bank.HasCheongsamStyles)
            {
                ToolStripMenuItem hair = Command(wardrobe.DropDownItems, "cheongsam-hair", "旗袍动作造型", null);
                Command(hair.DropDownItems, "cheongsam-updo", "盘发（贴近原设）", delegate { ChangeCheongsamHair(true); });
                Command(hair.DropDownItems, "cheongsam-long-hair", "长发（保留款）", delegate { ChangeCheongsamHair(false); });
            }
            if (bank.HasMaidFaceStyles)
            {
                ToolStripMenuItem face = Command(wardrobe.DropDownItems, "maid-face", "女仆动作脸型", null);
                Command(face.DropDownItems, "maid-classic", "常态脸（默认）", delegate { ChangeMaidFace(false); });
                Command(face.DropDownItems, "maid-cute", "萌萌脸", delegate { ChangeMaidFace(true); });
            }
            ToolStripMenuItem shape = Command(menu.Items, "shape", "体型与大小", null);
            Command(shape.DropDownItems, "standard", "标准 Q 版", delegate { ChangeVariant("standard"); });
            Command(shape.DropDownItems, "big-head", "大头 Q 版", delegate { ChangeVariant("big-head"); });
            if (bank.HasOfficial) Command(shape.DropDownItems, "official", "等身桌宠", delegate { ChangeVariant("official"); });
            shape.DropDownItems.Add(new ToolStripSeparator());
            foreach (int value in new int[] { 160, 240, 360, 432 })
            {
                int size = value;
                Command(shape.DropDownItems, "size-" + size, size == 160 ? "小号" : size == 240 ? "中号" : size == 360 ? "大号" : "超大号", delegate { ChangeSize(SizeForPreset(size)); });
            }
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem edge = Command(menu.Items, "edge", "收纳到边缘", null);
            Command(edge.DropDownItems, "dock-left", "左侧抓框", delegate { DockAtSide(-1); });
            Command(edge.DropDownItems, "dock-right", "右侧抓框", delegate { DockAtSide(1); });
            Command(menu.Items, "expand", "展开全身", delegate { ResumeAnimation(); ExpandDock(false, Cursor.Position); });
            Command(menu.Items, "home", "回到右下角", delegate { ResumeAnimation(); ReturnToCorner(); });
            ToolStripMenuItem extras = Command(menu.Items, "extras", "额外功能", null);
            Command(extras.DropDownItems, "gravity", "重力下落（默认关闭）", ToggleGravity);
            menu.Items.Add(new ToolStripSeparator());
            Command(menu.Items, "breathing", "待机呼吸与眨眼", delegate { breathing = !breathing; if (breathing) ResumeAnimation(); DrawFrame(); });
            Command(menu.Items, "pause", "暂停自动动画", delegate { paused = !paused; lastTick = clock.Elapsed.TotalSeconds; DrawFrame(); });
            Command(menu.Items, "topmost", "保持置顶", delegate { TopMost = !TopMost; });
            menu.Items.Add(new ToolStripSeparator());
            Command(menu.Items, "exit", "退出桌宠", Close);
            menu.Opening += delegate { RefreshMenu(); };
            RefreshMenu();
        }

        private void RefreshMenu()
        {
            // Named references keep state correct when groups or context-sensitive rows move.
            commands["resume"].Visible = paused;
            commands["pause"].Checked = paused;
            commands["pause"].Text = paused ? "继续自动动画" : "暂停自动动画";
            commands["breathing"].Checked = breathing;
            commands["breathing"].Text = dockSide == 0 ? "待机呼吸与眨眼" : "待机呼吸与眨眼（展开后）";
            commands["topmost"].Checked = TopMost;
            commands["gravity"].Checked = gravityEnabled;
            if (commands.ContainsKey("resting"))
            {
                commands["resting"].Visible = variant == "official";
                commands["rest-stand"].Checked = !seated && !sleeping && pinnedOfficialPose == null;
                commands["rest-sit"].Checked = seated || pinnedOfficialPose == "sit";
                commands["rest-wave"].Checked = pinnedOfficialPose == "wave";
                commands["rest-pickup"].Checked = pinnedOfficialPose == "pickup";
            }
            commands["standard"].Checked = variant == "standard";
            commands["big-head"].Checked = variant == "big-head";
            if (commands.ContainsKey("official")) commands["official"].Checked = variant == "official";
            foreach (int size in new int[] { 160, 240, 360, 432 }) commands["size-" + size].Checked = petSize == SizeForPreset(size);
            foreach (string clothing in Outfits) commands["outfit-" + clothing].Checked = outfit == clothing;
            if (commands.ContainsKey("cheongsam-hair"))
            {
                commands["cheongsam-hair"].Visible = variant == "official" && outfit == "red-cheongsam";
                commands["cheongsam-updo"].Checked = bank.CheongsamUpdo;
                commands["cheongsam-long-hair"].Checked = !bank.CheongsamUpdo;
            }
            commands["sit"].Enabled = CanSit();
            if (commands.ContainsKey("maid-face"))
            {
                commands["maid-face"].Visible = variant == "official" && outfit == "maid";
                commands["maid-classic"].Checked = !bank.MaidCuteFace;
                commands["maid-cute"].Checked = bank.MaidCuteFace;
            }
            commands["sit"].Checked = IsSittingRestPose();
            commands["sit"].Text = !CanSit() ? "坐下（标准 Q 女仆装）" : IsSittingRestPose() ? (variant == "official" ? "站起来（官方原立绘）" : "站起来") : "坐下休息";
            commands["sleep"].Text = sleeping ? "唤醒" : "睡觉";
            commands["sleep"].Checked = sleeping;
            commands["dock-left"].Checked = dockSide == -1;
            commands["dock-right"].Checked = dockSide == 1;
            commands["dock-left"].Text = "左侧抓框";
            commands["dock-right"].Text = "右侧抓框";
            commands["expand"].Visible = dockSide != 0;
        }

        private void ResumeAnimation()
        {
            paused = false;
            lastTick = clock.Elapsed.TotalSeconds;
        }

        private void PrepareInteraction()
        {
            ResumeAnimation();
            if (dockSide != 0) ExpandDock(false, Cursor.Position);
            // A deliberate action gets a stable stage; falling poses must not consume its duration.
            if (falling)
            {
                Top = landingArea.Bottom - Height;
                windowY = Top;
                falling = false;
            }
            pressed = dragging = false;
            Capture = false;
            verticalSpeed = tilt = dragSpeed = dragMagnitude = 0;
            landPoseUntil = 0;
            landingTime = -10;
        }

        private void ToggleSleep()
        {
            bool wake = sleeping;
            PrepareInteraction();
            RectangleF previousFootprint = PlacementFootprint();
            sleeping = !wake;
            seated = false;
            pinnedOfficialPose = null;
            feedbackUntil = annoyedUntil = 0;
            jumpTime = -10;
            ReconcileOfficialPosePlacement(previousFootprint, false);
            DrawFrame();
        }
    }
}
