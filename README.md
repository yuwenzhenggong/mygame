# 修仙 Demo —— 一个能长大的最小骨架

用 **Unity 2021.3.19f1c1** + C# 搭的「鬼谷八荒 / 太吾绘卷」类型游戏的地基。

目标不是做个完整游戏，而是**先把数据驱动这条管线跑通**，之后你只写 JSON，不写代码。

---

## 一、怎么打开

1. 打开 **Unity Hub**（`E:\Unity\Unity Hub\Unity Hub.exe`）
2. 点「打开」→ 选择 `E:\mygame` 目录
3. 编辑器打开后，点顶部的 ▶ **Play**

不需要往场景里拖任何东西。`Bootstrap.cs` 会自动把界面建出来。

> 如果 Unity Hub 提示版本不匹配，选 `2021.3.19f1c1`。

---

## 二、跑起来是什么样

顶部一行状态：姓名 / 境界 / 天数 / 修为 / 气血 / 灵石 / 攻击。

底部六个按钮：

| 按钮 | 作用 |
|---|---|
| 修炼 | 天数和修为增加（功法会加成修炼速度） |
| 突破 | 修为够了就进阶，气血回满 |
| 游历 | 按权重抽一个**满足条件**的事件，弹出选项 |
| 存档 / 读档 | 存到 `%USERPROFILE%\AppData\LocalLow\MyGame\修仙 Demo\save_01.json` |
| 重开 | 新的一世 |

---

## 三、目录结构

```
Assets/
├─ Scripts/
│  ├─ Bootstrap.cs              自动入口（Play 就跑起来）
│  ├─ Core/
│  │  ├─ DataModels.cs          配置表的 C# 结构（字段名 = JSON 键名）
│  │  ├─ GameState.cs           存档里的全部数据
│  │  ├─ DataLoader.cs          读 Resources 下的 JSON
│  │  ├─ SaveSystem.cs          存档 / 读档 / 删档
│  │  └─ ContentLibrary.cs      把三张表读进内存，按 id 查询
│  ├─ Gameplay/
│  │  └─ GameSession.cs         全部玩法逻辑（不含 UI，可命令行测试）
│  └─ UI/
│     ├─ UIFactory.cs           纯代码搭 UGUI 的工具函数
│     └─ GameUI.cs              界面与交互
├─ Editor/
│  └─ ProjectSetup.cs           生成场景 + 工程配置 + 自检
└─ Resources/Data/
   ├─ realms.json               境界表
   ├─ techniques.json           功法表
   └─ events.json               事件表
```

---

## 四、加内容：只改 JSON，不碰代码

### 加一个境界

编辑 `realms.json`，往数组里推一项：

```json
{ "id": "lianxu", "name": "炼虚期", "qiRequired": 5000, "maxHp": 5000, "baseAttack": 700, "desc": "……" }
```

`qiRequired` 是**突破到下一境界**需要的修为，最高境界填 `0`。

### 加一门功法

编辑 `techniques.json`：

```json
{ "id": "t_fire", "name": "离火诀", "desc": "……", "attackBonus": 25, "maxHpBonus": 80, "cultivateBonus": 2 }
```

### 加一个事件

编辑 `events.json`：

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

`require` 字段留空表示无条件；`minRealm` 是境界索引（0 = 炼气期）。

> 改完 JSON **不用编译**，回编辑器直接 Play 就生效。

---

## 五、命令行自检

改完代码后，可以不开界面直接验证逻辑：

```powershell
& "E:\Unity\2021.3.19f1c1\Editor\Unity.exe" -batchmode -quit `
  -projectPath "E:\mygame" `
  -executeMethod ProjectSetup.RunAll `
  -logFile "E:\mygame\unity-run.log"
```

退出码 `0` = 全部通过。它会检查：
- 三张配置表能否解析
- 修炼是否真的加修为、突破是否真的进阶
- 每个事件引用的功法 / 事件 id 是否存在
- 存档写入后读回，数据是否一致

---

## 六、下一步该往上加什么

按性价比排序，一次只做一件：

1. **地图**：现在「游历」是空抽。改成 3~5 个地点，每个地点有自己的事件池。
2. **战斗**：加一个极简回合制（我方攻/防，敌方 HP），取代现在的「拔剑迎战 → 扣血」。
3. **NPC**：一个 `npcs.json` + 好感度数值，事件里能增减。
4. **Excel 工作流**：数据量上百条后，把 JSON 换成 Excel + 导出脚本（或上 Luban）。
5. **美术**：立绘 + 背景图，用 `Image` 替换掉纯色块。

**别做的事**：不要上 ECS/DOTS，不要引入第三方框架，不要做联机。这个规模用最土的 MonoBehaviour 写法最省命。
