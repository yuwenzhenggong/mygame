using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XianXia.Data;
using XianXia.World;

namespace XianXia.UI
{
    // HUD 与交互界面。挂在 GameRoot 上，由 Bootstrap 自动创建。
    // 布局按 640x360 逻辑像素算，窗口放大 2 倍 = 1280x720。
    public class GameUI : MonoBehaviour
    {
        const int MaxLogLines = 4;

        public static GameUI Instance { get; private set; }

        public ContentLibrary Content { get; private set; }
        public GameSession Session { get; private set; }

        readonly List<string> _log = new List<string>();

        Text _statusText;
        Text _logText;
        Text _promptText;

        RectTransform _hpBar;
        Image _hpFill;
        Text _hpText;
        Text _lootText;
        Text _questText;

        RectTransform _hudRoot;
        bool _hudVisible;

        RectTransform _eventPanel;
        Text _eventTitle;
        Text _eventBody;
        RectTransform _eventOptions;

        bool _initialized;

        void Awake()
        {
            Instance = this;
            Initialize();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 单独抽出来，好让编辑器预览/测试能在非运行状态下建界面
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            Instance = this;
            BuildUI();
            Content = ContentLibrary.Load();

            if (Content == null || Content.Events.Count == 0)
            {
                AppendLog("<color=#ff6b6b>配置表加载失败：检查 Assets/Resources/Data/ 下的 json。</color>");
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
            scaler.referencePixelsPerUnit = PixelArtSettings.PixelsPerUnit;

            // 平时隐藏的 HUD 容器：状态栏、日志、按钮都挂在这下面，ESC 整体开关
            var hud = UIFactory.NewRect("Hud", canvasGo.transform);
            UIFactory.Stretch(hud, 0f, 0f, 0f, 0f);
            _hudRoot = hud;

            // 顶部状态栏
            var statusPanel = UIFactory.NewImage("StatusPanel", hud, new Color(0.05f, 0.06f, 0.08f, 0.82f));
            statusPanel.raycastTarget = false;
            UIFactory.Anchor(statusPanel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -88f), new Vector2(-12f, -12f));

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

            var hint = UIFactory.NewText("Hint", statusPanel.transform, "WASD 移动 · E 互动", UIFactory.SizeSmall,
                new Color(0.60f, 0.66f, 0.76f), TextAnchor.MiddleRight);
            UIFactory.Anchor(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-150f, 0f), new Vector2(-8f, 0f));

            // 左下日志
            var logPanel = UIFactory.NewImage("LogPanel", hud, new Color(0.05f, 0.06f, 0.08f, 0.74f));
            logPanel.raycastTarget = false;
            UIFactory.Anchor(logPanel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(12f, 88f), new Vector2(332f, 168f));

            _logText = UIFactory.NewText("LogText", logPanel.transform, "", UIFactory.SizeNormal,
                new Color(0.82f, 0.85f, 0.90f), TextAnchor.LowerLeft);
            UIFactory.Stretch(_logText.rectTransform, 8f, 6f, 8f, 6f);

            // 互动提示（按钮栏正上方）
            _promptText = UIFactory.NewText("Prompt", canvasGo.transform, "", UIFactory.SizeNormal,
                new Color(0.90f, 0.95f, 1f), TextAnchor.MiddleCenter);
            var promptRt = _promptText.rectTransform;
            promptRt.anchorMin = new Vector2(0.5f, 0f);
            promptRt.anchorMax = new Vector2(0.5f, 0f);
            promptRt.pivot = new Vector2(0.5f, 0f);
            promptRt.sizeDelta = new Vector2(360f, 16f);
            promptRt.anchoredPosition = new Vector2(0f, 64f);

            // 底部按钮栏
            var bar = UIFactory.NewRect("ButtonBar", hud);
            UIFactory.Anchor(bar, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(12f, 12f), new Vector2(-12f, 60f));

            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            AddBarButton(bar, "存档", OnSave);
            AddBarButton(bar, "读档", OnLoad);
            AddBarButton(bar, "重开", OnRestart);

            BuildHpBar(canvasGo.transform);
            BuildEventPanel(canvasGo.transform);

            // 默认收起：一进游戏就是干净的画面，只留互动提示和一条血条
            hud.gameObject.SetActive(false);
            _hudVisible = false;
        }

        // HUD 收起时也常驻的血条——战斗中看不到血量等于没法打
        void BuildHpBar(Transform parent)
        {
            var bg = UIFactory.NewImage("HpBar", parent, new Color(0.05f, 0.05f, 0.07f, 0.82f));
            bg.raycastTarget = false;
            UIFactory.Anchor(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -26f), new Vector2(152f, -12f));
            _hpBar = bg.rectTransform;

