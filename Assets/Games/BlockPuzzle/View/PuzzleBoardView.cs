using System.Collections.Generic;
using BlockPuzzle.Controller;
using BlockPuzzle.Model;
using PrimeTween;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using TMPro;
using UnityEngine;

namespace BlockPuzzle.View
{
    // Board cells are one world unit; row 0 is the top row. The offer sits below the board at a smaller scale.
    public sealed class PuzzleBoardView : MonoBehaviour, IPuzzleBoardView
    {
        private const int FrameOrder = 0;
        private const int CellOrder = 1;
        private const int GhostOrder = 5;
        private const int BlockOrder = 10;
        private const int GemOrder = 11;
        private const int OfferOrder = 20;
        private const int LiftedOrder = 100;

        [SerializeField, ReadOnlyInInspector] private Camera _camera;
        [SerializeField, ReadOnlyInInspector] private PuzzleSkin _skin;
        [SerializeField, ReadOnlyInInspector] private PuzzleFeelConfig _feel;
        [SerializeField, ReadOnlyInInspector] private PuzzleSfx _sfx;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _callout;

        private readonly PieceView[] _pieces = new PieceView[BlockState.OfferSize];
        private readonly List<SpriteRenderer> _ghosts = new List<SpriteRenderer>();
        private readonly List<int> _highlighted = new List<int>();
        private Transform _content;
        private SpriteRenderer[] _blocks = new SpriteRenderer[0];
        private SpriteRenderer[] _gems = new SpriteRenderer[0];
        private byte[] _shown = new byte[0];
        private BlockState _state;
        private ShapeCatalog _shapes;
        private Vector2 _origin;

        public Camera Camera => _camera;
        public PuzzleSkin Skin => _skin;
        public PuzzleSfx Sfx => _sfx;
        public PuzzleFeelConfig Feel => _feel;
        public int GemsLeft { get; set; }
        public BlockState State => _state;

        private int Width => _state.Width;
        private int Height => _state.Height;
        private float OfferY => -Height * 0.5f - _feel.OfferGap - _feel.OfferHeight * 0.5f;

        public void Show(BlockState state, ShapeCatalog shapes)
        {
            if (_content != null) Destroy(_content.gameObject);
            _content = new GameObject("Board").transform;
            _content.SetParent(transform, false);
            _state = state;
            _shapes = shapes;
            _origin = new Vector2(-(Width - 1) * 0.5f, (Height - 1) * 0.5f);
            _ghosts.Clear();
            _highlighted.Clear();
            _blocks = new SpriteRenderer[state.CellCount];
            _gems = new SpriteRenderer[state.CellCount];
            _shown = new byte[state.CellCount];

            var frame = Renderer("Frame", _skin.Frame, _skin.FrameTint, FrameOrder, Vector3.zero);
            frame.drawMode = SpriteDrawMode.Sliced;
            frame.size = new Vector2(Width + 0.35f, Height + 0.35f);
            GemsLeft = 0;
            for (var cell = 0; cell < state.CellCount; cell++)
            {
                var position = CellPosition(cell);
                Renderer("Cell", _skin.Cell, _skin.CellTint, CellOrder, position);
                _blocks[cell] = Renderer("Block", _skin.Block, Color.white, BlockOrder, position);
                _gems[cell] = Renderer("Gem", _skin.Gem, Color.white, GemOrder, position);
                _gems[cell].enabled = state.Gems[cell];
                GemsLeft += state.Gems[cell] ? 1 : 0;
                SetBlock(cell, state.Cells[cell]);
            }

            for (var slot = 0; slot < _pieces.Length; slot++)
            {
                _pieces[slot] = null;
                if (state.Offer[slot] != BlockState.NoShape) Deal(slot, state.Offer[slot], state.OfferColours[slot]).Root.localScale = Vector3.one * _feel.OfferScale;
            }

            RefreshFits();
            _callout.alpha = 0f;
            _callout.transform.position = new Vector3(0f, Height * 0.5f - 1.2f, 0f);
            var content = Rect.MinMaxRect(-Width * 0.5f - 0.2f, OfferY - _feel.OfferHeight * 0.5f, Width * 0.5f + 0.2f, Height * 0.5f + 0.2f);
            CameraFitter.Fit(_camera, content, _feel.TopReserved, _feel.BottomReserved, 0.3f);
        }

        public Vector3 CellPosition(int cell) => new Vector3(_origin.x + cell % Width, _origin.y - cell / Width, 0f);

        public SpriteRenderer BlockAt(int cell) => _blocks[cell];
        public SpriteRenderer GemAt(int cell) => _gems[cell];

        public void SetBlock(int cell, byte colour)
        {
            _shown[cell] = colour;
            var block = _blocks[cell];
            Tween.StopAll(block.transform);
            block.transform.localScale = Vector3.one;
            block.enabled = colour != 0;
            if (colour != 0) block.color = _skin.ColourOf(colour);
        }

        public PieceView Deal(int slot, int shape, byte colour)
        {
            _pieces[slot]?.Destroy();
            var piece = new PieceView(_content, _shapes[shape], _skin.Block, _skin.SpriteMaterial, _skin.ColourOf(colour), OfferOrder);
            piece.Root.localPosition = SlotPosition(slot);
            piece.Root.localScale = Vector3.zero;
            _pieces[slot] = piece;
            return piece;
        }

