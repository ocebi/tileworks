using UnityEngine;

namespace Tileworks.Tiles.View
{
    // One leg of a falling piece's path in the FallMover: where it ends, whether it slides or lands, when it may start,
    // and the order it was queued in.
    internal struct FallLeg
    {
        public readonly Vector3 To;
        public readonly bool Slide;
        public readonly float StartAt;
        public readonly int Seq;
        public bool Lands;
        public bool Started;

        public FallLeg(Vector3 to, bool slide, bool lands, float startAt, int seq)
        {
            To = to;
            Slide = slide;
            Lands = lands;
            StartAt = startAt;
            Seq = seq;
            Started = false;
        }
    }

    // A piece the FallMover moves: where it is, how fast, its plan and its queue of legs.
    internal sealed class FallRunner
    {
        private FallLeg[] _legs = new FallLeg[8];
        private int _head;

        public int Id;
        public int Move;
        public Transform Piece;
        public Vector3 Position;
        public float Speed;
        public float PlannedEnd;
        public float PlannedExit;
        public bool LandsAtEnd;
        public bool Moving;
        public int Count;

        public ref FallLeg Head => ref _legs[_head];

        // Whether the first queued leg that ends below `y` was queued before leg `seq`.
        public bool PassesBelow(float y, int seq)
        {
            for (var i = 0; i < Count; i++)
            {
                ref readonly var leg = ref _legs[(_head + i) % _legs.Length];
                if (leg.To.y < y) return leg.Seq < seq;
            }

            return false;
        }

        public void Begin(int id, Transform piece, float now, int move)
        {
            Id = id;
            Move = move;
            Piece = piece;
            Position = piece.localPosition;
            Speed = 0f;
            PlannedEnd = now;
            PlannedExit = 0f;
            LandsAtEnd = false;
            Moving = false;
            Count = 0;
            _head = 0;
        }

        // A later leg means the piece won't land at the end of the one before.
        public void Append(in FallLeg leg)
        {
            if (Count > 0) _legs[(_head + Count - 1) % _legs.Length].Lands = false;
            if (Count == _legs.Length)
            {
                var grown = new FallLeg[_legs.Length * 2];
                for (var i = 0; i < Count; i++) grown[i] = _legs[(_head + i) % _legs.Length];
                _legs = grown;
                _head = 0;
            }

            _legs[(_head + Count) % _legs.Length] = leg;
            Count++;
        }

        public void Pop()
        {
            _head = (_head + 1) % _legs.Length;
            Count--;
        }
    }
}
