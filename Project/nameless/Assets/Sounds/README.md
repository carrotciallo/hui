# 音效候选库（阶段一）

来源：Freesound，全部 **CC0**（publicdomain/zero/1.0），可免署名、可商用。
本目录只做候选素材入库，**没有任何播放代码**。筛选确定最终清单后，阶段二再实装。

- 文件为 Freesound 的 HQ 预览 MP3（44.1kHz），便于快速试听；阶段二定稿后可替换为原始 WAV。
- 时长要求：除动画/持续类（`07_IceMelt`、`08_PortalEnter`、`10_CannonLoad`）外，尽量控制在 0.2~0.8s；超出 1.0s 的短音效在表格里标 **⚠超长**，二阶段可裁剪或直接剔除。
- 文件名格式：`用途slug_音效ID_原始名.mp3`，其中音效 ID 即 Freesound sound id。
- `.meta` 已由 Unity 自动导入生成并随文件一起提交（导入器为默认设置：`loadType: 0` 压缩入内存、`preloadAudioData: 0`）。二阶段实装时再按用途调预加载和单声道。

共 102 个候选，按 12 个用途目录分装。

| 目录 | 用途 | 数量 | 时长范围 |
| --- | --- | --- | --- |
| `01_Jump` | 跳跃（LabPlayer 起跳） | 6 | 0.15~0.38s |
| `02_Collision_Light` | 碰撞·轻（低挡位） | 6 | 0.11~0.40s |
| `03_Collision_Medium` | 碰撞·中（木/金属，中挡位） | 12 | 0.13~0.71s |
| `04_Collision_Heavy` | 碰撞·重（高挡位 / 红热） | 6 | 0.40~0.78s |
| `05_CrateFall` | 框掉落 | 6 | 0.35~0.89s |
| `06_IceBreak` | 冰碎裂（LabIce 破碎） | 13 | 0.25~0.90s |
| `07_IceMelt` | 冰融化（LabIce 融化，可循环） | 6 | 1.10~2.43s |
| `08_PortalEnter` | 进入传送门动画（LabTeleportPoint） | 6 | 0.81~2.26s |
| `09_PortalUI` | 传送门选择界面（UI 悬停/选择/确认/取消） | 8 | 0.08~0.50s |
| `10_CannonLoad` | 大炮装填 | 14 | 0.26~1.58s |
| `11_CannonFire` | 大炮发射 | 6 | 0.40~1.19s |
| `12_RopeBounce` | 横绳反弹（LabHorizontalRope） | 13 | 0.17~0.75s |

