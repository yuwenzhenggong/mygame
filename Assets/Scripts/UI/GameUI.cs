using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XianXia.Data;

namespace XianXia.UI
{
    // HUD 与交互界面。挂在 GameRoot 上，由 Bootstrap 自动创建。
    // 布局按 640x360 逻辑像素算，窗口放大 2 倍 = 1280x720。
    // 注意：这里不再画背景——背景就是世界本身（地图），HUD 只做叠加。
    public class GameUI : MonoBehaviour
    {
        const int MaxLogLines = 5;

        ContentLibrary _content;
        GameSession _session;
        readonly List<string> _log = new List<string>();

        Text _statusText;
        Text _logText;

        RectTransform _eventPanel;
        Text _eventTitle;
        Text _eventBody;
        RectTransform _eventOptions;

        bool _initialized;

        void Awake()
        {
            Initialize();
        }

        // 单独抽出来，好让编辑器预览/测试能在非运行状态下建界面
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            BuildUI();
            _content = ContentLibrary.Load();

            if (_content == null || _content.Realms.Count == 0)
            {
                AppendLog("<color=#ff6b6b>配置表加载失败：检查 Assets/Resources/Data/ 下的 json 文件。</color>");
                Refresh();
                return;
            }

            NewGame();
        }

        // ---------------- 搭界面 ----------------

        void BuildUI()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(PixelArtSettings.ReferenceWidth, PixelArtSettings.ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            // 和精灵的 Pixels Per Unit 对齐，32px 的图在 UI 里就是 32 逻辑像素
            scaler.referencePixelsPerUnit = PixelArtSettings.PixelsPerUnit;

            // 顶部状态栏
            var statusPanel = UIFactory.NewImage("StatusPanel", canvasGo.transform, new Color(0.05f, 0.06f, 0.08f, 0.82f));
            statusPanel.raycastTarget = false;
            UIFactory.Anchor(statusPanel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -76f), new Vector2(-12f, -12f));

            // 立绘：用主角的正面图（48x64 原尺寸显示，1:1 映射到逻辑像素最清晰）
            var hero = UIFactory.NewSpriteImage("Hero", statusPanel.transform, "hero_front", Color.white);
            hero.raycastTarget = false;
            var heroRt = hero.rectTransform;
            heroRt.anchorMin = new Vector2(0f, 0.5f);
            heroRt.anchorMax = new Vector2(0f, 0.5f);
            heroRt.pivot = new Vector2(0f, 0.5f);
            heroRt.anchoredPosition = new Vector2(2f, 0f);
            heroRt.sizeDelta = new Vector2(48f, 64f);

            _statusText = UIFactory.NewText("StatusText", statusPanel.transform, "", UIFactory.SizeNormal,
                new Color(0.86f, 0.89f, 0.95f), TextAnchor.MiddleLeft);
            UIFactory.Stretch(_statusText.rectTransform, 56f, 4f, 150f, 4f);

