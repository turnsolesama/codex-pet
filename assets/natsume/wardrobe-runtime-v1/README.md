# Q 版换装运行素材

对应 Windows 桌宠 0.3.0。使用内置 image_gen 编辑现有 Q 版资产，保留女仆装之外的四套既有服装设计，并为大头版重新适配衣服、发型和发饰。没有制作等身版。

| 服装 | 标准 Q 版 | 大头 Q 版 |
| --- | --- | --- |
| 日常私服 | [待机](standard/casual/idle.png) | [待机](big-head/casual/idle.png) |
| 粉色服务员装 | [待机](standard/pink-waitress/idle.png) | [待机](big-head/pink-waitress/idle.png) |
| 冬日外套 | [待机](standard/winter-coat/idle.png) | [待机](big-head/winter-coat/idle.png) |
| 红金旗袍 | [待机](standard/red-cheongsam/idle.png) | [待机](big-head/red-cheongsam/idle.png) |

每个文件夹包含 `idle.png`、`closed.png`、`annoyed.png`、`happy.png`、`shy.png`。标准版待机沿用并复制 [restoration-v1](../restoration-v1/REVIEW.md) 的四张服装稿，补四种表情；大头版先生成四张换装母版，再逐套补表情。原图与旧版本保留。

本目录共 40 张运行素材，其中 36 张为本轮生成、4 张为标准版原稿副本。加上已有两种女仆装的 10 张帧，程序内嵌 50 张服装与表情图。程序加载时生成透明遮罩，不覆盖白底原图；当前动作仍采用表情帧与整体变换。

两种体型的完整生成提示词与逐图验证：

- [标准 Q 版说明](README-standard.md) · [提示词](prompts-standard.md) · [验证](verification-standard.json)
- [大头 Q 版说明](README-big-head.md) · [提示词](prompts-big-head.md) · [验证](verification-big-head.json)

[查看程序实际渲染的换装衣橱](../../../desktop/qa-v0.3.0/index.html) · [桌宠使用说明](../../../desktop/README.md)

所有素材与可执行程序仅保存在本地，未发布。
