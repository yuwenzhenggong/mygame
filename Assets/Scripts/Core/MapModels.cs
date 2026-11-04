using System;
using System.Collections.Generic;

namespace XianXia.Data
{
    // 地图配置：一张字符网格 + 字符到精灵的对照表
    [Serializable]
    public class MapTable
    {
        public List<string> rows = new List<string>();
        public List<MapLegendEntry> legend = new List<MapLegendEntry>();
    }

    [Serializable]
    public class MapLegendEntry
    {
        public string ch;      // 地图里的一个字符
        public string sprite;  // Resources/Art 下的精灵名
        public bool solid;     // 是否阻挡
    }
}
