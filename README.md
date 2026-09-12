<p align="center">
  <img src="docs/screenshots/standard-maid.png" width="200" alt="标准 Q 版女仆装">
  <img src="docs/screenshots/big-head-cheongsam.png" width="200" alt="大头 Q 版红金旗袍">
  <img src="docs/screenshots/edge-grip.png" width="200" alt="边缘收纳探头">
</p>

# 枣子姐桌宠 NatsumePet

《星光咖啡馆与死神之蝶》四季夏目（枣子姐）的 Q 版 Windows 桌宠。双击启动、无需安装，角色图片内置于程序中；不联网、不添加开机启动。

**[下载 v0.4.1 · NatsumePet.exe](https://github.com/turnsolesama/codex-pet/releases/tag/v0.4.1)**（约 82 MB，需 Windows 自带的 .NET Framework 4.x）

## 特性

- **两种 Q 版体型**：标准 Q 版与大头 Q 版，200 / 280 / 360 三档尺寸；重复启动不会出现第二只
- **五套服装自由换**：女仆装、日常私服、粉色服务员装、冬日外套、红金旗袍；每种「体型 × 服装」组合都有待机、闭眼、开心、害羞、嫌弃五张独立表情帧
- **按部位点击互动**：点头发开心、点脸害羞、点衣服嫌弃、点脚轻跳；短时间连点三次会被嫌弃
- **物理拖拽**：按住拎起、松手落地回弹；支持睡觉、置顶、暂停，透明区域不遮挡后面的窗口
- **边缘收纳**：拖到屏幕左右边缘或角落松手，自动藏到边缘，双手抓住屏幕边框探头；点击展开，按住拖出恢复

## 互动一览

| 操作 | 回应 |
| --- | --- |
| 点头发 | 开心表情，轻轻摇晃 |
| 点脸 | 害羞表情，小幅躲闪 |
| 点衣服 | 嫌弃表情，短暂抖动 |
| 点脚 | 开心轻跳 |
| 短时间连续点三次 | 嫌弃，提醒别一直戳 |
| 按住拖动 | 提起、倾斜；松手下落并回弹 |
| 睡觉时点击 | 唤醒 |
| 拖到左右边缘 / 四角松手 | 身体藏到屏幕边缘，双手抓框探头 |
| 点击或拖动探出的头 | 展开小人 |

右键小人或系统托盘图标打开菜单：换装、切换体型与尺寸、睡觉、置顶、暂停、手动收纳到左侧 / 右侧、退出。双击托盘图标把小人带回鼠标所在屏幕的右下角。

标准 Q 版女仆装额外接回了原动作稿：坐下 / 站起、睡觉打盹、被拎起晃动、落地蹲下回弹。

## 从源码构建

源码为纯 C# 5 + WinForms + GDI+，无外部程序包。`desktop/build.ps1` 调用 Windows 自带的 .NET Framework C# 编译器，把 `assets/` 中的立绘内嵌为资源，输出到 `releases/desktop-v0.4.1/`：

```powershell
cd desktop
./build.ps1
```

诊断模式：`NatsumePet.exe --smoke-test --diagnostics <绝对目录>` 输出测试结果与透明渲染 PNG；`--outfit casual` 可指定初始服装（`maid` / `casual` / `pink-waitress` / `winter-coat` / `red-cheongsam`）。

## 目录结构

```
desktop/src/                    C# 源码（窗口、分层透明、精灵、交互）
desktop/build.ps1               构建脚本：编译并打包 exe、使用说明、package.json
assets/natsume/                 构建引用的立绘素材（表情帧、服装、抓框、动作稿）
docs/screenshots/               README 截图
```

素材由 AI 生成并经多轮修复与审核；生成提示词与校验记录见 `assets/natsume/edge-grip-v2/` 与 `assets/natsume/wardrobe-runtime-v1/` 下的文档。

## 说明

本作品为个人兴趣的同人创作。角色「四季夏目」出自 Yuzusoft 游戏《星光咖啡馆与死神之蝶》，版权归原公司所有。程序不联网、不写注册表、不添加开机启动。

更多个人项目见 [portfolio](https://github.com/turnsolesama/portfolio)。
