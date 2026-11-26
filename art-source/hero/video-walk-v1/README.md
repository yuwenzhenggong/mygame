# 视频行走序列 v1

- 来源：用户提供的 `生成指定条件的视频.mp4`，原视频保存在 `source.mp4`。
- 选取第 88–107 帧的完整步行周期，从中采样 12 帧，采样编号与源文件校验值见 `manifest.json`。
- 清除草地、地面阴影和边缘混色。整套使用同一个裁切范围和缩放比例，输出 48×64 RGBA 透明 PNG。
- 已安装到 `Assets/Resources/Art/hero_run_down_0.png` 至 `_11.png`。前十张保留原 GUID，新增两张导入配置与现有素材一致。
- `WorldRoot.BuildPlayer` 原本就会连号加载走路素材，不需要额外绑定 Animator。`PlayerWalker.WalkFrameDuration` 改为 0.07 秒，完整循环 0.84 秒；朝左用原图，朝右镜像。
- 当前站立和攻击素材不变。替换前的十张走路图及其元数据保存在 `backup-runtime/`。

## 复现与预览

使用安装了 Pillow、NumPy、imageio_ffmpeg 的 Python 执行 `extract_walk.py` 可重新生成 `frames/`；脚本不直接覆盖游戏资源。`preview_and_verify.py` 检查已安装资源并生成动画预览，检查结果在 `validation.json`。

- `previews/room-walk-demo.gif`：当前房间地图上的站立、左右行走、停止、两段攻击合成演示。
- `previews/idle-walk-attack.gif`：站立、走路、攻击的外观并排对比。
- `previews/contact-sheet.png`：12 帧总览。
- `cutouts/`：清背景后的原分辨率素材，便于后续重新导出。

所有演示都是使用已安装素材制作的离线预览，不是 Unity 实机录像。本次 Unity 启动因许可证未激活退出（exit 1），因此尚未进行引擎内验证；运行时 C# 已使用本机 Unity 附带的编译器和程序集编译通过。验证记录见 `build-verification.json`。
