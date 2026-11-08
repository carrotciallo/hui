# 可摆放像素画

把预先画好的 PNG 素材放在本目录，关卡编辑器的「像素画」面板会扫描这些 Sprite。

导入器 `LabArtImporter` 对本目录固定使用：

- Sprite Single
- 16 Pixels Per Unit
- Point 过滤
- 无 Mipmap、无纹理压缩
- Full Rect、中心 pivot

在 `GameJam → Tuning Lab Level Editor` 的工具面板选择「像素画」，设置占地宽高（例如 1×1、2×2、3×2），再在 Scene 视图左键放置。像素画只负责显示，不创建碰撞体。
