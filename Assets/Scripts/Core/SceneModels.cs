using System;
using System.Collections.Generic;

namespace XianXia.Data
{
    // 场景注册表：所有场景的配置都在 scenes.json 里
    [Serializable]
    public class SceneRegistry
    {
        public string defaultScene = "safehouse";
        public List<SceneEntry> scenes = new List<SceneEntry>();
    }

    [Serializable]
    public class SceneEntry
    {
        public string id;
        public string image;        // Resources 下的精灵名
        public string mask;         // Resources 下的遮罩文本
        public int imageWidth;
        public int imageHeight;
        public int cellSize = 16;
        public int spawnImageX;     // 默认出生点（场景图像素坐标）
        public int spawnImageY;
    }
}
