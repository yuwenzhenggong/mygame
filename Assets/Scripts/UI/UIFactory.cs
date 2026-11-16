using UnityEngine;
using UnityEngine.UI;

namespace XianXia.UI
{
    // 纯代码搭 UGUI 的小工具集。
    // 好处：不依赖场景里的 prefab，脚本改完直接跑，不会出现「场景引用丢了」这种坑。
    public static class UIFactory
    {
        static Font _mainFont;
        static bool _hookedFontRebuild;

        // 像素游戏的字号都按「逻辑像素」算：12 在 2 倍窗口下就是 24 个物理像素
        public const int SizeSmall = 12;
        public const int SizeNormal = 12;
        public const int SizeTitle = 14;

        public static Font MainFont
        {
            get
            {
                if (_mainFont != null) return _mainFont;

                // 1) 优先用 Assets/Resources/Fonts/ 下你自己放的字体（文件名随意）
                var custom = Resources.LoadAll<Font>("Fonts");
                if (custom != null)
                {
                    foreach (var f in custom)
                    {
                        if (f != null) { _mainFont = f; break; }
                    }
                }

                // 2) 退回到系统中文字体。Unity 内置 Arial 不含中文字形，会显示方块
                if (_mainFont == null)
                {
                    string[] candidates = { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "SimSun", "宋体", "Arial" };
                    _mainFont = Font.CreateDynamicFontFromOSFont(candidates, SizeNormal);
                }

                if (_mainFont == null) _mainFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

                // 3) 字体图集用 Point 过滤，放大后才是硬边像素而不是模糊
                if (!_hookedFontRebuild)
                {
                    Font.textureRebuilt += OnFontTextureRebuilt;
                    _hookedFontRebuild = true;
                }

                return _mainFont;
            }
        }

        static void OnFontTextureRebuilt(Font font)
        {
            try
            {
                if (font != null && font.material != null && font.material.mainTexture != null)
                    font.material.mainTexture.filterMode = FilterMode.Point;
            }
            catch
            {
                // 个别 Unity 版本上动态字体的材质不可写，忽略即可，不影响功能
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static Image NewImage(string name, Transform parent, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        // 从 Resources/Art/ 取精灵；取不到就退回纯色块，保证界面不崩
        public static Image NewSpriteImage(string name, Transform parent, string spriteName, Color tint,
                                           Image.Type type = Image.Type.Simple)
        {
            var img = NewImage(name, parent, tint);
            var sprite = Resources.Load<Sprite>(PixelArtSettings.ArtResourceRoot + spriteName);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = type;
            }
            else
            {
                Debug.LogWarning("[UIFactory] 找不到精灵: " + PixelArtSettings.ArtResourceRoot + spriteName);
            }
            return img;
        }

        public static Text NewText(string name, Transform parent, string content, int size, Color color,
                                   TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = MainFont;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.text = content;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // ---------------- 暖白像素对话框 ----------------
        // 配色与描边层次取自 UI 素材包里的组件 SVG，在 UGUI 里用嵌套矩形还原，
        // 这样任意尺寸都是硬边，不依赖 SVG 插件，也不会被缩放糊掉。

        public static readonly Color DlgDark  = new Color32(0x29, 0x26, 0x2e, 0xff);
        public static readonly Color DlgCream = new Color32(0xfc, 0xf8, 0xee, 0xff);
        public static readonly Color DlgTan   = new Color32(0xbf, 0xb4, 0xa3, 0xff);
        public static readonly Color DlgGold  = new Color32(0xb4, 0xa2, 0x7d, 0xff);
        public static readonly Color DlgInner = new Color32(0xe9, 0xe3, 0xd8, 0xff);

        // 按 colors/insets 依次向内铺色，还原「外框 → 米白 → 细线 → 内芯」的层次。
        // 返回最外层 Image，innerMost 是最里层的 Transform，用来放内容。
        public static Image NewLayeredBox(string name, Transform parent, Color[] colors, float[] insets,
                                          out Transform innerMost)
        {
            Image root = null;
            Transform cur = parent;

            for (int i = 0; i < colors.Length; i++)
            {
                var img = NewImage(name + "_" + i, cur, colors[i]);
                img.raycastTarget = false;

                if (i == 0) root = img;
                else Stretch(img.rectTransform, insets[i], insets[i], insets[i], insets[i]);

                cur = img.transform;
            }

            innerMost = cur;
            return root;
        }

        // 跟着素材包做的米白按钮：细棕边 + 米白底 + 深色字
        public static Button NewDialogButton(string name, Transform parent, string label, int size,
                                             UnityEngine.Events.UnityAction onClick)
        {
            Transform inner;
            var root = NewLayeredBox(name, parent,
                new Color[] { DlgTan, DlgCream },
                new float[] { 0f, 2f },
                out inner);

            var img = inner.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.85f, 0.78f, 1f);
            colors.pressedColor = new Color(0.74f, 0.70f, 0.62f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.04f;
            btn.colors = colors;

            var txt = NewText("Label", inner, label, size, DlgDark, TextAnchor.MiddleCenter);
            Stretch(txt.rectTransform, 4f, 2f, 4f, 2f);

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static Button NewButton(string name, Transform parent, string label, int size, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(name, parent, new Color(0.16f, 0.21f, 0.29f, 1f));
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.80f, 0.87f, 1f, 1f);
            colors.pressedColor = new Color(0.58f, 0.68f, 0.86f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;

            var txt = NewText("Label", img.transform, label, size, new Color(0.93f, 0.95f, 1f), TextAnchor.MiddleCenter);
            Stretch(txt.rectTransform, 4f, 2f, 4f, 2f);

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }
    }
}
