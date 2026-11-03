using System;
using System.Collections.Generic;

namespace XianXia.Data
{
    // 配置表 DTO —— 字段名必须和 JSON 里的键完全一致。
    // JsonUtility 不支持 Dictionary，也不支持顶层数组，所以每个表都包一层。

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
        public int qiRequired;   // 突破到下一境界所需修为；最高境界填 0
        public int maxHp;
        public int baseAttack;
        public string desc;
    }

    [Serializable]
    public class TechniqueTable
    {
        public List<TechniqueDef> techniques = new List<TechniqueDef>();
    }

    [Serializable]
    public class TechniqueDef
    {
        public string id;
        public string name;
        public string desc;
        public int attackBonus;
        public int maxHpBonus;
        public int cultivateBonus;
    }

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
        public int weight = 10;   // 随机权重，越大越容易出现
        public bool onceOnly;     // true 表示一生只触发一次
        public EventRequire require;
        public List<EventOption> options = new List<EventOption>();
    }

    [Serializable]
    public class EventRequire
    {
        public int minRealm;
        public int maxRealm;      // 0 或未填视为无上限
        public int minSpiritStones;
        public string hasFlag;
        public string notFlag;
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
        public string type;   // stat / flag / learn / goto
        public string key;    // stat: hp|cultivation|spiritStones；flag: 标记名；learn: 功法 id；goto: 事件 id
        public int value;
    }
}
