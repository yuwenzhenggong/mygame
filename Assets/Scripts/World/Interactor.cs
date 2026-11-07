using System.Collections.Generic;
using UnityEngine;
using XianXia.Data;
using XianXia.UI;

namespace XianXia.World
{
    // 走近可互动对象 → 底部出现提示 → 按 E 触发事件。
    // 挂在玩家身上，每帧算「离我最近且当前可用的那个」。
    public class Interactor : MonoBehaviour
    {
        public class Entry
        {
            public InteractionDef Def;
            public SpriteRenderer Marker;
            public Vector2 WorldPosition;
        }

        List<Entry> _entries = new List<Entry>();

        public void SetEntries(List<Entry> entries)
        {
            _entries = entries ?? new List<Entry>();
        }

        void Update()
        {
            var ui = GameUI.Instance;
            if (ui == null || ui.Session == null) return;

            // 事件面板打开 / 已经倒下 → 不处理互动
            if (GameFlow.IsBlocked)
            {
                ui.SetPrompt(null);
                return;
            }

            var pos = (Vector2)transform.position;
            Entry best = null;
            float bestDist = float.MaxValue;

            foreach (var entry in _entries)
            {
                string reason;
                bool available = ui.Session.IsInteractionAvailable(entry.Def, out reason);

                // 不可用的互动把标记藏掉，玩家一眼就知道哪些已经做过
                if (entry.Marker != null) entry.Marker.enabled = available;
                if (!available) continue;

                float radius = Mathf.Max(0.1f, entry.Def.radius / (float)PixelArtSettings.PixelsPerUnit);
                float dist = Vector2.Distance(pos, entry.WorldPosition);
                if (dist <= radius && dist < bestDist)
                {
                    bestDist = dist;
                    best = entry;
                }
            }

            if (best == null)
            {
                ui.SetPrompt(null);
                return;
            }

            ui.SetPrompt("按 <color=#7ee787>E</color> 与「" + best.Def.name + "」互动");

            if (Input.GetKeyDown(KeyCode.E))
            {
                // 传送类互动：不开事件面板，直接换场景
                if (best.Def.action == "travel")
                {
                    if (string.IsNullOrEmpty(best.Def.targetScene))
                    {
                        ui.AppendLog("<color=#ff6b6b>传送点没配 targetScene: " + best.Def.id + "</color>");
                        return;
                    }

                    ui.Session.MarkInteractionUsed(best.Def);
                    ui.SetPrompt(null);
                    WorldRoot.Instance.TravelTo(best.Def.targetScene, best.Def.targetImageX, best.Def.targetImageY);
                    return;
                }

                // 其余都交给 UI 按 action 分流（event / refine / rest / storage / quest / breakthrough）
                ui.Session.MarkInteractionUsed(best.Def);
                ui.HandleInteraction(best.Def);
            }
        }
    }
}
