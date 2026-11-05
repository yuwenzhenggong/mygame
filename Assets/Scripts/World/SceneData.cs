using System.Collections.Generic;
using UnityEngine;
using XianXia.Data;

namespace XianXia.World
{
    // 一个「整幅画」场景：背景是单张图，碰撞来自一张字符遮罩。
    //
    // 坐标系约定：
    //   世界坐标 1 单位 = PixelsPerUnit 像素，原点在图片左下角
    //   遮罩第 0 行 = 图片顶部，所以 world y 越大对应遮罩行号越小
    public class SceneData
    {
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

        public static SceneData Load()
        {
            var cfg = DataLoader.Load<SceneConfig>("Data/scene");
            if (cfg == null)
            {
                Debug.LogError("[Scene] 读不到 Data/scene.json");
                return null;
            }

            var maskAsset = Resources.Load<TextAsset>(cfg.mask);
            if (maskAsset == null)
            {
                Debug.LogError("[Scene] 读不到遮罩文本: " + cfg.mask);
                return null;
            }

            var scene = new SceneData
            {
                ImageName = cfg.image,
                ImageWidth = cfg.imageWidth,
                ImageHeight = cfg.imageHeight,
                CellSize = cfg.cellSize > 0 ? cfg.cellSize : 16
            };

            var lines = new List<string>();
            foreach (var raw in maskAsset.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith("//")) continue;      // 注释
                if (line.Trim().Length == 0) continue;
                lines.Add(line);
            }

            if (lines.Count == 0)
            {
                Debug.LogError("[Scene] 遮罩是空的");
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
                    Debug.LogError("[Scene] 遮罩第 " + gy + " 行长度 " + line.Length + "，应为 " + scene._cols);
                    return null;
                }
                for (int gx = 0; gx < scene._cols; gx++)
                    scene._solid[gy * scene._cols + gx] = line[gx] == '#';
            }

            // 出生点：图片像素 → 世界坐标（y 轴要翻过来）
            scene.Spawn = new Vector2(
                cfg.spawnImageX / (float)PixelArtSettings.PixelsPerUnit,
                (cfg.imageHeight - cfg.spawnImageY) / (float)PixelArtSettings.PixelsPerUnit);

            return scene;
        }

        public bool IsSolidCell(int gx, int gy)
        {
            if (gx < 0 || gy < 0 || gx >= _cols || gy >= _rows) return true;   // 图外当墙
            return _solid[gy * _cols + gx];
        }

        public bool IsSolidAt(Vector2 worldPos)
        {
            float px = worldPos.x * PixelArtSettings.PixelsPerUnit;
            float py = ImageHeight - worldPos.y * PixelArtSettings.PixelsPerUnit;
            return IsSolidCell(Mathf.FloorToInt(px / CellSize), Mathf.FloorToInt(py / CellSize));
        }

        // 世界坐标 → 遮罩格（供调试/自检用）
        public Vector2Int WorldToCell(Vector2 worldPos)
        {
            float px = worldPos.x * PixelArtSettings.PixelsPerUnit;
            float py = ImageHeight - worldPos.y * PixelArtSettings.PixelsPerUnit;
            return new Vector2Int(Mathf.FloorToInt(px / CellSize), Mathf.FloorToInt(py / CellSize));
        }
    }
}
