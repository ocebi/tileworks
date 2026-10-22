using PrimeTween;
using Tileworks.Kit.Unity;
using UnityEngine;

namespace TileMatch.View
{
    public sealed class TrayView : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private SpriteRenderer _background;
        [SerializeField, ReadOnlyInInspector] private GameFeelConfig _feel;

        public int Capacity { get; private set; } = 7;
        public float ClearBusyUntil { get; set; }
        public float SlotSpacing => _feel.TraySlotSpacing;

        public Vector3 AnchorOf(int slot)
        {
            var offset = (slot - (Capacity - 1) * 0.5f) * _feel.TraySlotSpacing;
            return transform.position + new Vector3(offset, 0f, 0f);
        }

        public float Width => Capacity * _feel.TraySlotSpacing + _feel.TrayPadding * 2f;

        public void SetCapacity(int capacity, float duration, float delay)
        {
            Capacity = capacity;
            var size = new Vector2(Width, _feel.TrayHeight);
            if (duration <= 0f) _background.size = size;
            else Tween.Custom(this, _background.size, size, duration, (view, value) => view._background.size = value, startDelay: delay);
        }
    }
}
