using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace XianXia.World
{
    // 世界根节点：建地图、放玩家、挂相机跟随。
    // 全部运行时用代码搭，不依赖场景里的 prefab 或 Tile Palette 资源。
    public class WorldRoot : MonoBehaviour
    {
        public MapGrid Grid { get; private set; }
        public Transform PlayerTransform { get; private set; }

        bool _built;

        void Start()
        {
            Build();
        }

        public void Build()
        {
            if (_built) return;
            _built = true;

            Grid = MapGrid.Load();
            if (Grid == null)
            {
                Debug.LogError("[World] 地图加载失败，世界不会出现");
                return;
            }

            BuildTilemap();
            BuildPlayer();
            SetupCamera();

            Debug.Log("[World] 地图 " + Grid.Width + "x" + Grid.Height + "，出生点 " + Grid.Spawn);
        }

        void BuildTilemap()
        {
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(transform, false);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            var tilemapGo = new GameObject("Tilemap");
            tilemapGo.transform.SetParent(gridGo.transform, false);

            var tilemap = tilemapGo.AddComponent<Tilemap>();
            var tilemapRenderer = tilemapGo.AddComponent<TilemapRenderer>();
            tilemapRenderer.sortingOrder = 0;

            // 运行时 AddComponent 的 TilemapRenderer 没有默认材质，必须手动给，否则什么都不画
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader) { name = "PixelTilemap" };
                tilemapRenderer.material = mat;
            }
            else
            {
                Debug.LogError("[World] 找不到 Sprites/Default 着色器");
            }

            // 同一种精灵只建一个 Tile 实例
            var tileCache = new Dictionary<string, TileBase>();

            for (int y = 0; y < Grid.Height; y++)
            {
                for (int x = 0; x < Grid.Width; x++)
                {
                    var spriteName = Grid.SpriteName[x, y];
                    if (string.IsNullOrEmpty(spriteName)) continue;

                    TileBase tile;
                    if (!tileCache.TryGetValue(spriteName, out tile))
                    {
                        var sprite = Resources.Load<Sprite>(PixelArtSettings.ArtResourceRoot + spriteName);
                        if (sprite == null)
                        {
                            Debug.LogError("[World] 找不到精灵: " + spriteName);
                            continue;
                        }

                        var t = ScriptableObject.CreateInstance<Tile>();
                        t.sprite = sprite;
                        t.colliderType = Tile.ColliderType.None;   // 碰撞自己算，不用 Tilemap 的
                        tile = t;
                        tileCache[spriteName] = tile;
                    }

                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }

            tilemap.CompressBounds();
        }

        void BuildPlayer()
        {
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);

            var spawn = MapGrid.CellCenter(Grid.Spawn.x, Grid.Spawn.y);
            playerGo.transform.position = new Vector3(spawn.x, spawn.y, 0f);

            // 精灵 32x48，pivot 在中心 → 身体往上抬 0.75，让脚底正好落在玩家坐标上
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(playerGo.transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, 0.75f, 0f);

            var sr = bodyGo.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>(PixelArtSettings.ArtResourceRoot + "char_hero");
            sr.sortingOrder = 10;
            if (sr.sprite == null) Debug.LogError("[World] 找不到角色精灵 char_hero");

            var walker = playerGo.AddComponent<PlayerWalker>();
            walker.Init(Grid);

            PlayerTransform = playerGo.transform;
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[World] 场景里没有 MainCamera，临时创建一个");
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.055f, 0.065f, 0.085f, 1f);
                cam.transform.position = new Vector3(0f, 0f, -10f);
            }

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.Init(PlayerTransform, Grid);
        }
    }
}
