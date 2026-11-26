# 拍照女 · 市区站立形象

原图来自 `D:/1游戏素材/拍照女站立帧.png`，备份保存在 `source.png`。
`prepare_sprite.py` 清除低透明度杂点，以同一个完整人物范围裁切后导出 48×64 透明 PNG，保留原有角色配色。

游戏资源：`Assets/Resources/Art/npc_photo_girl.png`，32 PPU、Point 采样、无压缩、无 mipmap。
`WorldRoot.BuildPhotoGirl` 在市区 `overworld` 创建名为“拍照女”的场景对象。
脚底位于原地图像素坐标 `(672, 310)`，在自动售货机左侧的人行道上。
沿用主角 1.5 倍角色缩放，身体子对象抬高半个精灵高度，使脚底对齐地图位置。
NPC 随市区场景容器创建和销毁。

- `character-comparison.png`：与当前主角的同尺度对比。
- `city-placement-preview.png`：游戏地图上的位置合成预览，不是 Unity 录像。
- `report.json`：源文件与成品校验值、透明背景及落脚位置检查、安装和编译结果。

本次 C# 编译通过；Unity 启动仍因许可证未激活退出，尚未完成引擎内验证。
