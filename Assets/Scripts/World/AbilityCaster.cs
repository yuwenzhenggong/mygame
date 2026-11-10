using UnityEngine;
using XianXia.Data;

namespace XianXia.World
{
    // 主动异能：数字键 1~7 对应 abilities.json 里的顺序。
    // 同一时间只允许一个持续型 buff，后放的顶掉前一个（避免叠满暴打）。
    public class AbilityCaster : MonoBehaviour
    {
        const int MaxSlots = 7;

        static readonly KeyCode[] SlotKeys = new KeyCode[]
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
            KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7
        };

        readonly float[] _cooldowns = new float[MaxSlots];

        float _buffLeft;
        float _buffDamageAdd;
        float _buffDamageTakenMul = 1f;
        float _buffCooldownMul = 1f;
        bool _buffReveal;

        // 给 PlayerCombat 读
        public float BonusDamage { get { return _buffLeft > 0f ? _buffDamageAdd : 0f; } }
        public float DamageTakenMul { get { return _buffLeft > 0f ? _buffDamageTakenMul : 1f; } }
        public float CooldownMul { get { return _buffLeft > 0f ? _buffCooldownMul : 1f; } }
        public bool HasBuff { get { return _buffLeft > 0f; } }
        public float BuffLeft { get { return _buffLeft; } }

        public float CooldownLeft(int slot)
        {
            return slot >= 0 && slot < MaxSlots ? _cooldowns[slot] : 0f;
        }

        void Update()
        {
            for (int i = 0; i < MaxSlots; i++)
                if (_cooldowns[i] > 0f) _cooldowns[i] -= Time.deltaTime;

            if (_buffLeft > 0f)
            {
                _buffLeft -= Time.deltaTime;
                if (_buffLeft <= 0f) { _buffLeft = 0f; _buffDamageAdd = 0f; _buffDamageTakenMul = 1f; _buffCooldownMul = 1f; }
            }

            if (GameFlow.IsBlocked) return;

            var ui = UI.GameUI.Instance;
            if (ui == null || ui.Session == null) return;

            for (int i = 0; i < MaxSlots; i++)
                if (Input.GetKeyDown(SlotKeys[i])) TryCast(i, ui);
        }

        void TryCast(int slot, UI.GameUI ui)
        {
            var defs = ui.Content != null ? ui.Content.Abilities : null;
            if (defs == null || slot < 0 || slot >= defs.Count) return;

            var def = defs[slot];
            if (def == null || !def.active) return;

            if (!ui.Session.HasAbility(def.id))
            {
                ui.AppendLog("<color=#8a8f98>还没获得「" + def.name + "」。</color>");
                return;
            }

            if (_cooldowns[slot] > 0f)
            {
                ui.AppendLog("<color=#8a8f98>「" + def.name + "」冷却中 " +
                             Mathf.CeilToInt(_cooldowns[slot]) + " 秒。</color>");
                return;
            }

            _cooldowns[slot] = Mathf.Max(0.5f, def.activeCooldown);
            Cast(def, ui);
        }

        void Cast(AbilityDef def, UI.GameUI ui)
        {
            // 瞬发范围：伤害 + 击退
            if (def.activeBurstDamage > 0)
            {
                int hits = 0;
                foreach (var e in FindObjectsOfType<Enemy>())
                {
                    if (e == null) continue;
                    if (Vector2.Distance(transform.position, e.transform.position) > def.activeBurstRadius) continue;

                    e.TakeDamage(def.activeBurstDamage);

                    var push = e.transform.position - transform.position;
                    if (push.sqrMagnitude < 0.0001f) push = Vector3.right;
                    e.Push(push.normalized * def.activeBurstKnockback);

                    DamageNumber.Spawn(
                        e.transform.position + Vector3.up * (1.1f * PixelArtSettings.CharacterScale),
                        def.activeBurstDamage, new Color(0.55f, 0.85f, 1f), PixelArtSettings.CharacterScale);
                    hits++;
                }

                if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.12f, 0.16f);
                if (hits > 0) HitStop.Freeze(0.05f);
            }

            // 瞬发：回血
            if (def.activeHeal > 0)
            {
                ui.Session.ChangeHp(def.activeHeal);
                ui.RefreshHud();
            }

            // 持续型 buff
            if (def.activeDuration > 0f)
            {
                _buffLeft = def.activeDuration;
                _buffDamageAdd = def.activeDamageAdd;
                _buffDamageTakenMul = def.activeDamageTakenMul;
                _buffCooldownMul = def.activeCooldownMul;
                _buffReveal = def.activeReveal;

                if (_buffReveal)
                    foreach (var e in FindObjectsOfType<Enemy>())
                        if (e != null) e.Highlight(def.activeDuration);
            }

            var text = string.IsNullOrEmpty(def.activeDesc) ? def.name : def.activeDesc;
            ui.AppendLog("<color=#8ab4ff>「" + def.name + "」</color>" + text);
            ui.RefreshHud();
        }

        // 背包里显示：冷却还剩几秒
        void OnDisable()
        {
            for (int i = 0; i < MaxSlots; i++) _cooldowns[i] = 0f;
            _buffLeft = 0f;
            _buffDamageAdd = 0f;
            _buffDamageTakenMul = 1f;
            _buffCooldownMul = 1f;
        }
    }
}
