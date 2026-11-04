using UnityEngine;

namespace XianXia.World
{
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

        public void Init(MapGrid grid)
        {
            _grid = grid;
        }

        void Start()
        {
            _body = GetComponentInChildren<SpriteRenderer>();
        }

        void Update()
        {
            if (_grid == null) return;
            if (GameFlow.IsBlocked) return;   // 事件面板打开时不许走

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            var dir = new Vector2(h, v);
            if (dir.sqrMagnitude < 0.0001f) return;

            dir.Normalize();

            var delta = dir * Speed * Time.deltaTime;
            var pos = (Vector2)transform.position;

            // 两个轴分开走，撞墙时能沿墙滑而不是卡死
            pos = MoveAxis(pos, new Vector2(delta.x, 0f));
            pos = MoveAxis(pos, new Vector2(0f, delta.y));

            transform.position = new Vector3(pos.x, pos.y, 0f);

            if (_body != null && Mathf.Abs(h) > 0.01f) _body.flipX = h < 0f;
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
