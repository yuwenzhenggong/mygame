using UnityEngine;

namespace XianXia.World
{
    // 地上的掉落物：上下浮动，玩家靠近自动拾取。
    public class Drop : MonoBehaviour
    {
        public enum DropKind { Resonance, Material }

        public DropKind Kind;
        public int Amount = 1;

        Transform _player;
        Vector3 _basePos;
        float _phase;
        bool _taken;

        public void Init(DropKind kind, int amount, Transform player, float phase)
        {
            Kind = kind;
            Amount = amount;
            _player = player;
            _basePos = transform.position;
            _phase = phase;
        }

        void Update()
        {
            // 上下浮动，让掉落物在杂乱背景里更容易被看到
            _phase += Time.deltaTime * 3f;
            transform.position = _basePos + new Vector3(0f, Mathf.Sin(_phase) * 0.08f, 0f);

            if (_taken || _player == null) return;
            if (Vector2.Distance(transform.position, _player.position) > 0.9f) return;

            var ui = UI.GameUI.Instance;
            if (ui == null || ui.Session == null) return;

            _taken = true;

            var msg = ui.Session.CollectDrop(Kind == DropKind.Resonance, Amount);
            if (!string.IsNullOrEmpty(msg)) ui.AppendLog(msg);
            ui.RefreshHud();

            Destroy(gameObject);
        }
    }
}
