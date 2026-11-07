using System.Collections.Generic;
using UnityEngine;
using XianXia.Data;

namespace XianXia.World
{
    // 世界根节点：铺背景图、放玩家、摆互动标记、挂相机跟随。
    // 支持换场景：LoadScene 会销毁旧世界再重建。
    public class WorldRoot : MonoBehaviour
    {
        public static WorldRoot Instance { get; private set; }

        public SceneData Scene { get; private set; }
        public Transform PlayerTransform { get; private set; }

        ContentLibrary _content;
        GameObject _container;
        bool _started;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (_started) return;
            _started = true;

            _content = ContentLibrary.Load();

            // 有存档就从存档的场景/位置起步，否则进默认场景
            var state = SaveSystem.Load();
            if (state != null && !string.IsNullOrEmpty(state.sceneId))
            {
                var saved = new Vector2(state.playerX, state.playerY);
                // 存档位置如果是 (0,0) 说明是旧档，退回场景默认出生点
                if (saved.sqrMagnitude < 0.0001f) saved = new Vector2(-1f, -1f);
                LoadScene(state.sceneId, saved.x < 0f ? (Vector2?)null : saved);
            }
            else
            {
                LoadScene(SceneData.DefaultSceneId(), null);
            }
        }

        // worldPos 为 null 时用场景自带出生点
        public void LoadScene(string sceneId, Vector2? worldPos)
        {
            var data = SceneData.Load(sceneId);
            if (data == null)
            {
                Debug.LogError("[World] 场景加载失败: " + sceneId);
                return;
            }

            Scene = data;

            if (_content == null) _content = ContentLibrary.Load();

            // 换场景前把地上没捡的掉落物收进背包。
            // 掉落物挂在 container 上会被一起销毁，不这么兜底的话
            // "打完忘了捡就永远没了"，而敌人又不会重刷，会直接卡死进度。
            CollectLooseDrops();

            if (_container != null) Destroy(_container);
            _container = new GameObject("Scene_" + data.Id);
            _container.transform.SetParent(transform, false);

            BuildBackdrop();
            BuildPlayer(worldPos ?? data.Spawn);
            BuildEnemies();
            BuildInteractables();
            SetupCamera();

            Debug.Log("[World] 进入场景 " + data.Id + "（" + data.ImageWidth + "x" + data.ImageHeight
                      + "，遮罩 " + data.Cols + "x" + data.Rows + "）");
        }

        // 按目标场景的图片像素坐标传送
        public void TravelTo(string sceneId, int imageX, int imageY)
        {
            var target = SceneData.Load(sceneId);
            if (target == null) return;
            LoadScene(sceneId, target.ImageToWorld(imageX, imageY));

            // 回到安全屋当作一个检查点：自动存一次，免得打了一路白打
            if (sceneId != SafehouseSceneId) return;

            var ui = UI.GameUI.Instance;
            if (ui == null || ui.Session == null) return;

            ui.CapturePosition();
            if (SaveSystem.Save(ui.Session.State))
                ui.AppendLog("<color=#6ec1ff>回到安全屋，已自动存档。</color>");
        }

        const string SafehouseSceneId = "safehouse";

