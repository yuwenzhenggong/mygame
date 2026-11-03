using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
        cam.orthographicSize = 5f;
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void SetupPlayer()
    {
        PlayerSettings.companyName = "MyGame";
        PlayerSettings.productName = "修仙 Demo";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
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

        Debug.Log("[SelfCheck] 完成，失败项 = " + failures);
        return failures;
    }
}
