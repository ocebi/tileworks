using Tileworks.Tiles.Model;

namespace BlockPuzzle.Model
{
    public static class BlockScoring
    {
        public const int PointsPerClearedCell = 10;

        // Cells cleared × lines × (1 + streak): clearing several lines at once or on consecutive moves pays more.
        public static int ClearPoints(int cells, int lines, int streak) => PointsPerClearedCell * cells * lines * (1 + streak);
    }

    public sealed class PlacePieceHandler : IActionHandler<BlockState, PlacePiece>
    {
        private readonly ShapeCatalog _shapes;
        private readonly BlockEventIds _ids;

        public PlacePieceHandler(ShapeCatalog shapes, BlockEventIds ids)
        {
            _shapes = shapes;
            _ids = ids;
        }

        public RejectReason Validate(BlockState s, in PlacePiece a)
        {
            if (s.Outcome != Outcome.Ongoing) return RejectReason.GameOver;
            if (a.Slot < 0 || a.Slot >= BlockState.OfferSize) return RejectReason.InvalidTarget;
            if (s.Offer[a.Slot] == BlockState.NoShape) return BlockRejects.EmptySlot;
            return Placement.Fits(s, _shapes[s.Offer[a.Slot]], a.Anchor) ? RejectReason.None : BlockRejects.Blocked;
        }

        public void Apply(BlockState s, in PlacePiece a, RuleContext ctx)
        {
            var shape = _shapes[s.Offer[a.Slot]];
            var colour = s.OfferColours[a.Slot];
            PiecePlaced.Emit(ctx.Events, _ids, a.Slot, a.Anchor, shape.Id);
            for (var i = 0; i < shape.Count; i++)
            {
                var cell = Placement.CellOf(s, shape, a.Anchor, i);
                s.Cells[cell] = colour;
                CellFilled.Emit(ctx.Events, _ids, cell, colour);
            }

            s.Offer[a.Slot] = BlockState.NoShape;
            s.Placements++;
            s.Score += shape.Count;
            s.JustPlaced = true;
            ScoreChanged.Emit(ctx.Events, _ids, s.Score, shape.Count);
        }
    }

    public sealed class RefreshOfferHandler : IActionHandler<BlockState, RefreshOffer>
    {
        private readonly OfferDealer _dealer;

        public RefreshOfferHandler(OfferDealer dealer) => _dealer = dealer;

        public RejectReason Validate(BlockState s, in RefreshOffer a)
        {
            if (s.Outcome != Outcome.Lost) return RejectReason.NotApplicable;
            return _dealer.CanDealFitting(s) ? RejectReason.None : RejectReason.NoRoom;
        }

        public void Apply(BlockState s, in RefreshOffer a, RuleContext ctx)
        {
            s.Outcome = Outcome.Ongoing;
            s.ContinuesUsed++;
            s.Streak = 0;
            _dealer.Deal(s, ref ctx.Rng.Get(BlockRules.OfferStream), allMustFit: true, ctx.Events);
        }
    }

    // Clears every full row and column at once, collects gems on them and scores the clear.
    public sealed class LineClearSystem : IRuleSystem<BlockState>
    {
        private readonly BlockEventIds _ids;

        public LineClearSystem(BlockEventIds ids) => _ids = ids;

        public bool Run(BlockState s, RuleContext ctx)
        {
            if (!s.JustPlaced) return false;
            s.JustPlaced = false;

            int rows = 0, columns = 0, lines = 0;
            for (var y = 0; y < s.Height; y++)
            {
                if (!Placement.RowFull(s, y)) continue;
                rows |= 1 << y;
                lines++;
            }

            for (var x = 0; x < s.Width; x++)
            {
                if (!Placement.ColumnFull(s, x)) continue;
                columns |= 1 << x;
                lines++;
            }

            if (lines == 0)
            {
                s.Streak = 0;
                return false;
            }

            var cleared = 0;
            for (var cell = 0; cell < s.CellCount; cell++)
            {
                if ((rows & 1 << cell / s.Width) == 0 && (columns & 1 << cell % s.Width) == 0) continue;
                var gem = s.Gems[cell];
                CellCleared.Emit(ctx.Events, _ids, cell, s.Cells[cell], cleared++, gem);
                s.Cells[cell] = 0;
                if (!gem) continue;
                s.Gems[cell] = false;
                s.GemsLeft--;
            }

            var points = BlockScoring.ClearPoints(cleared, lines, s.Streak);
            s.Streak++;
            s.Score += points;
            LinesCleared.Emit(ctx.Events, _ids, lines, cleared, s.Streak, points);
            ScoreChanged.Emit(ctx.Events, _ids, s.Score, points);
            return true;
        }
    }

    public sealed class OfferRefillSystem : IRuleSystem<BlockState>
    {
        private readonly OfferDealer _dealer;

        public OfferRefillSystem(OfferDealer dealer) => _dealer = dealer;

        public bool Run(BlockState s, RuleContext ctx)
        {
            if (s.OfferLeft > 0) return false;
            _dealer.Deal(s, ref ctx.Rng.Get(BlockRules.OfferStream), allMustFit: false, ctx.Events);
            return true;
        }
    }

    public sealed class BlockEndConditions : IEndCondition<BlockState>
    {
        private readonly ShapeCatalog _shapes;
        private readonly BlockEventIds _ids;

        public BlockEndConditions(ShapeCatalog shapes, BlockEventIds ids)
        {
            _shapes = shapes;
            _ids = ids;
        }

        public Outcome Evaluate(BlockState s, RuleContext ctx)
        {
            var level = s.Level;
            var won = level.Goal == BlockGoal.Gems ? s.GemsLeft == 0 : s.Score >= level.ScoreTarget;
            if (won)
            {
                ctx.Events.Emit(_ids.LevelWon, 0);
                return Outcome.Won;
            }

            for (var slot = 0; slot < BlockState.OfferSize; slot++)
            {
                if (s.Offer[slot] != BlockState.NoShape && Placement.FitsAnywhere(s, _shapes[s.Offer[slot]])) return Outcome.Ongoing;
            }

            ctx.Events.Emit(_ids.LevelLost, 0);
            return Outcome.Lost;
        }
    }
}
