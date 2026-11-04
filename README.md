# 修仙 Demo —— 像素风、数据驱动的最小骨架

用 **Unity 2021.3.19f1c1** + C# 搭的「鬼谷八荒 / 太吾绘卷」类型游戏的地基。

两条核心约定：

- **美术走像素风**（基准 640×360，PPU 32）——一个人能画得动
- **内容走 JSON**（境界 / 功法 / 事件）——加内容不用写代码

---

## 一、怎么打开

1. 打开 **Unity Hub**（`E:\Unity\Unity Hub\Unity Hub.exe`）
2. 点「打开」→ 选择 `E:\mygame` 目录
3. 编辑器打开后，点顶部的 ▶ **Play**

不需要往场景里拖任何东西。`Bootstrap.cs` 会自动把界面建出来。

> 版本必须选 `2021.3.19f1c1`（中国版），换版本会触发资源重导和 API 差异。

---

## 二、像素管线（重点）

### 基准参数

| 参数 | 值 | 定义在哪 |
|---|---|---|
| 逻辑分辨率 | **640×360** | `PixelArtSettings.ReferenceWidth/Height` |
| Pixels Per Unit | **32** | `PixelArtSettings.PixelsPerUnit` |
| 地形格边长 | 32 px | `PixelArtSettings.TileSize` |
| 窗口默认 | 1280×720（正好 2 倍） | `ProjectSetup.SetupPlayer` |

**为什么是 640×360 而不是 320×180**：中文最小要 12px 才看得清，再小就糊成一片。
这个游戏文字量大，320×180 一行放不下几个字。

改分辨率只需要改 `Assets/Scripts/Core/PixelArtSettings.cs`，导入器、UI、摄像机会跟着变。

### 美术放哪

```
Assets/Resources/Art/          ← 贴图全丢这里，导入设置自动套用
Assets/Resources/Fonts/        ← 字体丢这里，渲染模式自动套用
```

**放进去就不用管了。** `Assets/Editor/PixelArtImporter.cs` 会自动设成：

- Filter Mode = **Point**（不设就是 Bilinear，像素会糊）
- Compression = **None**（默认压缩会弄脏色块）
- Mip Maps = **关**
- Pixels Per Unit = **32**
- Mesh Type = **Full Rect**（UI 平铺需要）

### 字体

已经装好 **Fusion Pixel Font 12px 简体中文**（OFL 协议，可商用）：
`Assets/Resources/Fonts/fusion-pixel-12px-zh_hans.ttf`

- 授权原文：`Assets/Resources/Fonts/fusion-pixel-OFL.txt`
- 上游：https://github.com/TakWolf/fusion-pixel-font

`Assets/Editor/PixelFontImporter.cs` 会把字体设为 **Hinted Raster**（关抗锯齿）。
这是像素游戏最容易漏的一步——不关的话 12px 中文放大到 24px 是一团灰雾。

**想换字体**：直接把 ttf 丢进 `Assets/Resources/Fonts/`，删掉旧的，文件名随意。
`UIFactory.MainFont` 会取该目录下的第一个字体。整个目录空着就退回系统字体（能用但不好看）。

### 相机

场景 `Assets/Scenes/Main.unity` 的 Main Camera 上挂了 **PixelPerfectCamera**：

```
assetsPPU = 32   refResolution = 640×360   pixelSnapping = on   upscaleRT = off
```

`upscaleRT` 故意关掉：当前 UI 是 Overlay Canvas，开 RT 会让 UI 不跟着缩放。
等你加了世界渲染/地图，想追求更硬的像素感再把 `upscaleRT` 打开。

---

## 三、跑起来是什么样

顶部一行状态：立绘（32×48 占位小人）/ 姓名 / 境界 / 天数 / 修为 / 气血 / 灵石 / 攻击。
底部六个按钮：修炼 / 突破 / 游历 / 存档 / 读档 / 重开。背景是平铺的草地像素图。

气血归零后所有行动会被拦住，必须点「重开」。

---

## 四、目录结构

```
Assets/
├─ Scripts/
│  ├─ Bootstrap.cs              自动入口（Play 就跑起来）
│  ├─ Core/
│  │  ├─ PixelArtSettings.cs    ★ 像素参数的唯一真源
│  │  ├─ DataModels.cs          配置表的 C# 结构（字段名 = JSON 键名）
│  │  ├─ GameState.cs           存档里的全部数据
│  │  ├─ DataLoader.cs          读 Resources 下的 JSON
│  │  ├─ SaveSystem.cs          存档 / 读档 / 删档
│  │  └─ ContentLibrary.cs      把三张表读进内存，按 id 查询
│  ├─ Gameplay/
│  │  └─ GameSession.cs         全部玩法逻辑（不含 UI，可命令行测试）
│  └─ UI/
│     ├─ UIFactory.cs           纯代码搭 UGUI + 字体加载
│     └─ GameUI.cs              界面与交互
├─ Editor/
│  ├─ PixelArtImporter.cs       ★ 贴图自动套像素导入设置
│  ├─ PixelFontImporter.cs      ★ 字体自动关抗锯齿
│  └─ ProjectSetup.cs           生成场景 + 工程配置 + 自检
└─ Resources/
   ├─ Art/                      像素占位图（5 张）
   ├─ Fonts/                    像素字体 + 授权
   └─ Data/                     境界表 / 功法表 / 事件表
```

