using UnityEngine;
using UnityEngine.EventSystems;
using XianXia.UI;
using XianXia.World;

namespace XianXia
{
    // 自动入口：不管当前场景里有什么，按下 Play 就会把游戏跑起来。
    // 不需要手动往场景里拖任何东西 —— 这也是新手最容易踩的坑。
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (Object.FindObjectOfType<GameUI>() != null) return;

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Object.DontDestroyOnLoad(es);
            }

            var root = new GameObject("GameRoot");
            Object.DontDestroyOnLoad(root);

            root.AddComponent<WorldRoot>();   // 世界：地图 + 玩家 + 相机
            root.AddComponent<GameUI>();      // HUD：叠在世界上面
        }
    }
}
