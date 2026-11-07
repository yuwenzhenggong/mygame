using System;
using System.Collections.Generic;

namespace XianXia
{
    [Serializable]
    public class FlagEntry
    {
        public string key;
        public int value;
    }

    // 按敌人种类累计的击杀数。任务接取时用它回填进度，
    // 否则"先打完再接任务"会因为敌人不重刷而永远做不完。
    [Serializable]
    public class KillCount
    {
        public string enemyId;
        public int count;
    }

    // 玩家存档的全部数据。只有这里的东西会被写进存档文件，所以想存什么就加到这儿。
    [Serializable]
    public class GameState
    {
        public string playerName = "无名";

        public int level = 1;
        public int exp;
        public int hp = 100;
        public int maxHp = 100;

        public List<FlagEntry> flags = new List<FlagEntry>();
        public List<string> abilities = new List<string>();          // 已获得的异能 id
        public List<string> usedInteractions = new List<string>();   // 已用过的一次性互动

        // 资源
        public int resonanceCount;          // 异能残响
        public int breakthroughMaterials;   // 突破素材

        // 累计经验（等级会重置 exp，突破条件用这个才稳定）
        public int totalExp;

        // 任务
        public string questState = "未接取";   // 未接取 / 进行中 / 已完成
        public int questKills;

        // 炼化次数（突破条件之一）
        public int refineCount;

        // 境界下标（0=觉醒者，1=凝核者）
        public int realmIndex;

        // 已击杀的刷怪点 id（死亡后不再重复刷新，除非重开）
        public List<string> deadEnemies = new List<string>();

        // 按敌人种类累计的击杀数（不依赖任务状态）
        public List<KillCount> killCounts = new List<KillCount>();

        // 位置与场景（跨场景后要能回到原地）
        public string sceneId = "safehouse";
        public float playerX;
        public float playerY;

        public int GetFlag(string key)
        {
            if (flags == null) return 0;
            for (int i = 0; i < flags.Count; i++)
                if (flags[i] != null && flags[i].key == key) return flags[i].value;
            return 0;
        }

        public bool HasFlag(string key) => GetFlag(key) != 0;

        public void SetFlag(string key, int value)
        {
            if (flags == null) flags = new List<FlagEntry>();
            for (int i = 0; i < flags.Count; i++)
            {
                if (flags[i] != null && flags[i].key == key)
                {
                    flags[i].value = value;
                    return;
                }
            }
            flags.Add(new FlagEntry { key = key, value = value });
        }
    }
}
