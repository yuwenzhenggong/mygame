using UnityEngine;

namespace XianXia.World
{
    public enum Facing
    {
        Front = 0,   // 朝下（面向镜头）
        Right = 1,
        Back = 2,    // 朝上（背对镜头）
        Left = 3
    }

    // WASD / 方向键走路。不用 Rigidbody2D——这类俯视角走格不需要物理，
    // 自己算 AABB 碰撞反而更稳、更好调。
    public class PlayerWalker : MonoBehaviour
    {
        public float Speed = 5.5f;

        // 脚底碰撞盒的半宽/半高（世界单位）。只测脚下，不测全身，
        // 这样上半身可以「探进」墙里一点，走位手感更好。
        public float HalfWidth = 0.28f;
        public float HalfHeight = 0.16f;

        // 向下跑动的帧时长。10 帧循环，0.06s/帧 → 0.6 秒一个循环
        public float WalkFrameDuration = 0.06f;

        // 普通攻击序列帧时长。21 帧 × 0.025s ≈ 0.525 秒。
        public float AttackFrameDuration = 0.025f;

        SceneData _scene;
        SpriteRenderer _body;

        // 按 Facing 枚举下标存：0=正 1=右 2=背 3=左
        readonly Sprite[] _sprites = new Sprite[4];

        // 向下的跑步动画帧（帧数不定，来自序列帧素材）；没有就退回静态站立图
        Sprite[] _walkDown = new Sprite[0];

        // 普通攻击序列帧。攻击图是 120x120 画布，角色本体仍按 64px 高对齐。
        Sprite[] _attackFrames = new Sprite[0];

        Facing _facing = Facing.Front;
        bool _walking;
        int _walkFrame;
        float _walkTimer;

        bool _attacking;
        int _attackFrame;
        float _attackTimer;
        bool _attackFaceLeft;

        // 角色的"面向"简化成左右两态：只由左右输入决定，上下走时保持上一次的左右。
        // 约定：朝左 = 用原始序列帧；朝右 = 水平翻转。所以默认朝左。
        bool _faceLeft = true;

        const float IdleBodyY = 1f;

        public Facing CurrentFacing => _facing;
        public bool FaceLeft => _faceLeft;
        public bool IsAttacking => _attacking;

        public void Init(SceneData scene)
        {
            _scene = scene;
        }

        public void SetSprites(Sprite front, Sprite right, Sprite back, Sprite left)
        {
            _sprites[(int)Facing.Front] = front;
            _sprites[(int)Facing.Right] = right;
            _sprites[(int)Facing.Back] = back;
            _sprites[(int)Facing.Left] = left;
            ApplySprite();
        }

        public void SetWalkDown(Sprite[] frames)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            if (frames != null)
            {
                foreach (var f in frames)
                    if (f != null) list.Add(f);
            }
            _walkDown = list.ToArray();
            _walkFrame = 0;
            ApplySprite();
        }

