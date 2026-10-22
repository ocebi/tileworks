using Match3.Model;
using Tileworks.Kit.Unity;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Match3.View
{
    // Each special resting on its cell plays its idle loop; see SpecialIdler.
    public sealed class Match3Idle : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private Match3BoardView _board;

        private readonly SpecialIdler _idler = new SpecialIdler();

        private void LateUpdate()
        {
            var state = _board.State;
            if (state == null || !_idler.Begin(_board.Effects, Time.unscaledTime)) return;
            for (var cell = 0; cell < state.CellCount; cell++)
            {
                var kind = state.Pieces[cell];
                if (kind != Piece.RocketRow && kind != Piece.RocketColumn && kind != Piece.Bomb && kind != Piece.Disco) continue;
                var piece = _board.Get(state.Ids[cell]);
                var at = _board.CellPosition(cell);
                if (piece == null || !piece.enabled || !SpecialIdler.Resting(piece.transform, at)) continue;
                if (kind == Piece.Bomb) _idler.Bomb(at, _board.Feel.BombFuse);
                else if (kind == Piece.Disco) _idler.Disco(at, Color.white);
                else _idler.Rocket(piece.transform, cell);
            }
        }
    }
}
