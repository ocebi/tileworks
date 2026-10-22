using PrimeTween;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using TileMatch.Controller;
using TileMatch.Model;
using UnityEngine;

namespace TileMatch.View
{
    public sealed class BoardView : MonoBehaviour, IBoardView
    {
        [SerializeField, ReadOnlyInInspector] private Camera _camera;
        [SerializeField, ReadOnlyInInspector] private Transform _tilesRoot;
        [SerializeField, ReadOnlyInInspector] private Transform _poolRoot;
        [SerializeField, ReadOnlyInInspector] private TileView _tilePrefab;
        [SerializeField, ReadOnlyInInspector] private TrayView _tray;
        [SerializeField, ReadOnlyInInspector] private TileSkin _skin;
        [SerializeField, ReadOnlyInInspector] private GameFeelConfig _feel;
        [SerializeField, ReadOnlyInInspector] private SfxPlayer _sfx;

        private ViewPool<TileView> _pool;
        private TileView[] _views = new TileView[0];

        public Camera Camera => _camera;
        public TrayView Tray => _tray;
        public TileSkin Skin => _skin;
        public GameFeelConfig Feel => _feel;
        public SfxPlayer Audio => _sfx;
        public LayeredBoardMapper Mapper { get; private set; }
        public TileMatchState State { get; private set; }
        public ModifierSet Modifiers { get; private set; }

        public TileView ViewOf(int tile) => tile >= 0 && tile < _views.Length ? _views[tile] : null;

        public void Show(TileMatchState state, LayeredBoardMapper mapper, ModifierSet modifiers)
        {
            Clear();
            State = state;
            Mapper = mapper;
            Modifiers = modifiers;
            _pool ??= new ViewPool<TileView>(_tilePrefab, _poolRoot);
            _pool.Prewarm(state.TileCount);
            _views = new TileView[state.TileCount];

            for (var tile = 0; tile < state.TileCount; tile++)
            {
                if (state.Locations[tile] == TileLocation.Board) Spawn(tile);
            }

            var bounds = mapper.Bounds;
            _tray.transform.position = new Vector3(bounds.center.x, bounds.yMin - _feel.TrayGap, 0f);
            _tray.SetCapacity(state.TrayCapacity, 0f, 0f);
            var content = Rect.MinMaxRect(Mathf.Min(bounds.xMin, -_tray.Width * 0.5f), bounds.yMin - _feel.TrayGap - 0.7f,
                Mathf.Max(bounds.xMax, _tray.Width * 0.5f), bounds.yMax);
            CameraFitter.Fit(_camera, content, _feel.TopReserved, _feel.BottomReserved, 0.25f);
        }

        public TileView Spawn(int tile)
        {
            var view = _pool.Get(_tilesRoot);
            view.Owner = this;
            view.Tile = tile;
            var slot = State.SlotOf(tile);
            view.BoardPosition = Mapper.ToWorld(slot);
            view.transform.position = view.BoardPosition;
            view.Setup(_skin.Face, _skin.Shadow, IconFor(tile), _skin.IconScale);
            view.SetBoardSorting(Mapper.SortingOrder(slot));
            view.SetTint(State.IsFree(tile) ? Color.white : _skin.CoveredTint);
            view.SetOverlay(OverlayFor(tile));
            _views[tile] = view;
            return view;
        }

        public System.Collections.Generic.IEnumerable<TileView> TrayViews()
        {
            foreach (var view in _views)
            {
                if (view != null && view.State == TileViewState.Tray) yield return view;
            }
        }

        public void Release(TileView view)
        {
            if (view.Tile >= 0 && view.Tile < _views.Length && _views[view.Tile] == view) _views[view.Tile] = null;
            _pool.Release(view);
        }

        public Sprite IconFor(int tile)
        {
            var hidden = (Modifiers.Traits(State, tile) & TileTraits.KindHidden) != 0;
            return hidden ? _skin.HiddenBack : _skin.IconOf(State.Kinds[tile]);
        }

        public void Clear()
        {
            _pool?.ReleaseAll();
            _views = new TileView[0];
        }

        public void Nope(int tile)
        {
            var view = ViewOf(tile);
            if (view == null) return;
            Tween.ShakeLocalPosition(view.transform, new Vector3(_feel.NopeShake, 0f, 0f), _feel.NopeDuration, frequency: 20);
            _sfx.Play(Sfx.Nope);
        }

        private Sprite OverlayFor(int tile)
        {
            foreach (var spec in State.Level.Modifiers)
            {
                if (spec.Tile != tile) continue;
                if (spec.Type == ModifierTypes.Frozen) return _skin.IceOf(Mathf.Max(1, spec.Value));
                if (spec.Type == ModifierTypes.Linked) return _skin.Link;
            }

            return null;
        }
    }
}
