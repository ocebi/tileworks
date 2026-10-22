using PrimeTween;
using Tileworks.Kit.Unity;
using UnityEngine;

namespace TileMatch.View
{
    public enum TileViewState : byte
    {
        Board,
        Flying,
        Tray,
        Leaving,
    }

    public sealed class TileView : MonoBehaviour, IPoolable
    {
        private const int Layers = 4;

        [SerializeField, ReadOnlyInInspector] private SpriteRenderer _shadow;
        [SerializeField, ReadOnlyInInspector] private SpriteRenderer _face;
        [SerializeField, ReadOnlyInInspector] private SpriteRenderer _icon;
        [SerializeField, ReadOnlyInInspector] private SpriteRenderer _overlay;

        private Vector3 _flightFrom;
        private TrayView _flightTray;
        private float _flightArc;
        private float _flightFromScale;
        private float _flightToScale;

        public BoardView Owner { get; set; }
        public int Tile { get; set; } = -1;
        public int TraySlot { get; set; } = -1;
        public TileViewState State { get; set; }
        public Vector3 BoardPosition { get; set; }
        public int BoardSorting { get; private set; }
        public Sprite IconSprite => _icon.sprite;

        public void Setup(Sprite face, Sprite shadow, Sprite icon, float iconScale)
        {
            _face.sprite = face;
            _shadow.sprite = shadow;
            _icon.sprite = icon;
            _icon.transform.localScale = Vector3.one * iconScale;
        }

        public void SetIcon(Sprite icon) => _icon.sprite = icon;

        public Sprite PendingIcon { get; set; }

        public void ApplyPendingIcon() => _icon.sprite = PendingIcon;

        public void ReleaseToOwner()
        {
            if (Owner != null) Owner.Release(this);
        }

        public void SetOverlay(Sprite overlay)
        {
            _overlay.sprite = overlay;
            _overlay.enabled = overlay != null;
            _overlay.color = Color.white;
        }

        public void SetTint(Color tint)
        {
            _face.color = tint;
            _icon.color = tint;
        }

        public Color Tint => _face.color;

        public void SetSorting(int order)
        {
            var baseOrder = order * Layers;
            _shadow.sortingOrder = baseOrder;
            _face.sortingOrder = baseOrder + 1;
            _icon.sortingOrder = baseOrder + 2;
            _overlay.sortingOrder = baseOrder + 3;
        }

        public void SetBoardSorting(int order)
        {
            BoardSorting = order;
            SetSorting(order);
        }

        public void SetShadowVisible(bool visible) => _shadow.enabled = visible;

        public Tween FadeOverlay(float duration, float delay) =>
            Tween.Alpha(_overlay, 0f, duration, startDelay: delay);

        public void BeginFlight(TrayView tray, float arc, float toScale)
        {
            _flightFrom = transform.position;
            _flightTray = tray;
            _flightArc = arc;
            _flightFromScale = transform.localScale.x;
            _flightToScale = toScale;
            State = TileViewState.Flying;
        }

        // Quadratic Bézier toward the slot's current anchor, so a tray shift mid-flight stays continuous.
        public void UpdateFlight(float t)
        {
            var to = _flightTray.AnchorOf(TraySlot);
            var control = (_flightFrom + to) * 0.5f + Vector3.up * _flightArc;
            var u = 1f - t;
            transform.position = u * u * _flightFrom + 2f * u * t * control + t * t * to;
            transform.localScale = Vector3.one * Mathf.Lerp(_flightFromScale, _flightToScale, t);
        }

        public void OnSpawned()
        {
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
            SetTint(Color.white);
            SetOverlay(null);
            _shadow.enabled = true;
            TraySlot = -1;
            State = TileViewState.Board;
        }

        public void OnDespawned()
        {
            Tween.StopAll(transform);
            Tile = -1;
        }
    }
}
