using System;

namespace XianXia.Data
{
    // 场景配置：一张整幅背景图 + 一张碰撞遮罩
    [Serializable]
    public class SceneConfig
    {
        public string image = "Art/city_street";   // Resources 下的精灵名
        public string mask = "Data/scene_mask";    // Resources 下的遮罩文本
        public int imageWidth;
        public int imageHeight;
        public int cellSize = 16;
        public int spawnImageX;
        public int spawnImageY;
    }
}
