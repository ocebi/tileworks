using Tileworks.Kit.Unity;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    // Fits the board camera to the board, clear of the HUD above and the booster bar below, and again whenever the screen
    // or its safe area changes.
    public sealed class BoardCameraFit : MonoBehaviour
    {
        private const float BarGap = 0.01f;

        [SerializeField, ReadOnlyInInspector] private Camera _camera;
        [SerializeField, ReadOnlyInInspector] private BoosterBarView _boosters;

        private Rect _content;
        private float _top;
        private float _bottom;
        private float _padding;
        private bool _fitted;
        private Vector2Int _screen;
        private Rect _safe;
        private float _applied;

        public void Fit(Rect content, float topReserved, float bottomReserved, float padding)
        {
            (_content, _top, _bottom, _padding, _fitted) = (content, topReserved, bottomReserved, padding, true);
            Apply();
        }

        private void LateUpdate()
        {
            if (_fitted && (Pixels != _screen || Screen.safeArea != _safe || Bottom() != _applied)) Apply();
        }

        // The camera's own size, since in the Editor Screen.width/height can belong to another window.
        private Vector2Int Pixels => new Vector2Int(_camera.pixelWidth, _camera.pixelHeight);

        private float Bottom() => _boosters == null ? _bottom : Mathf.Max(_bottom, _boosters.ScreenTop / Mathf.Max(1, _camera.pixelHeight) + BarGap);

        private void Apply()
        {
            (_screen, _safe, _applied) = (Pixels, Screen.safeArea, Bottom());
            CameraFitter.Fit(_camera, _content, _top, _applied, _padding);
        }
    }
}
