using System.Collections.Generic;
using UnityEngine;
using XianXia.Data;

namespace XianXia.World
{
    // 把 map.json 的字符网格解析成可查询的格子数据。
    // 注意：JSON 里第 0 行是地图「顶部」，而世界坐标 y 轴朝上，所以要翻转。
    public class MapGrid
    {
        public int Width;
        public int Height;
        public bool[,] Solid;
        public string[,] SpriteName;
        public Vector2Int Spawn;

        public static MapGrid Load()
        {
            var table = DataLoader.Load<MapTable>("Data/map");
            if (table == null || table.rows == null || table.rows.Count == 0)
            {
                Debug.LogError("[MapGrid] 地图为空或读取失败");
                return null;
            }

            var grid = new MapGrid();
            grid.Height = table.rows.Count;
            grid.Width = table.rows[0].Length;
            grid.Solid = new bool[grid.Width, grid.Height];
            grid.SpriteName = new string[grid.Width, grid.Height];
            grid.Spawn = new Vector2Int(grid.Width / 2, grid.Height / 2);

            var legend = new Dictionary<string, MapLegendEntry>();
            if (table.legend != null)
            {
                foreach (var entry in table.legend)
                    if (entry != null && !string.IsNullOrEmpty(entry.ch)) legend[entry.ch] = entry;
            }

            bool foundSpawn = false;

            for (int row = 0; row < grid.Height; row++)
            {
                var line = table.rows[row];
                if (line.Length != grid.Width)
                {
                    Debug.LogError("[MapGrid] 第 " + row + " 行长度是 " + line.Length + "，应为 " + grid.Width);
                    return null;
                }

                int y = grid.Height - 1 - row;   // 顶部行 → 世界最高 y

                for (int x = 0; x < grid.Width; x++)
                {
                    string ch = line[x].ToString();

                    MapLegendEntry entry;
                    if (!legend.TryGetValue(ch, out entry))
                    {
                        Debug.LogError("[MapGrid] 未知字符 '" + ch + "'（行 " + row + "，列 " + x + "）");
                        continue;
                    }

                    grid.Solid[x, y] = entry.solid;
                    grid.SpriteName[x, y] = entry.sprite;

                    if (ch == "@")
                    {
                        grid.Spawn = new Vector2Int(x, y);
                        foundSpawn = true;
                    }
                }
            }

            if (!foundSpawn) Debug.LogWarning("[MapGrid] 地图里没有 @ 出生点，改用地图中心");

            return grid;
        }

        public bool IsSolid(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return true;   // 图外一律视为墙
            return Solid[x, y];
        }

        public bool IsSolidAt(Vector2 worldPos)
        {
            return IsSolid(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));
        }

        // 格子中心的世界坐标
        public static Vector2 CellCenter(int x, int y) => new Vector2(x + 0.5f, y + 0.5f);
    }
}
