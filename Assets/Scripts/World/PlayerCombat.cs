using UnityEngine;
using XianXia.UI;

namespace XianXia.World
{
    // 玩家的攻击与受击。挂在玩家身上。
    // 普攻是两段连招：左手直拳 → 右手直拳，第二段伤害更高。
    public class PlayerCombat : MonoBehaviour
    {
        public int Damage = 12;
        public float Cooldown = 0.3f;
        public float Reach = 0.85f;     // 判定圆心离玩家多远
        public float Radius = 0.95f;    // 判定半径
        public float InvulnerableTime = 0.6f;
        public float ImpactDelay = 0.12f; // 对齐动画里拳头真正打出的时刻

        // 连招参数
        public int ComboHits = 2;            // 一共几段
        public int FramesPerHit = 3;         // 每段几帧
        public float ComboWindow = 0.8f;     // 这段时间内再按 J 就接下一段
        public float LastHitDamageMul = 1.6f; // 最后一段（收招）的伤害倍率

        // 新素材里两段普攻都朝同一侧出拳，所以默认不翻转。
        // 如果以后换成"左右开弓"的素材，打开这个开关即可让它们统一朝向。
        public bool FlipOddHits = false;

        float _cooldownLeft;
        float _invulnLeft;
        float _flashLeft = -1f;
        float _pendingHitLeft = -1f;
        float _pendingDamageMul = 1f;
        int _comboIndex;         // 下一次按 J 出第几段（0 起）
        float _comboWindowLeft;
        SpriteRenderer _body;
        PlayerWalker _walker;
        Facing _attackFacing;
        bool _dying;

        void Start()
        {
            _body = GetComponentInChildren<SpriteRenderer>();
            _walker = GetComponent<PlayerWalker>();

            // 连招素材从 Resources 连号加载，缺帧就停，方便以后换帧数
            var attackFrames = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < 64; i++)
            {
                var sprite = Resources.Load<Sprite>(
                    PixelArtSettings.ArtResourceRoot + "hero_combo_" + i);
                if (sprite == null) break;
                attackFrames.Add(sprite);
            }

            if (_walker != null)
            {
                _walker.SetAttackFrames(attackFrames.ToArray());
                if (attackFrames.Count == 0)
                    Debug.LogWarning("[Combat] 没找到连招帧 hero_combo_*");
            }
        }

        void Update()
        {
            if (_dying) return;
            if (GameFlow.IsBlocked) return;

            if (_cooldownLeft > 0f) _cooldownLeft -= Time.deltaTime;

            // 连招窗口：过了这段时间没再按 J，就回到第一段
            if (_comboWindowLeft > 0f)
            {
                _comboWindowLeft -= Time.deltaTime;
                if (_comboWindowLeft <= 0f) _comboIndex = 0;
            }

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

            bool busy = _walker != null && _walker.IsAttacking;
            if (Input.GetKeyDown(KeyCode.J) && !busy && _cooldownLeft <= 0f) Attack();
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
            int hit = Mathf.Clamp(_comboIndex, 0, Mathf.Max(1, ComboHits) - 1);
            int start = hit * FramesPerHit;

            _cooldownLeft = CurrentCooldown();

            _attackFacing = _walker != null ? _walker.CurrentFacing : Facing.Front;
            if (_walker != null)
            {
                bool invert = FlipOddHits && (hit % 2) == 1;
                _walker.PlayAttack(start, FramesPerHit, invert);
            }

            // 出招的一点视觉反馈（偏冷，和挨打的红色区分开）
            SetTint(new Color(0.75f, 0.88f, 1f));
            _flashLeft = 0.08f;

            // 最后一段是收招，伤害更高
            _pendingDamageMul = (hit == ComboHits - 1) ? LastHitDamageMul : 1f;
            _pendingHitLeft = Mathf.Max(0f, ImpactDelay);

            // 窗口内再按 J 就接下一段，否则回到第一段
            _comboWindowLeft = ComboWindow;
            _comboIndex = (hit + 1) % Mathf.Max(1, ComboHits);
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
            int dmg = Mathf.RoundToInt(CurrentDamage() * Mathf.Max(0.01f, _pendingDamageMul));

            int hitCount = 0;
            var enemies = FindObjectsOfType<Enemy>();
            foreach (var e in enemies)
            {
                if (e == null) continue;
                if (Vector2.Distance((Vector2)e.transform.position, center) > Radius) continue;

                e.TakeDamage(dmg);
                DamageNumber.Spawn(
                    e.transform.position + Vector3.up * (1.1f * PixelArtSettings.CharacterScale),
                    dmg, new Color(1f, 0.92f, 0.55f), PixelArtSettings.CharacterScale);
                hitCount++;
            }

            // 一次攻击只顿一次，打中几只都算一下
            if (hitCount > 0) Punch();
        }

        // 命中反馈：顿帧 + 震屏
        void Punch()
        {
            HitStop.Freeze(0.045f);
            if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.10f, 0.12f);
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

            // 挨打也要有反馈，但震屏更重、不顿帧（顿帧会让挨打显得黏）
            DamageNumber.Spawn(
                transform.position + Vector3.up * (1.35f * PixelArtSettings.CharacterScale),
                actual, new Color(1f, 0.38f, 0.38f), PixelArtSettings.CharacterScale);
            if (CameraFollow.Instance != null) CameraFollow.Instance.Shake(0.14f, 0.16f);

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
