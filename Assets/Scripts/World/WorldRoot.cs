using UnityEngine;

namespace XianXia.World
{
    // 世界根节点：铺背景图、放玩家、挂相机跟随。
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

            PlayerTransform = playerGo.transform;
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
