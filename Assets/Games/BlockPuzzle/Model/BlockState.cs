using System;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace BlockPuzzle.Model
{
    public sealed class BlockState : IRuleState<BlockState>
    {
        public const int OfferSize = 3;
        public const int NoShape = -1;

        public BlockState(BlockLevel level)
        {
            Level = level;
            Width = level.Width;
            Height = level.Height;
            Cells = new byte[level.CellCount];
            Gems = new bool[level.CellCount];
            Array.Fill(Offer, NoShape);
        }

        public BlockLevel Level { get; }
        public int Width { get; }
        public int Height { get; }
        public int CellCount => Cells.Length;
        public byte[] Cells { get; }
        public bool[] Gems { get; }
        public int[] Offer { get; } = new int[OfferSize];
        public byte[] OfferColours { get; } = new byte[OfferSize];
        public int Score { get; set; }
        public int Streak { get; set; }
        public int GemsLeft { get; set; }
        public int Placements { get; set; }
        public int ContinuesUsed { get; set; }
        public Outcome Outcome { get; set; }

        // Set by a placement, consumed by the line-clear system in the same action.
        public bool JustPlaced { get; set; }

        public int OfferLeft
        {
            get
            {
                var left = 0;
                for (var i = 0; i < OfferSize; i++) left += Offer[i] == NoShape ? 0 : 1;
                return left;
            }
        }

        public int FilledCount
        {
            get
            {
                var filled = 0;
                for (var i = 0; i < Cells.Length; i++) filled += Cells[i] == 0 ? 0 : 1;
                return filled;
            }
        }

        public void CopyTo(BlockState target)
        {
            Array.Copy(Cells, target.Cells, Cells.Length);
            Array.Copy(Gems, target.Gems, Gems.Length);
            Array.Copy(Offer, target.Offer, OfferSize);
            Array.Copy(OfferColours, target.OfferColours, OfferSize);
            target.Score = Score;
            target.Streak = Streak;
            target.GemsLeft = GemsLeft;
            target.Placements = Placements;
            target.ContinuesUsed = ContinuesUsed;
            target.Outcome = Outcome;
            target.JustPlaced = JustPlaced;
        }

        public ulong ComputeHash()
        {
            var hash = StableHash.Combine(0xB10C, (ulong)(Width << 8 | Height));
            for (var i = 0; i < Cells.Length; i++) hash = StableHash.Combine(hash, (ulong)Cells[i] << 1 | (Gems[i] ? 1UL : 0UL));
            for (var i = 0; i < OfferSize; i++) hash = StableHash.Combine(hash, (ulong)(uint)Offer[i] << 8 | OfferColours[i]);
            hash = StableHash.Combine(hash, (ulong)(uint)Score << 32 | (uint)Streak);
            return StableHash.Combine(hash, (ulong)GemsLeft << 16 | (ulong)ContinuesUsed << 8 | (byte)Outcome);
        }
    }
}
