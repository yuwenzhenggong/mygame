using UnityEngine;

namespace XianXia.World
{
    // 相机跟随 + 地图边界夹紧。
    // 不做平滑插值：像素游戏里相机停在半像素上会让整屏抖动，
    // 直接跟随反而最稳（PixelPerfectCamera 还会再吸附一次）。
    public class CameraFollow : MonoBehaviour
    {
        Transform _target;
        MapGrid _grid;
        Camera _cam;

        public void Init(Transform target, MapGrid grid)
        {
            _target = target;
            _grid = grid;
            _cam = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            if (_target == null || _grid == null || _cam == null) return;

            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            float x = _target.position.x;
            float y = _target.position.y;

            // 地图比视野小就居中，否则夹在边界内
            x = _grid.Width <= halfW * 2f ? _grid.Width * 0.5f : Mathf.Clamp(x, halfW, _grid.Width - halfW);
            y = _grid.Height <= halfH * 2f ? _grid.Height * 0.5f : Mathf.Clamp(y, halfH, _grid.Height - halfH);

            transform.position = new Vector3(x, y, -10f);
        }
    }
}
