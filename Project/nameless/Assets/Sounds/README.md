# 音效库（阶段二实装中）

来源：Freesound，全部 **CC0**（publicdomain/zero/1.0），免署名、可商用。
投票结果见 issue #60 正文，每人不限勾选次数；102 个候选中入选 19 个，弃选的 83 个文件连同 `.meta` 已删除。

文件名格式：`用途slug_FreesoundID_原始名.mp3`。导入器保持 Unity 默认（`loadType: 0` 压缩入内存、`preloadAudioData: 0`）。

**素材目录已迁到 `Assets/Sounds/Resources/`**：`LabAudio` 用 `Resources.LoadAll<AudioClip>("<NN_用途>")` 按目录整批加载，因此不需要往场景里挂任何东西，也不需要 Inspector 引用——和 `LabHud` 用 IMGUI 是同一个零场景依赖的路子。GUID 在迁移中原样保留。

## 播放接线

接线表和 Cue→触发点对照随实装 commit 一起补（`Assets/TuningLab/LabAudio.cs`，当前在工作区未提交）。

## `01_Jump` — 跳跃（LabPlayer 起跳）

| 文件 | 时长 | 来源 | 试听 |
| --- | --- | --- | --- |
| `jump_386647_SFX_Jump_15.mp3` | 0.15s | [Freesound 386647](https://freesound.org/people/jalastram/sounds/386647/) | [386647](https://cdn.freesound.org/previews/386/386647_3905081-hq.mp3) |
| `jump_386636_SFX_Jump_25.mp3` | 0.25s | [Freesound 386636](https://freesound.org/people/jalastram/sounds/386636/) | [386636](https://cdn.freesound.org/previews/386/386636_3905081-hq.mp3) |

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

