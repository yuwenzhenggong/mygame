using UnityEngine;

namespace XianXia.World
{
    // 相机跟随 + 场景边界夹紧。
    // 不做平滑插值：像素游戏里相机停在半像素上会让整屏抖动，
    // 直接跟随反而最稳（PixelPerfectCamera 还会再吸附一次）。
    public class CameraFollow : MonoBehaviour
    {
        Transform _target;
        SceneData _scene;
        Camera _cam;

        public void Init(Transform target, SceneData scene)
        {
            _target = target;
            _scene = scene;
            _cam = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            if (_target == null || _scene == null || _cam == null) return;

            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            float x = _target.position.x;
            float y = _target.position.y;

            // 场景比视野小就居中，否则夹在边界内
            x = _scene.WorldWidth <= halfW * 2f ? _scene.WorldWidth * 0.5f : Mathf.Clamp(x, halfW, _scene.WorldWidth - halfW);
            y = _scene.WorldHeight <= halfH * 2f ? _scene.WorldHeight * 0.5f : Mathf.Clamp(y, halfH, _scene.WorldHeight - halfH);

            transform.position = new Vector3(x, y, -10f);
        }
    }
}
