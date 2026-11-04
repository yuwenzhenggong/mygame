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
        public float Speed = 3.5f;

        // 脚底碰撞盒的半宽/半高（世界单位）。只测脚下，不测全身，
        // 这样上半身可以「探进」墙里一点，走位手感更好。
        public float HalfWidth = 0.28f;
        public float HalfHeight = 0.16f;

        MapGrid _grid;
        SpriteRenderer _body;

        // 按 Facing 枚举下标存：0=正 1=右 2=背 3=左
        readonly Sprite[] _sprites = new Sprite[4];
        Facing _facing = Facing.Front;

        public Facing CurrentFacing => _facing;

        public void Init(MapGrid grid)
        {
            _grid = grid;
        }

        public void SetSprites(Sprite front, Sprite right, Sprite back, Sprite left)
        {
            _sprites[(int)Facing.Front] = front;
            _sprites[(int)Facing.Right] = right;
            _sprites[(int)Facing.Back] = back;
            _sprites[(int)Facing.Left] = left;
            ApplySprite();
        }

        void Start()
        {
            if (_body == null) _body = GetComponentInChildren<SpriteRenderer>();
            ApplySprite();
        }

        void Update()
        {
            if (_grid == null) return;
            if (GameFlow.IsBlocked) return;   // 事件面板打开时不许走

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            var dir = new Vector2(h, v);
            if (dir.sqrMagnitude < 0.0001f) return;

            UpdateFacing(h, v);
            dir.Normalize();

            var delta = dir * Speed * Time.deltaTime;
            var pos = (Vector2)transform.position;

            // 两个轴分开走，撞墙时能沿墙滑而不是卡死
            pos = MoveAxis(pos, new Vector2(delta.x, 0f));
            pos = MoveAxis(pos, new Vector2(0f, delta.y));

            transform.position = new Vector3(pos.x, pos.y, 0f);
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
            ApplySprite();
        }

        void ApplySprite()
        {
            if (_body == null) _body = GetComponentInChildren<SpriteRenderer>();
            if (_body == null) return;

            var sprite = _sprites[(int)_facing];
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
            return _grid.IsSolidAt(new Vector2(feet.x - HalfWidth, feet.y - HalfHeight)) ||
                   _grid.IsSolidAt(new Vector2(feet.x + HalfWidth, feet.y - HalfHeight)) ||
                   _grid.IsSolidAt(new Vector2(feet.x - HalfWidth, feet.y + HalfHeight)) ||
                   _grid.IsSolidAt(new Vector2(feet.x + HalfWidth, feet.y + HalfHeight));
        }
    }
}
