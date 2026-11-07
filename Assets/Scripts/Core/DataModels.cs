using System;
using System.Collections.Generic;

namespace XianXia.Data
{
    // 配置表 DTO —— 字段名必须和 JSON 里的键完全一致。
    // JsonUtility 不支持 Dictionary，也不支持顶层数组，所以每个表都包一层。

    // 异能
    [Serializable]
    public class AbilityTable
    {
        public List<AbilityDef> abilities = new List<AbilityDef>();
    }

    [Serializable]
    public class AbilityDef
    {
        public string id;
        public string name;
        public string desc;
        public int maxHpBonus;

        // 战斗加成（只在炼化出来的异能上生效）
        public int damageBonus;            // 普攻额外伤害
        public float damageReduction;      // 受到伤害减免（0~1）
        public float cooldownReduction;    // 普攻冷却缩短（0~1）

        public bool refinable;             // true = 只能从炼化台获得
    }

    // 任务
    [Serializable]
    public class QuestTable
    {
        public List<QuestDef> quests = new List<QuestDef>();
    }

    [Serializable]
    public class QuestDef
    {
        public string id;
        public string name;
        public string desc;
        public string targetEnemy;   // 要杀的敌人 id
        public int targetCount = 1;
        public int rewardExp;
    }

    // 境界
    [Serializable]
    public class RealmTable
    {
        public List<RealmDef> realms = new List<RealmDef>();
    }

    [Serializable]
    public class RealmDef
    {
        public string id;
        public string name;
        public string desc;
        public int maxHpBonus;
        public int damageBonus;
    }

    // 场景里可以互动的东西（售货机、长椅、地铁口……）
    [Serializable]
    public class InteractionTable
    {
        public List<InteractionDef> interactables = new List<InteractionDef>();
    }

    [Serializable]
    public class InteractionDef
    {
        public string id;
        public string scene = "overworld";   // 属于哪个场景
        public string name;
        public string action = "event";       // event / travel（M4 再加 refine/rest/storage/quest/breakthrough）
        public int imageX;             // 场景图上的像素坐标
        public int imageY;
        public int radius = 72;        // 触发半径（像素）
        public string eventId;         // action=event 时播的事件
        public bool onceOnly;          // 只能互动一次
        public string requireAbility;  // 需要某个异能才能互动，留空表示无条件

        // action=travel 专用
        public string targetScene;
        public int targetImageX;
        public int targetImageY;
    }

    // 事件
    [Serializable]
    public class EventTable
    {
        public List<EventDef> events = new List<EventDef>();
    }

    [Serializable]
    public class EventDef
    {
        public string id;
        public string title;
        public string text;
        public List<EventOption> options = new List<EventOption>();
    }

    [Serializable]
    public class EventOption
    {
        public string text;
        public string resultText;
        public List<Effect> effects = new List<Effect>();
    }

    // 一条效果 = 一次数据改动。代码只认识这几种 type，加内容不用改代码。
    [Serializable]
    public class Effect
    {
        public string type;   // exp / hp / maxhp / ability / flag / goto
        public string key;    // ability: 异能 id；flag: 标记名；goto: 事件 id
        public int value;
    }
}
