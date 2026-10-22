using System;
using System.Collections.Generic;
using PrimeTween;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworks.Tiles.View
{
    // The level list popup: a scrolling grid of tiles, the progress so far, and one button that plays the selected level.
    // Only the rows on screen have tiles; they are rebound as the list scrolls, so it stays cheap with thousands of levels.
    public sealed class LevelListView : MonoBehaviour, ILevelListView
    {
        private const int BufferRows = 1;

        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private LevelListSkin _skin;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _group;
        [SerializeField, ReadOnlyInInspector] private RectTransform _panel;
        [SerializeField, ReadOnlyInInspector] private Button[] _openers = new Button[0];
        [SerializeField, ReadOnlyInInspector] private Button _close;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _progress;
        [SerializeField, ReadOnlyInInspector] private RectTransform _progressFill;
        [SerializeField, ReadOnlyInInspector] private ScrollRect _scroll;
        [SerializeField, ReadOnlyInInspector, Tooltip("Supplies the cell size, spacing, padding and columns; the view places the tiles itself.")]
        private GridLayoutGroup _grid;
        [SerializeField, ReadOnlyInInspector] private LevelTile _template;
        [SerializeField, ReadOnlyInInspector] private Button _play;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _playLabel;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _caption;

        private readonly List<LevelTile> _tiles = new List<LevelTile>();
        private readonly List<int> _shown = new List<int>();
        private GridWindow _layout;
        private PopupMotion _popup;
        private int _count;
        private ILevelBadges _badges;
        private int _current;
        private int _selected = -1;
        private int _first = -1;

        public event Action OpenRequested;
        public event Action<int> TileClicked;
        public event Action PlayClicked;

        public bool IsOpen => _popup.IsOpen;

        private RectTransform Content => (RectTransform)_grid.transform;
        private float ViewportHeight => _scroll.viewport.rect.height;

        private readonly TextNumbers _text = new TextNumbers();

        private void Awake()
        {
            _popup = new PopupMotion(_uiMotion, _group, _panel);
            _popup.HideNow();
            _template.gameObject.SetActive(false);
            _layout = new GridWindow(_grid.constraintCount, _grid.cellSize, _grid.spacing, _grid.padding);
            _grid.enabled = false;
            if (_grid.TryGetComponent(out ContentSizeFitter fitter)) fitter.enabled = false;
            _scroll.onValueChanged.AddListener(_ => Refresh(false));
            foreach (var opener in _openers) opener.onClick.AddListener(() => OpenRequested?.Invoke());
            _close.onClick.AddListener(Close);
            _play.onClick.AddListener(() => PlayClicked?.Invoke());
        }

        public void SetBadges(ILevelBadges badges) => _badges = badges;

        public void Open(int count, int current, int selected, bool playable)
        {
            _count = count;
            _current = current;
            Content.sizeDelta = new Vector2(Content.sizeDelta.x, _layout.ContentHeight(count));
            var cleared = Mathf.Min(current, count);
            _text.Clear().Append(cleared).Append(" of ").Append(count).Append(" cleared").Apply(_progress);
            _progressFill.anchorMax = new Vector2(count > 0 ? (float)cleared / count : 0f, 1f);
            _selected = -1;
            ScrollTo(selected);
            Select(selected, playable);
            _popup.Open();
            Stagger();
        }

        // The rows on screen pop in one after another; the current tile breathes again once they have settled.
        private void Stagger()
        {
            var rows = 0;
            for (var slot = 0; slot < _tiles.Count; slot++)
            {
                var index = _shown[slot];
                if (index < 0) continue;
                var row = (index - _first) / _grid.constraintCount;
                rows = Mathf.Max(rows, row + 1);
                _uiMotion.PopIn(_tiles[slot].transform, _uiMotion.StaggerDelay(row));
            }

            if (_uiMotion.Reduced) BreatheCurrent();
            else Tween.Delay(this, _uiMotion.StaggerDelay(rows) + _uiMotion.Config.PopDuration, static (LevelListView view) => view.BreatheCurrent(), useUnscaledTime: true);
        }

        private void BreatheCurrent()
        {
            var tile = TileOf(_current);
            if (tile != null && IsOpen) _uiMotion.Breathe(tile.transform);
        }

        public void Select(int index, bool playable)
        {
            var previous = TileOf(_selected);
            if (previous != null) Emphasise(previous, false);
            _selected = index;
            var tile = TileOf(index);
            if (tile != null) Emphasise(tile, true);
            var number = index + 1;
            var replay = index < _current;
            _text.Set(_playLabel, replay ? "Replay Level " : "Play Level ", number);
            _play.interactable = playable;
            if (replay) Caption("Replay for fun: no coins, no lives lost.", false);
            else if (playable) Caption("Your next level", false);
            else Caption("Out of lives. Cleared levels are free to replay.", true);
        }

        public void RefuseLocked(int index)
        {
            var tile = TileOf(index);
            if (tile != null) _uiMotion.Shake(tile.Body);
            Caption($"Level {index + 1} is locked. Beat Level {_current + 1} first.", true);
        }

        public void Close() => _popup.Close();

        // A rotation or a resized browser window changes how many rows fit.
        private void OnRectTransformDimensionsChange()
        {
            if (_layout != null && _count > 0) Refresh(true);
        }

        private void Refresh(bool force)
        {
            var (first, end) = _layout.Visible(_count, Content.anchoredPosition.y, ViewportHeight, BufferRows);
            while (_tiles.Count < end - first) AddTile();
            if (!force && first == _first) return;
            _first = first;

            var free = new List<int>();
            for (var slot = 0; slot < _tiles.Count; slot++)
            {
                var index = _shown[slot];
                if (force || index < first || index >= end) free.Add(slot);
            }

            var next = 0;
            for (var index = first; index < end && next < free.Count; index++)
            {
                if (!force && _shown.IndexOf(index) >= 0) continue;
                Bind(free[next++], index);
            }

            for (; next < free.Count; next++) Unbind(free[next]);
        }

        private void Bind(int slot, int index)
        {
            var tile = _tiles[slot];
            StopMotion(tile);
            _shown[slot] = index;
            tile.gameObject.SetActive(true);
            ((RectTransform)tile.transform).anchoredPosition = _layout.PositionOf(index, Content.rect.width);
            tile.Show(index + 1, index < _current ? LevelTileState.Cleared : index == _current ? LevelTileState.Current : LevelTileState.Locked, _skin,
                _badges?.BadgeOf(index) ?? LevelBadge.None);
            tile.Highlight(index == _selected);
            tile.Body.localScale = Vector3.one * (index == _selected ? _skin.SelectedScale : 1f);
            if (index == _current) _uiMotion.Breathe(tile.transform);
        }

        private void Unbind(int slot)
        {
            StopMotion(_tiles[slot]);
            _shown[slot] = -1;
            _tiles[slot].gameObject.SetActive(false);
        }

        private static void StopMotion(LevelTile tile)
        {
            UiMotion.StopBreathing(tile.transform);
            Tween.StopAll(tile.Body);
            tile.Body.localScale = Vector3.one;
        }

        private LevelTile TileOf(int index)
        {
            var slot = index >= 0 ? _shown.IndexOf(index) : -1;
            return slot >= 0 ? _tiles[slot] : null;
        }

        private void AddTile()
        {
            var tile = Instantiate(_template, Content);
            var rect = (RectTransform)tile.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = _grid.cellSize;
            var slot = _tiles.Count;
            tile.Button.onClick.AddListener(() =>
            {
                if (_shown[slot] >= 0) TileClicked?.Invoke(_shown[slot]);
            });
            _tiles.Add(tile);
            _shown.Add(-1);
        }

        private void Emphasise(LevelTile tile, bool selected)
        {
            tile.Highlight(selected);
            Tween.StopAll(tile.Body);
            var scale = Vector3.one * (selected ? _skin.SelectedScale : 1f);
            if (_uiMotion.Reduced || !IsOpen) tile.Body.localScale = scale;
            else Tween.Scale(tile.Body, scale, _skin.SelectDuration, Ease.OutBack, useUnscaledTime: true);
        }

        private void Caption(string text, bool refusal)
        {
            _caption.text = text;
            _caption.color = refusal ? _skin.Refusal : _skin.Caption;
        }

        // Puts the row holding `index` in the middle of the well.
        private void ScrollTo(int index)
        {
            _scroll.StopMovement();
            Content.anchoredPosition = new Vector2(Content.anchoredPosition.x, _layout.ScrollToCentre(index, _count, ViewportHeight));
            Refresh(true);
        }

        // Only stops tweens: on a scene unload the tiles may already be destroyed, so nothing is reset.
        private void OnDestroy()
        {
            foreach (var tile in _tiles)
            {
                Tween.StopAll(tile.Body);
                if (tile != null) Tween.StopAll(tile.transform);
            }
        }
    }
}