            var hint = UIFactory.NewText("Hint", statusPanel.transform, "WASD / 方向键 移动", UIFactory.SizeSmall,
                new Color(0.60f, 0.66f, 0.76f), TextAnchor.MiddleRight);
            UIFactory.Anchor(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-142f, 0f), new Vector2(-8f, 0f));

            // 左下日志
            var logPanel = UIFactory.NewImage("LogPanel", canvasGo.transform, new Color(0.05f, 0.06f, 0.08f, 0.74f));
            logPanel.raycastTarget = false;
            UIFactory.Anchor(logPanel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(12f, 68f), new Vector2(332f, 148f));

            _logText = UIFactory.NewText("LogText", logPanel.transform, "", UIFactory.SizeNormal,
                new Color(0.82f, 0.85f, 0.90f), TextAnchor.LowerLeft);
            UIFactory.Stretch(_logText.rectTransform, 8f, 6f, 8f, 6f);

            // 底部按钮栏
            var bar = UIFactory.NewRect("ButtonBar", canvasGo.transform);
            UIFactory.Anchor(bar, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(12f, 12f), new Vector2(-12f, 60f));

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            AddBarButton(bar, "修炼", OnCultivate);
            AddBarButton(bar, "突破", OnBreakthrough);
            AddBarButton(bar, "游历", OnTravel);
            AddBarButton(bar, "存档", OnSave);
            AddBarButton(bar, "读档", OnLoad);
            AddBarButton(bar, "重开", OnRestart);

            BuildEventPanel(canvasGo.transform);
        }

        void AddBarButton(RectTransform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var btn = UIFactory.NewButton(label, parent, label, UIFactory.SizeNormal, action);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 48f;
        }

        void BuildEventPanel(Transform canvasRoot)
        {
            var overlay = UIFactory.NewImage("EventOverlay", canvasRoot, new Color(0f, 0f, 0f, 0.72f));
            UIFactory.Stretch(overlay.rectTransform, 0f, 0f, 0f, 0f);
            _eventPanel = overlay.rectTransform;

            var inner = UIFactory.NewImage("EventBox", overlay.transform, new Color(0.09f, 0.11f, 0.14f, 0.99f));
            var rt = inner.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(560f, 300f);
            rt.anchoredPosition = Vector2.zero;

            _eventTitle = UIFactory.NewText("Title", inner.transform, "", UIFactory.SizeTitle,
                new Color(0.95f, 0.90f, 0.72f), TextAnchor.MiddleLeft);
            UIFactory.Anchor(_eventTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -46f), new Vector2(-20f, -12f));

            _eventBody = UIFactory.NewText("Body", inner.transform, "", UIFactory.SizeNormal,
                new Color(0.85f, 0.88f, 0.92f), TextAnchor.UpperLeft);
            UIFactory.Anchor(_eventBody.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -170f), new Vector2(-20f, -52f));

            var options = UIFactory.NewRect("Options", inner.transform);
            UIFactory.Anchor(options, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 16f), new Vector2(-20f, 164f));
            _eventOptions = options;

            var vlg = options.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            _eventPanel.gameObject.SetActive(false);
        }

        // ---------------- 行动 ----------------

        void NewGame()
        {
            _log.Clear();
            _session = new GameSession(_content, new GameState());
            _session.LearnTechnique("t_basic");

            AppendLog("你生于青云山下的小村，自幼能感灵气。");
            AppendLog("WASD 走动，「修炼」积修为，「游历」遇事。");
            Refresh();
        }

        // 气血归零后不再允许行动，必须重开
        bool BlockIfDead()
        {
            if (_session == null || !_session.IsDead) return false;
            AppendLog("<color=#ff6b6b>你气血耗尽，魂归天地。点「重开」再入轮回。</color>");
            Refresh();
            return true;
        }

        void OnCultivate()
        {
            if (BlockIfDead()) return;
            AppendLog(_session.Cultivate());
            Refresh();
        }

        void OnBreakthrough()
        {
            if (BlockIfDead()) return;
            var r = _session.TryBreakthrough();
            AppendLog(r.ok ? "<color=#7ee787>" + r.text + "</color>" : "<color=#ffa657>" + r.text + "</color>");
            Refresh();
        }

        void OnTravel()
        {
            if (BlockIfDead()) return;

            var ev = _session.PickEvent();
            if (ev == null)
            {
                AppendLog("四下无事，你走了半日，一无所获。");
                Refresh();
                return;
            }
            ShowEvent(ev);
        }

        void OnSave()
        {
            if (_session == null) return;
            AppendLog(SaveSystem.Save(_session.State) ? "<color=#6ec1ff>已存档。</color>" : "<color=#ff6b6b>存档失败，看 Console。</color>");
            Refresh();
        }

        void OnLoad()
        {
            if (_session == null) return;

            var state = SaveSystem.Load();
            if (state == null)
            {
                AppendLog("<color=#ffa657>没有找到存档。</color>");
                Refresh();
                return;
            }

            _session = new GameSession(_content, state);
            AppendLog("<color=#6ec1ff>已读档。</color>");
            Refresh();
        }

        void OnRestart()
        {
            NewGame();
        }

        // ---------------- 事件面板 ----------------

        void ShowEvent(EventDef ev)
        {
            if (_eventPanel == null) return;

            _eventPanel.gameObject.SetActive(true);
            _eventTitle.text = ev.title;
            _eventBody.text = ev.text;

            // 先隐藏再销毁：Destroy 要等到帧末才生效，不隐藏的话旧按钮会闪一下、还能被误点
            for (int i = _eventOptions.childCount - 1; i >= 0; i--)
            {
                var child = _eventOptions.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            var opts = ev.options ?? new List<EventOption>();
            foreach (var opt in opts)
            {
                var captured = opt;
                var btn = UIFactory.NewButton("Option", _eventOptions, captured.text, UIFactory.SizeNormal,
                    () => OnChoose(ev, captured));
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 34f;
                le.preferredHeight = 34f;
            }

            if (opts.Count == 0)
            {
                var btn = UIFactory.NewButton("Option", _eventOptions, "离开", UIFactory.SizeNormal, CloseEvent);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 34f;
                le.preferredHeight = 34f;
            }

            Refresh();
        }

        void OnChoose(EventDef ev, EventOption opt)
        {
            var result = _session.ResolveOption(ev, opt);
            if (!string.IsNullOrEmpty(result.text)) AppendLog(result.text);
            if (_session.IsDead) AppendLog("<color=#ff6b6b>你气血耗尽，魂归天地。点「重开」再入轮回。</color>");
            Refresh();

            if (!string.IsNullOrEmpty(result.nextEventId))
            {
                var next = _content.GetEvent(result.nextEventId);
                if (next != null)
                {
                    ShowEvent(next);
                    return;
                }
            }
            CloseEvent();
        }

        void CloseEvent()
        {
            if (_eventPanel != null) _eventPanel.gameObject.SetActive(false);
            Refresh();
        }

        // ---------------- 杂项 ----------------

        void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _log.Add(line);
            while (_log.Count > MaxLogLines) _log.RemoveAt(0);
        }

        void Refresh()
        {
            if (_statusText != null && _session != null) _statusText.text = _session.StatusText();
            if (_logText != null) _logText.text = string.Join("\n", _log.ToArray());

            // 事件面板开着、或者人已经死了 → 冻结走动
            bool modalOpen = _eventPanel != null && _eventPanel.gameObject.activeSelf;
            GameFlow.IsBlocked = modalOpen || (_session != null && _session.IsDead);
        }
    }
}
