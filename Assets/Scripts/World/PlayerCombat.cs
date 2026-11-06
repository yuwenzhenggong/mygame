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

        float _cooldownLeft;
        float _invulnLeft;
        float _flashLeft = -1f;
        SpriteRenderer _body;
        bool _dying;

        void Start()
        {
            _body = GetComponentInChildren<SpriteRenderer>();
        }

        void Update()
        {
            if (_dying) return;
            if (GameFlow.IsBlocked) return;

            if (_cooldownLeft > 0f) _cooldownLeft -= Time.deltaTime;

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
            _cooldownLeft = Cooldown;

            // 出招的一点视觉反馈（偏冷，和挨打的红色区分开）
            SetTint(new Color(0.75f, 0.88f, 1f));
            _flashLeft = 0.08f;

            var walker = GetComponent<PlayerWalker>();
            var facing = walker != null ? walker.CurrentFacing : Facing.Front;
            var center = (Vector2)transform.position + FacingDir(facing) * Reach;

            var enemies = FindObjectsOfType<Enemy>();
            foreach (var e in enemies)
            {
                if (e == null) continue;
                if (Vector2.Distance((Vector2)e.transform.position, center) <= Radius)
                    e.TakeDamage(Damage);
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

            var msg = ui.Session.ChangeHp(-amount);
            ui.AppendLog(msg != null ? "<color=#ff6b6b>受到 " + amount + " 点伤害</color>" : "<color=#ff6b6b>受到伤害</color>");
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
