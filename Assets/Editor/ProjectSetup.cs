using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;
using XianXia;

// 编辑期工具：生成主场景、配置工程、自我检查。
// 命令行用法：
//   Unity.exe -batchmode -quit -projectPath E:\mygame -executeMethod ProjectSetup.RunAll -logFile -
public static class ProjectSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("修仙 Demo/生成主场景并配置工程")]
    public static void RunFromMenu()
    {
        SetupScene();
        SetupPlayer();
        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] 场景与工程设置已生成。");
    }

    // 命令行入口：配置 + 自检，任一失败则以非 0 退出码结束
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
        PlayerSettings.productName = "修仙 Demo";

        // 窗口分辨率必须是基准分辨率的整数倍，否则像素会被插值糊掉
        PlayerSettings.defaultScreenWidth = PixelArtSettings.ReferenceWidth * 2;
        PlayerSettings.defaultScreenHeight = PixelArtSettings.ReferenceHeight * 2;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        // 关掉所有质量等级的抗锯齿
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

    // 强制重导美术资源，让 PixelArtImporter 的像素设置立刻生效（不然要等 Unity 自己重导）
    static void ReimportArt()
    {
        var folder = PixelArtSettings.ArtRoot.TrimEnd('/');
        if (!Directory.Exists(folder)) return;

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        foreach (var guid in guids)
        {
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
        Debug.Log("[Setup] 已重导美术资源 " + guids.Length + " 张");
    }

    // 强制重导字体，让 PixelFontImporter 的关抗锯齿设置生效
    static void ReimportFonts()
    {
        const string folder = "Assets/Resources/Fonts";
        if (!Directory.Exists(folder)) return;

        var guids = AssetDatabase.FindAssets("t:Font", new[] { folder });
        foreach (var guid in guids)
        {
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
        Debug.Log("[Setup] 已重导字体 " + guids.Length + " 个");
    }

    // 不依赖 UI 的逻辑自检：配置表能读、修行能涨、突破能成、事件能结算、存档能往返。
    static int SelfCheck()
    {
        int failures = 0;

        var content = XianXia.ContentLibrary.Load();
        if (content == null || content.Realms.Count == 0)
        {
            Debug.LogError("[SelfCheck] 境界表为空或读取失败");
            return 1;
        }

        Debug.Log("[SelfCheck] 境界 " + content.Realms.Count + " 条 / 功法 " + content.Techniques.Count +
                  " 条 / 事件 " + content.Events.Count + " 个");

        if (content.Techniques.Count == 0) { Debug.LogError("[SelfCheck] 功法表为空"); failures++; }
        if (content.Events.Count == 0) { Debug.LogError("[SelfCheck] 事件表为空"); failures++; }

        var session = new XianXia.GameSession(content, new XianXia.GameState());
        if (!session.LearnTechnique("t_basic")) { Debug.LogError("[SelfCheck] 学习初始功法失败"); failures++; }

        int before = session.State.cultivation;
        var gain = session.Cultivate();
        if (session.State.cultivation <= before) { Debug.LogError("[SelfCheck] 修炼没有增加修为: " + gain); failures++; }

        session.State.cultivation = 999999;
        var bt = session.TryBreakthrough();
        if (!bt.ok || session.State.realmIndex != 1)
        {
            Debug.LogError("[SelfCheck] 突破失败: " + bt.text);
            failures++;
        }
        else
        {
            Debug.Log("[SelfCheck] 突破 OK -> " + session.CurrentRealm.name);
        }

        // 事件：所有事件的选项都要能结算，且引用的功法 id 必须存在
        foreach (var ev in content.Events)
        {
            if (ev == null || ev.options == null) continue;

            foreach (var opt in ev.options)
            {
                if (opt == null || opt.effects == null) continue;
                foreach (var ef in opt.effects)
                {
                    if (ef == null) continue;
                    if (ef.type == "learn" && content.GetTechnique(ef.key) == null)
                    {
                        Debug.LogError("[SelfCheck] 事件 " + ev.id + " 引用了不存在的功法: " + ef.key);
                        failures++;
                    }
                    if (ef.type == "goto" && content.GetEvent(ef.key) == null)
                    {
                        Debug.LogError("[SelfCheck] 事件 " + ev.id + " 引用了不存在的事件: " + ef.key);
                        failures++;
                    }
                }
            }

            if (ev.options.Count > 0)
            {
                var probe = new XianXia.GameSession(content, new XianXia.GameState());
                probe.ResolveOption(ev, ev.options[0]);
            }
        }

        // 存档往返
        var toSave = new XianXia.GameState { playerName = "测试者", realmIndex = 2, cultivation = 1234, spiritStones = 77 };
        toSave.SetFlag("foo", 1);
        toSave.techniques.Add("t_sword");

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
            else if (loaded.realmIndex != 2 || loaded.cultivation != 1234 ||
                     loaded.spiritStones != 77 || !loaded.HasFlag("foo") ||
                     loaded.techniques.Count != 1)
            {
                Debug.LogError("[SelfCheck] 存档往返数据不一致: " + JsonUtility.ToJson(loaded));
                failures++;
            }
            else
            {
                Debug.Log("[SelfCheck] 存档往返 OK -> " + XianXia.SaveSystem.SavePath);
                XianXia.SaveSystem.Delete();
            }
        }

        // 事件筛选：不符合 require 的事件不该被抽到
        var fresh = new XianXia.GameSession(content, new XianXia.GameState());
        if (fresh.IsEligible(content.GetEvent("e_inheritance")))
        {
            Debug.LogError("[SelfCheck] 未满足条件的事件被判定为可选");
            failures++;
        }

        // 像素美术管线检查：精灵存在、PPU 一致、过滤方式是 Point
        string[] spriteNames = { "tile_grass", "tile_rock", "tile_water", "tile_path",
                                 "hero_front", "hero_right", "hero_back", "hero_left" };
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

        Debug.Log("[SelfCheck] 像素精灵 " + spriteOk + "/" + spriteNames.Length +
                  " 张通过（PPU=" + PixelArtSettings.PixelsPerUnit +
                  "，基准 " + PixelArtSettings.ReferenceWidth + "x" + PixelArtSettings.ReferenceHeight + "）");

        // 像素字体检查：有就用自己带的，没有就退回系统字体（不判失败，只是提示）
        var fonts = Resources.LoadAll<Font>("Fonts");
        if (fonts == null || fonts.Length == 0)
        {
            Debug.LogWarning("[SelfCheck] Resources/Fonts 下没有字体，会退回系统字体，中文可能不清晰");
        }
        else
        {
            Debug.Log("[SelfCheck] 像素字体已就绪: " + fonts[0].name + "（" + fonts.Length + " 个）");
        }

        // 地图配置检查
        var map = XianXia.World.MapGrid.Load();
        if (map == null)
        {
            Debug.LogError("[SelfCheck] 地图加载失败");
            failures++;
        }
        else
        {
            Debug.Log("[SelfCheck] 地图 " + map.Width + "x" + map.Height + "，出生点 " + map.Spawn);

            if (map.IsSolid(map.Spawn.x, map.Spawn.y))
            {
                Debug.LogError("[SelfCheck] 出生点落在实心格上，玩家会被卡死");
                failures++;
            }

            // 出生点必须至少有一个可走方向，否则一开局就卡住
            bool hasExit = !map.IsSolid(map.Spawn.x + 1, map.Spawn.y) ||
                           !map.IsSolid(map.Spawn.x - 1, map.Spawn.y) ||
                           !map.IsSolid(map.Spawn.x, map.Spawn.y + 1) ||
                           !map.IsSolid(map.Spawn.x, map.Spawn.y - 1);
            if (!hasExit)
            {
                Debug.LogError("[SelfCheck] 出生点四周全是墙");
                failures++;
            }
        }

        Debug.Log("[SelfCheck] 完成，失败项 = " + failures);
        return failures;
    }

    // 把世界渲染成一张 640x360 的 PNG，用来检查像素比例、精灵排序、Tilemap 是否画得出来。
    // 命令行：-executeMethod ProjectSetup.CaptureWorldPreview
    [MenuItem("修仙 Demo/渲染世界预览图")]
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

        var rootGo = new GameObject("__PreviewWorld");
        var world = rootGo.AddComponent<XianXia.World.WorldRoot>();
        world.Build();

        if (world.Grid == null)
        {
            Debug.LogError("[Preview] 世界构建失败");
            EditorApplication.Exit(1);
            return;
        }

        // 顺便把 HUD 也建出来。ScreenSpaceOverlay 的 Canvas 相机拍不到，
        // 所以临时改成 ScreenSpaceCamera 才能一起进截图。
        var uiGo = new GameObject("__PreviewUI");
        var gameUI = uiGo.AddComponent<XianXia.UI.GameUI>();
        gameUI.Initialize();   // 编辑模式下 Awake 不会跑，得手动初始化
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
        float cx = Mathf.Clamp(world.Grid.Spawn.x + 0.5f, halfW, world.Grid.Width - halfW);
        float cy = Mathf.Clamp(world.Grid.Spawn.y + 0.5f, cam.orthographicSize, world.Grid.Height - cam.orthographicSize);
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
