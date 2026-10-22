using PrimeTween;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworks.Tiles.View
{
    // The Home's level path: a few level tiles on a gentle wave with dots between them, the current level biggest, ringed and breathing,
    // with the mascot peeking out behind it. The scene builder makes the tiles and dots; the view lays them out in its own rect.
    public sealed class LevelPathView : MonoBehaviour, ILevelPathView
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private LevelPathConfig _config;
        [SerializeField, ReadOnlyInInspector] private LevelListSkin _skin;
        [SerializeField, ReadOnlyInInspector] private LevelTile[] _nodes = new LevelTile[0];
        [SerializeField, ReadOnlyInInspector] private Image[] _dots = new Image[0];
        [SerializeField, ReadOnlyInInspector, Tooltip("Optional; peeks out from behind the current node.")] private RectTransform _mascot;

        private ILevelPathHandler _handler;
        private ILevelBadges _badges;
        private int _first;
        private int _count;
        private int _current;

        private RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                var slot = i;
                _nodes[i].Button.onClick.AddListener(() => OnTapped(slot));
                _nodes[i].gameObject.SetActive(false);
            }

            foreach (var dot in _dots) dot.gameObject.SetActive(false);
        }

        public void Bind(ILevelPathHandler handler) => _handler = handler;

        public void Show(int first, int count, int current, ILevelBadges badges)
        {
            _first = first;
            _count = Mathf.Clamp(count, 0, _nodes.Length);
            _current = current;
            _badges = badges;
            Layout();
            for (var i = 0; i < _count; i++) _uiMotion.PopIn(_nodes[i].Body, _uiMotion.StaggerDelay(i));
        }

        public void RefuseLocked(int index)
        {
            var slot = index - _first;
            if (slot >= 0 && slot < _count) _uiMotion.Shake(_nodes[slot].Body);
        }

        // A rotation or a resized browser window changes the width the path spans.
        private void OnRectTransformDimensionsChange()
        {
            if (_count > 0) Layout();
        }

        private void Layout()
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                if (i >= _count)
                {
                    StopMotion(node);
                    node.gameObject.SetActive(false);
                    continue;
                }

                var index = _first + i;
                var state = StateOf(i);
                node.gameObject.SetActive(true);
                node.Show(index + 1, state, _skin, _badges?.BadgeOf(index) ?? LevelBadge.None);
                node.Highlight(state == LevelTileState.Current);
                ((RectTransform)node.transform).anchoredPosition = PositionOf(i);
                Idle(node, state);
            }

            var dot = 0;
            for (var gap = 0; gap < _count - 1; gap++)
            {
                var cleared = _first + gap + 1 <= _current;
                var from = Edge(gap, gap + 1);
                var to = Edge(gap + 1, gap);
                for (var k = 0; k < _config.DotsPerGap && dot < _dots.Length; k++, dot++)
                {
                    var image = _dots[dot];
                    image.gameObject.SetActive(true);
                    image.color = cleared ? _config.ClearedDot : _config.LockedDot;
                    image.rectTransform.sizeDelta = Vector2.one * _config.DotSize;
                    image.rectTransform.anchoredPosition = Vector2.Lerp(from, to, (k + 1f) / (_config.DotsPerGap + 1f));
                }
            }

            for (; dot < _dots.Length; dot++) _dots[dot].gameObject.SetActive(false);

            var currentSlot = _current - _first;
            if (_mascot != null && currentSlot >= 0 && currentSlot < _count) _mascot.anchoredPosition = PositionOf(currentSlot) + _config.MascotOffset;
        }

        // Where the dots towards `towards` start: just outside the node's edge, so none hide under it.
        private Vector2 Edge(int slot, int towards)
        {
            var node = (RectTransform)_nodes[slot].transform;
            var centre = PositionOf(slot);
            var radius = node.rect.height * 0.5f * ScaleOf(StateOf(slot)) + _config.DotSize;
            return centre + (PositionOf(towards) - centre).normalized * radius;
        }

        private LevelTileState StateOf(int slot)
        {
            var index = _first + slot;
            return index < _current ? LevelTileState.Cleared : index == _current ? LevelTileState.Current : LevelTileState.Locked;
        }

        private float ScaleOf(LevelTileState state) =>
            _config.NodeScale * (state == LevelTileState.Current ? _config.CurrentScale : state == LevelTileState.Locked ? _config.LockedScale : 1f);

        // Nodes sit a step apart around the rect's middle and rise and fall on a wave centred on the middle node.
        private Vector2 PositionOf(float slot)
        {
            var width = Rect.rect.width * (1f - 2f * _config.EndMargin);
            var step = _count > 1 ? Mathf.Min(_config.Step, width / (_count - 1)) : 0f;
            var middle = (_count - 1) * 0.5f;
            return new Vector2(step * (slot - middle), _config.Wave * Mathf.Sin((slot - middle) * _config.WaveStep * Mathf.Deg2Rad));
        }

        // The current node is bigger and breathes; locked ones are smaller.
        private void Idle(LevelTile node, LevelTileState state)
        {
            var target = node.transform;
            Tween.StopAll(target);
            var scale = ScaleOf(state);
            target.localScale = Vector3.one * scale;
            if (state != LevelTileState.Current || _uiMotion.Reduced) return;
            Tween.Scale(target, scale, scale * _config.BreatheScale, _config.BreathePeriod * 0.5f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo,
                useUnscaledTime: true);
        }

        private void OnTapped(int slot)
        {
            if (slot < _count) _handler?.OnNodeTapped(_first + slot);
        }

        private static void StopMotion(LevelTile node)
        {
            Tween.StopAll(node.transform);
            Tween.StopAll(node.Body);
            node.transform.localScale = Vector3.one;
            node.Body.localScale = Vector3.one;
        }

        private void OnDestroy()
        {
            foreach (var node in _nodes)
            {
                if (node == null) continue;
                Tween.StopAll(node.transform);
                Tween.StopAll(node.Body);
            }
        }
    }
}
