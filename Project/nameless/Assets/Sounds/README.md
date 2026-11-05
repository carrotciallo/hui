# 音效库（阶段二实装中）

来源：Freesound，全部 **CC0**（publicdomain/zero/1.0），免署名、可商用。
投票结果见 issue #60 正文，每人不限勾选次数；102 个候选中入选 19 个，弃选的 83 个文件连同 `.meta` 已删除。
投票后按体感返工过一次：跳跃槽入选的 2 条（386647 / 386636）大家都不满意，已连同 `.meta` 删除，换成下表这一条——所以当前库内共 18 个文件。

文件名格式：`用途slug_FreesoundID_原始名.mp3`。导入器保持 Unity 默认（`loadType: 0` 压缩入内存、`preloadAudioData: 0`）。

**素材目录已迁到 `Assets/Sounds/Resources/`**：`LabAudio` 用 `Resources.LoadAll<AudioClip>("<NN_用途>")` 按目录整批加载，因此不需要往场景里挂任何东西，也不需要 Inspector 引用——和 `LabHud` 用 IMGUI 是同一个零场景依赖的路子。GUID 在迁移中原样保留。

## 播放接线

运行时由 `Assets/TuningLab/LabAudio.cs` 挂载（`TuningLab.BuildAll` 里 `AddComponent`，不进场景、不进 prefab）。各目录整批加载后按顺序轮换播放，同 Cue 有 0.05s 冷却（用 `Time.unscaledTime`，HUD 能把 timeScale 压到 0.05），音量/音高抖动见 `LabAudio` 里的 `Volumes` / `PitchJitter` 表。

| Cue | 触发点 | 位置 |
| --- | --- | --- |
| `Jump` | 起跳 `BeginAir()` 之后 | [LabPlayer.cs:432](../TuningLab/LabPlayer.cs) |
| `ImpactLight/Medium/Heavy` | 地形撞击按 `LastImpactTier` 分挡：0–1 轻、2 中、3+ 重；另外 HUD 调试砸边走 `TuningLab.PlayerHit` 订阅 | [LabPlayer.cs:586](../TuningLab/LabPlayer.cs)、[LabAudio.cs](../TuningLab/LabAudio.cs) |
| `CrateFall` | `SetEdge` 把边缘打成 `Spirit`（框脱落），碰撞和调试砸边都走这条 | [LabPlayer.cs:908](../TuningLab/LabPlayer.cs) |
| `IceMelt` | 冰融化回普通地形 | [LabPlayer.cs:649](../TuningLab/LabPlayer.cs) |
| `IceBreak` | `BreakGroup()` 整组碎裂（同 Cue 冷却会把逐块 `BreakOne` 的重复请求并成一声） | [LabIce.cs:91](../TuningLab/LabIce.cs) |
| `PortalEnter` | 启动传送（`Departing`）和返场落地 | [TuningLab.cs:628](../TuningLab/TuningLab.cs)、[TuningLab.cs:665](../TuningLab/TuningLab.cs) |
| `PortalUi` | 选择界面：切换 / 确认 / 取消 | [TuningLab.cs:708](../TuningLab/TuningLab.cs)、[TuningLab.cs:725](../TuningLab/TuningLab.cs)、[TuningLab.cs:742](../TuningLab/TuningLab.cs) |
| `CannonLoad` | `TryAbsorb()` 把队友吞入膛内装填 | [LabPlayer.cs:1085](../TuningLab/LabPlayer.cs) |
| `CannonFire` | `Fire()` 开炮 | [LabPlayer.cs:1145](../TuningLab/LabPlayer.cs) |
| `RopeBounce` | 横绳反弹（仅 `tier >= 3` 那次弹起） | [LabHorizontalRope.cs:95](../TuningLab/LabHorizontalRope.cs) |

有意留静的两处：转换器装填（`TryAbsorbConverter`）不发声——它和开炮不是同一件事；冰的逐块碎裂不单独出声，见上表。静音开关是 `LabAudio.Muted`（静态字段，HUD 没做按钮，代码里设）。

