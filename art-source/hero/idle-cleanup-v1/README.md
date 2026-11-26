# 站立图透明边缘清理

已替换 `Assets/Resources/Art/hero_front.png`。原图及导入元数据备份为
`hero_front.before.png` 和 `hero_front.before.png.meta`。

清除腿缝、鞋底周围的青绿色背景残留，去掉低透明度杂边，并把保留的轮廓
改为与步行素材一致的清晰像素边缘。保留像素的 RGB 颜色不变。
原底行是残留背景，清掉后将身体整体向下对齐 1 像素，保持鞋底落在画布底边；
画布仍是 48×64，没有缩放身体，导入元数据及 GUID 不变。

`before-after.png` 的左侧为清理前，右侧为清理后，分别展示浅色和深色背景。
`clean_idle.py` 生成预览；添加 `--apply` 才覆盖游戏素材。
`report.json` 记录清理和校验结果，本次 24 张步行及 5 张攻击素材均未修改。
