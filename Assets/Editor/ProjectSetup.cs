using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;
using XianXia;

// 编辑期工具：生成主场景、配置工程、自我检查、渲染预览图。
// 命令行用法：
//   Unity.exe -batchmode -quit -projectPath E:\mygame -executeMethod ProjectSetup.RunAll -logFile -
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("都市异能 Demo/生成主场景并配置工程")]
    public static void RunFromMenu()
    {
        SetupScene();
        SetupPlayer();
        ReimportArt();
        ReimportFonts();
        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] 场景与工程设置已生成。");
    }

    public static void RunAll()
    {
        int failures = 0;

        try
        {
            SetupScene();
            SetupPlayer();
            ReimportArt();
            ReimportFonts();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] 场景与工程设置 OK");
        }
        catch (Exception e)
        {
            Debug.LogError("[Setup] 失败: " + e);
            failures++;
        }

        try
        {
            failures += SelfCheck();
        }
        catch (Exception e)
        {
            Debug.LogError("[SelfCheck] 异常: " + e);
            failures++;
        }

        Debug.Log(failures == 0 ? "[RunAll] 全部通过" : "[RunAll] " + failures + " 项失败");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static void SetupScene()
    {
        Directory.CreateDirectory("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.055f, 0.065f, 0.085f, 1f);
        cam.orthographic = true;
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        // 像素相机：自动算整数倍缩放 + 像素吸附，避免跑动时抖动和半像素缝隙
        var ppc = camGo.AddComponent<PixelPerfectCamera>();
        ppc.assetsPPU = PixelArtSettings.PixelsPerUnit;
        ppc.refResolutionX = PixelArtSettings.ReferenceWidth;
        ppc.refResolutionY = PixelArtSettings.ReferenceHeight;
        ppc.upscaleRT = false;   // UI 是 Overlay Canvas，开 RT 反而会让 UI 不跟着缩放
        ppc.pixelSnapping = true;
        ppc.cropFrameX = false;
        ppc.cropFrameY = false;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void SetupPlayer()
    {
        PlayerSettings.companyName = "MyGame";
        PlayerSettings.productName = "都市异能 Demo";

        // 窗口分辨率必须是基准分辨率的整数倍，否则像素会被插值糊掉
        PlayerSettings.defaultScreenWidth = PixelArtSettings.ReferenceWidth * 2;
        PlayerSettings.defaultScreenHeight = PixelArtSettings.ReferenceHeight * 2;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        try
        {
            int original = QualitySettings.GetQualityLevel();
            int count = QualitySettings.names.Length;
            for (int i = 0; i < count; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.antiAliasing = 0;
            }
            QualitySettings.SetQualityLevel(original, false);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Setup] 关闭抗锯齿失败（不影响运行）: " + e.Message);
        }
    }

    static void ReimportArt()
    {
        var folder = PixelArtSettings.ArtRoot.TrimEnd('/');
        if (!Directory.Exists(folder)) return;

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        foreach (var guid in guids)
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

        Debug.Log("[Setup] 已重导美术资源 " + guids.Length + " 张");
    }

    static void ReimportFonts()
    {
        const string folder = "Assets/Resources/Fonts";
        if (!Directory.Exists(folder)) return;

        var guids = AssetDatabase.FindAssets("t:Font", new[] { folder });
        foreach (var guid in guids)
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

        Debug.Log("[Setup] 已重导字体 " + guids.Length + " 个");
    }

    // 不依赖 UI 的逻辑自检
    static int SelfCheck()
    {
        int failures = 0;

        var content = XianXia.ContentLibrary.Load();
        if (content == null || content.Events.Count == 0)
        {
            Debug.LogError("[SelfCheck] 事件表为空或读取失败");
            return 1;
        }

        Debug.Log("[SelfCheck] 异能 " + content.Abilities.Count + " 个 / 互动点 " + content.Interactions.Count +
                  " 个 / 事件 " + content.Events.Count + " 个 / 任务 " + content.Quests.Count +
                  " 个 / 境界 " + content.Realms.Count + " 个");

        if (content.Abilities.Count == 0) { Debug.LogError("[SelfCheck] 异能表为空"); failures++; }
        if (content.Interactions.Count == 0) { Debug.LogError("[SelfCheck] 互动点为空"); failures++; }

        // 境界表：至少要有一档，且第一档不能有额外加成
        if (content.Realms.Count == 0)
        {
            Debug.LogError("[SelfCheck] 境界表为空，突破会没有目标");
            failures++;
        }
        else
        {
            var r0 = content.Realms[0];
            if (r0.maxHpBonus != 0 || r0.damageBonus != 0)
                Debug.LogWarning("[SelfCheck] 第一档境界带了加成，会让初始数值偏高: " + r0.name);

            for (int i = 1; i < content.Realms.Count; i++)
            {
                var prev = content.Realms[i - 1];
                var cur = content.Realms[i];
                if (cur.maxHpBonus < prev.maxHpBonus || cur.damageBonus < prev.damageBonus)
                {
                    Debug.LogError("[SelfCheck] 境界「" + cur.name + "」的加成低于前一档「" + prev.name + "」，突破会变弱");
                    failures++;
                }
            }
        }

        // 任务表：至少要有一条，具体的目标敌人校验放在敌人表加载之后
        if (content.Quests.Count == 0)
        {
            Debug.LogError("[SelfCheck] 任务表为空，情报板会没有委托");
            failures++;
        }

        // 1) 互动点引用的东西必须存在
        var sceneIds = new System.Collections.Generic.List<string>();
        var reg0 = XianXia.World.SceneData.LoadRegistry();
        if (reg0 != null)
            foreach (var e in reg0.scenes)
                if (e != null && !string.IsNullOrEmpty(e.id)) sceneIds.Add(e.id);

        foreach (var def in content.Interactions)
        {
            if (def == null) continue;

            if (!string.IsNullOrEmpty(def.scene) && !sceneIds.Contains(def.scene))
            {
                Debug.LogError("[SelfCheck] 互动点 " + def.id + " 属于不存在的场景: " + def.scene);
                failures++;
            }

            if (def.action == "travel")
            {
                if (string.IsNullOrEmpty(def.targetScene) || !sceneIds.Contains(def.targetScene))
                {
                    Debug.LogError("[SelfCheck] 互动点 " + def.id + " 传送目标不存在: " + def.targetScene);
                    failures++;
                }
            }
            else if (def.action == "quest")
            {
                if (content.MainQuest == null)
                {
                    Debug.LogError("[SelfCheck] 互动点 " + def.id + " 是任务板，但 quests.json 里没有任务");
                    failures++;
                }
            }
            else if (def.action == "refine" || def.action == "rest" ||
                     def.action == "storage" || def.action == "breakthrough")
            {
                // 据点功能点，不需要 eventId
            }
            else if (content.GetEvent(def.eventId) == null)
            {
                Debug.LogError("[SelfCheck] 互动点 " + def.id + " 引用了不存在的事件: " + def.eventId);
                failures++;
            }

            if (!string.IsNullOrEmpty(def.requireAbility) && content.GetAbility(def.requireAbility) == null)
            {
                Debug.LogError("[SelfCheck] 互动点 " + def.id + " 需要不存在的异能: " + def.requireAbility);
                failures++;
            }
        }

        // 2) 事件效果引用的异能 / 事件必须存在
        foreach (var ev in content.Events)
        {
            if (ev == null || ev.options == null) continue;

            foreach (var opt in ev.options)
            {
                if (opt == null || opt.effects == null) continue;

                foreach (var ef in opt.effects)
                {
                    if (ef == null) continue;
                    if (ef.type == "ability" && content.GetAbility(ef.key) == null)
                    {
                        Debug.LogError("[SelfCheck] 事件 " + ev.id + " 引用了不存在的异能: " + ef.key);
                        failures++;
                    }
                    if (ef.type == "goto" && content.GetEvent(ef.key) == null)
                    {
                        Debug.LogError("[SelfCheck] 事件 " + ev.id + " 引用了不存在的事件: " + ef.key);
                        failures++;
                    }
                }
            }

            if (ev.options != null && ev.options.Count > 0)
            {
                var probe = new XianXia.GameSession(content, new XianXia.GameState());
                probe.ResolveOption(ev, ev.options[0]);
            }
        }

        // 3) 经验与升级
        var session = new XianXia.GameSession(content, new XianXia.GameState());
        int levelBefore = session.Level;
        session.GainExp(session.ExpToNext + 5);
        if (session.Level != levelBefore + 1)
        {
            Debug.LogError("[SelfCheck] 经验够了却没有升级: " + levelBefore + " -> " + session.Level);
            failures++;
        }
        else
        {
            Debug.Log("[SelfCheck] 升级 OK -> Lv." + session.Level + "（生命上限 " + session.MaxHp + "）");
        }

        // 4) 学异能 & 生命上限加成
        int maxHpBefore = session.MaxHp;
        session.LearnAbility("a_force");
        if (!session.HasAbility("a_force"))
        {
            Debug.LogError("[SelfCheck] 学习异能失败");
            failures++;
        }
        else if (session.MaxHp <= maxHpBefore)
        {
            Debug.LogError("[SelfCheck] 念力没有提升生命上限: " + maxHpBefore + " -> " + session.MaxHp);
            failures++;
        }

        // 5) 一次性互动：用过之后应该不可用
        var onceDef = content.GetInteraction("i_sign");
        if (onceDef != null)
        {
            string reason;
            if (!session.IsInteractionAvailable(onceDef, out reason))
            {
                Debug.LogError("[SelfCheck] 一次性互动初始就不可用: " + reason);
                failures++;
            }
            session.MarkInteractionUsed(onceDef);
            if (session.IsInteractionAvailable(onceDef, out reason))
            {
                Debug.LogError("[SelfCheck] 一次性互动用过后仍然可用");
                failures++;
            }
        }

        // 6) 条件互动：地铁口需要灵视
        var subway = content.GetInteraction("i_subway");
        if (subway != null)
        {
            string reason;
            var fresh = new XianXia.GameSession(content, new XianXia.GameState());
            if (fresh.IsInteractionAvailable(subway, out reason))
            {
                Debug.LogError("[SelfCheck] 没异能时地铁口不该可用");
                failures++;
            }
            fresh.LearnAbility("a_sight");
            if (!fresh.IsInteractionAvailable(subway, out reason))
            {
                Debug.LogError("[SelfCheck] 有灵视后地铁口仍不可用: " + reason);
                failures++;
            }
        }

        // 7) 存档往返
        var toSave = new XianXia.GameState { playerName = "测试者", level = 3, exp = 42, hp = 55, maxHp = 120 };
        toSave.SetFlag("foo", 1);
        toSave.abilities.Add("a_force");
        toSave.usedInteractions.Add("i_sign");

        // 新增的进度字段也要能存住，漏一个就会导致"重进游戏进度倒退"
        toSave.realmIndex = 1;
        toSave.totalExp = 137;
        toSave.resonanceCount = 4;
        toSave.breakthroughMaterials = 3;
        toSave.refineCount = 2;
        toSave.questState = "已完成";
        toSave.questKills = 2;
        toSave.sceneId = "jungle";
        toSave.playerX = 15.25f;
        toSave.playerY = 8.125f;
        toSave.deadEnemies.Add("s_attacker_1");
        toSave.killCounts.Add(new XianXia.KillCount { enemyId = "attacker", count = 2 });

        if (!XianXia.SaveSystem.Save(toSave))
        {
            Debug.LogError("[SelfCheck] 存档失败");
            failures++;
        }
        else
        {
            var loaded = XianXia.SaveSystem.Load();
            if (loaded == null)
            {
                Debug.LogError("[SelfCheck] 读档失败");
                failures++;
            }
            else if (loaded.level != 3 || loaded.exp != 42 || loaded.hp != 55 || loaded.maxHp != 120 ||
                     !loaded.HasFlag("foo") || loaded.abilities.Count != 1 || loaded.usedInteractions.Count != 1)
            {
                Debug.LogError("[SelfCheck] 存档往返数据不一致: " + JsonUtility.ToJson(loaded));
                failures++;
            }
            else if (loaded.realmIndex != 1 || loaded.totalExp != 137 ||
                     loaded.resonanceCount != 4 || loaded.breakthroughMaterials != 3 ||
                     loaded.refineCount != 2 || loaded.questState != "已完成" || loaded.questKills != 2 ||
                     loaded.sceneId != "jungle" || loaded.deadEnemies.Count != 1 ||
                     loaded.killCounts.Count != 1 || loaded.killCounts[0].count != 2)
            {
                Debug.LogError("[SelfCheck] 进度字段存档往返不一致: " + JsonUtility.ToJson(loaded));
                failures++;
            }
            else
            {
                Debug.Log("[SelfCheck] 存档往返 OK");
                XianXia.SaveSystem.Delete();
            }
        }

        // 8) 场景（多场景：注册表里每个场景都要能加载，出生点要可走）
        var registry = XianXia.World.SceneData.LoadRegistry();
        if (registry == null)
        {
            Debug.LogError("[SelfCheck] 场景注册表读取失败");
            failures++;
        }
        else
        {
            bool defaultOk = false;
            foreach (var e in registry.scenes)
                if (e != null && e.id == registry.defaultScene) defaultOk = true;

            if (!defaultOk)
            {
                Debug.LogError("[SelfCheck] defaultScene 不在场景列表里: " + registry.defaultScene);
                failures++;
            }

            foreach (var entry in registry.scenes)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;

                var scene = XianXia.World.SceneData.Load(entry.id);
                if (scene == null)
                {
                    Debug.LogError("[SelfCheck] 场景加载失败: " + entry.id);
                    failures++;
                    continue;
                }

                Debug.Log("[SelfCheck] 场景 " + scene.Id + " " + scene.ImageWidth + "x" + scene.ImageHeight
                          + "，遮罩 " + scene.Cols + "x" + scene.Rows
                          + "，世界 " + scene.WorldWidth.ToString("0.##") + " x " + scene.WorldHeight.ToString("0.##"));

                if (Resources.Load<Sprite>(scene.ImageName) == null)
                {
                    Debug.LogError("[SelfCheck] " + scene.Id + " 找不到背景图: " + scene.ImageName);
                    failures++;
                }

                var cell = scene.WorldToCell(scene.Spawn);
                if (scene.IsSolidCell(cell.x, cell.y))
                {
                    Debug.LogError("[SelfCheck] " + scene.Id + " 的出生点落在实心格上: " + cell);
                    failures++;
                }

                bool hasExit = !scene.IsSolidCell(cell.x + 1, cell.y) || !scene.IsSolidCell(cell.x - 1, cell.y) ||
                               !scene.IsSolidCell(cell.x, cell.y + 1) || !scene.IsSolidCell(cell.x, cell.y - 1);
                if (!hasExit)
                {
                    Debug.LogError("[SelfCheck] " + scene.Id + " 的出生点四周全是墙");
                    failures++;
                }
            }
        }

        // 8.5) 敌人配置：刷怪点要指向存在的敌人，且不能刷在实心格里
        var enemyTable = XianXia.Data.DataLoader.Load<XianXia.Data.EnemyTable>("Data/enemies");
        if (enemyTable == null)
        {
            Debug.LogError("[SelfCheck] enemies.json 读取失败");
            failures++;
        }
        else
        {
            Debug.Log("[SelfCheck] 敌人种类 " + enemyTable.enemies.Count + "，刷新点 " + enemyTable.spawns.Count);

            // 任务的目标敌人必须真的存在，否则委托永远做不完
            foreach (var qd in content.Quests)
            {
                if (qd == null || string.IsNullOrEmpty(qd.targetEnemy)) continue;

                bool found = false;
                foreach (var d in enemyTable.enemies)
                    if (d != null && d.id == qd.targetEnemy) { found = true; break; }

                if (!found)
                {
                    Debug.LogError("[SelfCheck] 任务「" + qd.name + "」的目标敌人不存在: " + qd.targetEnemy);
                    failures++;
                }
            }

            foreach (var spawn in enemyTable.spawns)
            {
                if (spawn == null) continue;

                bool defOk = false;
                foreach (var d in enemyTable.enemies)
                    if (d != null && d.id == spawn.enemy) { defOk = true; break; }

                if (!defOk)
                {
                    Debug.LogError("[SelfCheck] 刷怪点 " + spawn.id + " 引用了不存在的敌人: " + spawn.enemy);
                    failures++;
                }

                var sc = XianXia.World.SceneData.Load(spawn.scene);
                if (sc == null)
                {
                    Debug.LogError("[SelfCheck] 刷怪点 " + spawn.id + " 的场景不存在: " + spawn.scene);
                    failures++;
                    continue;
                }

                var scCell = sc.WorldToCell(sc.ImageToWorld(spawn.imageX, spawn.imageY));
                if (sc.IsSolidCell(scCell.x, scCell.y))
                {
                    Debug.LogError("[SelfCheck] 刷怪点 " + spawn.id + " 落在实心格上: " + scCell);
                    failures++;
                }
            }
        }

        // 9) 像素精灵
        string[] spriteNames = { "city_street", "safehouse", "jungle_arena", "hero_front", "hero_right", "hero_back", "hero_left", "marker",
                                 "hero_run_down_0", "hero_run_down_1", "hero_run_down_2", "hero_run_down_3", "hero_run_down_4",
                                 "hero_run_down_5", "hero_run_down_6", "hero_run_down_7", "hero_run_down_8", "hero_run_down_9",
                                 "hero_combo_0", "hero_combo_1", "hero_combo_2",
                                 "hero_combo_3", "hero_combo_4", "hero_combo_5",
                                 "enemy_attacker", "enemy_beast", "enemy_elite", "px_white",
                                 "drop_resonance", "drop_material",
                                 "num_0", "num_1", "num_2", "num_3", "num_4",
                                 "num_5", "num_6", "num_7", "num_8", "num_9",
                                 "ui_portrait_hero", "ui_portrait_mask", "ui_statusbar",
                                 "ui_skill_a_force", "ui_skill_a_heal", "ui_skill_a_burn" };
        int spriteOk = 0;

        foreach (var spriteName in spriteNames)
        {
            var sprite = Resources.Load<Sprite>(PixelArtSettings.ArtResourceRoot + spriteName);
            if (sprite == null)
            {
                Debug.LogError("[SelfCheck] 找不到精灵: " + PixelArtSettings.ArtResourceRoot + spriteName);
                failures++;
                continue;
            }

            if (Mathf.Abs(sprite.pixelsPerUnit - PixelArtSettings.PixelsPerUnit) > 0.01f)
            {
                Debug.LogError("[SelfCheck] " + spriteName + " 的 PPU = " + sprite.pixelsPerUnit +
                               "，应为 " + PixelArtSettings.PixelsPerUnit);
                failures++;
            }

            if (sprite.texture.filterMode != FilterMode.Point)
            {
                Debug.LogError("[SelfCheck] " + spriteName + " 的 FilterMode = " + sprite.texture.filterMode + "，应为 Point");
                failures++;
            }

            spriteOk++;
        }

        Debug.Log("[SelfCheck] 像素精灵 " + spriteOk + "/" + spriteNames.Length + " 张通过");

        // 10) 字体
        var fonts = Resources.LoadAll<Font>("Fonts");
        if (fonts == null || fonts.Length == 0)
            Debug.LogWarning("[SelfCheck] Resources/Fonts 下没有字体，会退回系统字体");
        else
            Debug.Log("[SelfCheck] 像素字体已就绪: " + fonts[0].name);

        Debug.Log("[SelfCheck] 完成，失败项 = " + failures);
        return failures;
    }

    // 把世界渲染成一张 640x360 的 PNG，用来检查像素比例、精灵排序、背景图是否画得出来。
    // 命令行：-executeMethod ProjectSetup.CaptureWorldPreview   （默认场景）
    //         -executeMethod ProjectSetup.CaptureOverworldPreview（街道）
    [MenuItem("都市异能 Demo/渲染预览图（默认场景）")]
    public static void CaptureWorldPreview()
    {
        CaptureScene(XianXia.World.SceneData.DefaultSceneId(), "preview_world.png");
    }

    public static void CaptureOverworldPreview()
    {
        CaptureScene("overworld", "preview_overworld.png");
    }

    public static void CaptureJunglePreview()
    {
        CaptureScene("jungle", "preview_jungle.png");
    }

    static void CaptureScene(string sceneId, string fileName)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[Preview] 场景里没有 MainCamera");
            EditorApplication.Exit(1);
            return;
        }

        // 先建 HUD：WorldRoot 需要在 GameUI 之后建互动系统
        var uiGo = new GameObject("__PreviewUI");
        var gameUI = uiGo.AddComponent<XianXia.UI.GameUI>();
        gameUI.Initialize();

        var rootGo = new GameObject("__PreviewWorld");
        var world = rootGo.AddComponent<XianXia.World.WorldRoot>();
        world.LoadScene(sceneId, null);

        if (world.Scene == null)
        {
            Debug.LogError("[Preview] 世界构建失败");
            EditorApplication.Exit(1);
            return;
        }

        // ScreenSpaceOverlay 的 Canvas 相机拍不到，临时改成 ScreenSpaceCamera 才能一起进截图
        var canvas = uiGo.GetComponentInChildren<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
        }
        else
        {
            Debug.LogWarning("[Preview] 没找到 Canvas，截图里不会有 HUD");
        }

        const int w = PixelArtSettings.ReferenceWidth;
        const int h = PixelArtSettings.ReferenceHeight;

        cam.orthographic = true;
        cam.orthographicSize = h / 2f / PixelArtSettings.PixelsPerUnit;
        cam.aspect = (float)w / h;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;

        float halfW = cam.orthographicSize * cam.aspect;
        float cx = Mathf.Clamp(world.Scene.Spawn.x, halfW, world.Scene.WorldWidth - halfW);
        float cy = Mathf.Clamp(world.Scene.Spawn.y, cam.orthographicSize, world.Scene.WorldHeight - cam.orthographicSize);
        cam.transform.position = new Vector3(cx, cy, -10f);

        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;

        var outPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), fileName);
        File.WriteAllBytes(outPath, tex.EncodeToPNG());

        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(uiGo);
        UnityEngine.Object.DestroyImmediate(rootGo);

        Debug.Log("[Preview] 已保存: " + outPath);
        EditorApplication.Exit(0);
    }
}
