# 24 帧步行 v2

已替换游戏走路资源 `Assets/Resources/Art/hero_run_down_0.png` 至 `_23.png`。
`PlayerWalker.WalkFrameDuration` 为 0.035 秒，一轮仍为 0.84 秒；移动速度不变。

原视频每个步行周期约 20 帧。v1 只采样了其中 12 帧；v2 使用全部原始帧，
通过 FFmpeg 双向运动补偿插帧均匀输出 24 帧，并用循环上下文处理接缝。
保留 v1 的共同裁切范围和 48×64 像素画布，沿用相同背景清理方式。
前 12 个资源 GUID 保留，新增 12 个资源的导入设置保持一致。
v1 的游戏资源及播放代码在 `backup-runtime/` 中；站立和攻击素材未改变。

- `extract_walk.py`：重新生成 24 帧，不直接覆盖游戏素材。依赖同级 v1 的清背景函数和原视频。
- `preview_and_verify.py`：检查已安装素材，生成对比动图与连续移动的 60 FPS 视频。
- `previews/12-vs-24.gif`：左侧 v1，右侧 v2，两侧周期均为 0.84 秒。
- `previews/room-walk-24-60fps.mp4`：人物位置按 60 FPS 连续变化，走路图按 35 ms 切换。
- `validation.json`：资源完整性、原 GUID、透明背景、接缝和相邻帧变化量检查。
- `build-verification.json`：本次 C# 编译与 Unity 启动结果。

预览均为当前素材的离线合成，不是 Unity 实机录像。
本次运行时 C# 编译通过；Unity 启动仍因未激活许可证退出，因此尚未完成引擎内验证。
