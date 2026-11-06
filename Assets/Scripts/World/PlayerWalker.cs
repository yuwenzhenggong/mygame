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

        // 向下走路的帧时长。0.12s ≈ 8 帧/秒，跑得快时腿也该迈得勤
        public float WalkFrameDuration = 0.12f;

        SceneData _scene;
        SpriteRenderer _body;

        // 按 Facing 枚举下标存：0=正 1=右 2=背 3=左
        readonly Sprite[] _sprites = new Sprite[4];

        // 向下的 4 帧走路动画；没有就退回静态站立图
        readonly Sprite[] _walkDown = new Sprite[4];

        Facing _facing = Facing.Front;
        bool _walking;
        int _walkFrame;
        float _walkTimer;

        public Facing CurrentFacing => _facing;

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
            for (int i = 0; i < _walkDown.Length; i++)
                _walkDown[i] = (frames != null && i < frames.Length) ? frames[i] : null;
            ApplySprite();
        }

        bool HasWalkDown => _walkDown[0] != null;

        void Start()
        {
            if (_body == null) _body = GetComponentInChildren<SpriteRenderer>();
            ApplySprite();
        }

        void Update()
        {
            if (_scene == null) return;

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

            Sprite sprite = null;

            // 只有「向下走」有动画帧，其它方向还是静态站立图
            if (_walking && _facing == Facing.Front && _walkDown[_walkFrame] != null)
                sprite = _walkDown[_walkFrame];

            if (sprite == null) sprite = _sprites[(int)_facing];
            if (sprite != null) _body.sprite = sprite;
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
