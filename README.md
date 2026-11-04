# 修仙 Demo —— 像素风、可走路、数据驱动的最小骨架

用 **Unity 2021.3.19f1c1** + C# 搭的「鬼谷八荒 / 太吾绘卷」类型游戏的地基。

三条核心约定：

- **能走路**：WASD / 方向键在一张小地图上自由走动，格子碰撞
- **美术走像素风**（基准 640×360，PPU 32）——一个人能画得动
- **内容走 JSON**（地图 / 境界 / 功法 / 事件）——加内容不用写代码

---

## 一、怎么打开

1. 打开 **Unity Hub**（`E:\Unity\Unity Hub\Unity Hub.exe`）
2. 点「打开」→ 选择 `E:\mygame` 目录
3. 编辑器打开后，点顶部的 ▶ **Play**

不需要往场景里拖任何东西。`Bootstrap.cs` 会自动建好世界和 HUD。

> 版本必须选 `2021.3.19f1c1`（中国版），换版本会触发资源重导和 API 差异。

---

## 二、走路与地图

### 操作

| 键 | 作用 |
|---|---|
| **W A S D** 或 **方向键** | 移动（八方向，斜走不加速） |

撞上岩石和水会滑着走，不会卡死。事件面板弹出时移动会被冻结。

### 改地图：只改字符画

地图就是 `Assets/Resources/Data/map.json` 里的一张字符画：

```json
{
  "rows": [
    "########################################",
    "#..................,...................#",
    ...
  ],
  "legend": [
    { "ch": ".", "sprite": "tile_grass", "solid": false },
    { "ch": ",", "sprite": "tile_path",  "solid": false },
    { "ch": "#", "sprite": "tile_rock",  "solid": true  },
    { "ch": "~", "sprite": "tile_water", "solid": true  },
    { "ch": "@", "sprite": "tile_path",  "solid": false }
  ]
}
```

规则很简单：

- **`rows` 里每一行的字符数必须完全一样**（现在是 40 宽 × 23 高）
- **第 0 行是地图顶部**，最后一行是底部（程序会自动翻转 y 轴）
- **`@` 是出生点**，一个地图只能有一个
- 想加新地形：往 `legend` 里加一条，`sprite` 填 `Assets/Resources/Art/` 里的图名

改完不用编译，回编辑器直接 Play 就生效。

### 现有地图

40×23 格，一屏能看到 20×11 格，大约是两屏见方。有草地、土路十字、左上水塘、几堆山石，
外围一圈山石当边界。想扩地图就把 `rows` 加宽加高，相机边界会自动跟着算。

### 主角

主角是四向图，源文件是带**真 alpha 通道**的 1536×1024 四联图（WorkBuddy 处理后导出的 RGBA PNG），
已经切好并放到 `Assets/Resources/Art/`：

| 文件 | 朝向 | 何时用 |
|---|---|---|
| `hero_front.png` | 正面对镜头 | 往下走 / 初始 |
| `hero_right.png` | 右侧身 | 往右走 |
| `hero_back.png` | 背影 | 往上走 |
| `hero_left.png` | 左侧身 | 往左走 |

四个都是 **48×64** 的统一画布，内容水平居中、脚底对齐，所以切换朝向时不会跳位。

**走图规则**（`PlayerWalker.UpdateFacing`）：竖着走用正/背视图，横着走用左右视图；
输入同时存在时，竖直分量更大就优先用竖视图。想改成「斜走也用左右视图」改那几行判断即可。

**换一套角色图**：把新的四张图按同名覆盖就行，尺寸保持 48×64 即可（不同尺寸也能跑，
只要改 `WorldRoot.BuildPlayer` 里 body 的 `localPosition.y`——它是精灵高度的一半）。

### 碰撞怎么算的

没有用 `Rigidbody2D`。俯视角走格用不上物理引擎，`PlayerWalker.cs` 自己算
「脚底一个小矩形和周围格子是否重叠」，两个轴分开移动，所以撞墙能贴着墙滑。

- 身体上半身可以略微探进墙里，走位手感更自然
- 地图外一律当作墙，走不出去
- 半宽/半高在 `PlayerWalker` 上，可以直接在 Inspector 里调

---

## 三、像素管线

### 基准参数

