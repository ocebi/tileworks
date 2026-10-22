using System.Collections.Generic;
using Blast.Controller;
using Blast.Model;
using PrimeTween;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Blast.View
{
    // Cells are one world unit; row 0 is the top row. Each piece is a pooled sprite found by its model id.
    public sealed class BlastBoardView : MonoBehaviour, IBlastBoardView, IBoosterFxBoard, ILandingSound
    {
        private const int RimOrder = 0;
        private const int FillOrder = 1;
        private const int CellOrder = 2;
        private const int PieceOrder = 10;
        private const int AimOrder = 40;
        private const int FlashOrder = 50;
        private const int MovingOrder = 60;

        [SerializeField, ReadOnlyInInspector] private Camera _camera;
        [SerializeField, ReadOnlyInInspector] private BlastSkin _skin;
        [SerializeField, ReadOnlyInInspector] private BlastFeelConfig _feel;
        [SerializeField, ReadOnlyInInspector] private BlastSfx _sfx;
        [SerializeField, ReadOnlyInInspector] private BoosterFxConfig _boosterFx;
        [SerializeField, ReadOnlyInInspector] private BoardCameraFit _fit;
        [SerializeField, ReadOnlyInInspector] private BoardFx _effects;

        private readonly Dictionary<int, SpriteRenderer> _pieces = new Dictionary<int, SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private readonly List<Transform> _hinted = new List<Transform>();
        private readonly List<SpriteRenderer> _movable = new List<SpriteRenderer>();
        private Transform _content;
        private BlastState _state;
        private Vector2 _origin;
        private BlastBonus _bonus;
        private SpriteRenderer _aim;
        private IBoardLandingHandler _landings;
        private PresentationClock _clock;
        private float _effectsUntil;
        private int _originCell = -1;

        public Camera Camera => _camera;
        public BlastSkin Skin => _skin;
        public BlastFeelConfig Feel => _feel;
        public BlastSfx Sfx => _sfx;
        public BoosterFxConfig BoosterFx => _boosterFx;
        public BoardFx Effects => _effects;
        public Transform Root => transform;
        public Sprite FlashSprite => _skin.Flash;
        public Sprite RingSprite => _skin.Ring;
        public Sprite SparkSprite => _skin.Spark;
        public float UnitsPerPixel => _camera.orthographicSize * 2f / _camera.pixelHeight;
        public BlastState State => _state;
        public float HintDelay => _feel.HintDelay;
        public float TapBuffer => _feel.TapBuffer;
        public int OriginCell => _originCell;
        public bool EffectsBusy => _clock != null && _clock.Now < _effectsUntil;

        public void BindLandings(IBoardLandingHandler handler) => _landings = handler;
        public BlastFalls Falls { get; private set; }

        public void BindClock(PresentationClock clock)
        {
            _clock = clock;
            Falls?.Clear();
            Falls = new BlastFalls(this, clock);
        }

        public void Tick() => Falls?.Tick();
        public void SetOrigin(int cell) => _originCell = cell;

        // Presentation clock time until which the board is held for an effect.
        public void HoldEffects(float until) => _effectsUntil = Mathf.Max(_effectsUntil, until);

        public void SendLanded(int id, int cell) => _landings?.OnLanded(id, cell);
        public int Width => _state.Width;
        public int Height => _state.Height;

        public void Show(BlastState state)
        {
            HideHint();
            Falls?.Clear();
            foreach (var piece in _pieces.Values) Release(piece);
            _pieces.Clear();
            if (_content != null) Destroy(_content.gameObject);
            _content = new GameObject("Board").transform;
            _content.SetParent(transform, false);
            _state = state;
            _effectsUntil = 0f;
            _originCell = -1;
            _origin = new Vector2(-(Width - 1) * 0.5f, (Height - 1) * 0.5f);

            DrawBoard(state);
            for (var cell = 0; cell < state.CellCount; cell++)
            {
                if (!state.IsPlayable(cell)) continue;
                Renderer("Cell", _skin.Cell, Color.white, CellOrder, CellPosition(cell));
                if (state.Pieces[cell] != Piece.Empty) Add(state.Ids[cell], cell, state.Pieces[cell], state.Colours[cell], state.Hp[cell]);
            }

            var content = Rect.MinMaxRect(-Width * 0.5f - 0.2f, -Height * 0.5f - 0.6f, Width * 0.5f + 0.2f, Height * 0.5f + 0.2f);
            _fit.Fit(content, _feel.TopReserved, _feel.BottomReserved, 0.3f);
        }

        // The level-start drop and the win bonus; see the board view contract. Boxes are in place when the board appears.
        public void HidePieces() => BoardDropIn.Hide(Movable());
        public float DropIn() => _effects != null && _effects.Reduced
            ? BoardDropIn.Show(Movable())
            : BoardDropIn.Play(Movable(), _origin, Height, _feel.DropIn, this);

        private List<SpriteRenderer> Movable()
        {
            _movable.Clear();
            for (var cell = 0; cell < _state.CellCount; cell++)
            {
                if (_state.Pieces[cell] == Piece.Empty || _state.IsStatic(cell)) continue;
                var piece = Get(_state.Ids[cell]);
                if (piece != null) _movable.Add(piece);
            }

            return _movable;
        }

        void ILandingSound.Land(float at) => _sfx.Play(BlastCue.Land, at);

        public float PlayBonus(int moves, float delay) => (_bonus ??= new BlastBonus(this)).Play(moves, delay);
        public void SkipBonus() => _bonus?.Skip();

        public Vector3 WorldOf(Vector3 local) => _content.TransformPoint(local);

        public Vector3 LocalAt(Vector2 screen) => _content.InverseTransformPoint(ScreenToWorld(screen));

        public int CellAtScreen(Vector2 screen) => CellAt(ScreenToWorld(screen));

        public Rect ScreenRect
        {
            get
            {
                var min = _camera.WorldToScreenPoint(WorldOf(new Vector3(-Width * 0.5f - _skin.FrameMargin, -Height * 0.5f - _skin.FrameMargin, 0f)));
                var max = _camera.WorldToScreenPoint(WorldOf(new Vector3(Width * 0.5f + _skin.FrameMargin, Height * 0.5f + _skin.FrameMargin, 0f)));
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }

        public void Aim(int cell)
        {
            if (cell < 0)
            {
                if (_aim != null) _aim.gameObject.SetActive(false);
                return;
            }

            if (_aim == null) _aim = Renderer("Aim", _boosterFx.AimRing, _boosterFx.AimColour, AimOrder, Vector3.zero);
            _aim.gameObject.SetActive(true);
            _aim.transform.localPosition = CellPosition(cell);
            _aim.transform.localScale = Vector3.one * (_boosterFx.AimSize / _boosterFx.AimRing.bounds.size.x);
        }

        public void RefuseAim(int cell)
        {
            _sfx.Play(BlastCue.Refuse);
            var piece = _state.Pieces[cell] == Piece.Empty ? null : Get(_state.Ids[cell]);
            if (piece == null) return;
            Tween.StopAll(piece.transform);
            piece.transform.localRotation = Quaternion.identity;
            Tween.ShakeLocalRotation(piece.transform, new Vector3(0f, 0f, _boosterFx.RefuseDegrees), _boosterFx.RefuseDuration, frequency: 14f);
        }

        public Vector3 CellPosition(int cell) => new Vector3(_origin.x + cell % Width, _origin.y - cell / Width, 0f);

        // Where a new cube starts: `rows` rows above its cell, so a column's new cubes arrive stacked.
        public Vector3 SpawnPosition(int cell, int rows) => CellPosition(cell) + Vector3.up * rows;

        public int CellAt(Vector2 world)
        {
            if (_state == null) return -1;
            var x = Mathf.RoundToInt(world.x - _origin.x);
            var y = Mathf.RoundToInt(_origin.y - world.y);
            if (x < 0 || y < 0 || x >= Width || y >= Height) return -1;
            var cell = y * Width + x;
            return _state.IsPlayable(cell) ? cell : -1;
        }

        public void Refuse(int cell)
        {
            if (_state == null || !_pieces.TryGetValue(_state.Ids[cell], out var piece)) return;
            Tween.StopAll(piece.transform);
            piece.transform.localPosition = CellPosition(cell);
            Tween.ShakeLocalPosition(piece.transform, new Vector3(0.12f, 0f, 0f), _feel.RefuseDuration, frequency: 18f);
            _sfx.Play(BlastCue.Refuse);
        }

        private Vector3 ScreenToWorld(Vector2 screen) => _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));

        public void ShowHint(int[] cells, int count)
        {
            HideHint();
            for (var i = 0; i < count; i++)
            {
                var piece = Get(_state.Ids[cells[i]]);
                if (piece == null) continue;
                _hinted.Add(piece.transform);
                Tween.Scale(piece.transform, _feel.HintScale, _feel.HintPulse * 0.5f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
            }
        }

        public void HideHint()
        {
            foreach (var piece in _hinted)
            {
                if (piece == null) continue;
                Tween.StopAll(piece);
                piece.localScale = Vector3.one;
            }

            _hinted.Clear();
        }

        public SpriteRenderer Add(int id, int cell, Piece piece, byte colour, int hp)
        {
            var renderer = _pool.Count > 0 ? _pool.Pop() : Renderer("Piece", null, Color.white, PieceOrder, Vector3.zero);
            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
            renderer.transform.SetParent(_content, false);
            renderer.transform.localPosition = CellPosition(cell);
            renderer.transform.localScale = Vector3.one;
            renderer.sortingOrder = PieceOrder;
            Dress(renderer, piece, colour, hp);
            _pieces[id] = renderer;
            return renderer;
        }

        public SpriteRenderer Get(int id) => _pieces.TryGetValue(id, out var piece) ? piece : null;
        public int PieceCount => _pieces.Count;

        public void Dress(SpriteRenderer renderer, Piece piece, byte colour, int hp)
        {
            renderer.sprite = _skin.SpriteOf(piece, colour, hp);
            renderer.color = Color.white;
            renderer.transform.localRotation = Quaternion.identity;
        }

        public void Lift(SpriteRenderer piece) => piece.sortingOrder = MovingOrder;

        public void Land(SpriteRenderer piece) => piece.sortingOrder = PieceOrder;

        public void Remove(int id)
        {
            if (!_pieces.Remove(id, out var piece)) return;
            Falls?.Stop(id);
            Release(piece);
        }

        // An effect sprite over the pieces: a flash along a rocket's line, a bomb's ring, a spark.
        public SpriteRenderer Fx(Sprite sprite, Vector3 centre, Vector2 scale, Color colour)
        {
            var fx = _pool.Count > 0 ? _pool.Pop() : Renderer("Piece", null, Color.white, PieceOrder, Vector3.zero);
            fx.gameObject.SetActive(true);
            fx.transform.SetParent(_content, false);
            fx.transform.localPosition = centre;
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            fx.sprite = sprite;
            fx.color = colour;
            fx.sortingOrder = FlashOrder;
            return fx;
        }

        public void Release(SpriteRenderer piece)
        {
            Tween.StopAll(piece.transform);
            Tween.StopAll(piece);
            piece.gameObject.SetActive(false);
            piece.transform.SetParent(transform, false);
            _pool.Push(piece);
        }

        // The board follows the playable cells, so holes show the background. Each run of cells in a band of identical rows is
        // one rounded rect, drawn in the rim colour and again, inset, in the fill colour; overlapping rects merge into one shape.
        private void DrawBoard(BlastState state)
        {
            for (var top = 0; top < Height;)
            {
                var bottom = top;
                while (bottom + 1 < Height && SameRow(state, top, bottom + 1)) bottom++;
                for (var x = 0; x < Width;)
                {
                    if (!state.IsPlayable(top * Width + x))
                    {
                        x++;
                        continue;
                    }

                    var left = x;
                    while (x < Width && state.IsPlayable(top * Width + x)) x++;
                    var centre = (CellPosition(top * Width + left) + CellPosition(bottom * Width + x - 1)) * 0.5f;
                    var size = new Vector2(x - left, bottom - top + 1) + Vector2.one * (2f * _skin.FrameMargin);
                    Panel(centre, size, _skin.BoardRim, RimOrder);
                    Panel(centre, size - Vector2.one * (2f * _skin.BoardRimWidth), _skin.BoardFill, FillOrder);
                }

                top = bottom + 1;
            }
        }

        private bool SameRow(BlastState state, int a, int b)
        {
            for (var x = 0; x < Width; x++)
                if (state.IsPlayable(a * Width + x) != state.IsPlayable(b * Width + x)) return false;
            return true;
        }

        private void Panel(Vector3 centre, Vector2 size, Color colour, int order)
        {
            var panel = Renderer("Panel", _skin.BoardShape, colour, order, centre);
            panel.drawMode = SpriteDrawMode.Sliced;
            panel.size = size;
        }

        private SpriteRenderer Renderer(string name, Sprite sprite, Color colour, int order, Vector3 position)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(_content != null ? _content : transform, false);
            renderer.transform.localPosition = position;
            renderer.sprite = sprite;
            if (_skin.SpriteMaterial != null) renderer.sharedMaterial = _skin.SpriteMaterial;
            renderer.color = colour;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
