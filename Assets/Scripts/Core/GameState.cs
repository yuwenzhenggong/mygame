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

    // 玩家存档的全部数据。只有这里的东西会被写进存档文件，
    // 所以想存什么，就加到这儿。
    [Serializable]
    public class GameState
    {
        public string playerName = "无名散修";
        public int realmIndex;
        public int cultivation;
        public int hp = 100;
        public int spiritStones = 10;
        public int turn;

        public List<FlagEntry> flags = new List<FlagEntry>();
        public List<string> techniques = new List<string>();
        public List<string> finishedEvents = new List<string>();

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
