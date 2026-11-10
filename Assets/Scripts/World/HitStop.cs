using System.Collections;
using UnityEngine;

namespace XianXia.World
{
    // 命中顿帧：把 timeScale 压到很低一小会儿，制造"打到实感"。
    // 挂在 WorldRoot 上，跨场景不重建。
    public class HitStop : MonoBehaviour
    {
        static HitStop _instance;
        bool _busy;

        public static void Freeze(float seconds)
        {
            if (_instance == null || _instance._busy) return;
            _instance.StartCoroutine(_instance.Co(seconds));
        }

        void Awake()
        {
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                Time.timeScale = 1f;   // 别把游戏卡在暂停状态
            }
        }

        IEnumerator Co(float seconds)
        {
            // 弹窗/死亡冻结期间不做顿帧，否则会和暂停逻辑打架
            if (GameFlow.IsBlocked) yield break;

            _busy = true;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            _busy = false;
        }
    }
}
