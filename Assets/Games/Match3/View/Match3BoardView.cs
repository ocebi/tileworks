using System.Collections.Generic;
using Match3.Controller;
using Match3.Model;
using PrimeTween;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Match3.View
{
    // Cells are one world unit; row 0 is the top row. Pieces, floors and chains are pooled sprites found by their model
    // ids; a chain rides on its gem's sprite. Pieces show only inside the board's mask, so new ones slide in from under its
    // top edge; effects and flyers are drawn over it and can leave the board.
    public sealed class Match3BoardView : MonoBehaviour, IMatch3BoardView, ILandingSound
    {
        private const int RimOrder = 0;
        private const int FillOrder = 1;
        private const int CellOrder = 2;
        private const int FloorOrder = 4;
        private const int GlowOrder = 6;
        private const int PieceOrder = 10;
        private const int ChainOrder = 12;
        private const int RingOrder = 14;
        private const int FlashOrder = 50;
        private const int MovingOrder = 60;

        [SerializeField, ReadOnlyInInspector] private Camera _camera;
        [SerializeField, ReadOnlyInInspector] private Match3Skin _skin;
        [SerializeField, ReadOnlyInInspector] private Match3FeelConfig _feel;
        [SerializeField, ReadOnlyInInspector] private Match3Sfx _sfx;
        [SerializeField, ReadOnlyInInspector] private SpriteMask _mask;
        [SerializeField, ReadOnlyInInspector] private BoardCameraFit _fit;
        [SerializeField, ReadOnlyInInspector] private BoardFx _effects;

        private readonly Dictionary<int, SpriteRenderer> _pieces = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, SpriteRenderer> _floors = new Dictionary<int, SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private readonly SpriteRenderer[] _glows = new SpriteRenderer[2];
        private readonly List<SpriteRenderer> _movable = new List<SpriteRenderer>();
        private Transform _content;
        private SpriteRenderer _ring;
        private Match3State _state;
        private Vector2 _origin;
        private Match3Bonus _bonus;
        private Match3Hint _hint;
        private Match3PresenterKit _kit;
        private IPieceLandingHandler _landings;

        public Camera Camera => _camera;
        public Match3Skin Skin => _skin;
        public Match3FeelConfig Feel => _feel;
        public Match3Sfx Sfx => _sfx;
        public BoardFx Effects => _effects;
        public float HintDelay => _feel.HintDelay;
        public float TapBuffer => _feel.TapBuffer;
        public int Width => _state.Width;
        public int Height => _state.Height;
        public Match3State State => _state;
        public float CellSize => transform.lossyScale.x;
        public int OriginColumn { get; private set; }
        public bool EffectsBusy => _kit != null && _kit.EffectsBusy;

        private void Awake() => PrimeTweenConfig.SetTweensCapacity(_feel.TweenCapacity);

        // The attempt's presenter kit, which owns the falls and the effect hold.
        public void Attach(Match3PresenterKit kit) => _kit = kit;

        public void BindLandings(IPieceLandingHandler handler) => _landings = handler;

        public void ReportLanded(int id) => _landings?.OnLanded(id);

        public void SetOrigin(int cell)
        {
            if (cell >= 0 && _state != null) OriginColumn = cell % Width;
        }

        public void Show(Match3State state)
        {
            HideHint();
            foreach (var piece in _pieces.Values) Release(piece);
            foreach (var floor in _floors.Values) Release(floor);
            _pieces.Clear();
            _floors.Clear();
            if (_content != null) Destroy(_content.gameObject);
            _content = new GameObject("Board").transform;
            _content.SetParent(transform, false);
            _state = state;
            _origin = new Vector2(-(Width - 1) * 0.5f, (Height - 1) * 0.5f);

            for (var pass = 0; pass < 2; pass++) DrawFrame(state, rim: pass == 0);
            for (var cell = 0; cell < state.CellCount; cell++)
            {
                if (!state.IsPlayable(cell)) continue;
                Renderer("Cell", _skin.Cell(cell % Width, cell / Width), Color.white, CellOrder, CellPosition(cell));
                if (state.Floors[cell] > 0) AddFloor(state.FloorIds[cell], cell, state.Floors[cell]);
                if (state.Pieces[cell] == Piece.Empty) continue;
                var piece = Add(state.Ids[cell], cell, state.Pieces[cell], state.Colours[cell], state.Hp[cell]);
                if (state.Chained[cell]) Chain(piece, true);
            }

            _mask.sprite = _skin.Cell(0, 0);
            _mask.transform.localScale = new Vector3(Width, Height, 1f);

            _ring = Renderer("Selection", _skin.SelectRing, Color.white, RingOrder, Vector3.zero);
            _ring.gameObject.SetActive(false);
            for (var i = 0; i < _glows.Length; i++)
            {
                _glows[i] = Renderer("Hint", _skin.HintGlow, Color.white, GlowOrder, Vector3.zero);
                _glows[i].gameObject.SetActive(false);
            }

            _hint?.Stop();
            _hint = new Match3Hint(this, _glows);

            var content = Rect.MinMaxRect(-Width * 0.5f - 0.2f, -Height * 0.5f - 0.6f, Width * 0.5f + 0.2f, Height * 0.5f + 0.2f);
            _fit.Fit(content, _feel.TopReserved, _feel.BottomReserved, 0.3f);
        }

        // The level-start drop and the win bonus; see the board view contract.
        // Boxes and chained gems are on the board when it appears; the movable pieces rain in around them.
        public void HidePieces() => BoardDropIn.Hide(Movable());
        void ILandingSound.Land(float at) => _sfx.Play(Match3Cue.Land, at);
        public float DropIn() =>
            _effects != null && _effects.Reduced ? BoardDropIn.Show(Movable()) : BoardDropIn.Play(Movable(), _origin, Height, _feel.DropIn, this);

        public float PlayBonus(int moves, float delay) => (_bonus ??= new Match3Bonus(this)).Play(moves, delay);
        public void SkipBonus() => _bonus?.Skip();

        public Vector3 WorldOf(Vector3 local) => _content.TransformPoint(local);

        public Vector3 LocalOf(Vector3 world) => _content.InverseTransformPoint(world);

        public Vector3 CellPosition(int cell) => new Vector3(_origin.x + cell % Width, _origin.y - cell / Width, 0f);

        public Vector2 CellCentre(int cell) => WorldOf(CellPosition(cell));

        // Where a new gem starts: `rows` rows above its cell, so a column's new gems arrive stacked, and above the board's
        // top edge even where the column starts lower, so it waits under the mask.
        public Vector3 SpawnPosition(int cell, int rows)
        {
            var top = 0;
            while (top < Height && !_state.IsPlayable(top * Width + cell % Width)) top++;
            return CellPosition(cell) + Vector3.up * (rows + top);
        }

        public int CellAt(Vector2 world)
        {
            if (_state == null) return -1;
            var local = _content.InverseTransformPoint(world);
            var x = Mathf.RoundToInt(local.x - _origin.x);
            var y = Mathf.RoundToInt(_origin.y - local.y);
            if (x < 0 || y < 0 || x >= Width || y >= Height) return -1;
            var cell = y * Width + x;
            return _state.IsPlayable(cell) ? cell : -1;
        }

        public void Select(int cell)
        {
            if (_ring == null) return;
            Tween.StopAll(_ring.transform);
            _ring.gameObject.SetActive(cell >= 0);
            if (cell < 0) return;
            _ring.transform.localPosition = CellPosition(cell);
            _ring.transform.localScale = Vector3.zero;
            Tween.Scale(_ring.transform, 1f, _feel.SelectIn, Ease.OutBack);
            Tween.Scale(_ring.transform, 1f, _feel.SelectPulseScale, _feel.SelectPulse * 0.5f, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo,
                startDelay: _feel.SelectIn);
        }

        // No match: both pieces go there and back. A chain shakes. Anything else wobbles the dragged piece.
        public void Refuse(int from, int to, RejectReason reason)
        {
            if (_state == null) return;
            HideHint();
            var piece = PieceAt(from);
            if (reason == Match3Rejects.Chained)
            {
                var chained = _state.Chained[from] ? from : to;
                var chain = PieceAt(chained);
                if (chain != null && chain.transform.childCount > 0)
                    Tween.ShakeLocalRotation(chain.transform.GetChild(0), new Vector3(0f, 0f, _feel.ChainShakeDegrees), _feel.ChainShake, frequency: 16f);
                _sfx.Play(Match3Cue.Refuse);
                return;
            }

            if (reason == Match3Rejects.NoMatch)
            {
                Bounce(piece, from, to);
                Bounce(PieceAt(to), to, from);
                _sfx.Play(Match3Cue.Refuse);
                return;
            }

            if (piece == null) return;
            Tween.StopAll(piece.transform);
            piece.transform.localPosition = CellPosition(from);
            Tween.ShakeLocalPosition(piece.transform, new Vector3(0.08f, 0f, 0f), 0.1f, frequency: 18f);
        }

        // The two pieces glow and lean towards each other until HideHint.
        public void ShowHint(int from, int to) => _hint?.Show(from, to);

        public void HideHint() => _hint?.Hide();

        public SpriteRenderer Add(int id, int cell, Piece piece, byte colour, int hp)
        {
            var renderer = Take(PieceOrder, true);
            renderer.transform.localPosition = CellPosition(cell);
            Dress(renderer, piece, colour, hp);
            _pieces[id] = renderer;
            return renderer;
        }

        public SpriteRenderer Get(int id) => _pieces.TryGetValue(id, out var piece) ? piece : null;
        public int PieceCount => _pieces.Count;

        public SpriteRenderer GetFloor(int id) => _floors.TryGetValue(id, out var floor) ? floor : null;

        public void Dress(SpriteRenderer renderer, Piece piece, byte colour, int hp)
        {
            renderer.sprite = _skin.SpriteOf(piece, colour, hp);
            renderer.color = Color.white;
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
        }

        // The chain is a child of its gem, so it shakes and moves with it.
        public SpriteRenderer Chain(SpriteRenderer gem, bool on)
        {
            var existing = gem.transform.childCount > 0 ? gem.transform.GetChild(0).GetComponent<SpriteRenderer>() : null;
            if (!on)
            {
                if (existing != null) Release(existing);
                return null;
            }

            var chain = Take(ChainOrder, true);
            chain.transform.SetParent(gem.transform, false);
            chain.transform.localPosition = Vector3.zero;
            chain.sprite = _skin.Chain;
            return chain;
        }

        public void Lift(SpriteRenderer piece) => piece.sortingOrder = MovingOrder;

        public void Land(SpriteRenderer piece) => piece.sortingOrder = PieceOrder;

        // A piece that flies off the board, like a flyer, stops being clipped by it.
        public void Unmask(SpriteRenderer piece) => piece.maskInteraction = SpriteMaskInteraction.None;

        public void Remove(int id)
        {
            if (!_pieces.Remove(id, out var piece)) return;
            Release(piece);
        }

        public void RemoveFloor(int id)
        {
            if (!_floors.Remove(id, out var floor)) return;
            Release(floor);
        }

        // An effect sprite over the pieces: a flash along a rocket's line, a bomb's ring, a spark.
        public SpriteRenderer Fx(Sprite sprite, Vector3 centre, Vector2 scale, Color colour)
        {
            var fx = Take(FlashOrder, false);
            fx.transform.localPosition = centre;
            fx.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            fx.sprite = sprite;
            fx.color = colour;
            return fx;
        }

        public void Release(SpriteRenderer renderer)
        {
            Tween.StopAll(renderer.transform);
            Tween.StopAll(renderer);
            for (var i = renderer.transform.childCount - 1; i >= 0; i--)
            {
                var child = renderer.transform.GetChild(i).GetComponent<SpriteRenderer>();
                if (child != null) Release(child);
            }

            renderer.gameObject.SetActive(false);
            renderer.transform.SetParent(transform, false);
            _pool.Push(renderer);
        }

        private List<SpriteRenderer> Movable()
        {
            _movable.Clear();
            for (var cell = 0; cell < _state.CellCount; cell++)
            {
                if (_state.Pieces[cell] == Piece.Empty || Match3Board.IsStatic(_state, cell)) continue;
                if (_pieces.TryGetValue(_state.Ids[cell], out var piece)) _movable.Add(piece);
            }

            return _movable;
        }

        public SpriteRenderer PieceAt(int cell) => cell >= 0 && _state.Pieces[cell] != Piece.Empty ? Get(_state.Ids[cell]) : null;

        private void Bounce(SpriteRenderer piece, int from, int to)
        {
            if (piece == null) return;
            var home = CellPosition(from);
            var there = Vector3.Lerp(home, CellPosition(to), 0.5f);
            Tween.StopAll(piece.transform);
            Tween.LocalPosition(piece.transform, home, there, _feel.RefuseThere, Ease.OutQuad);
            Tween.LocalPosition(piece.transform, there, home, _feel.RefuseBack, Ease.OutBack, startDelay: _feel.RefuseThere);
        }

        private void AddFloor(int id, int cell, int layers)
        {
            var floor = Take(FloorOrder, false);
            floor.transform.localPosition = CellPosition(cell);
            floor.sprite = _skin.Floor(layers);
            _floors[id] = floor;
        }

        private SpriteRenderer Take(int order, bool masked)
        {
            var renderer = _pool.Count > 0 ? _pool.Pop() : Renderer("Sprite", null, Color.white, order, Vector3.zero);
            renderer.maskInteraction = masked ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
            renderer.transform.SetParent(_content, false);
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
            renderer.color = Color.white;
            renderer.sortingOrder = order;
            return renderer;
        }

        // Holes show the background: a 9-sliced rect per run of cells in a band of identical rows; all rims, then all fills.
        private void DrawFrame(Match3State state, bool rim)
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
                    var frame = Renderer(rim ? "Rim" : "Fill", rim ? _skin.BoardRim : _skin.BoardFill, Color.white, rim ? RimOrder : FillOrder, centre);
                    frame.drawMode = SpriteDrawMode.Sliced;
                    frame.size = new Vector2(x - left, bottom - top + 1) + Vector2.one * (2f * (rim ? _skin.FrameMargin : _skin.FrameMargin - _skin.FrameRimWidth));
                }

                top = bottom + 1;
            }
        }

        private bool SameRow(Match3State state, int a, int b)
        {
            for (var x = 0; x < Width; x++)
                if (state.IsPlayable(a * Width + x) != state.IsPlayable(b * Width + x)) return false;
            return true;
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

        private void OnDestroy() => _hint?.Stop();
    }
}
