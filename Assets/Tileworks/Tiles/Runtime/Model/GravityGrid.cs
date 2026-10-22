using System;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Model
{
    // A board that gravity settles: row 0 is the top. Static pieces hold still and end a column segment like holes do.
    public interface IGravityGrid
    {
        int Width { get; }
        int Height { get; }
        GravityLegs Legs { get; }
        bool IsPlayable(int cell);
        bool IsStatic(int cell);
        bool IsEmpty(int cell);
        int IdAt(int cell);
        byte ColourAt(int cell);

        // Carries the piece, its id and its in-flight flag to an empty cell.
        void Move(int from, int to);

        // Puts a new piece in an empty cell, its colour from the game's refill stream.
        void Spawn(int cell, ref Pcg32 rng);

        // Marks the piece in the cell as in flight until the view reports it landed.
        void Launch(int cell);
    }

    // One leg of a piece's path while the board settles; From is -1 for a new piece, which enters Drop rows above the top.
    public struct SettleStep
    {
        public int Id;
        public int From;
        public int To;
        public int Drop;
        public byte Colour;
        public bool Continues;

        public bool Lands => !Continues;
    }

    // The legs of one settle, plus per-column scratch; owned by the state so concurrent playouts never share it.
    public sealed class GravityLegs
    {
        private SettleStep[] _steps;

        public GravityLegs(int width, int cellCount)
        {
            _steps = new SettleStep[Math.Max(4, cellCount * 4)];
            StepAt = new int[cellCount];
            Top = new int[width];
            Spawned = new int[width];
        }

        public int Count { get; private set; }
        public ref readonly SettleStep this[int index] => ref _steps[index];

        internal int[] StepAt { get; }
        internal int[] Top { get; }
        internal int[] Spawned { get; }

        internal void Clear()
        {
            Count = 0;
            Array.Fill(StepAt, -1);
            Array.Clear(Spawned, 0, Spawned.Length);
        }

        internal ref SettleStep At(int index) => ref _steps[index];

        internal int Add(in SettleStep step)
        {
            if (Count == _steps.Length) Array.Resize(ref _steps, _steps.Length * 2);
            _steps[Count] = step;
            return Count++;
        }
    }
}
