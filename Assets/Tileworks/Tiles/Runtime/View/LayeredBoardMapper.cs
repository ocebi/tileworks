using Tileworks.Tiles.Model;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    public interface ISlotOccupancy
    {
        bool IsOccupied(int slot);
    }

    // Half-unit grid → world, centred on the layout; upper layers shift by LayerOffset for a stacked look.
    public sealed class LayeredBoardMapper
    {
        private readonly LayeredLayout _layout;
        private readonly Vector2 _centre;

        public LayeredBoardMapper(LayeredLayout layout, float halfUnit, Vector2 layerOffset)
        {
            _layout = layout;
            HalfUnit = halfUnit;
            LayerOffset = layerOffset;
            _centre = new Vector2((layout.MinX + layout.MaxX) * 0.5f, (layout.MinY + layout.MaxY) * 0.5f);
        }

        public float HalfUnit { get; }
        public Vector2 LayerOffset { get; }
        public float TileSize => _layout.Footprint * HalfUnit;

        public Vector3 ToWorld(int slot)
        {
            var s = _layout.CoordOf(slot);
            var half = _layout.Footprint * 0.5f;
            return new Vector3(
                (s.X + half - _centre.x) * HalfUnit + s.Layer * LayerOffset.x,
                -(s.Y + half - _centre.y) * HalfUnit + s.Layer * LayerOffset.y,
                0f);
        }

        public Rect Bounds
        {
            get
            {
                var width = (_layout.MaxX - _layout.MinX) * HalfUnit + _layout.MaxLayer * Mathf.Abs(LayerOffset.x);
                var height = (_layout.MaxY - _layout.MinY) * HalfUnit + _layout.MaxLayer * Mathf.Abs(LayerOffset.y);
                return new Rect(-width * 0.5f, -height * 0.5f, width, height);
            }
        }

        // Highest occupied slot whose footprint (plus tolerance) contains the point; no colliders (ADR-0012).
        public int Pick(Vector2 world, ISlotOccupancy occupancy, float tolerance = 0f)
        {
            var best = -1;
            var bestLayer = -1;
            var bestDistance = float.MaxValue;
            var extent = TileSize * 0.5f + tolerance;
            for (var slot = 0; slot < _layout.CellCount; slot++)
            {
                if (!occupancy.IsOccupied(slot)) continue;
                var centre = ToWorld(slot);
                var dx = Mathf.Abs(world.x - centre.x);
                var dy = Mathf.Abs(world.y - centre.y);
                if (dx > extent || dy > extent) continue;
                var layer = _layout.CoordOf(slot).Layer;
                var distance = dx * dx + dy * dy;
                if (layer < bestLayer || (layer == bestLayer && distance >= bestDistance)) continue;
                best = slot;
                bestLayer = layer;
                bestDistance = distance;
            }

            return best;
        }

        public int SortingOrder(int slot)
        {
            var s = _layout.CoordOf(slot);
            return s.Layer * 1000 + (_layout.MaxY - s.Y) * 10 + (s.X & 1);
        }
    }
}
