using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Tileworks.Tiles.View
{
    // Press and hold a button to read what it does. A hold that got as far as the explanation doesn't also count as a tap.
    public sealed class HoldPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Tween _timer;
        private bool _pressed;
        private bool _held;

        public event Action Pressed;
        public event Action Held;
        public event Action Released;

        public float HoldSeconds { get; set; } = 0.35f;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            _held = false;
            Pressed?.Invoke();
            _timer.Stop();
            _timer = Tween.Delay(this, HoldSeconds, hold => hold.OnHeld(), useUnscaledTime: true);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        // True once after a hold, so the click that ends it is ignored.
        public bool ConsumeHold()
        {
            var held = _held;
            _held = false;
            return held;
        }

        private void OnDisable()
        {
            _timer.Stop();
            _pressed = false;
        }

        private void OnHeld()
        {
            if (!_pressed) return;
            _held = true;
            Held?.Invoke();
        }

        private void Release()
        {
            if (!_pressed) return;
            _pressed = false;
            _timer.Stop();
            Released?.Invoke();
        }
    }
}
