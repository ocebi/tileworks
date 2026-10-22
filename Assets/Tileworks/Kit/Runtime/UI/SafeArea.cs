using UnityEngine;

namespace Tileworks.Kit.UI
{
    // Fits this stretched rect to the screen's safe area, clear of notches and the home indicator.
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private Canvas _canvas;
        private Rect _applied;
        private Vector2 _screen;

        private void OnEnable()
        {
            var parent = GetComponentInParent<Canvas>();
            _canvas = parent != null ? parent.rootCanvas : null;
            Apply();
        }

        // The safe area can change while the canvas keeps its size, so this checks every frame.
        private void Update() => Apply();

        private void Apply()
        {
            var safe = Screen.safeArea;
            // The canvas's size, not Screen.width/height: in the Device Simulator those can be the Editor window's while the
            // safe area is in device pixels.
            var screen = _canvas != null ? _canvas.pixelRect.size : new Vector2(Screen.width, Screen.height);
            if (screen.x <= 0f || screen.y <= 0f || (safe == _applied && screen == _screen)) return;
            _applied = safe;
            _screen = screen;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rect.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
