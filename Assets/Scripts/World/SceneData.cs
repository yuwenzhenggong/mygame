using System.Collections.Generic;
using UnityEngine;
using XianXia.Data;

namespace XianXia.World
{
    // 一个场景 = 一张整幅背景图 + 一张字符遮罩 + 一个出生点。
    // 所有场景登记在 Data/scenes.json 里，按 id 加载。
    //
    // 坐标系约定：
    //   世界坐标 1 单位 = PixelsPerUnit 像素，原点在图片左下角
    //   遮罩第 0 行 = 图片顶部，所以 world y 越大对应遮罩行号越小
    public class SceneData
    {
        public string Id;
        public string ImageName;
        public int ImageWidth;
        public int ImageHeight;
        public int CellSize = 16;
        public Vector2 Spawn;

        bool[] _solid;
        int _cols;
        int _rows;

        public int Cols => _cols;
        public int Rows => _rows;

        public float WorldWidth => ImageWidth / (float)PixelArtSettings.PixelsPerUnit;
        public float WorldHeight => ImageHeight / (float)PixelArtSettings.PixelsPerUnit;

        // ---------------- 注册表 ----------------

        public static SceneRegistry LoadRegistry()
        {
            var reg = DataLoader.Load<SceneRegistry>("Data/scenes");
            if (reg == null || reg.scenes == null || reg.scenes.Count == 0)
            {
                Debug.LogError("[Scene] 读不到 Data/scenes.json 或场景列表为空");
                return null;
            }
            return reg;
        }

        public static string DefaultSceneId()
        {
            var reg = LoadRegistry();
            if (reg == null) return "safehouse";
            return string.IsNullOrEmpty(reg.defaultScene) ? reg.scenes[0].id : reg.defaultScene;
        }

        public static SceneData Load(string sceneId)
        {
            var reg = LoadRegistry();
            if (reg == null) return null;

            SceneEntry entry = null;
            foreach (var e in reg.scenes)
            {
                if (e != null && e.id == sceneId) { entry = e; break; }
            }

            if (entry == null)
            {
                Debug.LogError("[Scene] 场景不存在: " + sceneId);
                return null;
            }

            var maskAsset = Resources.Load<TextAsset>(entry.mask);
            if (maskAsset == null)
            {
                Debug.LogError("[Scene] 读不到遮罩文本: " + entry.mask);
                return null;
            }

            var scene = new SceneData
            {
                Id = entry.id,
                ImageName = entry.image,
                ImageWidth = entry.imageWidth,
                ImageHeight = entry.imageHeight,
                CellSize = entry.cellSize > 0 ? entry.cellSize : 16
            };

            var lines = new List<string>();
            foreach (var raw in maskAsset.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith("//")) continue;
                if (line.Trim().Length == 0) continue;
                lines.Add(line);
            }

            if (lines.Count == 0)
            {
                Debug.LogError("[Scene] 遮罩是空的: " + entry.mask);
                return null;
            }

            scene._rows = lines.Count;
            scene._cols = lines[0].Length;
            scene._solid = new bool[scene._cols * scene._rows];

            for (int gy = 0; gy < scene._rows; gy++)
            {
                var line = lines[gy];
                if (line.Length != scene._cols)
                {
                    Debug.LogError("[Scene] " + entry.mask + " 第 " + gy + " 行长度 " + line.Length + "，应为 " + scene._cols);
                    return null;
                }
                for (int gx = 0; gx < scene._cols; gx++)
                    scene._solid[gy * scene._cols + gx] = line[gx] == '#';
            }

            scene.Spawn = scene.ImageToWorld(entry.spawnImageX, entry.spawnImageY);
            return scene;
        }

        // ---------------- 坐标转换 ----------------

        public Vector2 ImageToWorld(int px, int py)
        {
            return new Vector2(
                px / (float)PixelArtSettings.PixelsPerUnit,
                (ImageHeight - py) / (float)PixelArtSettings.PixelsPerUnit);
        }

        public Vector2 WorldToImage(Vector2 world)
        {
            return new Vector2(
                world.x * PixelArtSettings.PixelsPerUnit,
                ImageHeight - world.y * PixelArtSettings.PixelsPerUnit);
        }

        // ---------------- 碰撞 ----------------

        public bool IsSolidCell(int gx, int gy)
        {
            if (gx < 0 || gy < 0 || gx >= _cols || gy >= _rows) return true;   // 图外当墙
            return _solid[gy * _cols + gx];
        }

        public bool IsSolidAt(Vector2 worldPos)
        {
            var img = WorldToImage(worldPos);
            return IsSolidCell(Mathf.FloorToInt(img.x / CellSize), Mathf.FloorToInt(img.y / CellSize));
        }

        public Vector2Int WorldToCell(Vector2 worldPos)
        {
            var img = WorldToImage(worldPos);
            return new Vector2Int(Mathf.FloorToInt(img.x / CellSize), Mathf.FloorToInt(img.y / CellSize));
        }
    }
}
