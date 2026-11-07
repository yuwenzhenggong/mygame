using UnityEngine;
using XianXia.UI;

namespace XianXia.World
{
    // 玩家的攻击与受击。挂在玩家身上。
    // 普攻是"朝向 + 前方一段圆形判定"，不做连招/闪避/弹反。
    public class PlayerCombat : MonoBehaviour
    {
        public int Damage = 12;
        public float Cooldown = 0.5f;
        public float Reach = 0.85f;     // 判定圆心离玩家多远
        public float Radius = 0.95f;    // 判定半径
        public float InvulnerableTime = 0.6f;
        public float ImpactDelay = 0.25f; // 对齐攻击序列中刀光真正挥出的时刻

        float _cooldownLeft;
        float _invulnLeft;
        float _flashLeft = -1f;
        float _pendingHitLeft = -1f;
        SpriteRenderer _body;
        PlayerWalker _walker;
        Facing _attackFacing;
        bool _dying;

        void Start()
        {
            _body = GetComponentInChildren<SpriteRenderer>();
            _walker = GetComponent<PlayerWalker>();

            // 普通攻击素材从 Resources 连号加载，缺帧就停，方便以后替换帧数。
            var attackFrames = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < 64; i++)
            {
                var sprite = Resources.Load<Sprite>(
                    PixelArtSettings.ArtResourceRoot + "hero_attack_" + i);
                if (sprite == null) break;
                attackFrames.Add(sprite);
            }

            if (_walker != null)
            {
                _walker.SetAttackFrames(attackFrames.ToArray());
                if (attackFrames.Count == 0)
                    Debug.LogWarning("[Combat] 没找到普通攻击帧 hero_attack_*");
            }
        }

        void Update()
        {
            if (_dying) return;
            if (GameFlow.IsBlocked) return;

            if (_cooldownLeft > 0f) _cooldownLeft -= Time.deltaTime;

            if (_pendingHitLeft >= 0f)
            {
                _pendingHitLeft -= Time.deltaTime;
                if (_pendingHitLeft <= 0f)
                {
                    _pendingHitLeft = -1f;
                    ResolveHit();
                }
            }

            if (_invulnLeft > 0f)
            {
                _invulnLeft -= Time.deltaTime;
                if (_invulnLeft <= 0f) SetTint(Color.white);
            }
            else if (_flashLeft > 0f)
            {
                _flashLeft -= Time.deltaTime;
                if (_flashLeft <= 0f) SetTint(Color.white);
            }

            if (Input.GetKeyDown(KeyCode.J) && _cooldownLeft <= 0f) Attack();
        }

        static Vector2 FacingDir(Facing f)
        {
            switch (f)
            {
                case Facing.Back: return Vector2.up;
                case Facing.Left: return Vector2.left;
                case Facing.Right: return Vector2.right;
                default: return Vector2.down;
            }
        }

        void Attack()
        {
            _cooldownLeft = CurrentCooldown();

            _attackFacing = _walker != null ? _walker.CurrentFacing : Facing.Front;
            if (_walker != null) _walker.PlayAttack();

            // 出招的一点视觉反馈（偏冷，和挨打的红色区分开）
            SetTint(new Color(0.75f, 0.88f, 1f));
            _flashLeft = 0.08f;

            // 等动画真正挥到有效帧时再结算，避免按 J 的瞬间敌人就掉血。
            _pendingHitLeft = Mathf.Max(0f, ImpactDelay);
        }

        // 伤害 / 冷却 / 减伤都受已炼化的异能影响
        int CurrentDamage()
        {
            var ui = GameUI.Instance;
            if (ui == null || ui.Session == null) return Damage;
            return Damage + ui.Session.TotalDamageBonus();
        }

        float CurrentCooldown()
        {
            var ui = GameUI.Instance;
            if (ui == null || ui.Session == null) return Cooldown;
            return Cooldown * (1f - ui.Session.TotalCooldownReduction());
        }

        int ReduceIncoming(int amount)
        {
            var ui = GameUI.Instance;
            if (ui == null || ui.Session == null) return amount;
            return Mathf.Max(1, Mathf.RoundToInt(amount * (1f - ui.Session.TotalDamageReduction())));
        }

        void ResolveHit()
        {
            var center = (Vector2)transform.position + FacingDir(_attackFacing) * Reach;
            int dmg = CurrentDamage();

            var enemies = FindObjectsOfType<Enemy>();
            foreach (var e in enemies)
            {
                if (e == null) continue;
                if (Vector2.Distance((Vector2)e.transform.position, center) <= Radius)
                    e.TakeDamage(dmg);
            }
        }

        public void TakeDamage(int amount)
        {
            if (_dying || _invulnLeft > 0f) return;

            var ui = GameUI.Instance;
            if (ui == null || ui.Session == null) return;

            _invulnLeft = InvulnerableTime;
            SetTint(new Color(1f, 0.4f, 0.4f));
            _flashLeft = -1f;

            int actual = ReduceIncoming(amount);
            var msg = ui.Session.ChangeHp(-actual);
            ui.AppendLog("<color=#ff6b6b>受到 " + actual + " 点伤害</color>");
            ui.RefreshHud();

            if (ui.Session.State.hp <= 0) Die();
        }

        void Die()
        {
            _dying = true;

            var ui = GameUI.Instance;
            if (ui != null && ui.Session != null)
            {
                // 倒下后送回安全屋，满血复活
                ui.Session.State.hp = ui.Session.MaxHp;
                ui.Session.ClampHp();
                ui.AppendLog("<color=#ff6b6b>你倒下了，被拖回了安全屋。</color>");
                ui.RefreshHud();
            }

            if (WorldRoot.Instance != null)
                WorldRoot.Instance.LoadScene("safehouse", null);
        }

        void SetTint(Color c)
        {
            if (_body == null) _body = GetComponentInChildren<SpriteRenderer>();
            if (_body != null) _body.color = c;
        }
    }
}
