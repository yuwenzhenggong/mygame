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

        RectTransform _statusRoot;
        RectTransform _gameArea;
        CanvasScaler _canvasScaler;
        float _infoTop = -1f;
        int _lastScreenW;
        int _lastScreenH;
        Image _hpFill;
        Image _expFill;
        Text _infoLine;
        readonly Image[] _skillIcons = new Image[7];
        readonly Text[] _skillCds = new Text[7];
        readonly bool[] _skillIconLoaded = new bool[7];
        readonly bool[] _skillIconTried = new bool[7];

        RectTransform _backpackPanel;
        Text _backpackBody;

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
            // 固定像素缩放：稍后按「和 PixelPerfectCamera 相同的整数倍率」设置 scaleFactor，
            // 这样 HUD 和地图是同一个比例，不会在非 16:9 窗口下被拉大。
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = PixelArtSettings.PixelsPerUnit;
            _canvasScaler = scaler;

            // 只在「地图实际渲染的那块 640x360 区域」里布局 UI。
            // 窗口比 16:9 宽/窄时地图两侧会有黑边，HUD 不该跑到黑边上去。
            var gameArea = UIFactory.NewRect("GameArea", canvasGo.transform);
            gameArea.anchorMin = new Vector2(0.5f, 0.5f);
            gameArea.anchorMax = new Vector2(0.5f, 0.5f);
            gameArea.pivot = new Vector2(0.5f, 0.5f);
            gameArea.sizeDelta = new Vector2(PixelArtSettings.ReferenceWidth, PixelArtSettings.ReferenceHeight);
            gameArea.anchoredPosition = Vector2.zero;
            _gameArea = gameArea;

            ApplyPixelScale();

            // 平时隐藏的 HUD 容器：状态栏、日志、按钮都挂在这下面，ESC 整体开关
            var hud = UIFactory.NewRect("Hud", gameArea);
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
            _promptText = UIFactory.NewText("Prompt", gameArea, "", UIFactory.SizeNormal,
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

            BuildStatusBar(gameArea);
            BuildEventPanel(gameArea);
            BuildBackpackPanel(gameArea);

            // 默认收起：一进游戏就是干净的画面，只留互动提示和一条血条
            hud.gameObject.SetActive(false);
            _hudVisible = false;
        }

        // ---------------- 人物状态栏 ----------------
        // 素材原图的内容区尺寸，用来把子元素位置按比例换算到面板内
        const float BarSrcW = 1396f;
        const float BarSrcH = 659f;
        const float BarW = 280f;
        const float BarH = 132f;

        // 7 个技能格在原图里的横坐标和宽度（相对内容区左上角）
        static readonly float[] SlotX = { 49f, 241f, 432f, 621f, 810f, 999f, 1187f };
        static readonly float[] SlotW = { 177f, 176f, 174f, 174f, 174f, 172f, 171f };

        static float BX(float x) { return x / BarSrcW * BarW; }
        static float BY(float y) { return y / BarSrcH * BarH; }

        // 在父节点里按「左上角为原点、向下为正」摆放
        static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        void BuildStatusBar(Transform parent)
        {
            var root = UIFactory.NewRect("StatusBar", parent);
            PlaceTL(root, 6f, 6f, BarW, BarH);
            _statusRoot = root;

            var bg = UIFactory.NewSpriteImage("Bg", root, "ui_statusbar", Color.white);
            bg.raycastTarget = false;
            UIFactory.Stretch(bg.rectTransform, 0f, 0f, 0f, 0f);

            // 头像：用一张圆角遮罩裁切，否则方形头像的直角会盖住素材框的圆角
            var maskImg = UIFactory.NewSpriteImage("PortraitMask", root, "ui_portrait_mask", Color.white);
            maskImg.raycastTarget = false;
            PlaceTL(maskImg.rectTransform, BX(42f), BY(27f), BX(331f), BY(352f));

            var mask = maskImg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var portrait = UIFactory.NewSpriteImage("Portrait", maskImg.transform, "ui_portrait_hero", Color.white);
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;
            UIFactory.Stretch(portrait.rectTransform, 0f, 0f, 0f, 0f);

            // 生命条 / 经验条（条太窄，数值不放在条里，交给 ESC 面板和背包）
            _hpFill = MakeTrack(root, "HpTrack", 523f, 205f, 783f, 69f, new Color(0.86f, 0.24f, 0.28f, 1f));
            _expFill = MakeTrack(root, "ExpTrack", 645f, 314f, 660f, 50f, new Color(0.28f, 0.72f, 0.95f, 1f));

            // 7 个技能格：图标 + 冷却倒计时
            for (int i = 0; i < 7; i++)
            {
                var slot = UIFactory.NewRect("Skill" + i, root);
                PlaceTL(slot, BX(SlotX[i]), BY(433f), BX(SlotW[i]), BY(180f));

                // 先建成全透明：没有图标素材的技能就保持空着，露出素材本身的空槽
                var icon = UIFactory.NewImage("Icon", slot, new Color(1f, 1f, 1f, 0f));
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                UIFactory.Stretch(icon.rectTransform, 3f, 3f, 3f, 3f);
                _skillIcons[i] = icon;

                var cd = UIFactory.NewText("Cd", slot, "", UIFactory.SizeSmall,
                    new Color(1f, 0.95f, 0.7f, 1f), TextAnchor.MiddleCenter);
                UIFactory.Stretch(cd.rectTransform, 0f, 0f, 0f, 0f);
                _skillCds[i] = cd;
            }

            // 面板下面的一行：资源 + 任务进度
            _infoLine = UIFactory.NewText("Info", parent, "", UIFactory.SizeSmall,
                new Color(0.92f, 0.94f, 1f, 0.95f), TextAnchor.MiddleLeft);
            UIFactory.Anchor(_infoLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(8f, -6f - BarH - 17f), new Vector2(430f, -6f - BarH - 3f));
        }

        // 进度条：底板是素材里的深色凹槽，填充条按比例拉伸
        Image MakeTrack(Transform parent, string name, float rx, float ry, float rw, float rh, Color color)
        {
            var track = UIFactory.NewRect(name, parent);
            PlaceTL(track, BX(rx), BY(ry), BX(rw), BY(rh));

            var fill = UIFactory.NewImage("Fill", track, color);
            fill.raycastTarget = false;
            var f = fill.rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = Vector2.one;
            f.offsetMin = new Vector2(1f, 1f);
            f.offsetMax = new Vector2(-1f, -1f);

            return fill;
        }

        // 画布缩放取「和 PixelPerfectCamera 相同的整数倍率」：
        // 1 个画布单位正好等于地图里的 1 个像素，HUD 既不会被拉大，也不会跑出地图。
        void ApplyPixelScale()
        {
            if (_canvasScaler == null) return;

            int sw = Screen.width > 0 ? Screen.width : PixelArtSettings.ReferenceWidth;
            int sh = Screen.height > 0 ? Screen.height : PixelArtSettings.ReferenceHeight;

            int sx = Mathf.Max(1, sw / PixelArtSettings.ReferenceWidth);
            int sy = Mathf.Max(1, sh / PixelArtSettings.ReferenceHeight);
            int scale = Mathf.Max(1, Mathf.Min(sx, sy));

            if (!Mathf.Approximately(_canvasScaler.scaleFactor, scale))
                _canvasScaler.scaleFactor = scale;
        }

        void LateUpdate()
        {
            UpdateGameArea();
            UpdateStatusBarVisibility();
        }

        // GameArea 跟着「地图实际渲染出来的那块区域」走：
        //   场景比视野大（街道 / 丛林）→ 铺满整个画面，HUD 就在屏幕左上角
        //   场景比视野小（安全屋）    → 居中一小块，HUD 跟着缩到这块里
        void UpdateGameArea()
        {
            if (_gameArea == null || _canvasScaler == null) return;

            var world = WorldRoot.Instance;
            if (world == null || world.Scene == null) return;

            var follow = CameraFollow.Instance;
            var cam = follow != null ? follow.Cam : null;
            if (cam == null || !cam.orthographic) return;

            float visH = cam.orthographicSize * 2f;
            float visW = visH * cam.aspect;
            if (visW < 0.001f || visH < 0.001f) return;

            float fracW = Mathf.Clamp01(world.Scene.WorldWidth / visW);
            float fracH = Mathf.Clamp01(world.Scene.WorldHeight / visH);

            float s = Mathf.Max(1f, _canvasScaler.scaleFactor);
            var size = new Vector2(Screen.width / s * fracW, Screen.height / s * fracH);

            if ((_gameArea.sizeDelta - size).sqrMagnitude > 0.01f)
                _gameArea.sizeDelta = size;
        }

        // 状态栏只在战斗地图（有刷怪点的场景）显示；ESC 面板打开时也让位
        void UpdateStatusBarVisibility()
        {
            var world = WorldRoot.Instance;
            bool isBattle = world != null && world.SceneEnemySpawns > 0;
            bool showBar = isBattle && !_hudVisible;

            if (_statusRoot != null && _statusRoot.gameObject.activeSelf != showBar)
                _statusRoot.gameObject.SetActive(showBar);

            if (_infoLine != null)
            {
                if (_infoLine.gameObject.activeSelf == _hudVisible)
                    _infoLine.gameObject.SetActive(!_hudVisible);

                // 没有状态栏时，信息行顶到最上面，不留空档
                float top = showBar ? (6f + BarH + 3f) : 6f;
                if (!Mathf.Approximately(_infoTop, top))
                {
                    _infoTop = top;
                    UIFactory.Anchor(_infoLine.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(8f, -top - 14f), new Vector2(430f, -top));
                }
            }
        }

        void Update()
        {
            // 窗口尺寸变了要重算缩放，否则 HUD 会和地图错位
            if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
            {
                _lastScreenW = Screen.width;
                _lastScreenH = Screen.height;
                ApplyPixelScale();
            }
            // 背包开着的时候，B / ESC 都用来关背包
            if (_backpackPanel != null && _backpackPanel.gameObject.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Escape)) CloseBackpack();
                return;
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                if (_eventPanel != null && _eventPanel.gameObject.activeSelf) return;
                OpenBackpack();
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // 事件弹窗开着的时候，ESC 不去动 HUD，免得把正在读的文本关掉
            if (_eventPanel != null && _eventPanel.gameObject.activeSelf) return;

            ToggleHud();
        }

        public void ToggleHud()
        {
            _hudVisible = !_hudVisible;
            if (_hudRoot != null) _hudRoot.gameObject.SetActive(_hudVisible);
            // 状态栏和信息行的显隐由 UpdateStatusBarVisibility 每帧统一处理
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

            // ---------- 对话框底板：四层描边，比例按素材包 SVG（1760x312）缩到 640x360 基准 ----------
            Transform boxInner;
            var box = UIFactory.NewLayeredBox("DialogBox", overlay.transform,
                new Color[] { UIFactory.DlgDark, UIFactory.DlgCream, UIFactory.DlgTan, UIFactory.DlgCream },
                new float[] { 0f, 2f, 5f, 6f },
                out boxInner);

            var boxRt = box.rectTransform;
            boxRt.anchorMin = new Vector2(0.5f, 0f);
            boxRt.anchorMax = new Vector2(0.5f, 0f);
            boxRt.pivot = new Vector2(0.5f, 0f);
            boxRt.sizeDelta = new Vector2(620f, 180f);
            boxRt.anchoredPosition = new Vector2(0f, 10f);

            // ---------- 姓名牌：骑在底板上沿，带一根金色竖条 ----------
            Transform plateInner;
            var plate = UIFactory.NewLayeredBox("NamePlate", box.transform,
                new Color[] { UIFactory.DlgDark }, new float[] { 0f }, out plateInner);

            var plateRt = plate.rectTransform;
            plateRt.anchorMin = new Vector2(0f, 1f);
            plateRt.anchorMax = new Vector2(0f, 1f);
            plateRt.pivot = new Vector2(0f, 0f);
            plateRt.sizeDelta = new Vector2(104f, 22f);
            plateRt.anchoredPosition = new Vector2(26f, -2f);

            var accent = UIFactory.NewImage("Accent", plate.transform, UIFactory.DlgGold);
            accent.raycastTarget = false;
            UIFactory.Anchor(accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(6f, -6f), new Vector2(8f, 6f));

            _eventTitle = UIFactory.NewText("Title", plate.transform, "", UIFactory.SizeNormal,
                UIFactory.DlgCream, TextAnchor.MiddleLeft);
            UIFactory.Anchor(_eventTitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(14f, 0f), new Vector2(-6f, 0f));

            // ---------- 头像框 + 真实头像 ----------
            Transform frameInner;
            var frame = UIFactory.NewLayeredBox("PortraitFrame", box.transform,
                new Color[] { UIFactory.DlgDark, UIFactory.DlgTan, UIFactory.DlgInner },
                new float[] { 0f, 3f, 4f }, out frameInner);

            var frameRt = frame.rectTransform;
            frameRt.anchorMin = new Vector2(0f, 0f);
            frameRt.anchorMax = new Vector2(0f, 0f);
            frameRt.pivot = new Vector2(0f, 0f);
            frameRt.sizeDelta = new Vector2(72f, 72f);
            frameRt.anchoredPosition = new Vector2(16f, 92f);

            var portrait = UIFactory.NewSpriteImage("Portrait", frameInner, "ui_portrait_hero", Color.white);
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;
            UIFactory.Stretch(portrait.rectTransform, 2f, 2f, 2f, 2f);

            // ---------- 正文 ----------
            _eventBody = UIFactory.NewText("Body", box.transform, "", UIFactory.SizeNormal,
                UIFactory.DlgDark, TextAnchor.UpperLeft);
            UIFactory.Anchor(_eventBody.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(100f, 62f), new Vector2(-16f, -14f));

            // ---------- 分隔线 ----------
            var line = UIFactory.NewImage("Divider", box.transform,
                new Color(UIFactory.DlgTan.r, UIFactory.DlgTan.g, UIFactory.DlgTan.b, 0.55f));
            line.raycastTarget = false;
            UIFactory.Anchor(line.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(100f, 54f), new Vector2(-16f, 55f));

            // ---------- 选项条：横向一排，和素材包的按钮行一致 ----------
            var options = UIFactory.NewRect("Options", box.transform);
            UIFactory.Anchor(options, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(16f, 10f), new Vector2(-16f, 50f));
            _eventOptions = options;

            var hlg = options.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

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
        public void CapturePosition()
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
                var btn = UIFactory.NewDialogButton("Option", _eventOptions, captured.text, UIFactory.SizeNormal,
                    () => OnChoose(ev, captured));
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.minWidth = 140f;
                le.preferredWidth = 140f;
            }

            if (opts.Count == 0)
            {
                var btn = UIFactory.NewDialogButton("Option", _eventOptions, "离开", UIFactory.SizeNormal, CloseEvent);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.minWidth = 140f;
                le.preferredWidth = 140f;
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
            var next = Session.NextRealm;
            if (next == null)
            {
                ShowMessage("突破点", "你已经站在「" + Session.RealmName + "」。再往上的路还没修完。");
                return;
            }

            bool allOk;
            var body = Session.BreakthroughPanelText(out allOk);
            body += "\n\n突破后：「<color=#ffd479>" + next.name + "</color>」　生命上限 +" +
                    next.maxHpBonus + "　普攻伤害 +" + next.damageBonus;

            if (!allOk)
            {
                body += "\n\n<color=#ffa657>还差点火候。出去多打几只，把残响和素材凑够。</color>";
                ShowMessage("突破点", body);
                return;
            }

            body += "\n\n<color=#7ee787>三个条件都齐了。</color>";

            ShowChoices("突破点", body, new List<Choice>
            {
                new Choice { Label = "盘膝坐下，就此突破", OnClick = DoBreakthrough },
                new Choice { Label = "再等等", OnClick = CloseEvent }
            });
        }

        void DoBreakthrough()
        {
            var msg = Session.DoBreakthrough();
            if (string.IsNullOrEmpty(msg)) { CloseEvent(); return; }

            AppendLog("<color=#ffd479>突破：踏入「" + Session.RealmName + "」</color>");
            Refresh();

            // 突破即自动存档 —— 这一刻丢了才是真的难受
            CapturePosition();
            bool saved = SaveSystem.Save(Session.State);

            var text = msg + "\n\n" +
                       (saved ? "<color=#6ec1ff>已自动存档。</color>"
                              : "<color=#ff6b6b>自动存档失败，看 Console。</color>") +
                       "\n\n<color=#8ab4ff>Demo 的主线到这里结束。</color>\n" +
                       "你还可以回林子里练手，按 ESC 打开面板存档退出。";

            ShowResult("突破", text);
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
                    var btn = UIFactory.NewDialogButton("Option", _eventOptions, captured.Label, UIFactory.SizeNormal,
                        () => { if (captured.OnClick != null) captured.OnClick(); });
                    var le = btn.gameObject.AddComponent<LayoutElement>();
                    le.minWidth = 140f;
                    le.preferredWidth = 140f;
                }
            }

            Refresh();
        }

        // ---------------- 背包 ----------------

        void BuildBackpackPanel(Transform canvasRoot)
        {
            var overlay = UIFactory.NewImage("BackpackOverlay", canvasRoot, new Color(0f, 0f, 0f, 0.72f));
            UIFactory.Stretch(overlay.rectTransform, 0f, 0f, 0f, 0f);
            _backpackPanel = overlay.rectTransform;

            // 和事件对话框同一套暖白描边
            Transform boxInner;
            var box = UIFactory.NewLayeredBox("BackpackBox", overlay.transform,
                new Color[] { UIFactory.DlgDark, UIFactory.DlgCream, UIFactory.DlgTan, UIFactory.DlgCream },
                new float[] { 0f, 2f, 5f, 6f },
                out boxInner);

            var rt = box.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(620f, 300f);
            rt.anchoredPosition = Vector2.zero;

            // 姓名牌：同样骑在左上沿
            Transform plateInner;
            var plate = UIFactory.NewLayeredBox("BagPlate", box.transform,
                new Color[] { UIFactory.DlgDark }, new float[] { 0f }, out plateInner);

            var plateRt = plate.rectTransform;
            plateRt.anchorMin = new Vector2(0f, 1f);
            plateRt.anchorMax = new Vector2(0f, 1f);
            plateRt.pivot = new Vector2(0f, 0f);
            plateRt.sizeDelta = new Vector2(104f, 22f);
            plateRt.anchoredPosition = new Vector2(26f, -2f);

            var accent = UIFactory.NewImage("Accent", plate.transform, UIFactory.DlgGold);
            accent.raycastTarget = false;
            UIFactory.Anchor(accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(6f, -6f), new Vector2(8f, 6f));

            var title = UIFactory.NewText("Title", plate.transform, "背包", UIFactory.SizeNormal,
                UIFactory.DlgCream, TextAnchor.MiddleLeft);
            UIFactory.Anchor(title.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(14f, 0f), new Vector2(-6f, 0f));

            _backpackBody = UIFactory.NewText("Body", box.transform, "", UIFactory.SizeNormal,
                UIFactory.DlgDark, TextAnchor.UpperLeft);
            UIFactory.Anchor(_backpackBody.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(20f, 62f), new Vector2(-20f, -18f));

            // 分隔线，和对话框呼应
            var line = UIFactory.NewImage("Divider", box.transform,
                new Color(UIFactory.DlgTan.r, UIFactory.DlgTan.g, UIFactory.DlgTan.b, 0.55f));
            line.raycastTarget = false;
            UIFactory.Anchor(line.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(20f, 56f), new Vector2(-20f, 57f));

            var btn = UIFactory.NewDialogButton("Close", box.transform, "关闭（B）", UIFactory.SizeNormal, CloseBackpack);
            UIFactory.Anchor(btn.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-70f, 12f), new Vector2(70f, 48f));

            _backpackPanel.gameObject.SetActive(false);
        }

        public void OpenBackpack()
        {
            if (_backpackPanel == null || Session == null) return;

            var sb = new System.Text.StringBuilder();
            var st = Session.State;

            var caster = (WorldRoot.Instance != null && WorldRoot.Instance.PlayerTransform != null)
                ? WorldRoot.Instance.PlayerTransform.GetComponent<XianXia.World.AbilityCaster>()
                : null;

            sb.AppendLine("<color=#8a6a3a>资源</color>　残响 ×" + st.resonanceCount +
                          "　素材 ×" + st.breakthroughMaterials);
            sb.AppendLine("<color=#8a6a3a>境界</color>　" + Session.RealmName + "　Lv." + Session.Level +
                          "　经验 " + st.exp + " / " + Session.ExpToNext +
                          "　累计 " + st.totalExp + " / " + GameSession.BreakthroughExpNeed);
            sb.AppendLine("<color=#8a6a3a>委托</color>　" + Session.QuestProgressText());
            sb.AppendLine();

            sb.AppendLine("<color=#8a6a3a>异能</color>　" + st.abilities.Count + " / " + Content.Abilities.Count +
                          "　<color=#9a9288>（数字键 1~7 释放）</color>");

            for (int i = 0; i < Content.Abilities.Count; i++)
            {
                var a = Content.Abilities[i];
                if (a == null) continue;

                if (!st.abilities.Contains(a.id))
                {
                    if (a.refinable) sb.AppendLine("　" + (i + 1) + ". <color=#b3ada3>？？　未炼化</color>");
                    continue;
                }

                var line = "　" + (i + 1) + ". <color=#2f6fb0>" + a.name + "</color>";

                if (!a.active)
                    line += "　" + a.desc;
                else
                {
                    if (caster != null && caster.CooldownLeft(i) > 0f)
                        line += "　<color=#c07a1e>冷却 " + Mathf.CeilToInt(caster.CooldownLeft(i)) + "s</color>";
                    if (!string.IsNullOrEmpty(a.activeDesc)) line += "　" + a.activeDesc;
                }

                sb.AppendLine(line);
            }

            _backpackBody.text = sb.ToString();
            _backpackPanel.gameObject.SetActive(true);
            Refresh();
        }

        public void CloseBackpack()
        {
            if (_backpackPanel == null) return;
            _backpackPanel.gameObject.SetActive(false);
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

            RefreshStatusBar();

            // 事件面板 / 背包开着 → 冻结走动和互动
            bool modalOpen = (_eventPanel != null && _eventPanel.gameObject.activeSelf)
                          || (_backpackPanel != null && _backpackPanel.gameObject.activeSelf);
            GameFlow.IsBlocked = modalOpen || (Session != null && Session.IsDead);
        }

        static void SetFill(Image fill, float ratio)
        {
            var f = fill.rectTransform;
            f.anchorMin = new Vector2(0f, 0f);
            f.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            f.offsetMin = new Vector2(1f, 1f);
            f.offsetMax = new Vector2(-1f, -1f);
        }

        void RefreshStatusBar()
        {
            if (Session == null) return;

            // 生命
            int hp = Mathf.Max(0, Session.State.hp);
            int maxHp = Mathf.Max(1, Session.MaxHp);
            if (_hpFill != null) SetFill(_hpFill, hp / (float)maxHp);

            // 经验
            int exp = Mathf.Max(0, Session.State.exp);
            int expNeed = Mathf.Max(1, Session.ExpToNext);
            if (_expFill != null) SetFill(_expFill, exp / (float)expNeed);

            // 7 个技能格：有异能就亮，否则压暗；冷却中显示剩余秒数
            var caster = (WorldRoot.Instance != null && WorldRoot.Instance.PlayerTransform != null)
                ? WorldRoot.Instance.PlayerTransform.GetComponent<XianXia.World.AbilityCaster>()
                : null;

            var abilities = Content != null ? Content.Abilities : null;

            for (int i = 0; i < 7; i++)
            {
                var def = (abilities != null && i < abilities.Count) ? abilities[i] : null;
                bool owned = def != null && Session.HasAbility(def.id);

                if (_skillIcons[i] != null)
                {
                    if (def != null && !_skillIconTried[i])
                    {
                        var sprite = Resources.Load<Sprite>(PixelArtSettings.ArtResourceRoot + "ui_skill_" + def.id);
                        if (sprite != null) { _skillIcons[i].sprite = sprite; _skillIconLoaded[i] = true; }
                        _skillIconTried[i] = true;   // 找不到就只试一次，别每帧 Load
                    }

                    // 没有素材的技能保持空槽：透明，不画占位
                    if (!_skillIconLoaded[i])
                        _skillIcons[i].color = new Color(1f, 1f, 1f, 0f);
                    else
                        _skillIcons[i].color = owned ? Color.white : new Color(1f, 1f, 1f, 0.16f);
                }

                if (_skillCds[i] != null)
                {
                    float cd = (caster != null && owned) ? caster.CooldownLeft(i) : 0f;
                    _skillCds[i].text = cd > 0.05f ? Mathf.CeilToInt(cd).ToString() : "";
                }
            }

            // 面板下方一行
            if (_infoLine != null)
            {
                var st = Session.State;
                _infoLine.text = "<color=#8ad7ff>残响 " + st.resonanceCount + "</color>　" +
                                 "<color=#ffd479>素材 " + st.breakthroughMaterials + "</color>　｜　" +
                                 Session.QuestProgressText();
            }
        }
    }
}