        public PieceView PieceAt(int slot) => _pieces[slot];

        // Where the pointer has to be released to drop the piece in `slot` at `anchor`.
        public Vector3 DropPoint(int slot, int anchor)
        {
            var piece = _pieces[slot];
            return CellPosition(anchor) - (Vector3)piece.TopLeftOffset - Vector3.up * (_feel.Lift + piece.Shape.Height * 0.5f);
        }

        public PieceView Take(int slot)
        {
            var piece = _pieces[slot];
            _pieces[slot] = null;
            return piece;
        }

        // A piece that cannot go anywhere on the final board is greyed out.
        public void RefreshFits()
        {
            for (var slot = 0; slot < _pieces.Length; slot++)
                _pieces[slot]?.SetDimmed(!Placement.FitsAnywhere(_state, _pieces[slot].Shape));
        }

        public void Callout(string text, float delay)
        {
            _callout.text = text;
            var target = _callout.transform;
            Tween.StopAll(target);
            Tween.StopAll(_callout);
            target.localScale = Vector3.one * 0.6f;
            Tween.Scale(target, 1f, 0.25f, Ease.OutBack, startDelay: delay);
            Tween.Alpha(_callout, 0f, 1f, 0.1f, startDelay: delay);
            Tween.Alpha(_callout, 1f, 0f, 0.3f, startDelay: delay + 0.8f);
        }

        public int SlotAt(Vector2 world)
        {
            if (_state == null || world.y > -Height * 0.5f || world.y < OfferY - _feel.OfferHeight * 0.5f - 0.5f) return -1;
            if (Mathf.Abs(world.x) > Width * 0.5f + 0.5f) return -1;
            var slot = Mathf.Clamp(Mathf.FloorToInt((world.x + Width * 0.5f) / (Width / (float)BlockState.OfferSize)), 0, BlockState.OfferSize - 1);
            return _pieces[slot] != null ? slot : -1;
        }

        public int DragTo(int slot, Vector2 world)
        {
            var piece = _pieces[slot];
            if (piece == null) return -1;
            if (!piece.Lifted)
            {
                piece.Lifted = true;
                Tween.StopAll(piece.Root);
                piece.Root.localScale = Vector3.one;
                piece.SetSorting(LiftedOrder);
            }

            var position = world + Vector2.up * (_feel.Lift + piece.Shape.Height * 0.5f);
            piece.Root.position = position;
            var topLeft = position + piece.TopLeftOffset;
            var x = Mathf.RoundToInt(topLeft.x - _origin.x);
            var y = Mathf.RoundToInt(_origin.y - topLeft.y);
            return x >= 0 && x < Width && y >= 0 && y < Height ? y * Width + x : -1;
        }

        public void Preview(int slot, int anchor)
        {
            foreach (var ghost in _ghosts) ghost.enabled = false;
            foreach (var cell in _highlighted) _blocks[cell].color = _skin.ColourOf(_shown[cell]);
            _highlighted.Clear();
            var piece = _pieces[slot];
            if (anchor < 0 || piece == null) return;

            var shape = piece.Shape;
            var ghostColour = piece.Colour;
            ghostColour.a = _skin.GhostAlpha;
            for (var i = 0; i < shape.Count; i++)
            {
                if (_ghosts.Count <= i) _ghosts.Add(Renderer("Ghost", _skin.Block, Color.white, GhostOrder, Vector3.zero));
                var ghost = _ghosts[i];
                ghost.transform.localPosition = CellPosition(Placement.CellOf(_state, shape, anchor, i));
                ghost.color = ghostColour;
                ghost.enabled = true;
            }

            var (rows, columns) = Placement.LinesCompletedBy(_state, shape, anchor);
            for (var cell = 0; cell < _state.CellCount; cell++)
            {
                var inLine = (rows & 1 << cell / Width) != 0 || (columns & 1 << cell % Width) != 0;
                if (!inLine || _shown[cell] == 0) continue;
                _highlighted.Add(cell);
                _blocks[cell].color = piece.Colour;
            }
        }

        public void Return(int slot)
        {
            var piece = _pieces[slot];
            if (piece == null) return;
            piece.Lifted = false;
            Tween.Position(piece.Root, transform.TransformPoint(SlotPosition(slot)), 0.18f, Ease.OutQuad);
            Tween.Scale(piece.Root, _feel.OfferScale, 0.18f, Ease.OutQuad).OnComplete(piece, p => p.SetSorting(OfferOrder));
        }

        private Vector3 SlotPosition(int slot) => new Vector3((slot - 1) * Width / (float)BlockState.OfferSize, OfferY, 0f);

        private SpriteRenderer Renderer(string name, Sprite sprite, Color colour, int order, Vector3 position)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(_content, false);
            renderer.transform.localPosition = position;
            renderer.sprite = sprite;
            if (_skin.SpriteMaterial != null) renderer.sharedMaterial = _skin.SpriteMaterial;
            renderer.color = colour;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