        // 把当前场景里还没被捡走的掉落物直接结算进背包
        void CollectLooseDrops()
        {
            if (_container == null) return;

            var ui = UI.GameUI.Instance;
            if (ui == null || ui.Session == null) return;

            foreach (var drop in _container.GetComponentsInChildren<Drop>(true))
            {
                if (drop == null) continue;
                var msg = ui.Session.CollectDrop(drop.Kind == Drop.DropKind.Resonance, drop.Amount);
                if (!string.IsNullOrEmpty(msg)) ui.AppendLog(msg);
            }
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
            go.transform.SetParent(_container.transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -10;

            // 精灵 pivot 在中心，放到 (宽/2, 高/2) 让图片左下角落在世界原点
            var size = sprite.bounds.size;
            go.transform.position = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
        }

        void BuildPlayer(Vector2 spawn)
        {
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(_container.transform, false);
            playerGo.transform.position = new Vector3(spawn.x, spawn.y, 0f);

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
            // 跑步动画帧数不固定：从 0 开始连号加载，遇到缺失就停
            var runFrames = new List<Sprite>();
            for (int i = 0; i < 64; i++)
            {
                var sprite = Resources.Load<Sprite>(PixelArtSettings.ArtResourceRoot + "hero_run_down_" + i);
                if (sprite == null) break;
                runFrames.Add(sprite);
            }
            walker.SetWalkDown(runFrames.ToArray());
            if (runFrames.Count == 0) Debug.LogWarning("[World] 没找到跑步帧 hero_run_down_*");

            playerGo.AddComponent<PlayerCombat>();

            PlayerTransform = playerGo.transform;
        }

        // 敌人按 enemies.json 的 spawns 摆，只摆当前场景的
        void BuildEnemies()
        {
            var table = DataLoader.Load<EnemyTable>("Data/enemies");
            if (table == null || table.spawns == null) return;

            var pxWhite = LoadSprite("px_white");
            int count = 0;

            foreach (var spawn in table.spawns)
            {
                if (spawn == null || spawn.scene != Scene.Id) continue;

                // 已经打死的刷怪点不再刷出来
                var session = UI.GameUI.Instance != null ? UI.GameUI.Instance.Session : null;
                if (session != null && session.State.deadEnemies != null &&
                    session.State.deadEnemies.Contains(spawn.id)) continue;

                EnemyDef def = null;
                foreach (var d in table.enemies)
                    if (d != null && d.id == spawn.enemy) { def = d; break; }

                if (def == null)
                {
                    Debug.LogError("[World] 敌人配置不存在: " + spawn.enemy + "（刷新点 " + spawn.id + "）");
                    continue;
                }

                var sprite = LoadSprite(def.sprite);
                var world = Scene.ImageToWorld(spawn.imageX, spawn.imageY);

                var go = new GameObject("Enemy_" + spawn.id);
                go.transform.SetParent(_container.transform, false);
                go.transform.position = new Vector3(world.x, world.y, 0f);

                var enemy = go.AddComponent<Enemy>();
                enemy.SpawnId = spawn.id;
                enemy.Init(def, Scene, PlayerTransform, sprite, pxWhite);
                enemy.OnDeath = OnEnemyDeath;
                count++;
            }

            Debug.Log("[World] 本场景敌人 " + count + " 个");
        }

        void OnEnemyDeath(Enemy e)
        {
            if (e == null || e.Def == null) return;

            var pos = e.transform.position;
            var ui = UI.GameUI.Instance;
            var session = ui != null ? ui.Session : null;

            if (session != null)
            {
                // 记下这个刷怪点已清，重进场景不会复活
                if (!string.IsNullOrEmpty(e.SpawnId) && session.State.deadEnemies != null &&
                    !session.State.deadEnemies.Contains(e.SpawnId))
                    session.State.deadEnemies.Add(e.SpawnId);

                ui.AppendLog("<color=#9ef0a0>击败 " + e.Def.name + "</color>");

                var expMsg = session.GainExp(e.Def.exp);
                if (!string.IsNullOrEmpty(expMsg)) ui.AppendLog(expMsg);

                // 任务进度（只统计任务指定的敌人）
                var questMsg = session.ReportKill(e.Def.id);
                if (!string.IsNullOrEmpty(questMsg)) ui.AppendLog(questMsg);

                ui.RefreshHud();
            }

            SpawnDrops(pos, false, e.Def.resonance);
            SpawnDrops(pos, true, e.Def.materials);
        }

        // 掉落物稍微散开一点，避免几个东西叠在同一个像素上
        void SpawnDrops(Vector3 pos, bool material, int count)
        {
            if (count <= 0) return;

            var sprite = LoadSprite(material ? "drop_material" : "drop_resonance");
            if (sprite == null) return;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(material ? "Drop_Material" : "Drop_Resonance");
                go.transform.SetParent(_container.transform, false);

                float ox = Random.Range(-0.4f, 0.4f);
                float oy = Random.Range(-0.3f, 0.3f);
                go.transform.position = pos + new Vector3(ox, oy, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = 8;

                var drop = go.AddComponent<Drop>();
                drop.Init(material ? Drop.DropKind.Material : Drop.DropKind.Resonance,
                          1, PlayerTransform, Random.Range(0f, 6.28f));
            }
        }

        void BuildInteractables()
        {
            var markerSprite = LoadSprite("marker");
            var entries = new List<Interactor.Entry>();

            foreach (var def in _content.Interactions)
            {
                if (def == null || string.IsNullOrEmpty(def.id)) continue;
                // 只摆当前场景的互动点
                if (def.scene != Scene.Id) continue;

                var world = Scene.ImageToWorld(def.imageX, def.imageY);

                var go = new GameObject("Marker_" + def.id);
                go.transform.SetParent(_container.transform, false);
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

            Debug.Log("[World] 本场景互动点 " + entries.Count + " 个");
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
