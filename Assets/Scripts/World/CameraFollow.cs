using UnityEngine;

namespace XianXia.World
{
    // 相机跟随 + 场景边界夹紧。
    // 不做平滑插值：像素游戏里相机停在半像素上会让整屏抖动，
    // 直接跟随反而最稳（PixelPerfectCamera 还会再吸附一次）。
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance;

        Transform _target;
        SceneData _scene;
        Camera _cam;

        float _shakeAmount;
        float _shakeDuration;
        float _shakeLeft;

        public void Init(Transform target, SceneData scene)
        {
            _target = target;
            _scene = scene;
            _cam = GetComponent<Camera>();
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 震屏：amount 是世界单位（0.1 约等于 3 个屏幕像素），duration 是秒
        public void Shake(float amount, float duration)
        {
            _shakeAmount = Mathf.Max(_shakeAmount, amount);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
            _shakeLeft = Mathf.Max(_shakeLeft, duration);
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

            if (_shakeLeft > 0f)
            {
                // 用不受 timeScale 影响的时间，顿帧期间震屏照常播放
                _shakeLeft -= Time.unscaledDeltaTime;
                float k = _shakeDuration > 0f ? Mathf.Clamp01(_shakeLeft / _shakeDuration) : 0f;
                x += Random.Range(-1f, 1f) * _shakeAmount * k;
                y += Random.Range(-1f, 1f) * _shakeAmount * k;

                if (_shakeLeft <= 0f) { _shakeAmount = 0f; _shakeDuration = 0f; }
            }

            transform.position = new Vector3(x, y, -10f);
        }
    }
}
