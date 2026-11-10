using System.Collections.Generic;
using UnityEngine;

namespace XianXia.World
{
    // 伤害飘字：把数字拆成 num_* 精灵横向排开，边升边淡出。
    // 用精灵而不是 TextMesh，是为了不受字体材质影响，保证像素对齐。
    public class DamageNumber : MonoBehaviour
    {
        const float DigitWidth = 0.25f;   // 8px / PPU 32
        const float Life = 0.75f;
        const float RiseSpeed = 1.1f;
        const float FadeStart = 0.45f;

        static readonly Sprite[] Digits = new Sprite[10];
        static bool _loaded;

        Vector3 _start;
        float _t;
        SpriteRenderer[] _digits;
        Color _color;

        // scale 建议传 PixelArtSettings.CharacterScale，和角色同档大小
        public static void Spawn(Vector3 worldPos, int amount, Color color, float scale)
        {
            int value = Mathf.Abs(amount);
            if (value <= 0) return;

            LoadDigits();

            var text = value.ToString();
            var root = new GameObject("Dmg_" + text);
            root.transform.position = worldPos;
            root.transform.localScale = Vector3.one * scale;

            var comp = root.AddComponent<DamageNumber>();
            comp._start = worldPos;
            comp._color = color;

            float startX = -(text.Length - 1) * DigitWidth * 0.5f;
            var list = new List<SpriteRenderer>();

            for (int i = 0; i < text.Length; i++)
            {
                int d = text[i] - '0';
                if (d < 0 || d > 9) continue;

                var go = new GameObject("d" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(startX + i * DigitWidth, 0f, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Digits[d];
                sr.color = color;
                sr.sortingOrder = 90;   // 压在角色(10)和标记(20)之上
                list.Add(sr);
            }

            comp._digits = list.ToArray();
        }

        static void LoadDigits()
        {
            if (_loaded) return;
            _loaded = true;
            for (int i = 0; i < 10; i++)
                Digits[i] = Resources.Load<Sprite>(PixelArtSettings.ArtResourceRoot + "num_" + i);
        }

        void Update()
        {
            // 用不受 timeScale 影响的时间，顿帧时飘字也不会僵住
            _t += Time.unscaledDeltaTime;
            transform.position = _start + new Vector3(0f, _t * RiseSpeed, 0f);

            if (_t >= FadeStart && _digits != null)
            {
                float a = Mathf.Clamp01(1f - (_t - FadeStart) / (Life - FadeStart));
                var c = new Color(_color.r, _color.g, _color.b, a);
                for (int i = 0; i < _digits.Length; i++)
                    if (_digits[i] != null) _digits[i].color = c;
            }

            if (_t >= Life) Destroy(gameObject);
        }
    }
}