        public void SetAttackFrames(Sprite[] frames)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            if (frames != null)
            {
                foreach (var f in frames)
                    if (f != null) list.Add(f);
            }
            _attackFrames = list.ToArray();
            _attackFrame = 0;
            _attackTimer = 0f;
            _attacking = false;
        }

        public void PlayAttack()
        {
            if (_attackFrames.Length == 0) return;

            _attacking = true;
            _attackFrame = 0;
            _attackTimer = 0f;
            // 出招期间锁住左右朝向，避免左右输入让整个刀光中途翻转。
            _attackFaceLeft = _faceLeft;
            ApplySprite();
        }

        bool HasWalkDown => _walkDown.Length > 0;

        void Start()
        {
            if (_body == null) _body = GetComponentInChildren<SpriteRenderer>();
            ApplySprite();
        }

        void Update()
        {
            if (_scene == null) return;

            if (_attacking) AdvanceAttack(Time.deltaTime);

            if (GameFlow.IsBlocked)
            {
                SetIdle();
                return;
            }

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            var dir = new Vector2(h, v);
            if (dir.sqrMagnitude < 0.0001f)
            {
                SetIdle();
                return;
            }

            // 横向输入才改变面向；上下走时沿用上一次的左右
            bool wasLeft = _faceLeft;
            if (Mathf.Abs(h) > 0.01f) _faceLeft = h < 0f;
            if (_faceLeft != wasLeft) ApplySprite();

            UpdateFacing(h, v);
            dir.Normalize();

            var start = (Vector2)transform.position;
            var pos = start;
            var delta = dir * Speed * Time.deltaTime;

            // 两个轴分开走，撞墙时能沿墙滑而不是卡死
            pos = MoveAxis(pos, new Vector2(delta.x, 0f));
            pos = MoveAxis(pos, new Vector2(0f, delta.y));
            transform.position = new Vector3(pos.x, pos.y, 0f);

            // 顶着墙走的时候位置没变，这时候不该播走路动画（否则是原地踏步）
            if ((pos - start).sqrMagnitude > 1e-8f) AdvanceWalk(Time.deltaTime);
            else SetIdle();
        }

        // 竖着走优先用正/背视图，横着走用左右视图
        void UpdateFacing(float h, float v)
        {
            Facing next;
            if (Mathf.Abs(v) >= Mathf.Abs(h))
                next = v > 0f ? Facing.Back : Facing.Front;   // y 增大 = 往屏幕上方走 = 背对镜头
            else
                next = h > 0f ? Facing.Right : Facing.Left;

            if (next == _facing) return;
            _facing = next;
            _walkTimer = 0f;
            ApplySprite();
        }

        void AdvanceWalk(float dt)
        {
            if (!HasWalkDown)
            {
                SetIdle();
                return;
            }

            _walking = true;
            _walkTimer += dt;

            float dur = Mathf.Max(0.02f, WalkFrameDuration);
            while (_walkTimer >= dur)
            {
                _walkTimer -= dur;
                _walkFrame = (_walkFrame + 1) % _walkDown.Length;
            }

            ApplySprite();
        }

        void AdvanceAttack(float dt)
        {
            if (!_attacking) return;

            _attackTimer += dt;
            float frameDuration = Mathf.Max(0.01f, AttackFrameDuration);
            int frame = Mathf.FloorToInt(_attackTimer / frameDuration);

            if (frame >= _attackFrames.Length)
            {
                _attacking = false;
                _attackFrame = 0;
                _attackTimer = 0f;
                ApplySprite();
                return;
            }

            if (frame != _attackFrame)
            {
                _attackFrame = frame;
                ApplySprite();
            }
        }

        void SetIdle()
        {
            if (!_walking && _walkFrame == 0) return;
            _walking = false;
            _walkFrame = 0;
            _walkTimer = 0f;
            ApplySprite();
        }

        void ApplySprite()
        {
            if (_body == null) _body = GetComponentInChildren<SpriteRenderer>();
            if (_body == null) return;

            if (_attacking && _attackFrame >= 0 && _attackFrame < _attackFrames.Length)
            {
                var attackSprite = _attackFrames[_attackFrame];
                if (attackSprite != null)
                {
                    _body.sprite = attackSprite;
                    // 攻击画布更大，把 pivot 中心抬到画布高度的一半，
                    // 让画布底边始终落在玩家脚下，不会因切图跳位。
                    _body.transform.localPosition =
                        new Vector3(0f, attackSprite.bounds.size.y * 0.5f, 0f);
                    _body.flipX = !_attackFaceLeft;
                    return;
                }
            }

            // 有序列帧时：所有方向共用同一套跑步帧，左右靠 flipX 表达
            bool useRun = _walkDown.Length > 0;

            Sprite sprite = null;
            if (useRun && _walking && _walkFrame >= 0 && _walkFrame < _walkDown.Length)
                sprite = _walkDown[_walkFrame];

            if (sprite == null)
            {
                // 静止：有跑步帧就统一用正面站立图（靠翻转表达左右），
                // 没有序列帧时退回原来的四向站立图
                sprite = useRun ? _sprites[(int)Facing.Front] : _sprites[(int)_facing];
            }

            if (sprite != null) _body.sprite = sprite;
            _body.transform.localPosition = new Vector3(0f, IdleBodyY, 0f);
            // 朝左用原图，朝右水平翻转
            _body.flipX = useRun && !_faceLeft;
        }

        Vector2 MoveAxis(Vector2 pos, Vector2 delta)
        {
            if (delta.sqrMagnitude < 1e-8f) return pos;
            var next = pos + delta;
            return Blocks(next) ? pos : next;
        }

        bool Blocks(Vector2 feet)
        {
            return _scene.IsSolidAt(new Vector2(feet.x - HalfWidth, feet.y - HalfHeight)) ||
                   _scene.IsSolidAt(new Vector2(feet.x + HalfWidth, feet.y - HalfHeight)) ||
                   _scene.IsSolidAt(new Vector2(feet.x - HalfWidth, feet.y + HalfHeight)) ||
                   _scene.IsSolidAt(new Vector2(feet.x + HalfWidth, feet.y + HalfHeight));
        }
    }
}