            _hpFill = UIFactory.NewImage("Fill", bg.transform, new Color(0.85f, 0.26f, 0.26f, 1f));
            _hpFill.raycastTarget = false;
            var f = _hpFill.rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.offsetMin = Vector2.zero;
            f.offsetMax = Vector2.zero;

            _hpText = UIFactory.NewText("Text", bg.transform, "", UIFactory.SizeSmall,
                new Color(1f, 1f, 1f, 0.95f), TextAnchor.MiddleCenter);
            UIFactory.Stretch(_hpText.rectTransform, 0f, 0f, 0f, 0f);

            // 残响 / 素材计数（HUD 收起时也要看得到，不然打完怪不知道捡了什么）
            _lootText = UIFactory.NewText("Loot", parent, "", UIFactory.SizeSmall,
                new Color(0.85f, 0.9f, 1f, 0.95f), TextAnchor.MiddleLeft);
            UIFactory.Anchor(_lootText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -44f), new Vector2(240f, -28f));

            // 任务追踪
            _questText = UIFactory.NewText("Quest", parent, "", UIFactory.SizeSmall,
                new Color(0.95f, 0.88f, 0.62f, 0.95f), TextAnchor.MiddleLeft);
            UIFactory.Anchor(_questText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(12f, -62f), new Vector2(300f, -46f));
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // 事件弹窗开着的时候，ESC 不去动 HUD，免得把正在读的文本关掉
            if (_eventPanel != null && _eventPanel.gameObject.activeSelf) return;

            ToggleHud();
        }

        public void ToggleHud()
        {
            _hudVisible = !_hudVisible;
            if (_hudRoot != null) _hudRoot.gameObject.SetActive(_hudVisible);
            // HUD 打开时状态栏里已经有血量数字，常驻血条就先藏起来，免得重叠
            if (_hpBar != null) _hpBar.gameObject.SetActive(!_hudVisible);
            if (_lootText != null) _lootText.gameObject.SetActive(!_hudVisible);
            if (_questText != null) _questText.gameObject.SetActive(!_hudVisible);
        }

        // 给战斗脚本调用：数值变了立刻刷新界面
        public void RefreshHud()
        {
            Refresh();
        }

        void AddBarButton(RectTransform parent, string label, UnityEngine.Events.UnityAction action)
        {
            var btn = UIFactory.NewButton(label, parent, label, UIFactory.SizeNormal, action);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 60f;
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

        // ---------------- 游戏流程 ----------------

        void NewGame()
        {
            _log.Clear();

            // 有存档就接着玩（"启动能读档"）
            var saved = SaveSystem.Load();
            if (saved != null)
            {
                Session = new GameSession(Content, saved);
                AppendLog("<color=#6ec1ff>已读取存档。</color>");
            }
            else
            {
                Session = new GameSession(Content, new GameState());
                AppendLog("你在一间安全的屋子里醒来，不记得自己是怎么到的这里。");
                AppendLog("走近发光的东西，按 E 互动；走到门口能出去。");
            }
            Refresh();
        }

        // 把玩家当前所在场景和坐标写回存档数据
        void CapturePosition()
        {
            var world = WorldRoot.Instance;
            if (world == null || world.Scene == null || world.PlayerTransform == null) return;
            if (Session == null) return;

            Session.State.sceneId = world.Scene.Id;
            Session.State.playerX = world.PlayerTransform.position.x;
            Session.State.playerY = world.PlayerTransform.position.y;
        }

        void OnSave()
        {
            if (Session == null) return;
            CapturePosition();
            AppendLog(SaveSystem.Save(Session.State) ? "<color=#6ec1ff>已存档。</color>" : "<color=#ff6b6b>存档失败，看 Console。</color>");
            Refresh();
        }

        void OnLoad()
        {
            if (Content == null) return;

            var state = SaveSystem.Load();
            if (state == null)
            {
                AppendLog("<color=#ffa657>没有找到存档。</color>");
                Refresh();
                return;
            }

            Session = new GameSession(Content, state);
            AppendLog("<color=#6ec1ff>已读档。</color>");

            // 读档要先把场景切回去，再放玩家
            var world = WorldRoot.Instance;
            if (world != null)
            {
                var pos = new Vector2(state.playerX, state.playerY);
                world.LoadScene(string.IsNullOrEmpty(state.sceneId) ? SceneData.DefaultSceneId() : state.sceneId,
                                pos.sqrMagnitude < 0.0001f ? (Vector2?)null : pos);
            }

            Refresh();
        }

        void OnRestart()
        {
            if (Content == null) return;
            SaveSystem.Delete();
            NewGame();

            var world = WorldRoot.Instance;
            if (world != null) world.LoadScene(SceneData.DefaultSceneId(), null);

            AppendLog("<color=#ffa657>已重开。</color>");
            Refresh();
        }

        // ---------------- 事件面板 ----------------

        public void ShowEvent(EventDef ev)
        {
            if (_eventPanel == null || ev == null) return;

            _eventPanel.gameObject.SetActive(true);
            _eventTitle.text = ev.title;
            _eventBody.text = ev.text;

            ClearOptionButtons();

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
            var result = Session.ResolveOption(ev, opt);
            if (!string.IsNullOrEmpty(result.text)) AppendLog(result.text);
            Refresh();

            if (!string.IsNullOrEmpty(result.nextEventId))
            {
                var next = Content.GetEvent(result.nextEventId);
                if (next != null)
                {
                    ShowEvent(next);
                    return;
                }
            }

            var shown = result.text;
            if (Session.IsDead) shown += "\n\n<color=#ff6b6b>你倒下了。按 ESC 打开界面，点「重开」。</color>";
            if (string.IsNullOrEmpty(shown))
            {
                CloseEvent();
                return;
            }

            AppendLog(shown);
            ShowResult(ev.title, shown);
        }

        // 结果直接显示在弹窗里。HUD 平时是隐藏的，日志看不见，
        // 所以不能让玩家选完了没有任何反馈。
        void ShowResult(string title, string text)
        {
            if (_eventPanel == null) return;

            _eventPanel.gameObject.SetActive(true);
            _eventTitle.text = title;
            _eventBody.text = text;

            ClearOptionButtons();

            var btn = UIFactory.NewButton("Option", _eventOptions, "继续", UIFactory.SizeNormal, CloseEvent);
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 34f;
            le.preferredHeight = 34f;

            Refresh();
        }

        void ClearOptionButtons()
        {
            // 先隐藏再销毁：Destroy 要等到帧末才生效，不隐藏的话旧按钮会闪一下、还能被误点
            for (int i = _eventOptions.childCount - 1; i >= 0; i--)
            {
                var child = _eventOptions.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        void CloseEvent()
        {
            if (_eventPanel != null) _eventPanel.gameObject.SetActive(false);
            Refresh();
        }

        // ---------------- 据点互动分流 ----------------

        public class Choice
        {
            public string Label;
            public UnityEngine.Events.UnityAction OnClick;
        }

        public void HandleInteraction(InteractionDef def)
        {
            if (def == null || Session == null) return;

            switch (def.action)
            {
                case "refine": OpenRefine(); break;
                case "rest": DoRest(); break;
                case "storage": OpenStorage(); break;
                case "quest": OpenQuest(); break;
                case "breakthrough": OpenBreakthrough(); break;
                default: OpenEvent(def); break;
            }
        }

        void OpenEvent(InteractionDef def)
        {
            var ev = Content.GetEvent(def.eventId);
            if (ev == null)
            {
                AppendLog("<color=#ff6b6b>事件不存在: " + def.eventId + "</color>");
                return;
            }
            ShowEvent(ev);
        }

        void OpenRefine()
        {
            if (!Session.CanRefine)
            {
                ShowMessage("炼化台", "残响不够。去外面打点异能者或异兽，把残响带回来。");
                return;
            }

            var list = Session.RefinableAbilities();
            if (list.Count == 0)
            {
                ShowMessage("炼化台", "能炼化的异能都已经炼完了。");
                return;
            }

            var body = "锅底的青火烧得很稳。把一缕残响丢进去，只烧得出一样东西：\n";
            foreach (var a in list)
                body += "\n· <color=#8ab4ff>" + a.name + "</color>　" + a.desc;

            var choices = new List<Choice>();
            foreach (var a in list)
            {
                var captured = a;
                choices.Add(new Choice { Label = "炼化「" + captured.name + "」", OnClick = () => DoRefine(captured.id) });
            }
            choices.Add(new Choice { Label = "先不炼", OnClick = CloseEvent });

            ShowChoices("炼化台", body, choices);
        }

        void DoRefine(string abilityId)
        {
            var msg = Session.Refine(abilityId);
            if (string.IsNullOrEmpty(msg)) { CloseEvent(); return; }

            AppendLog(msg);
            Refresh();
            ShowResult("炼化", msg);
        }

        void DoRest()
        {
            if (Session.State.hp >= Session.MaxHp)
            {
                ShowMessage("休整床", "你现在状态很好，不需要休息。");
                return;
            }

            var msg = Session.HealFull();
            AppendLog(msg);
            Refresh();
            ShowResult("休整床", msg);
        }

        void OpenStorage()
        {
            ShowMessage("物资箱", Session.StorageText());
        }

        void OpenQuest()
        {
            var q = Session.MainQuest;
            if (q == null) { ShowMessage("情报板", "板上什么都没有。"); return; }

            if (Session.QuestCompleted)
            {
                ShowMessage("情报板", "「" + q.name + "」\n\n这单已经结了，你在板上记了一笔。");
                return;
            }

            if (Session.QuestAccepted)
            {
                ShowMessage("情报板", "「" + q.name + "」\n\n" + Session.QuestProgressText());
                return;
            }

            ShowChoices("情报板",
                "一张压在图钉下的照片，背面写着「" + q.name + "」。\n\n" + q.desc,
                new List<Choice>
                {
                    new Choice { Label = "接下这单", OnClick = DoAcceptQuest },
                    new Choice { Label = "再看看", OnClick = CloseEvent }
                });
        }

        void DoAcceptQuest()
        {
            var msg = Session.AcceptQuest();
            if (!string.IsNullOrEmpty(msg)) AppendLog(msg);
            Refresh();
            ShowResult("情报板", msg ?? "……");
        }

        void OpenBreakthrough()
        {
            bool allOk;
            var body = Session.BreakthroughPanelText(out allOk);
            body += allOk
                ? "\n\n<color=#7ee787>三个条件都齐了。</color>"
                : "\n\n<color=#ffa657>还差点火候。</color>";

            ShowMessage("突破点", body);
        }

        void ShowMessage(string title, string body)
        {
            ShowChoices(title, body, new List<Choice> { new Choice { Label = "离开", OnClick = CloseEvent } });
        }

        // 通用弹窗：标题 + 正文 + 若干按钮
        public void ShowChoices(string title, string body, List<Choice> choices)
        {
            if (_eventPanel == null) return;

            _eventPanel.gameObject.SetActive(true);
            _eventTitle.text = title;
            _eventBody.text = body;
            ClearOptionButtons();

            if (choices != null)
            {
                foreach (var c in choices)
                {
                    var captured = c;
                    var btn = UIFactory.NewButton("Option", _eventOptions, captured.Label, UIFactory.SizeNormal,
                        () => { if (captured.OnClick != null) captured.OnClick(); });
                    var le = btn.gameObject.AddComponent<LayoutElement>();
                    le.minHeight = 32f;
                    le.preferredHeight = 32f;
                }
            }

            Refresh();
        }

        // ---------------- 对外 ----------------

        public void SetPrompt(string text)
        {
            if (_promptText != null) _promptText.text = text ?? string.Empty;
        }

        public void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _log.Add(line);
            while (_log.Count > MaxLogLines) _log.RemoveAt(0);
        }

        void Refresh()
        {
            if (_statusText != null && Session != null) _statusText.text = Session.StatusText();
            if (_logText != null) _logText.text = string.Join("\n", _log.ToArray());

            // 常驻血条
            if (_hpFill != null && _hpText != null && Session != null)
            {
                int cur = Mathf.Max(0, Session.State.hp);
                int max = Mathf.Max(1, Session.MaxHp);
                float ratio = Mathf.Clamp01(cur / (float)max);

                var f = _hpFill.rectTransform;
                f.anchorMin = new Vector2(0f, 0f);
                f.anchorMax = new Vector2(ratio, 1f);
                f.offsetMin = Vector2.zero;
                f.offsetMax = Vector2.zero;

                _hpText.text = cur + " / " + max;
            }

            // 残响 / 素材计数
            if (_lootText != null && Session != null)
            {
                _lootText.text = "<color=#8ad7ff>残响 ×" + Session.State.resonanceCount +
                                 "</color>　<color=#ffd479>素材 ×" + Session.State.breakthroughMaterials + "</color>";
            }

            // 任务追踪
            if (_questText != null && Session != null) _questText.text = Session.QuestProgressText();

            // 事件面板开着、或者人已经倒下 → 冻结走动和互动
            bool modalOpen = _eventPanel != null && _eventPanel.gameObject.activeSelf;
            GameFlow.IsBlocked = modalOpen || (Session != null && Session.IsDead);
        }
    }
}