| 参数 | 值 | 定义在哪 |
|---|---|---|
| 逻辑分辨率 | **640×360** | `PixelArtSettings.ReferenceWidth/Height` |
| Pixels Per Unit | **32** | `PixelArtSettings.PixelsPerUnit` |
| 地形格边长 | 32 px = 1 世界单位 | `PixelArtSettings.TileSize` |
| 窗口默认 | 1280×720（正好 2 倍） | `ProjectSetup.SetupPlayer` |

**为什么是 640×360 而不是 320×180**：中文最小要 12px 才看得清，再小就糊成一片。
这个游戏文字量大，320×180 一行放不下几个字。

### 美术放哪

```
Assets/Resources/Art/          ← 贴图全丢这里，导入设置自动套用
Assets/Resources/Fonts/        ← 字体丢这里，渲染模式自动套用
```

`Assets/Editor/PixelArtImporter.cs` 会自动设成：
Filter Mode = **Point**、Compression = **None**、Mip Maps = **关**、
PPU = **32**、Mesh Type = **Full Rect**。

### 字体

已装好 **Fusion Pixel Font 12px 简体中文**（OFL 协议，可商用）：
`Assets/Resources/Fonts/fusion-pixel-12px-zh_hans.ttf`

`PixelFontImporter.cs` 会把字体设为 **Hinted Raster**（关抗锯齿）。
这是像素游戏最容易漏的一步——不关的话 12px 中文放大到 24px 是一团灰雾。

换字体：把 ttf 丢进 `Assets/Resources/Fonts/`，删掉旧的，文件名随意。

### 相机

场景里的 Main Camera 挂了 **PixelPerfectCamera**：
`assetsPPU = 32`、`refResolution = 640×360`、`pixelSnapping = on`、`upscaleRT = off`。

`CameraFollow.cs` 负责跟随 + 地图边界夹紧。**故意不做平滑插值**——
像素游戏里相机停在半像素上会让整屏抖动，直接跟随最稳。

---

## 四、目录结构

```
Assets/
├─ Scripts/
│  ├─ Bootstrap.cs              自动入口（Play 就跑起来）
│  ├─ Core/
│  │  ├─ PixelArtSettings.cs    ★ 像素参数的唯一真源
│  │  ├─ GameFlow.cs            全局输入闸门（弹窗时冻结移动）
│  │  ├─ MapModels.cs           地图 JSON 结构
│  │  ├─ DataModels.cs          境界/功法/事件的 JSON 结构
│  │  ├─ GameState.cs           存档里的全部数据
│  │  ├─ DataLoader.cs          读 Resources 下的 JSON
│  │  ├─ SaveSystem.cs          存档 / 读档 / 删档
│  │  └─ ContentLibrary.cs      把配置表读进内存，按 id 查询
│  ├─ World/
│  │  ├─ MapGrid.cs             ★ 字符画 → 格子数据（碰撞/出生点）
│  │  ├─ WorldRoot.cs           ★ 运行时建 Tilemap + 放玩家 + 挂相机
│  │  ├─ PlayerWalker.cs        ★ WASD 移动 + 格子碰撞
│  │  └─ CameraFollow.cs        跟随 + 边界夹紧
│  ├─ Gameplay/
│  │  └─ GameSession.cs         全部玩法逻辑（不含 UI，可命令行测试）
│  └─ UI/
│     ├─ UIFactory.cs           纯代码搭 UGUI + 字体加载
│     └─ GameUI.cs              HUD、事件弹窗、按钮
├─ Editor/
│  ├─ PixelArtImporter.cs       贴图自动套像素导入设置
│  ├─ PixelFontImporter.cs      字体自动关抗锯齿
│  └─ ProjectSetup.cs           生成场景 / 工程配置 / 自检 / 渲染预览图
└─ Resources/
   ├─ Art/                      8 张图：4 张地形 + 4 张主角四向
   ├─ Fonts/                    像素字体 + 授权
   └─ Data/                     map.json / realms / techniques / events
```

---

## 五、加内容：只改 JSON，不碰代码

### 加一个境界

`Assets/Resources/Data/realms.json`：

```json
{ "id": "lianxu", "name": "炼虚期", "qiRequired": 5000, "maxHp": 5000, "baseAttack": 700, "desc": "……" }
```

`qiRequired` 是**突破到下一境界**需要的修为，最高境界填 `0`。

### 加一门功法

`Assets/Resources/Data/techniques.json`：

```json
{ "id": "t_fire", "name": "离火诀", "desc": "……", "attackBonus": 25, "maxHpBonus": 80, "cultivateBonus": 2 }
```

