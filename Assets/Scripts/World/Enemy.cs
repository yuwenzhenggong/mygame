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

        Sprite[] _idleFrames = new Sprite[0];
        Sprite[] _walkFrames = new Sprite[0];
        Sprite[] _attackFrames = new Sprite[0];
        float _visualTime;
        float _attackVisualLeft;
        float _attackImpactLeft = -1f;
        float _walkFrameDuration;
        bool _wasMoving;
        bool _attackFaceRight;
        const float IdleFrameDuration = 0.1f;
        const float AttackFrameDuration = 0.05f;

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

            _idleFrames = LoadAnimation("idle");
            _walkFrames = LoadAnimation("walk");
            _attackFrames = LoadAnimation("attack");
            // 追逐越慢，步频也越低，精英的步态会比普通异兽沉重。
            _walkFrameDuration = 0.035f * 3f / Mathf.Max(0.1f, def.speed);

            BuildBar(sprite, pxWhite);
        }

        Sprite[] LoadAnimation(string action)
        {
            var frames = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < 64; i++)
            {
                var frame = Resources.Load<Sprite>(PixelArtSettings.ArtResourceRoot + Def.sprite + "_" + action + "_" + i);
                if (frame == null) break;
                frames.Add(frame);
            }
            return frames.ToArray();
        }

        void UpdateSpriteAnimation(bool moving, Vector2 direction, float dt)
        {
            if (_body == null || (_idleFrames.Length == 0 && _walkFrames.Length == 0 && _attackFrames.Length == 0)) return;

            if (_attackVisualLeft > 0f && _attackFrames.Length > 0)
            {
                float elapsed = _attackFrames.Length * AttackFrameDuration - _attackVisualLeft;
                int frame = Mathf.Clamp(Mathf.FloorToInt(elapsed / AttackFrameDuration), 0, _attackFrames.Length - 1);
                _body.sprite = _attackFrames[frame];
                _body.flipX = _attackFaceRight;
                _attackVisualLeft = Mathf.Max(0f, _attackVisualLeft - dt);
                _visualTime = 0f;
                return;
            }

            if (moving != _wasMoving) _visualTime = 0f;
            _wasMoving = moving;
            if (Mathf.Abs(direction.x) > 0.01f) _body.flipX = direction.x > 0f;
            var frames = moving && _walkFrames.Length > 0 ? _walkFrames : _idleFrames;
            if (frames.Length == 0) return;
            float duration = moving ? _walkFrameDuration : IdleFrameDuration;
            _body.sprite = frames[Mathf.FloorToInt(_visualTime / duration) % frames.Length];
            _visualTime += dt;
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

            // 扑咬期间停步：先蓄力，画面张嘴前扑时才结算伤害。
            // 玩家在前摇期间离开攻击距离，就能躲过这一击。
            if (_attackVisualLeft > 0f)
            {
                _attackTimer -= Time.deltaTime;
                if (_attackImpactLeft >= 0f)
                {
                    _attackImpactLeft -= Time.deltaTime;
                    if (_attackImpactLeft <= 0f)
                    {
                        _attackImpactLeft = -1f;
                        var target = _player.GetComponent<PlayerCombat>();
                        if (target != null && dist <= Def.attackRange) target.TakeDamage(Def.damage);
                    }
                }
                UpdateSpriteAnimation(false, Vector2.zero, Time.deltaTime);
                return;
            }

            if (dist > Def.aggroRange)
            {
                UpdateSpriteAnimation(false, Vector2.zero, Time.deltaTime);
                return;
            }

            if (dist > Def.attackRange)
            {
                var dir = (playerPos - myPos).normalized;
                var before = transform.position;
                Move(dir * Def.speed * Time.deltaTime);
                UpdateSpriteAnimation((transform.position - before).sqrMagnitude > 1e-8f, dir, Time.deltaTime);
                return;
            }

            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f)
            {
                UpdateSpriteAnimation(false, playerPos - myPos, Time.deltaTime);
                return;
            }

            _attackTimer = Def.attackInterval;
            _attackFaceRight = Mathf.Abs(playerPos.x - myPos.x) > 0.01f ? playerPos.x > myPos.x : _body.flipX;
            _attackVisualLeft = _attackFrames.Length * AttackFrameDuration;
            UpdateSpriteAnimation(false, playerPos - myPos, Time.deltaTime);

            if (_attackFrames.Length > 0)
            {
                _attackImpactLeft = 5f * AttackFrameDuration;
                return;
            }

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
