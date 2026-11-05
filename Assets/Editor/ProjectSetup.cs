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
                  " 个 / 事件 " + content.Events.Count + " 个");

        if (content.Abilities.Count == 0) { Debug.LogError("[SelfCheck] 异能表为空"); failures++; }
        if (content.Interactions.Count == 0) { Debug.LogError("[SelfCheck] 互动点为空"); failures++; }

        // 1) 互动点引用的东西必须存在
        foreach (var def in content.Interactions)
        {
            if (def == null) continue;

            if (content.GetEvent(def.eventId) == null)
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
            else
            {
                Debug.Log("[SelfCheck] 存档往返 OK");
                XianXia.SaveSystem.Delete();
            }
        }

        // 8) 场景
        var scene = XianXia.World.SceneData.Load();
        if (scene == null)
        {
            Debug.LogError("[SelfCheck] 场景加载失败");
            failures++;
        }
        else
        {
            Debug.Log("[SelfCheck] 场景 " + scene.ImageWidth + "x" + scene.ImageHeight
                      + "，遮罩 " + scene.Cols + "x" + scene.Rows + " 格（每格 " + scene.CellSize + "px）"
                      + "，世界 " + scene.WorldWidth.ToString("0.##") + " x " + scene.WorldHeight.ToString("0.##"));

            if (Resources.Load<Sprite>(scene.ImageName) == null)
            {
                Debug.LogError("[SelfCheck] 找不到场景背景图: " + scene.ImageName);
                failures++;
            }

            var cell = scene.WorldToCell(scene.Spawn);
            if (scene.IsSolidCell(cell.x, cell.y))
            {
                Debug.LogError("[SelfCheck] 出生点落在实心格上: " + cell);
                failures++;
            }

            bool hasExit = !scene.IsSolidCell(cell.x + 1, cell.y) || !scene.IsSolidCell(cell.x - 1, cell.y) ||
                           !scene.IsSolidCell(cell.x, cell.y + 1) || !scene.IsSolidCell(cell.x, cell.y - 1);
            if (!hasExit)
            {
                Debug.LogError("[SelfCheck] 出生点四周全是墙");
                failures++;
            }
        }

        // 9) 像素精灵
        string[] spriteNames = { "city_street", "hero_front", "hero_right", "hero_back", "hero_left", "marker" };
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
    // 命令行：-executeMethod ProjectSetup.CaptureWorldPreview
    [MenuItem("都市异能 Demo/渲染世界预览图")]
    public static void CaptureWorldPreview()
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
        world.Build();

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

        var outPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "preview_world.png");
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
