using PrimeTween;
using UnityEngine;

namespace Match3.View
{
    // The idle hint: the two pieces of the hinted swap glow and lean towards each other twice, rest, and repeat until
    // Hide puts them back.
    public sealed class Match3Hint
    {
        private readonly Match3BoardView _board;
        private readonly SpriteRenderer[] _glows;
        private readonly Transform[] _hinted = new Transform[2];
        private readonly Vector3[] _homes = new Vector3[2];
        private Sequence _sequence;

        public Match3Hint(Match3BoardView board, SpriteRenderer[] glows)
        {
            _board = board;
            _glows = glows;
        }

        public void Show(int from, int to)
        {
            Hide();
            var feel = _board.Feel;
            var cells = new[] { from, to };
            for (var i = 0; i < 2; i++)
            {
                _glows[i].gameObject.SetActive(true);
                _glows[i].transform.localPosition = _board.CellPosition(cells[i]);
                _glows[i].color = new Color(1f, 1f, 1f, feel.HintGlowAlpha);
                _hinted[i] = _board.PieceAt(cells[i])?.transform;
                _homes[i] = _board.CellPosition(cells[i]);
            }

            var lean = (_board.CellPosition(to) - _board.CellPosition(from)) * feel.HintNudge;
            _sequence = Sequence.Create(cycles: -1);
            for (var nudge = 0; nudge < 2; nudge++)
            {
                Lean(from, lean, false, Ease.OutQuad);
                Lean(to, -lean, true, Ease.OutQuad);
                Lean(from, lean, false, Ease.InOutQuad, back: true);
                Lean(to, -lean, true, Ease.InOutQuad, back: true);
            }

            _sequence.ChainDelay(feel.HintRest);
        }

        public void Hide()
        {
            if (_sequence.isAlive) _sequence.Stop();
            for (var i = 0; i < _hinted.Length; i++)
            {
                if (_glows[i] != null) _glows[i].gameObject.SetActive(false);
                if (_hinted[i] == null) continue;
                _hinted[i].localPosition = _homes[i];
                _hinted[i] = null;
            }
        }

        public void Stop()
        {
            if (_sequence.isAlive) _sequence.Stop();
        }

        // One leg: the first piece's tween starts a step, the second runs alongside it.
        private void Lean(int cell, Vector3 lean, bool alongside, Ease ease, bool back = false)
        {
            var step = _board.Feel.HintStep;
            var piece = _hinted[alongside ? 1 : 0];
            var home = _board.CellPosition(cell);
            var tween = piece == null
                ? Tween.Delay(step)
                : back ? Tween.LocalPosition(piece, home + lean, home, step, ease) : Tween.LocalPosition(piece, home, home + lean, step, ease);
            if (alongside) _sequence.Group(tween);
            else _sequence.Chain(tween);
        }
    }
}
