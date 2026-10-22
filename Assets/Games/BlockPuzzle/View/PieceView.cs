using BlockPuzzle.Model;
using UnityEngine;

namespace BlockPuzzle.View
{
    // One offered piece: a root transform centred on the piece, with a block renderer per cell.
    public sealed class PieceView
    {
        private readonly SpriteRenderer[] _blocks;

        public PieceView(Transform parent, Shape shape, Sprite block, Material material, Color colour, int sortingOrder)
        {
            Shape = shape;
            Colour = colour;
            Root = new GameObject("Piece " + shape.Name).transform;
            Root.SetParent(parent, false);
            _blocks = new SpriteRenderer[shape.Count];
            for (var i = 0; i < shape.Count; i++)
            {
                var renderer = new GameObject("Block").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(Root, false);
                renderer.transform.localPosition = LocalCentre(i);
                renderer.sprite = block;
                if (material != null) renderer.sharedMaterial = material;
                renderer.color = colour;
                renderer.sortingOrder = sortingOrder;
                _blocks[i] = renderer;
            }
        }

        public Shape Shape { get; }
        public Color Colour { get; }
        public Transform Root { get; }
        public bool Lifted { get; set; }

        // Centre of the top-left block relative to the piece's centre, in board cells.
        public Vector2 TopLeftOffset => new Vector2(-(Shape.Width - 1) * 0.5f, (Shape.Height - 1) * 0.5f);

        public Vector2 LocalCentre(int i) => TopLeftOffset + new Vector2(Shape.Dx(i), -Shape.Dy(i));

        public void SetSorting(int order)
        {
            foreach (var block in _blocks) block.sortingOrder = order;
        }

        public void SetDimmed(bool dimmed)
        {
            var colour = dimmed ? Color.Lerp(Colour, Color.gray, 0.7f) : Colour;
            foreach (var block in _blocks) block.color = colour;
        }

        public void Destroy() => Object.Destroy(Root.gameObject);
    }
}
