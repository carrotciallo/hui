# 美术素材收集（Issue #26）

> 用途：给《回》的像素美术替换工作提供可直接查看、下载和评估的候选素材。这里是**素材候选清单**，不是已获批准的最终视觉方案；优先保持单一素材包的调色板和线条风格，不建议把多套风格直接拼进正式关卡。
>
> 收集日期：2026-11-04。下载前请再看作者页面和压缩包内的 LICENSE / README，并保存授权记录。

## 项目约束速查

按 [Issue #26](https://github.com/sfher/hui/issues/26)：1 格 = 16 px；像素边缘清晰，不抗锯齿、不渐变；Unity 导入用 Sprite、PPU 16、Point、无压缩、关闭 Mipmap；有限调色板；A、B、各条边及边态需要独立控制。素材可替换显示层，不能改变碰撞体和玩法规则。

当前关卡编辑器中的地刺为 16×6 px，钥匙为 1×1 格、门为 1×3 格；竖绳碰撞宽 6 px。下载的外观不自动代表这些碰撞规格也合适，导入后应按现有游戏尺寸重切或重绘。

## 优先试用：一套覆盖面较广的 16×16 素材

### Pixel Platformer Tileset — reodesuxz

[素材主页与下载](https://reodesuxz.itch.io/pixel-platformer-tileset)

- 页面列出 106 个 16×16 素材：草、石、沙、雪地形，冰块、单向平台、尖刺、水/岩浆、钥匙、门、箱子、角色和背景。
- 提供独立 PNG、图集和 sprite 索引；适合先验证关卡能否快速摆出完整画面。
- 作者页标注可用于个人和商业项目、无需署名；不允许转售或重新分发素材文件。
- **适合**：地形占位升级、钥匙/门/冰块/尖刺初版、背景参考。
- **限制**：包内角色不是 A/B 的方框造型；钥匙持顶、红热四态边框、传送点选择态、形态变体等没有按本项目规则制作。该包配色与笔触需要先跟策划确认，再决定是否作为整套基底。

### Pixel Platformer Tileset Vol. 4 — Ice & Snow, reodesuxz

[素材主页与下载](https://reodesuxz.itch.io/free-pixel-platformer-tileset-vol-4-ice-snow-16x16)

- 129 个 16×16 冰雪主题素材；含冰砖地形、四向尖刺、绳/藤、门、出口标志、检查点等，并提供透明 PNG、图集和 JSON。
- 作者页面说明与同系列 Vol. 1–3 使用相同网格与风格；页面标注商业用途可用、无需署名，勿单独转售或重新分发资源文件。
- **适合**：L3 冰块关、绳索和尖刺的外观候选；也可与同系列平台素材保持风格一致。
- **限制**：素材语义和项目碰撞规则不等价。比如绳子的穿越/反弹表现、冰块被撞破反馈、重生点状态仍需按游戏实际规则重新设计。

### 推荐的试用方式

先只下载同一作者的主包和冰雪 Vol. 4，挑选 10–15 张代表性贴图做一张 **16×16 棋盘预览**：地面、冰、钥匙、门、尖刺、绳、角色比例、背景。检查有限色板和 A/B 占位色能否协调，再决定是否拿它做环境底包。不要把第二套作者素材直接混入此试样。

## CC0 备选

| 素材 | 内容与适用点 | 授权 / 取舍 |
| --- | --- | --- |
| [Pulse Pixel Platformer Pack](https://edfaygo.itch.io/pulse-pixel-platformer-pack) | 43 张手绘 16×16 PNG：地形、角色动画、宝石/心、指示牌和终点旗；有独立透明 PNG 与分类图集。可快速比较一种简洁、明亮的画风。 | 页面标注 CC0、无需署名；适合原型或替代风格测试。覆盖的机关种类少。 |
| [Platformer Tileset 16x16 — Duasun](https://opengameart.org/content/platformer-tileset-16x16) | 16×16 平台地形，作为简洁地块参考。 | 页面标注 CC0；内容较少，不能单独覆盖项目素材清单。 |
| [Pixel Art Platformer Asset Pack — MattBas](https://opengameart.org/content/pixel-art-platformer-asset-pack) | 多种平台地形、UI、背景、小龙和道具；有地形变化可参考。 | 页面标注 CC0。整体主题偏自然/奇幻，需先核对与《回》的基调是否相容。 |
| [Tiny Tropical Tiles — GiMiCkO](https://gimicko.itch.io/tiny-tropical-tiles) | 44 张 16×16 海滩、植被和动画水面；含 PNG、Aseprite 源文件和单独 tile。 | 页面标注 CC0；仅适合作为另一套风格完整的环境备选，主题与本项目机关不完全匹配。 |
| [1-Bit Platformer Pack — Kenney](https://kenney.nl/assets/1-bit-platformer-pack) | 16×16、约 400 个资源，含地形与角色，可参考强对比、极简图形语言。 | 页面标注 CC0；单色风格与 Issue #26 的多色角色/等级信息层不匹配，建议只作构图参考。 |

## 按项目素材需求映射

| 项目需要 | 可先查看 | 仍需定制 / 核对 |
| --- | --- | --- |
| 地形、墙、背景 | reodesuxz 主包；同系列 Vol. 4 冰雪地形 | 拼接边缘、背景尺寸、与 16 色左右调色板的关系；不带入不需要的主题装饰。 |
| 冰块、冰面、融化反馈 | Vol. 4 冰雪地形；主包冰块 | 项目需要区分普通冰、可破坏冰和红热融化状态；命中反馈与碎裂动画尚缺。 |
| 地刺 | 主包或 Vol. 4 的尖刺 | 保持项目实际 16×6 px 碰撞轮廓；检查四向旋转后贴边位置。 |
| 绳 | Vol. 4 的绳/藤作为外观参考 | 实际竖绳宽 6 px、连续段/端头；v0/v1 阻挡、v2 穿越、v3/v4 反弹必须有清楚状态反馈。 |
| 钥匙、门 | 主包；Vol. 4 | 需要携带在玩家头顶的钥匙状态、门开/关两态，以及与现有任意钥匙开任意门规则一致的外观提示。 |
| A / B 玩家 | Pulse 包或主包只作比例/动画参考 | 必须重画成 A 框形角色、B 小方块；A 本体与四条边分层，边可独立显示 normal / hot1 / hot2 / spirit。B 需 10×10 px 碰撞体适配 16×16 画布。 |
| 形态、大炮、转向器 | 现有角色素材仅参考像素表现 | u 炮口四向、L 转向四向、= / ‖、矛、灵体共 13 种视觉组合；需要按 Issue #26 与规则文档逐个验收。 |
| 传送点、重生点、HUD | MattBas UI 作界面风格参考；主包终点标识可参考 | 传送点未点亮/点亮/双人触发状态、出生/重生标记、玩家眼睛和 v0–v4 描边仍需专门制作。 |

## 下载和入库建议

1. 先由策划从主候选和 CC0 备选中选定**一套主要画风/调色板**，不要一次把整套外部素材全部复制进工程。
2. 下载时连同页面地址、作者、许可类型、下载日期和原始 README 一起记入素材清单；原文件保存在项目素材目录内，并保留来源记录。
3. 导入 Unity 时按 Issue #26 设置：Pixels Per Unit 16、Point、Compression None、Mipmap 关闭；不接受非整数缩放和半像素位置。
4. 先做一张小型关卡试拼并核对碰撞箱、分层替换、边态变色；素材无法表达的机制反馈由美术补画，不通过改规则迁就素材。
5. 素材正式采用后，把作者/许可/来源写进游戏 credits 或项目的第三方素材清单；即使许可不强制署名也建议保留记录。

## 来源页

- [GitHub Issue #26：素材收集与规格](https://github.com/sfher/hui/issues/26)
- [reodesuxz Pixel Platformer Tileset](https://reodesuxz.itch.io/pixel-platformer-tileset)
- [reodesuxz Ice & Snow Vol. 4](https://reodesuxz.itch.io/free-pixel-platformer-tileset-vol-4-ice-snow-16x16)
- [Pulse Pixel Platformer Pack](https://edfaygo.itch.io/pulse-pixel-platformer-pack)
- [OpenGameArt 16×16 Platformer Tileset](https://opengameart.org/content/platformer-tileset-16x16)
- [OpenGameArt Pixel Art Platformer Asset Pack](https://opengameart.org/content/pixel-art-platformer-asset-pack)
- [Kenney 1-Bit Platformer Pack](https://kenney.nl/assets/1-bit-platformer-pack)