### 加一个事件

`Assets/Resources/Data/events.json`：

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

| type | 作用 | key 填什么 | value |
|---|---|---|---|
| `stat` | 改数值 | `hp` / `cultivation` / `spiritStones` | 增量（可为负） |
| `flag` | 打标记 | 标记名，随便起 | 1 |
| `learn` | 学功法 | 功法 id | 不用填 |
| `goto` | 接着播下一个事件 | 事件 id | 不用填 |

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
- 5 张精灵是否都是 Point 过滤、PPU 32
- 像素字体是否就位
- **地图能否解析、出生点是否落在实心格上、四周是否有路**

### 渲染一张预览图

```powershell
& "E:\Unity\2021.3.19f1c1\Editor\Unity.exe" -batchmode -quit `
  -projectPath "E:\mygame" `
  -executeMethod ProjectSetup.CaptureWorldPreview `
  -logFile "E:\mygame\preview.log"
```

会在项目根目录生成 `preview_world.png`（640×360，含 HUD）。
用来检查像素比例、精灵排序、Tilemap 是否画得出来，不用开编辑器。

编辑器菜单里也有：**修仙 Demo → 渲染世界预览图** / **生成主场景并配置工程**。

> 注意：跑批处理前必须先把 Unity 编辑器关掉，否则会报「另一个实例已打开此项目」。

---

## 七、像素游戏常见坑

| 症状 | 原因 | 解法 |
|---|---|---|
| 图糊了 | Filter Mode 是 Bilinear | 已自动设 Point；图放 `Resources/Art/` 下 |
| 有噪点脏边 | 贴图被压缩 | 已自动设 Uncompressed |
| 跑动时抖动 | 没开像素吸附 | 已开 `pixelSnapping` |
| 精灵尺寸对不齐 | PPU 不统一 | 全部 32，别混用 |
| 中文成了方块 | 用了内置 Arial | 已装中文字体，别删 `Resources/Fonts` |
| 文字发灰模糊 | 字体开了抗锯齿 | 已自动设 Hinted Raster |
| 地图整片不显示 | TilemapRenderer 没材质 | `WorldRoot` 里已手动给 `Sprites/Default` |
| 角色被墙卡住 | 碰撞盒太大 | 调 `PlayerWalker.HalfWidth / HalfHeight` |

---

## 八、下一步该往上加什么

按性价比排序，一次只做一件：

1. **走进去触发事件**：现在「游历」是随机抽。改成地图上放几个「地点」（坐标 + 事件池），
   走进某个格子就触发对应事件——这是从「能走」到「有内容」最关键的一步。
2. **NPC 和对话**：`npcs.json` 放坐标和名字，走过去弹对话。
3. **换正式美术**：Aseprite 画 32×32 地形 + 32×48 角色，覆盖同名文件即可。
4. **战斗**：极简回合制替掉现在「拔剑迎战 → 直接扣血」。
5. **多地图**：`map.json` 加个 `exits` 字段，走到边界切下一张图。

**别做的事**：不要上 ECS/DOTS，不要引入第三方框架，不要做联机。这个规模用最土的 MonoBehaviour 最省命。

---

## 附：美术资源说明

`Assets/Resources/Art/` 里现在是两类图：

**地形（脚本生成的占位，32×32）**

- `tile_grass` / `tile_rock` / `tile_water` / `tile_path`

**主角（从你的四向图切出来的，48×64）**

- `hero_front` / `hero_right` / `hero_back` / `hero_left`

切图流程记录一下，以后换素材可以照做：

1. 源图必须是**真 RGBA**（有 alpha 通道）。如果拿到的是白底 24 位 RGB，就得先
   「从图片边界 flood fill 白色区域」判定背景——不能按亮度阈值抠，
   否则白 T 恤和肤色会被一起挖空。
2. 用**预乘 alpha 的面积平均**下采样，反预乘输出，边缘不会混进白色变成白边。
3. 四张统一放到 48×64 画布，水平居中、底部对齐——四块源图的高度要尽量一致
   （现在这组误差 6px），否则切出来人物大小不一，转向时会忽大忽小。

> 注意地形还是占位的粗糙色块，跟精细的主角立绘放在一起会有画风落差。
> 正式地形建议用 Aseprite 重画：32×32 一格、配色跟主角的黑白灰调子统一。

正式美术覆盖同名文件即可，导入设置（Point 过滤 / 无压缩 / PPU 32）会自动套用。
