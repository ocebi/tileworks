using PrimeTween;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworks.Tiles.View
{
    // Shown while a booster is picked: four panels dim the screen around the board and the aim card covers the HUD, each on
    // its own sorting order. A tap on the dim tells the handler to cancel the pick.
    public sealed class BoosterPickOverlay : MonoBehaviour
    {
        private const int DimOrder = 5;

        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private BoosterBarConfig _config;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _dim;
        [SerializeField, ReadOnlyInInspector] private Image[] _dimPanels = new Image[4];
        [SerializeField, ReadOnlyInInspector] private Button[] _dimButtons = new Button[4];
        [SerializeField, ReadOnlyInInspector] private RectTransform _aimCard;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _aimGroup;
        [SerializeField, ReadOnlyInInspector] private Image _aimIcon;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _aimTitle;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _aimLine;
        [SerializeField, ReadOnlyInInspector] private string[] _aimLines = new string[0];
        [SerializeField, ReadOnlyInInspector] private Sprite[] _icons = new Sprite[0];

        private Vector2 _cardHome;
        private Tween _cardMotion;
        private Tween _dimMotion;
        private Tween _aimFade;
        private IBoosterPickHandler _handler;

        // Level 0 is the dim; 1 sits above it (the aim card, a lifted button); 2 above that.
        public static void Raise(Canvas canvas, int level)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = canvas.rootCanvas.sortingOrder + DimOrder + level;
        }

        private void Awake()
        {
            Raise(_dim.GetComponent<Canvas>(), 0);
            Raise(_aimGroup.GetComponent<Canvas>(), 1);
            foreach (var panel in _dimPanels) panel.color = _config.Dim;
            foreach (var button in _dimButtons) button.onClick.AddListener(() => _handler?.OnPickCancelled());
            _cardHome = _aimCard.anchoredPosition;
            _dim.alpha = 0f;
            _dim.blocksRaycasts = false;
            _dim.gameObject.SetActive(false);
            _aimGroup.alpha = 0f;
            _aimGroup.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _cardMotion.Stop();
            _dimMotion.Stop();
            _aimFade.Stop();
        }

        public void Bind(IBoosterPickHandler handler) => _handler = handler;

        public void Show(int slot, string title, Rect boardScreen)
        {
            Stop();
            _dim.gameObject.SetActive(true);
            _dim.blocksRaycasts = true;
            FitDim(boardScreen);
            _aimIcon.sprite = _icons[slot];
            _aimTitle.text = title;
            _aimLine.text = _aimLines[slot];
            _dimMotion = Tween.Alpha(_dim, 1f, Timed(_config.DimFade), useUnscaledTime: true);
            _aimGroup.gameObject.SetActive(true);
            _aimFade = Tween.Alpha(_aimGroup, 1f, Timed(_config.DimFade), useUnscaledTime: true);
            _aimCard.anchoredPosition = new Vector2(_cardHome.x, _cardHome.y + (_uiMotion.Reduced ? 0f : _config.CardDrop));
            _cardMotion = Tween.UIAnchoredPositionY(_aimCard, _cardHome.y, Timed(_config.CardIn), Ease.OutBack, useUnscaledTime: true);
        }

        public void Hide()
        {
            Stop();
            _dim.blocksRaycasts = false;
            _dimMotion = Tween.Alpha(_dim, 0f, Timed(_config.DimFade), useUnscaledTime: true).OnComplete(_dim, group => group.gameObject.SetActive(false));
            _cardMotion = Tween.UIAnchoredPositionY(_aimCard, _cardHome.y + _config.CardDrop, Timed(_config.CardOut), Ease.InQuad, useUnscaledTime: true);
            _aimFade = Tween.Alpha(_aimGroup, 0f, Timed(_config.CardOut), useUnscaledTime: true).OnComplete(_aimGroup, group => group.gameObject.SetActive(false));
        }

        private void Stop()
        {
            _dimMotion.Stop();
            _cardMotion.Stop();
            _aimFade.Stop();
        }

        private float Timed(float seconds) => _uiMotion.Reduced ? 0f : seconds;

        // Top, bottom, left and right panels leave the board's rectangle clear.
        private void FitDim(Rect boardScreen)
        {
            var root = (RectTransform)_dim.transform;
            var canvas = root.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, boardScreen.min, camera, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, boardScreen.max, camera, out var max);
            var r = root.rect;
            min = Vector2.Max(min, r.min);
            max = Vector2.Min(max, r.max);
            Place(0, new Vector2(r.xMin, max.y), r.max);
            Place(1, r.min, new Vector2(r.xMax, min.y));
            Place(2, new Vector2(r.xMin, min.y), new Vector2(min.x, max.y));
            Place(3, new Vector2(max.x, min.y), new Vector2(r.xMax, max.y));
        }

        private void Place(int panel, Vector2 from, Vector2 to)
        {
            var rect = _dimPanels[panel].rectTransform;
            var centre = ((RectTransform)_dim.transform).rect.center;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.Max(Vector2.zero, to - from);
            rect.anchoredPosition = (from + to) * 0.5f - centre;
        }
    }
}
