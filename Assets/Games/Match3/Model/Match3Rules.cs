using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Match3.Model
{
    // Builds the pipeline once; creates a state and engine per attempt. Each pass is one wave: specials fire, the board
    // settles in the wave after a clear, and matches clear in the wave after it settles.
    public sealed class Match3Rules
    {
        // 2: holes block falls, pieces fly until they land (Land, LandAll).
        public const int Version = 2;
        public const int RefillStream = 0;
        public const int SpecialStream = 1;
        public const int ShuffleStream = 2;
        public const int BoardStream = 3;

        public Match3Rules()
        {
            Registry = new EventTypeRegistry();
            Events = new Match3EventIds(Registry);
            Board = new Match3Board(Events);
            var matches = new MatchClearer(Board);
            Pipeline = new RulePipelineBuilder<Match3State>()
                .Handle(new SwapHandler(Board, matches), new SwapCodec())
                .Handle(new ActivateHandler(Board), new ActivateCodec())
                .Handle(new AddMovesHandler(Events), new AddMovesCodec())
                .Handle(new HammerHandler(Board), new HammerCodec())
                .Handle(new ShuffleBoardHandler(Events), new ShuffleBoardCodec())
                .Handle(new LandHandler(), new LandCodec())
                .Handle(new LandAllHandler(), new LandAllCodec())
                .Resolve(new SpecialSystem(Board))
                .Resolve(new SettleSystem(Events))
                .Resolve(new DuckSystem(Board))
                .Resolve(new MatchSystem(matches, Events))
                .Resolve(new ShuffleSystem(Events))
                .Resolve(new LandingSystem())
                .End(new Match3EndConditions(Events))
                .Build();
        }

        public EventTypeRegistry Registry { get; }
        public Match3EventIds Events { get; }
        public Match3Board Board { get; }
        public RulePipeline<Match3State> Pipeline { get; }

        // Random gems get colours from the seed here, before any action, so the view draws the board from the state. The
        // starting board has no match and at least one move.
        public Match3State CreateState(Match3Level level, ulong seed)
        {
            var s = new Match3State(level) { MovesLeft = level.Moves };
            var rng = new Pcg32(StableHash.Combine(seed, BoardStream));
            for (var cell = 0; cell < level.CellCount; cell++)
            {
                s.Pieces[cell] = level.Pieces[cell];
                s.Hp[cell] = level.Hp[cell];
                s.Colours[cell] = level.Tints[cell];
                s.Floors[cell] = level.Floors[cell];
                s.Chained[cell] = level.Chains[cell];
                if (level.Pieces[cell] != Piece.Empty) s.Ids[cell] = Match3Board.NewId(s);
            }

            for (var cell = 0; cell < level.CellCount; cell++)
            {
                if (level.Floors[cell] > 0) s.FloorIds[cell] = Match3Board.NewId(s);
                if (s.Pieces[cell] == Piece.Gem && s.Colours[cell] == 0) s.Colours[cell] = SafeColour(s, cell, ref rng);
            }

            for (var i = 0; i < level.Goals.Length; i++) s.GoalsLeft[i] = level.Goals[i].Count;
            if (!Matches.HasMove(s)) ShuffleSystem.Shuffle(s, ShuffleSystem.Free(s), ref rng);
            return s;
        }

        public RuleEngine<Match3State> CreateEngine(Match3Level level, ulong seed, EventBuffer buffer = null)
        {
            var rng = new RngStreams(seed);
            rng.Register("refill");
            rng.Register("special");
            rng.Register("shuffle");
            return new RuleEngine<Match3State>(CreateState(level, seed), Pipeline, rng, buffer);
        }

        // A colour that makes no match with the gems already coloured, starting from a random one.
        private static byte SafeColour(Match3State s, int cell, ref Pcg32 rng)
        {
            var colours = s.Level.Colours;
            var start = rng.NextInt(colours);
            for (var i = 0; i < colours; i++)
            {
                s.Colours[cell] = (byte)(1 + (start + i) % colours);
                if (!Matches.MatchAt(s, cell)) return s.Colours[cell];
            }

            return s.Colours[cell];
        }
    }
}
