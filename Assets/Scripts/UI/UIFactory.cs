using UnityEngine;
using UnityEngine.UI;

namespace XianXia.UI
{
    // 纯代码搭 UGUI 的小工具集。
    // 好处：不依赖场景里的 prefab，脚本改完直接跑，不会出现「场景引用丢了」这种坑。
    public static class UIFactory
    {
        static Font _mainFont;

        public static Font MainFont
        {
            get
            {
                if (_mainFont != null) return _mainFont;

                // 必须用系统中文字体，Unity 内置的 Arial 不含中文字形（会显示方块）
                string[] candidates = { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "SimSun", "宋体", "Arial" };
                _mainFont = Font.CreateDynamicFontFromOSFont(candidates, 20);

                if (_mainFont == null) _mainFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _mainFont;
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
            Stretch(txt.rectTransform, 10f, 4f, 10f, 4f);

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }
    }
}
