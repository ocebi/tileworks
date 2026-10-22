using PrimeTween;
using Tileworks.Kit.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tileworks.Kit.UI
{
    // Squashes a button while it is held, springs it back on release and clicks.
    [DisallowMultipleComponent]
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private Selectable _selectable;

        private bool _held;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_selectable.IsInteractable()) return;
            _held = true;
            _uiMotion.Press(transform);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_selectable.IsInteractable()) _uiMotion.PlayClick();
        }

        private void OnDisable()
        {
            _held = false;
            Tween.StopAll(transform);
            transform.localScale = Vector3.one;
        }

        private void Release()
        {
            if (!_held) return;
            _held = false;
            _uiMotion.Release(transform);
        }
    }
}
