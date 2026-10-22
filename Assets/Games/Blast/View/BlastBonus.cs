using System.Collections.Generic;
using Blast.Model;
using PrimeTween;
using Tileworks.Kit.Core;
using UnityEngine;

namespace Blast.View
{
    // After a win, each leftover move turns a random cube into a rocket that goes off along its row or column, popping
    // what it passes. Only the view plays it: the level is over, so the rules never see these rockets. Every step runs from
    // a callback on this object, so Skip stops what hasn't started yet.
    public sealed class BlastBonus
    {
        private readonly BlastBoardView _board;
        private readonly List<int> _cubes = new List<int>();
        private readonly HashSet<int> _popped = new HashSet<int>();
        private Pcg32 _random = new Pcg32(11UL);

        public BlastBonus(BlastBoardView board) => _board = board;

        // Returns the seconds until the last pop and the pause after it, or 0 with nothing to play.
        public float Play(int moves, float delay)
        {
            var state = _board.State;
            var feel = _board.Feel;
            _cubes.Clear();
            _popped.Clear();
            for (var cell = 0; cell < state.CellCount; cell++)
            {
                if (state.Pieces[cell] == Piece.Cube) _cubes.Add(cell);
            }

            var count = Mathf.Min(Mathf.Min(moves, feel.BonusRockets), _cubes.Count);
            var reach = Mathf.Max(_board.Width, _board.Height);
            var end = 0f;
            for (var i = 0; i < count; i++)
            {
                var pick = _random.NextInt(_cubes.Count);
                var cell = _cubes[pick];
                _cubes[pick] = _cubes[_cubes.Count - 1];
                _cubes.RemoveAt(_cubes.Count - 1);
                var row = _random.NextInt(2) == 0;
                var at = delay + i * feel.BonusStagger;
                Tween.Delay(this, at, b => b.Arm(cell, row));
                Tween.Delay(this, at + feel.BonusFuse, b => b.Fire(cell, row));
                end = Mathf.Max(end, at + feel.BonusFuse + feel.RocketPop(reach));
            }

            return count > 0 ? end + feel.ResultDelay : 0f;
        }

        public void Skip() => Tween.StopAll(this);

        private void Arm(int cell, bool row)
        {
            var piece = PieceAt(cell);
            if (piece == null || !_popped.Add(cell)) return;
            _board.Dress(piece, row ? Piece.RocketRow : Piece.RocketColumn, 0, 0);
            Tween.PunchScale(piece.transform, new Vector3(0.3f, 0.3f, 0f), _board.Feel.SpecialAppear);
        }

        private void Fire(int cell, bool row)
        {
            var piece = PieceAt(cell);
            if (piece == null) return;
            var feel = _board.Feel;
            var at = _board.CellPosition(cell);
            var axis = row ? Vector3.right : Vector3.up;
            var sprite = _board.Skin.SpriteOf(row ? Piece.RocketRow : Piece.RocketColumn, 0, 0);
            var reach = Mathf.Max(_board.Width, _board.Height);
            _board.Remove(_board.State.Ids[cell]);
            Half(at, axis, sprite, reach, feel.RocketFlight);
            Half(at, -axis, sprite, reach, feel.RocketFlight);
            _board.Sfx.Play(BlastCue.Rocket);
            var x = cell % _board.Width;
            var y = cell / _board.Width;
            for (var d = 1; d < reach; d++)
            {
                var step = d;
                foreach (var sign in Signs)
                {
                    var tx = row ? x + sign * d : x;
                    var ty = row ? y : y - sign * d;
                    if (tx < 0 || ty < 0 || tx >= _board.Width || ty >= _board.Height) continue;
                    var target = ty * _board.Width + tx;
                    Tween.Delay(this, feel.RocketPop(d), b => b.Pop(target, step));
                }
            }
        }

        private void Half(Vector3 at, Vector3 direction, Sprite sprite, int reach, float duration)
        {
            var half = _board.Fx(sprite, at, Vector2.one, Color.white);
            if (direction.x < 0f || direction.y < 0f) half.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Tween.LocalPosition(half.transform, at, at + direction * reach, duration, Ease.InQuad);
            Tween.Alpha(half, 1f, 0f, duration * 0.3f, startDelay: duration * 0.7f).OnComplete(_board, b => b.Release(half));
            if (_board.Effects != null) _board.Effects.Follow(half.transform, Color.white, 0f, duration);
        }

        private void Pop(int cell, int step)
        {
            if (!_board.State.IsPlayable(cell) || !_popped.Add(cell)) return;
            var piece = PieceAt(cell);
            if (piece == null) return;
            var id = _board.State.Ids[cell];
            var feel = _board.Feel;
            if (_board.Effects != null) _board.Effects.Shatter(_board.CellPosition(cell), _board.Skin.ColourOf(_board.State.Colours[cell]), 0f);
            Tween.Scale(piece.transform, 0f, feel.ClearDuration, Ease.InBack).OnComplete(_board, b => b.Remove(id));
            _board.Sfx.PlayStep(BlastCue.Blast, Mathf.Min(step, feel.PopSteps));
        }

        private SpriteRenderer PieceAt(int cell)
        {
            var state = _board.State;
            return state.Pieces[cell] == Piece.Empty ? null : _board.Get(state.Ids[cell]);
        }

        private static readonly int[] Signs = { 1, -1 };
    }
}
