using UnityEngine;

namespace XianXia.World
{
    // 世界根节点：铺背景图、放玩家、摆互动标记、挂相机跟随。
    // 全部运行时用代码搭，不依赖场景里的 prefab。
    public class WorldRoot : MonoBehaviour
    {
        public SceneData Scene { get; private set; }
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

            Scene = SceneData.Load();
            if (Scene == null)
            {
                Debug.LogError("[World] 场景加载失败，世界不会出现");
                return;
            }

            BuildBackdrop();
            BuildPlayer();
            BuildInteractables();
            SetupCamera();

            Debug.Log("[World] 场景 " + Scene.ImageWidth + "x" + Scene.ImageHeight
                      + "，世界 " + Scene.WorldWidth.ToString("0.##") + " x " + Scene.WorldHeight.ToString("0.##")
                      + " 单位，遮罩 " + Scene.Cols + "x" + Scene.Rows + " 格");
        }

        void BuildBackdrop()
        {
            var sprite = Resources.Load<Sprite>(Scene.ImageName);
            if (sprite == null)
            {
                Debug.LogError("[World] 找不到场景图: " + Scene.ImageName);
                return;
            }

            var go = new GameObject("Backdrop");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -10;   // 永远在所有角色后面

            // 精灵 pivot 在中心，放到 (宽/2, 高/2) 让图片左下角正好落在世界原点
            var size = sprite.bounds.size;
            go.transform.position = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
        }

        void BuildPlayer()
        {
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            playerGo.transform.position = new Vector3(Scene.Spawn.x, Scene.Spawn.y, 0f);

            // 精灵 48x64（PPU 32）→ 世界 1.5 x 2；pivot 在中心，所以身体抬 1.0 让脚底落在玩家坐标上
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(playerGo.transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, 1f, 0f);

            var sr = bodyGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            sr.sprite = LoadSprite("hero_front");

            var walker = playerGo.AddComponent<PlayerWalker>();
            walker.Init(Scene);
            walker.SetSprites(
                LoadSprite("hero_front"),
                LoadSprite("hero_right"),
                LoadSprite("hero_back"),
                LoadSprite("hero_left"));
            walker.SetWalkDown(new[]
            {
                LoadSprite("hero_walk_down_0"),
                LoadSprite("hero_walk_down_1"),
                LoadSprite("hero_walk_down_2"),
                LoadSprite("hero_walk_down_3")
            });

            PlayerTransform = playerGo.transform;
        }

        void BuildInteractables()
        {
            var content = ContentLibrary.Load();
            var markerSprite = LoadSprite("marker");
            var entries = new System.Collections.Generic.List<Interactor.Entry>();

            foreach (var def in content.Interactions)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;

                var world = ImageToWorld(def.imageX, def.imageY);

                var go = new GameObject("Marker_" + def.id);
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(world.x, world.y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = markerSprite;
                // 必须画在角色之上（角色是 10）：标记坐标常常正好落在角色身后，
                // 排在下面会被整个挡住，玩家看不到可互动点。
                sr.sortingOrder = 20;

                entries.Add(new Interactor.Entry
                {
                    Def = def,
                    Marker = sr,
                    WorldPosition = world
                });
            }

            var interactor = PlayerTransform.gameObject.AddComponent<Interactor>();
            interactor.SetEntries(entries);

            Debug.Log("[World] 可互动对象 " + entries.Count + " 个");
        }

        // 场景图像素坐标 → 世界坐标（y 轴要翻转）
        Vector2 ImageToWorld(int px, int py)
        {
            return new Vector2(
                px / (float)PixelArtSettings.PixelsPerUnit,
                (Scene.ImageHeight - py) / (float)PixelArtSettings.PixelsPerUnit);
        }

        Sprite LoadSprite(string name)
        {
            var sprite = Resources.Load<Sprite>(PixelArtSettings.ArtResourceRoot + name);
            if (sprite == null) Debug.LogError("[World] 找不到精灵: " + name);
            return sprite;
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
            follow.Init(PlayerTransform, Scene);
        }
    }
}
