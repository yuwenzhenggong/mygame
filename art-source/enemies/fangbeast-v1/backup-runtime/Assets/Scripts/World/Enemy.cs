using UnityEngine;
using XianXia.Data;

namespace XianXia.World
{
    // 一个敌人：直线追玩家，靠近就按间隔打，挨打闪红，血没了就死。
    // 不做绕路、不做巡逻路径、不做群体协作——按策划案的"复杂 AI 明确不做"。
    public class Enemy : MonoBehaviour
    {
        public EnemyDef Def;
        public int Hp;
        public string SpawnId;   // 对应 enemies.json 里的刷怪点 id，用来记录已击杀

        public System.Action<Enemy> OnDeath;

        SceneData _scene;
        Transform _player;
        SpriteRenderer _body;
        Transform _barFill;
        float _barWidthWorld;

        float _attackTimer;
        float _flashTimer = -1f;
        float _revealLeft;
        bool _dead;

        // 灵视显形：持续时间内给个偏冷的高亮
        public void Highlight(float seconds)
        {
            if (seconds > _revealLeft) _revealLeft = seconds;
        }

        // 被击退：逐轴推进并检查遮挡，避免推进墙里
        public void Push(Vector3 delta)
        {
            if (_dead || _scene == null) return;

            var cur = (Vector2)transform.position;
            var want = cur + (Vector2)delta;

            if (!_scene.IsSolidAt(new Vector2(want.x, cur.y)))
                transform.position = new Vector3(want.x, cur.y, transform.position.z);

            cur = (Vector2)transform.position;
            if (!_scene.IsSolidAt(new Vector2(cur.x, want.y)))
                transform.position = new Vector3(cur.x, want.y, transform.position.z);
        }

        // 按「击退度」折算后再推。force 是基础击退距离（世界单位），
        // resist=0 全额吃下，resist=1 完全免疫。
        public void ApplyKnockback(Vector3 dir, float force)
        {
            if (_dead || force <= 0f) return;

            float resist = Def != null ? Mathf.Clamp01(Def.knockbackResist) : 0f;
            float amount = force * (1f - resist);
            if (amount <= 0.001f) return;

            var d = new Vector3(dir.x, dir.y, 0f);
            if (d.sqrMagnitude < 0.0001f) return;
            Push(d.normalized * amount);
        }

        const float BarWidth = 0.9f;
        const float BarHeight = 0.1f;

        public void Init(EnemyDef def, SceneData scene, Transform player, Sprite sprite, Sprite pxWhite)
        {
            Def = def;
            _scene = scene;
            _player = player;
            Hp = Mathf.Max(1, def.hp);

            // 身体：pivot 在中心，往上抬半个精灵高度让脚底落在 transform 上
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.sprite = sprite;
            _body.sortingOrder = 9;
            if (sprite != null)
                bodyGo.transform.localPosition = new Vector3(0f, sprite.bounds.size.y * 0.5f, 0f);

            BuildBar(sprite, pxWhite);
        }

        void BuildBar(Sprite sprite, Sprite pxWhite)
        {
            if (pxWhite == null) return;

            float top = sprite != null ? sprite.bounds.size.y + 0.12f : 1.2f;
            float px = PixelArtSettings.PixelsPerUnit;
            _barWidthWorld = BarWidth;

            var root = new GameObject("Bar");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, top, 0f);

            var bg = new GameObject("BarBg");
            bg.transform.SetParent(root.transform, false);
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = pxWhite;
            bgSr.color = new Color(0.05f, 0.05f, 0.07f, 0.85f);
            bgSr.sortingOrder = 21;
            bg.transform.localScale = new Vector3(BarWidth * px, BarHeight * px, 1f);

            var fill = new GameObject("BarFill");
            fill.transform.SetParent(root.transform, false);
            var fillSr = fill.AddComponent<SpriteRenderer>();
            fillSr.sprite = pxWhite;
            fillSr.color = new Color(0.88f, 0.29f, 0.29f, 1f);
            fillSr.sortingOrder = 22;
            fill.transform.localScale = new Vector3(BarWidth * px, BarHeight * px, 1f);

            _barFill = fill.transform;
            UpdateBar();
        }

        void UpdateBar()
        {
            if (_barFill == null || Def == null) return;

            float ratio = Mathf.Clamp01(Hp / (float)Mathf.Max(1, Def.hp));
            float px = PixelArtSettings.PixelsPerUnit;

            _barFill.localScale = new Vector3(_barWidthWorld * ratio * px, BarHeight * px, 1f);
            // 从左边开始缩短
            _barFill.localPosition = new Vector3(-_barWidthWorld * 0.5f * (1f - ratio), 0f, 0f);
        }

        void Update()
        {
            if (_dead || _player == null || _scene == null) return;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _body != null && _revealLeft <= 0f) _body.color = Color.white;
            }

            // 灵视显形：没在闪红的时候给个偏冷的高亮
            if (_revealLeft > 0f)
            {
                _revealLeft -= Time.deltaTime;
                if (_body != null && _flashTimer <= 0f)
                    _body.color = _revealLeft > 0f ? new Color(0.72f, 0.95f, 1f, 1f) : Color.white;
            }

            // 事件面板开着时，敌人也停手
            if (GameFlow.IsBlocked) return;

            var myPos = (Vector2)transform.position;
            var playerPos = (Vector2)_player.position;
            float dist = Vector2.Distance(myPos, playerPos);

            if (dist > Def.aggroRange) return;

            if (dist > Def.attackRange)
            {
                var dir = (playerPos - myPos).normalized;
                Move(dir * Def.speed * Time.deltaTime);
                return;
            }

            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f) return;

            _attackTimer = Def.attackInterval;

            var combat = _player.GetComponent<PlayerCombat>();
            if (combat != null) combat.TakeDamage(Def.damage);
        }

        void Move(Vector2 delta)
        {
            var pos = (Vector2)transform.position;
            pos = MoveAxis(pos, new Vector2(delta.x, 0f));
            pos = MoveAxis(pos, new Vector2(0f, delta.y));
            transform.position = new Vector3(pos.x, pos.y, 0f);
        }

        Vector2 MoveAxis(Vector2 pos, Vector2 delta)
        {
            if (Mathf.Abs(delta.x) < 1e-6f && Mathf.Abs(delta.y) < 1e-6f) return pos;
            var next = pos + delta;
            return SolidAt(next) ? pos : next;
        }

        // 用身体中段做碰撞，比脚底宽松一点，避免和玩家互相卡住
        bool SolidAt(Vector2 pos)
        {
            const float half = 0.26f;
            return _scene.IsSolidAt(new Vector2(pos.x - half, pos.y)) ||
                   _scene.IsSolidAt(new Vector2(pos.x + half, pos.y)) ||
                   _scene.IsSolidAt(new Vector2(pos.x, pos.y - half)) ||
                   _scene.IsSolidAt(new Vector2(pos.x, pos.y + half));
        }

        public void TakeDamage(int amount)
        {
            if (_dead) return;

            Hp -= amount;
            _flashTimer = 0.12f;
            if (_body != null) _body.color = new Color(1f, 0.35f, 0.35f, 1f);
            UpdateBar();

            if (Hp <= 0) Die();
        }

        void Die()
        {
            _dead = true;
            if (OnDeath != null) OnDeath(this);
            Destroy(gameObject);
        }
    }
}