---

## 五、加内容：只改 JSON，不碰代码

### 加一个境界

编辑 `Assets/Resources/Data/realms.json`，往数组里推一项：

```json
{ "id": "lianxu", "name": "炼虚期", "qiRequired": 5000, "maxHp": 5000, "baseAttack": 700, "desc": "……" }
```

`qiRequired` 是**突破到下一境界**需要的修为，最高境界填 `0`。

### 加一门功法

编辑 `Assets/Resources/Data/techniques.json`：

```json
{ "id": "t_fire", "name": "离火诀", "desc": "……", "attackBonus": 25, "maxHpBonus": 80, "cultivateBonus": 2 }
```

### 加一个事件

编辑 `Assets/Resources/Data/events.json`：

```json
{
  "id": "e_my_event",
  "title": "标题",
  "text": "玩家看到的故事正文。",
  "weight": 10,
  "onceOnly": false,
  "require": { "minRealm": 0, "minSpiritStones": 0, "hasFlag": "", "notFlag": "" },
  "options": [
    {
      "text": "选项按钮文字",
      "resultText": "选完之后显示的结果文字",
      "effects": [
        { "type": "stat",  "key": "cultivation", "value": 50 },
        { "type": "stat",  "key": "spiritStones", "value": -20 },
        { "type": "stat",  "key": "hp", "value": -10 },
        { "type": "flag",  "key": "met_someone", "value": 1 },
        { "type": "learn", "key": "t_fire" },
        { "type": "goto",  "key": "e_next_event" }
      ]
    }
  ]
}
```

**四种效果，代码全认识：**

| type | 作用 | key 填什么 | value |
|---|---|---|---|
| `stat` | 改数值 | `hp` / `cultivation` / `spiritStones` | 增量（可为负） |
| `flag` | 打标记 | 标记名，随便起 | 1 |
| `learn` | 学功法 | 功法 id | 不用填 |
| `goto` | 接着播下一个事件 | 事件 id | 不用填 |

改完 JSON **不用编译**，回编辑器直接 Play 就生效。

---

## 六、命令行自检

```powershell
& "E:\Unity\2021.3.19f1c1\Editor\Unity.exe" -batchmode -quit `
  -projectPath "E:\mygame" `
  -executeMethod ProjectSetup.RunAll `
  -logFile "E:\mygame\unity-run.log"
```

退出码 `0` = 全部通过。检查项：

- 三张配置表能否解析
- 修炼是否真的加修为、突破是否真的进阶
- 每个事件引用的功法 / 事件 id 是否存在
- 存档写入后读回数据是否一致
- **5 张精灵是否都是 Point 过滤、PPU 32**
- **像素字体是否就位**

编辑器菜单里也有：**修仙 Demo → 生成主场景并配置工程**。

---

## 七、像素游戏常见坑

| 症状 | 原因 | 解法 |
|---|---|---|
| 图糊了 | Filter Mode 是 Bilinear | 已自动设 Point；手动拖的图放 `Resources/Art/` 下 |
| 有噪点脏边 | 贴图被压缩 | 已自动设 Uncompressed |
| 跑动时抖动 | 没开像素吸附 | 相机上的 PixelPerfectCamera `pixelSnapping` 已开 |
| 精灵尺寸对不齐 | PPU 不统一 | 全部 32，别混用 |
| 中文成了方块 | 用了内置 Arial | 已装中文字体，别删 `Resources/Fonts` |
| 文字发灰模糊 | 字体开了抗锯齿 | 已自动设 Hinted Raster |
| UI 平铺错位 | 精灵 Mesh Type 是 Tight | 已自动设 Full Rect |

---

## 八、下一步该往上加什么

按性价比排序，一次只做一件：

1. **地图**：现在「游历」是空抽。改成 3~5 个地点，每个地点有自己的事件池；用 Tilemap 拼地图，把 `tile_grass` / `tile_rock` / `tile_water` / `tile_path` 拖进 Tile Palette。
2. **立绘**：把 `char_hero.png` 换成正式美术，或者按境界换不同立绘（32×48 → 64×96）。
3. **战斗**：极简回合制替掉现在的「拔剑迎战 → 直接扣血」。
4. **NPC**：`npcs.json` + 好感度，事件里能增减。

**别做的事**：不要上 ECS/DOTS，不要引入第三方框架，不要做联机。这个规模用最土的 MonoBehaviour 最省命。

---

## 附：占位美术说明

`Assets/Resources/Art/` 里的 5 张图是我用脚本程序化生成的**纯占位**，不是正式美术：

- `tile_grass` / `tile_rock` / `tile_water` / `tile_path` —— 32×32 地形块，各一个色系
- `char_hero` —— 32×48 角色，白袍蓝边

它们的作用是让管线立刻可验证（PPU 对不对、平铺对不对、点过滤对不对）。
正式美术直接用 Aseprite 画 32×32 的图换掉同名文件即可，导入设置会自动套用。
