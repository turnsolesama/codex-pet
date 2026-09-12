# 标准 Q 版换装运行素材

2026-09-11。新增 4 套服装的闭眼、嫌弃、开心、害羞帧，共 16 张内置 `image_gen` 编辑结果；加上 4 张原版 idle 复制件，共 20 张。未改动历史母版，未使用或生成等身图。

| 服装 | 待机 | 闭眼 | 嫌弃 | 开心 | 害羞 |
| --- | --- | --- | --- | --- | --- |
| casual | [idle](standard/casual/idle.png) | [closed](standard/casual/closed.png) | [annoyed](standard/casual/annoyed.png) | [happy](standard/casual/happy.png) | [shy](standard/casual/shy.png) |
| pink-waitress | [idle](standard/pink-waitress/idle.png) | [closed](standard/pink-waitress/closed.png) | [annoyed](standard/pink-waitress/annoyed.png) | [happy](standard/pink-waitress/happy.png) | [shy](standard/pink-waitress/shy.png) |
| winter-coat | [idle](standard/winter-coat/idle.png) | [closed](standard/winter-coat/closed.png) | [annoyed](standard/winter-coat/annoyed.png) | [happy](standard/winter-coat/happy.png) | [shy](standard/winter-coat/shy.png) |
| red-cheongsam | [idle](standard/red-cheongsam/idle.png) | [closed](standard/red-cheongsam/closed.png) | [annoyed](standard/red-cheongsam/annoyed.png) | [happy](standard/red-cheongsam/happy.png) | [shy](standard/red-cheongsam/shy.png) |

各帧均为 1254 × 1254 RGB 白底图。以各自对应服装母版为编辑目标，仅要求变更面部；使用相同完整画布加载，不按每帧独立重新裁切。白底需由桌面渲染流程提取，源 PNG 没有透明通道。

16 张已逐张查看：自然闭眼与开心弯眼有明确区分，嫌弃为半眯眼与小嘴，害羞为睁眼红晕。发饰、服饰、姿势与脚部基线未见明显漂移。像素检查得到深色轮廓最大边界误差 1 像素，面部以外平均 RGB 差为 0.951–1.325/255；存在细微生成重绘，不能称为像素完全不变。四张 idle 与母版逐字节相同。

[完整提示词](prompts-standard.md) · [尺寸、SHA-256、对齐检查](verification-standard.json)

