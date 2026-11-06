# 设置图标

这些是 Issue #71 设置界面使用的 16×16 像素图标，来自用户提供的 **1-bit Pixel Icons** 图标包（`Sprites/` 目录）。

| 文件 | 原文件名 | 用途 |
| --- | --- | --- |
| `MasterVolume.png` | `Media_Audio_Sound_Volume_3.png` | 主音量 |
| `MusicVolume.png` | `Media_Musical_Note_Quaver.png` | 音乐音量 |
| `SfxVolume.png` | `RPG_Spell_Skill_Magic_Explosive_Explosion.png` | 音效音量 |
| `Resolution.png` | `Software_Hardware_Monitor_Display_PC_Computer.png` | 分辨率 |
| `Fullscreen.png` | `Software_Window_Fullscreen_Mode_Select_1.png` | 全屏 |
| `Windowed.png` | `Software_Windowed_Mode_Floating_Maximize.png` | 窗口模式 |
| `Brightness.png` | `Software_Monitor_Display_Brightness_Sun.png` | 亮度（预留） |
| `KeyBindings.png` | `Controller_Gamepad.png` | 按键绑定（预留） |
| `Close.png` | `Software_Exit_Quit_Crossout_Button.png` | 关闭 |
| `ArrowLeft.png` | `Arrows_Left_West.png` | 上一档分辨率 |
| `ArrowRight.png` | `Arrows_Right_East.png` | 下一档分辨率 |
| `Settings.png` | `Software_Options_Settings_Cogwheel_Gear_Mechanics.png` | 标题齿轮 |

## 替换

保持文件名不变，直接替换同名 PNG 即可；代码从 `Resources/SettingsIcons/<name>` 读取。导入设置应保持：

- Texture Type: Default
- Filter Mode: Point (no filter)
- Compression: None
- Generate Mip Maps: off
- Wrap Mode: Clamp

`LabSettingsUI.IconResourceFolder` 可以改目录；缺图时会用运行时生成的占位棋盘并输出一条警告。

## 授权

这次随图标目录没有附带原始授权文件。发布前需要按该图标包的原授权确认署名/商用条件；代码和替换入口不受影响。