## `01_Jump` — 跳跃（LabPlayer 起跳）

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `jump_350901_Jump_C_06.mp3` | 0.28s | 9KB | [350901](https://freesound.org/people/cabled_mess/sounds/350901/) | cabled_mess | jump | Simple retro video game sound effects created using the amazing, free  |
| `jump_386636_SFX_Jump_25.mp3` | 0.25s | 9KB | [386636](https://freesound.org/people/jalastram/sounds/386636/) | jalastram | jump | Jump SFX / 8bit |
| `jump_386647_SFX_Jump_15.mp3` | 0.15s | 6KB | [386647](https://freesound.org/people/jalastram/sounds/386647/) | jalastram | jump | Jump SFX / 8bit |
| `jump_386648_SFX_Jump_20.mp3` | 0.33s | 10KB | [386648](https://freesound.org/people/jalastram/sounds/386648/) | jalastram | jump | Jump SFX / 8bit |
| `jump_386649_SFX_Jump_19.mp3` | 0.38s | 11KB | [386649](https://freesound.org/people/jalastram/sounds/386649/) | jalastram | jump | Jump SFX / 8bit |
| `jump_386653_SFX_Jump_32.mp3` | 0.30s | 8KB | [386653](https://freesound.org/people/jalastram/sounds/386653/) | jalastram | jump | Jump SFX / 8bit |

## `02_Collision_Light` — 碰撞·轻（低挡位）

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `hit_light_030758_Nursery_0bject_count1.mp3` | 0.11s | 5KB | [30758](https://freesound.org/people/hello_flowers/sounds/30758/) | hello_flowers | soft hit | A soft pillow hitting a folden goiter HIT! |
| `hit_light_265185_clickswitch-02.mp3` | 0.28s | 9KB | [265185](https://freesound.org/people/simonjsounds/sounds/265185/) | simonjsounds | tap light | recorded with zoom recorder |
| `hit_light_265186_clickswitch-01.mp3` | 0.37s | 14KB | [265186](https://freesound.org/people/simonjsounds/sounds/265186/) | simonjsounds | tap light | recorded with zoom recorder |
| `hit_light_265187_clickswitch-07.mp3` | 0.25s | 9KB | [265187](https://freesound.org/people/simonjsounds/sounds/265187/) | simonjsounds | tap light | recorded with zoom recorder |
| `hit_light_653910_soft-hit.mp3` | 0.26s | 7KB | [653910](https://freesound.org/people/Krokulator/sounds/653910/) | Krokulator | soft hit | A soft hit sound. |
| `hit_light_872227_Soft_Impact_-_Pillow_Hit.mp3` | 0.40s | 11KB | [872227](https://freesound.org/people/KoimeMatsuchiya/sounds/872227/) | KoimeMatsuchiya | soft hit | A soft impact sound created by hitting a pillow. Recorded as a practic |

## `03_Collision_Medium` — 碰撞·中（木/金属，中挡位）

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `hit_metal_381614_snd_ImpactMediumMetal03.mp3` | 0.65s | 19KB | [381614](https://freesound.org/people/dorian.mastin/sounds/381614/) | dorian.mastin | metal impact | metal impact of a wheelchair with metal, recorded with a TASCAM DR100 |
| `hit_metal_381619_snd_ImpactSmallMetal05.mp3` | 0.46s | 14KB | [381619](https://freesound.org/people/dorian.mastin/sounds/381619/) | dorian.mastin | metal impact | metal impact of a wheelchair with metal, recorded with a TASCAM DR100 |
| `hit_metal_406491_hit_-_metallic_-_lawn_chair_01.mp3` | 0.71s | 18KB | [406491](https://freesound.org/people/Anthousai/sounds/406491/) | Anthousai | metal hit | Hitting the top part of a metal lawn chair with a wooden rod. Recorded |
| `hit_metal_406555_hit_-_metallic_-_metal_pipe_02.mp3` | 0.62s | 16KB | [406555](https://freesound.org/people/Anthousai/sounds/406555/) | Anthousai | metal hit | Hitting a metal pipe with a wooden rod. Recorded with a Tascam DR-40. |
| `hit_metal_406556_hit_-_metallic_-_metal_pipe_01.mp3` | 0.63s | 16KB | [406556](https://freesound.org/people/Anthousai/sounds/406556/) | Anthousai | metal hit | Hitting a metal pipe with a wooden rod. Recorded with a Tascam DR-40. |
| `hit_metal_406562_hit_-_metallic_01.mp3` | 0.68s | 17KB | [406562](https://freesound.org/people/Anthousai/sounds/406562/) | Anthousai | metal hit | A metallic hit. Recorded with a Tascam DR-40. |
| `hit_wood_171306_Hit_a_screwdriver_on_wood.mp3` | 0.27s | 8KB | [171306](https://freesound.org/people/Consul92/sounds/171306/) | Consul92 | wood hit | Hit a screwdriver on wood, free for commercial use without attribution |
| `hit_wood_218460_Wood_block_hit.mp3` | 0.28s | 9KB | [218460](https://freesound.org/people/thomasjaunism/sounds/218460/) | thomasjaunism | wood hit | Wood block hit |
| `hit_wood_319223_Single_Rock_Hitting_wood_3.mp3` | 0.13s | 5KB | [319223](https://freesound.org/people/worthahep88/sounds/319223/) | worthahep88 | wood hit | Single rock hitting wood to simulate bullets hitting wood. |
| `hit_wood_319228_Single_Rock_hitting_wood_2.mp3` | 0.27s | 8KB | [319228](https://freesound.org/people/worthahep88/sounds/319228/) | worthahep88 | wood hit | Rocks hitting wood to simulate bullets hitting wood |
| `hit_wood_452221_drawer_wood_hit_close.mp3` | 0.55s | 15KB | [452221](https://freesound.org/people/kyles/sounds/452221/) | kyles | wood hit | drawer wood hit close.flac |
| `hit_wood_547414_wood_hit.mp3` | 0.50s | 15KB | [547414](https://freesound.org/people/Ian_G/sounds/547414/) | Ian_G | wood hit | Wood hit |

## `04_Collision_Heavy` — 碰撞·重（高挡位 / 红热）

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `hit_heavy_794139_kicks_bass_impacts_subs_by_CVLTIV8R_47.mp3` | 0.78s | 19KB | [794139](https://freesound.org/people/CVLTIV8R/sounds/794139/) | CVLTIV8R | impact | kick kicks bas bassas sub subs low rumble bassfx impact impacts lowend |
| `hit_heavy_794141_kicks_bass_impacts_subs_by_CVLTIV8R_49.mp3` | 0.45s | 15KB | [794141](https://freesound.org/people/CVLTIV8R/sounds/794141/) | CVLTIV8R | impact | kick kicks bas bassas sub subs low rumble bassfx impact impacts lowend |
| `hit_heavy_794142_kicks_bass_impacts_subs_by_CVLTIV8R_5.mp3` | 0.40s | 14KB | [794142](https://freesound.org/people/CVLTIV8R/sounds/794142/) | CVLTIV8R | impact | kick kicks bas bassas sub subs low rumble bassfx impact impacts lowend |
| `hit_heavy_794146_kicks_bass_impacts_subs_by_CVLTIV8R_53.mp3` | 0.74s | 21KB | [794146](https://freesound.org/people/CVLTIV8R/sounds/794146/) | CVLTIV8R | impact | kick kicks bas bassas sub subs low rumble bassfx impact impacts lowend |
| `hit_heavy_794150_kicks_bass_impacts_subs_by_CVLTIV8R_57.mp3` | 0.60s | 19KB | [794150](https://freesound.org/people/CVLTIV8R/sounds/794150/) | CVLTIV8R | impact | kick kicks bas bassas sub subs low rumble bassfx impact impacts lowend |
| `hit_heavy_794155_kicks_bass_impacts_subs_by_CVLTIV8R_61.mp3` | 0.43s | 12KB | [794155](https://freesound.org/people/CVLTIV8R/sounds/794155/) | CVLTIV8R | impact | kick kicks bas bassas sub subs low rumble bassfx impact impacts lowend |

## `05_CrateFall` — 框掉落

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `crate_fall_167372_Steel_chain_being_dropped_and_dragged_on_a_cardb.mp3` | 0.66s | 13KB | [167372](https://freesound.org/people/ani_music/sounds/167372/) | ani_music | box drop | Meaty chain-on-deadened floor sound. Less high end. |
| `crate_fall_415991_Retro_Drop_01.mp3` | 0.69s | 18KB | [415991](https://freesound.org/people/LilMati/sounds/415991/) | LilMati | box drop | [ Retro_Drop_02.wav ] Retro, supply drop and/or bomb drop! (An object  |
| `crate_fall_483674_Moving_Box_of_Objects_-_4.mp3` | 0.89s | 26KB | [483674](https://freesound.org/people/SpaceJoe/sounds/483674/) | SpaceJoe | box drop | Various Objects Falling and Moving |
| `crate_fall_483675_Moving_Box_of_Objects_-_3.mp3` | 0.57s | 18KB | [483675](https://freesound.org/people/SpaceJoe/sounds/483675/) | SpaceJoe | box drop | Various Objects Falling and Moving |
| `crate_fall_697091_hollow_plastic_box_dropped_2.mp3` | 0.35s | 12KB | [697091](https://freesound.org/people/Lemato/sounds/697091/) | Lemato | box drop | Dropping an empty plastic food container with no lid onto a wooden flo |
| `crate_fall_697092_hollow_plastic_box_dropped.mp3` | 0.46s | 16KB | [697092](https://freesound.org/people/Lemato/sounds/697092/) | Lemato | box drop | Dropping an empty plastic food container with no lid onto a wooden flo |

## `06_IceBreak` — 冰碎裂（LabIce 破碎）

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `ice_break_177215_Ice_Crack_7.mp3` | 0.76s | 24KB | [177215](https://freesound.org/people/ecfike/sounds/177215/) | ecfike | ice crack | Ice cracking. |
| `ice_break_406120_water_-_ice_-_cooler_-_throwing_a_single_ice_cub.mp3` | 0.68s | 21KB | [406120](https://freesound.org/people/Anthousai/sounds/406120/) | Anthousai | ice hit | Throwing a single ice cube at the inside of the lid of an open cooler. |
| `ice_break_406121_water_-_ice_-_cooler_-_throwing_a_single_ice_cub.mp3` | 0.61s | 18KB | [406121](https://freesound.org/people/Anthousai/sounds/406121/) | Anthousai | ice hit | Throwing a single ice cube at the inside of the lid of an open cooler. |
| `ice_break_406122_water_-_ice_-_cooler_-_throwing_a_single_ice_cub.mp3` | 0.63s | 21KB | [406122](https://freesound.org/people/Anthousai/sounds/406122/) | Anthousai | ice hit | Throwing a single ice cube at the inside of the lid of an open cooler. |
| `ice_break_418194_Hard_Glass_Impact.mp3` | 0.65s | 18KB | [418194](https://freesound.org/people/deleted_user_3656686/sounds/418194/) | deleted_user_3656686 | glass crack | A meaty glass impact with some cracks forming afterward. Perfect sound |
| `ice_break_422669_Ice_Break_With_Hand.mp3` | 0.39s | 11KB | [422669](https://freesound.org/people/Lynx_5969/sounds/422669/) | Lynx_5969 | ice break | Real ice breaking sound without any license. |
| `ice_break_488509_ice.mp3` | 0.82s | 12KB | [488509](https://freesound.org/people/03Thib/sounds/488509/) | 03Thib | ice crack | rope cracking ice |
| `ice_break_591152_rock_hitting_icy_lake.mp3` | 0.90s | 24KB | [591152](https://freesound.org/people/ultraaxvii/sounds/591152/) | ultraaxvii | ice cracking | Produced this sound by slamming a piece of rock onto the icy surface o |
| `ice_break_624163_ice_cracking_01.mp3` | 0.70s | 19KB | [624163](https://freesound.org/people/wwstudioswastaken/sounds/624163/) | wwstudioswastaken | ice crack | Just the sound of me stepping on some thin ice. :D Enjoy! |
| `ice_break_668786_ice_break_on_ice.mp3` | 0.46s | 13KB | [668786](https://freesound.org/people/itinerantmonk108/sounds/668786/) | itinerantmonk108 | ice shatter | recorded on iPhone 12 |
| `ice_break_703115_crush1.mp3` | 0.45s | 15KB | [703115](https://freesound.org/people/franzzle/sounds/703115/) | franzzle | crack thin | This was actually some very thin ice I found in the grass. I cracked i |
| `ice_break_807378_spock_18_inches_Zildjian_Soundlab_Medium_Thin_Pr.mp3` | 0.30s | 10KB | [807378](https://freesound.org/people/Logicogonist/sounds/807378/) | Logicogonist | crack thin | Slightly low-pitched part of my Zildjian China and spock crashes in va |
| `ice_break_807379_spock_18_inches_Zildjian_Soundlab_Medium_Thin_Pr.mp3` | 0.25s | 9KB | [807379](https://freesound.org/people/Logicogonist/sounds/807379/) | Logicogonist | crack thin | Slightly low-pitched part of my Zildjian China and spock crashes in va |

## `07_IceMelt` — 冰融化（LabIce 融化，可循环）

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `ice_melt_177214_Ice_Crack_8.mp3` | 2.43s | 70KB | [177214](https://freesound.org/people/ecfike/sounds/177214/) | ecfike | ice melt | Ice cracking. |
| `ice_melt_177218_Ice_Hit_1.mp3` | 1.83s | 44KB | [177218](https://freesound.org/people/ecfike/sounds/177218/) | ecfike | ice melt | Ice cracking. |
| `ice_melt_177220_Ice_Hit_7.mp3` | 1.45s | 37KB | [177220](https://freesound.org/people/ecfike/sounds/177220/) | ecfike | ice melt | Ice cracking. |
| `ice_melt_262633_Ice_Crack_3.mp3` | 2.42s | 62KB | [262633](https://freesound.org/people/j_p_higgins/sounds/262633/) | j_p_higgins | ice melt | Sound of ice melting and cracking, recorded using a piezo element froz |
| `ice_melt_262634_Ice_Crack_2.mp3` | 1.71s | 50KB | [262634](https://freesound.org/people/j_p_higgins/sounds/262634/) | j_p_higgins | ice melt | Sound of ice melting and cracking, recorded using a piezo element froz |
| `ice_melt_262635_Ice_Crack_1.mp3` | 1.10s | 29KB | [262635](https://freesound.org/people/j_p_higgins/sounds/262635/) | j_p_higgins | ice melt | Sound of ice melting and cracking, recorded using a piezo element froz |

## `08_PortalEnter` — 进入传送门动画（LabTeleportPoint）

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `portal_enter_132593_the_portal.mp3` | 2.26s | 47KB | [132593](https://freesound.org/people/CeebFrack/sounds/132593/) | CeebFrack | portal | Made this in Audacity and WavePad for a portal or gateway that a playe |
| `portal_enter_494484_Portal_Phase_Jump.mp3` | 1.55s | 36KB | [494484](https://freesound.org/people/Vic5555/sounds/494484/) | Vic5555 | portal | Jumping/phasing through portals. |
| `portal_enter_659369_Retro_Portal_Opens_Up_or_Closes.mp3` | 0.81s | 18KB | [659369](https://freesound.org/people/LilMati/sounds/659369/) | LilMati | portal | Retro, Portal Opens Up or Closes If you enjoyed the sound, please STAR |
| `portal_enter_720108_Portal_Active.mp3` | 2.00s | 49KB | [720108](https://freesound.org/people/BaggoNotes/sounds/720108/) | BaggoNotes | portal | Looping portal sound, meant for an idle portal. |
| `portal_enter_720109_Portal_Despawn.mp3` | 1.50s | 36KB | [720109](https://freesound.org/people/BaggoNotes/sounds/720109/) | BaggoNotes | portal | Despawn effect for a disappearing portal. |
| `portal_enter_720110_Portal_Spawn.mp3` | 2.00s | 45KB | [720110](https://freesound.org/people/BaggoNotes/sounds/720110/) | BaggoNotes | portal | Spawning effect for a portal. |

## `09_PortalUI` — 传送门选择界面（UI 悬停/选择/确认/取消）

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `ui_396193_UI_Cute_Select_Major_6th.mp3` | 0.24s | 7KB | [396193](https://freesound.org/people/plasterbrain/sounds/396193/) | plasterbrain | ui select | A cheerful select sound for menus. You could also use it as a sound fo |
| `ui_583079_File_Select_metallic_wet_hollow.mp3` | 0.33s | 9KB | [583079](https://freesound.org/people/Breviceps/sounds/583079/) | Breviceps | ui select | Metallic wet/hollow file select sound |
| `ui_653382_select.mp3` | 0.16s | 5KB | [653382](https://freesound.org/people/Krokulator/sounds/653382/) | Krokulator | ui select | A sound that could probably be used as a "select" sound for an UI. |
| `ui_657948_Family_Friendly_Inspect_Sound_UI_or_In-Game_Noti.mp3` | 0.08s | 3KB | [657948](https://freesound.org/people/LilMati/sounds/657948/) | LilMati | ui select | [ Futuristic Inspect Sound, UI, or In-Game Notification.wav ] Family F |
| `ui_738050_WD40_quarter_full_-_Finger_tapping_8.mp3` | 0.19s | 7KB | [738050](https://freesound.org/people/Sadiquecat/sounds/738050/) | Sadiquecat | ui select | More in the pack! Subject : A mostly used 250ml bottle of WD-40 hit by |
| `ui_829014_vg_select.mp3` | 0.37s | 8KB | [829014](https://freesound.org/people/Mihacappy/sounds/829014/) | Mihacappy | ui select | Metallic sound that fits nicely as a select or confirm action for UI/G |
| `ui_864773_UI_Select_ceramic.mp3` | 0.50s | 14KB | [864773](https://freesound.org/people/avaol/sounds/864773/) | avaol | ui select | Made this a long time ago now but I think it is some various objects ( |
| `ui_865582_SYS_select.mp3` | 0.11s | 4KB | [865582](https://freesound.org/people/MirMaximus/sounds/865582/) | MirMaximus | ui select | Select sound effect. |

## `10_CannonLoad` — 大炮装填

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `cannon_load_094118_ClipLoad1.mp3` | 0.26s | 8KB | [94118](https://freesound.org/people/BMacZero/sounds/94118/) | BMacZero | mechanical load | A light mechanical thunk or a clip reload sound. |
| `cannon_load_266105_shotgun.mp3` | 1.17s | 27KB | [266105](https://freesound.org/people/Marregheriti/sounds/266105/) | Marregheriti | reload shotgun | shotgun with a reload cocking sound |
| `cannon_load_406227_Gun_cock_near_mono.mp3` | 0.62s | 16KB | [406227](https://freesound.org/people/_stubb/sounds/406227/) | _stubb | cock hammer | Pulled the hammer back on a prop weapon for the cocking sound. |
| `cannon_load_489615_MetallicSlide01.mp3` | 1.25s | 29KB | [489615](https://freesound.org/people/falcospizaetus/sounds/489615/) | falcospizaetus | metal slide | Metallic slide, useful for robots or mechanic contraptions. |
| `cannon_load_489617_MetallicSlide07.mp3` | 1.58s | 37KB | [489617](https://freesound.org/people/falcospizaetus/sounds/489617/) | falcospizaetus | metal slide | Metallic slide, useful for robots or mechanic contraptions. |
| `cannon_load_489618_MetallicSlide06.mp3` | 1.58s | 37KB | [489618](https://freesound.org/people/falcospizaetus/sounds/489618/) | falcospizaetus | metal slide | Metallic slide, useful for robots or mechanic contraptions. |
| `cannon_load_489619_MetallicSlide05.mp3` | 1.58s | 37KB | [489619](https://freesound.org/people/falcospizaetus/sounds/489619/) | falcospizaetus | metal slide | Metallic slide, useful for robots or mechanic contraptions. |
| `cannon_load_500294_Shotgun_Reload.mp3` | 1.15s | 28KB | [500294](https://freesound.org/people/Bratish/sounds/500294/) | Bratish | reload shotgun | This is sound of Shotgun Reloading. It is created using Baikal`s MP-65 |
| `cannon_load_522165_Bolter.mp3` | 0.62s | 17KB | [522165](https://freesound.org/people/magnuswaker/sounds/522165/) | magnuswaker | mechanical load | Sound of loading a metallic object. Base sample was the spring of a st |
| `cannon_load_679876_GunRack1.mp3` | 0.73s | 19KB | [679876](https://freesound.org/people/AKkingStudio/sounds/679876/) | AKkingStudio | gun load | The sound of a gun reloading/racking/cocking. It is short and fast, so |
| `cannon_load_679877_GunRack2.mp3` | 1.09s | 32KB | [679877](https://freesound.org/people/AKkingStudio/sounds/679877/) | AKkingStudio | gun load | The sound of a gun reloading/racking/cocking. Long and loud, I can see |
| `cannon_load_724126_lightergas.mp3` | 1.30s | 31KB | [724126](https://freesound.org/people/saha213131/sounds/724126/) | saha213131 | spark fuse | lighter gas |
| `cannon_load_766752_TOYMisc_Toy_Gun_Reload_ODIMog_OWISFX.mp3` | 0.98s | 19KB | [766752](https://freesound.org/people/OMogadime/sounds/766752/) | OMogadime | reload shotgun | A kids-toy shotgun being reloaded. |
| `cannon_load_855599_Pulling_and_Releasing_Barrett_M82A1_Charging_Han.mp3` | 0.86s | 22KB | [855599](https://freesound.org/people/qubodup/sounds/855599/) | qubodup | gun load | Lock-n-Load. Extracted from Snipers Fire at 48th Winston P. Wilson and |

## `11_CannonFire` — 大炮发射

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `cannon_fire_441373_Heavy_Artillery_Shot.mp3` | 0.51s | 13KB | [441373](https://freesound.org/people/LilMati/sounds/441373/) | LilMati | cannon shot | Heavy Artillery Shot This sound can for example be used in games, for  |
| `cannon_fire_515801_Retro_Pirate_Cannon_Shot.mp3` | 1.00s | 23KB | [515801](https://freesound.org/people/LilMati/sounds/515801/) | LilMati | cannon shot | [ Retro, Laser Shot 01.wav ] Retro, Pirate Cannon Shot |
| `cannon_fire_850499_Little_Cannon.mp3` | 0.40s | 10KB | [850499](https://freesound.org/people/vulkanbruder/sounds/850499/) | vulkanbruder | cannon shot | A cannon shot. Basically, a short version of Cannon Shot. Original sou |
| `cannon_fire_854183_Autocannon_Shot_1.mp3` | 0.88s | 22KB | [854183](https://freesound.org/people/qubodup/sounds/854183/) | qubodup | cannon shot | Autocannon shot. Likely a 25 mm M242 Bushmaster autocannon chain gun m |
| `cannon_fire_854184_Autocannon_Shot_2.mp3` | **1.19s ⚠超长** | 29KB | [854184](https://freesound.org/people/qubodup/sounds/854184/) | qubodup | cannon shot | Autocannon shot. Likely a 25 mm M242 Bushmaster autocannon chain gun m |
| `cannon_fire_854185_Autocannon_Shot_3.mp3` | **1.10s ⚠超长** | 27KB | [854185](https://freesound.org/people/qubodup/sounds/854185/) | qubodup | cannon shot | Autocannon shot. Likely a 25 mm M242 Bushmaster autocannon chain gun m |

## `12_RopeBounce` — 横绳反弹（LabHorizontalRope）

| 文件 | 时长 | 大小 | Freesound | 作者 | 检索词 | 描述 |
| --- | --- | --- | --- | --- | --- | --- |
| `rope_bounce_421568_CS_Magn_Obj_2_spr_2_bands_Samples_6_3.mp3` | 0.45s | 12KB | [421568](https://freesound.org/people/Word_Man_1/sounds/421568/) | Word_Man_1 | spring | Hits from a contact mic instrument with 2 rubber bands and 2 springs. |
| `rope_bounce_421572_CS_Magn_Obj_2_spr_2_bands_Samples_8_-_Part_2.mp3` | 0.26s | 7KB | [421572](https://freesound.org/people/Word_Man_1/sounds/421572/) | Word_Man_1 | spring | Hits from a contact mic instrument with 2 rubber bands and 2 springs. |
| `rope_bounce_532243_Snap_Layered_02.mp3` | 0.20s | 8KB | [532243](https://freesound.org/people/JoshuaPomeroy/sounds/532243/) | JoshuaPomeroy | snap | Stereo snap made from 3 layers including a finger snap, a wood block,  |
| `rope_bounce_532244_Snap_Layered_01.mp3` | 0.17s | 7KB | [532244](https://freesound.org/people/JoshuaPomeroy/sounds/532244/) | JoshuaPomeroy | snap | Stereo snap made from 3 layers including a finger snap, a wood block,  |
| `rope_bounce_532248_Snap_Layered_05.mp3` | 0.27s | 9KB | [532248](https://freesound.org/people/JoshuaPomeroy/sounds/532248/) | JoshuaPomeroy | snap | Stereo snap made from 3 layers including a finger snap, a wood block,  |
| `rope_bounce_540788_Boing_1.mp3` | 0.72s | 22KB | [540788](https://freesound.org/people/magnuswaker/sounds/540788/) | magnuswaker | boing | Springy noise from the Jews/Jaw Harp instrument. Phonetic reference: " |
| `rope_bounce_571119_Boing-SFX.mp3` | 0.22s | 5KB | [571119](https://freesound.org/people/elfstonepress/sounds/571119/) | elfstonepress | boing | Boing, Bong with fade, Droplet with reverb |
| `rope_bounce_614749_Boing.mp3` | 0.21s | 7KB | [614749](https://freesound.org/people/Hajisounds/sounds/614749/) | Hajisounds | boing | This is a boing sound I created by shaking a microphone (which resulte |
| `rope_bounce_616800_elastic1-013.mp3` | 0.52s | 11KB | [616800](https://freesound.org/people/fbarriac/sounds/616800/) | fbarriac | bounce elastic | High bouncing |
| `rope_bounce_686180_Muffled_spring_loosening.mp3` | 0.75s | 21KB | [686180](https://freesound.org/people/redpanda69/sounds/686180/) | redpanda69 | spring | The small bed inside a stapler sliding closed. You can hear the spring |
| `rope_bounce_695923_pogoboye.mp3` | 0.50s | 11KB | [695923](https://freesound.org/people/synxwtf/sounds/695923/) | synxwtf | boing | boing boing |
| `rope_bounce_751698_Game_Jump_Sound_-_Boing_1_of_2.mp3` | 0.30s | 9KB | [751698](https://freesound.org/people/el_boss/sounds/751698/) | el_boss | boing | A remixed boing sound for a jump action in a video game. LMK if you us |
| `rope_bounce_751699_Game_Jump_Sound_-_Boing_2_of_2.mp3` | 0.41s | 11KB | [751699](https://freesound.org/people/el_boss/sounds/751699/) | el_boss | boing | A remixed boing sound for a jump action in a video game. LMK if you us |


## 超长候选（>1.0s，需二阶段裁剪或剔除）

- `11_CannonFire/cannon_fire_854184_Autocannon_Shot_2.mp3` — 1.19s
- `11_CannonFire/cannon_fire_854185_Autocannon_Shot_3.mp3` — 1.10s

> 装填类整体偏长是「装填动作」本身有多步；若嫌拖沓，二阶段可直接截断到 0.8s 或改选更短的 click 组。