## `01_Jump` — 跳跃（LabPlayer 起跳）

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `jump_399095_8bit_Jump.mp3` | 0.11s | [Freesound 399095](https://freesound.org/people/plasterbrain/sounds/399095/) | [399095](https://cdn.freesound.org/previews/399/399095_4284968-hq.mp3) |

原始文件是 FLAC，已按库内统一规格转成 44.1kHz 单声道 mp3；0.11s 短于其它音效，起跳要的是干脆，不再拉长。

## `02_Collision_Light` — 碰撞·轻（低挡位）

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `hit_light_265185_clickswitch-02.mp3` | 0.28s | [Freesound 265185](https://freesound.org/people/simonjsounds/sounds/265185/) | [265185](https://cdn.freesound.org/previews/265/265185_2079872-hq.mp3) |
| `hit_light_265186_clickswitch-01.mp3` | 0.37s | [Freesound 265186](https://freesound.org/people/simonjsounds/sounds/265186/) | [265186](https://cdn.freesound.org/previews/265/265186_2079872-hq.mp3) |
| `hit_light_872227_Soft_Impact_-_Pillow_Hit.mp3` | 0.4s | [Freesound 872227](https://freesound.org/people/KoimeMatsuchiya/sounds/872227/) | [872227](https://cdn.freesound.org/previews/872/872227_19256798-hq.mp3) |

## `03_Collision_Medium` — 碰撞·中（木/金属，中挡位）

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `hit_wood_218460_Wood_block_hit.mp3` | 0.28s | [Freesound 218460](https://freesound.org/people/thomasjaunism/sounds/218460/) | [218460](https://cdn.freesound.org/previews/218/218460_3344538-hq.mp3) |
| `hit_wood_452221_drawer_wood_hit_close.mp3` | 0.55s | [Freesound 452221](https://freesound.org/people/kyles/sounds/452221/) | [452221](https://cdn.freesound.org/previews/452/452221_612689-hq.mp3) |
| `hit_metal_381614_snd_ImpactMediumMetal03.mp3` | 0.65s | [Freesound 381614](https://freesound.org/people/dorian.mastin/sounds/381614/) | [381614](https://cdn.freesound.org/previews/381/381614_1304060-hq.mp3) |
| `hit_metal_406491_hit_-_metallic_-_lawn_chair_01.mp3` | 0.71s | [Freesound 406491](https://freesound.org/people/Anthousai/sounds/406491/) | [406491](https://cdn.freesound.org/previews/406/406491_5923045-hq.mp3) |

## `04_Collision_Heavy` — 碰撞·重（高挡位 / 红热）

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `hit_heavy_794142_kicks_bass_impacts_subs_by_CVLTIV8R_5.mp3` | 0.4s | [Freesound 794142](https://freesound.org/people/CVLTIV8R/sounds/794142/) | [794142](https://cdn.freesound.org/previews/794/794142_2520418-hq.mp3) |

## `05_CrateFall` — 框掉落

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `crate_fall_697092_hollow_plastic_box_dropped.mp3` | 0.46s | [Freesound 697092](https://freesound.org/people/Lemato/sounds/697092/) | [697092](https://cdn.freesound.org/previews/697/697092_6944547-hq.mp3) |

## `06_IceBreak` — 冰碎裂（LabIce 破碎）

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `ice_break_703115_crush1.mp3` | 0.45s | [Freesound 703115](https://freesound.org/people/franzzle/sounds/703115/) | [703115](https://cdn.freesound.org/previews/703/703115_1823770-hq.mp3) |

## `07_IceMelt` — 冰融化（LabIce 融化，可循环）

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `ice_melt_262635_Ice_Crack_1.mp3` | 1.1s | [Freesound 262635](https://freesound.org/people/j_p_higgins/sounds/262635/) | [262635](https://cdn.freesound.org/previews/262/262635_3175560-hq.mp3) |

## `08_PortalEnter` — 进入传送门动画（LabTeleportPoint）

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `portal_enter_494484_Portal_Phase_Jump.mp3` | 1.55s | [Freesound 494484](https://freesound.org/people/Vic5555/sounds/494484/) | [494484](https://cdn.freesound.org/previews/494/494484_10496813-hq.mp3) |

## `09_PortalUI` — 传送门选择界面（UI 悬停/选择/确认/取消）

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `ui_657948_Family_Friendly_Inspect_Sound_UI_or_In-Game_Noti.mp3` | 0.08s | [Freesound 657948](https://freesound.org/people/LilMati/sounds/657948/) | [657948](https://cdn.freesound.org/previews/657/657948_6142149-hq.mp3) |

## `10_CannonLoad` — 大炮装填

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `cannon_load_094118_ClipLoad1.mp3` | 0.26s | [Freesound 94118](https://freesound.org/people/BMacZero/sounds/94118/) | [94118](https://cdn.freesound.org/previews/94/94118_1554918-hq.mp3) |

## `11_CannonFire` — 大炮发射

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `cannon_fire_515801_Retro_Pirate_Cannon_Shot.mp3` | 1s | [Freesound 515801](https://freesound.org/people/LilMati/sounds/515801/) | [515801](https://cdn.freesound.org/previews/515/515801_6142149-hq.mp3) |

## `12_RopeBounce` — 横绳反弹（LabHorizontalRope）

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `rope_bounce_751698_Game_Jump_Sound_-_Boing_1_of_2.mp3` | 0.3s | [Freesound 751698](https://freesound.org/people/el_boss/sounds/751698/) | [751698](https://cdn.freesound.org/previews/751/751698_9129912-hq.mp3) |
| `rope_bounce_540788_Boing_1.mp3` | 0.72s | [Freesound 540788](https://freesound.org/people/magnuswaker/sounds/540788/) | [540788](https://cdn.freesound.org/previews/540/540788_11537497-hq.mp3) |

