using Tileworks.Kit.Core;
using Tileworks.Tiles.Model;

namespace Blast.Model
{
    // Builds the pipeline once; creates a state and engine per attempt.
    public sealed class BlastRules
    {
        public const int Version = 2;
        public const int RefillStream = 0;
        public const int SpecialStream = 1;
        public const int ShuffleStream = 2;
        public const int BoardStream = 3;

        public BlastRules()
        {
            Registry = new EventTypeRegistry();
            Events = new BlastEventIds(Registry);
            Board = new BlastBoard(Events);
            Pipeline = new RulePipelineBuilder<BlastState>()
                .Handle(new TapHandler(Board), new TapCodec())
                .Handle(new AddMovesHandler(Events), new AddMovesCodec())
                .Handle(new HammerHandler(Board), new HammerCodec())
                .Handle(new ShuffleBoardHandler(Events), new ShuffleBoardCodec())
                .Handle(new LandHandler<BlastState>(), new LandCodec())
                .Handle(new LandAllHandler<BlastState>(), new LandAllCodec())
                .Resolve(new SpecialSystem(Board))
                .Resolve(new SettleSystem(Events))
                .Resolve(new DuckSystem(Board))
                .Resolve(new ShuffleSystem(Events))
                .Resolve(new LandAllSystem<BlastState>())
                .End(new BlastEndConditions(Events))
                .Build();
        }

        public EventTypeRegistry Registry { get; }
        public BlastEventIds Events { get; }
        public BlastBoard Board { get; }
        public RulePipeline<BlastState> Pipeline { get; }

        // Random cells get their colours from the seed here, before any action, so the view draws the board from the state.
        public BlastState CreateState(BlastLevel level, ulong seed)
        {
            var s = new BlastState(level) { MovesLeft = level.Moves };
            var rng = new Pcg32(StableHash.Combine(seed, BoardStream));
            for (var cell = 0; cell < level.CellCount; cell++)
            {
                s.Pieces[cell] = level.Pieces[cell];
                s.Hp[cell] = level.Hp[cell];
                s.Colours[cell] = level.Tints[cell];
                if (level.Pieces[cell] != Piece.Empty) s.Ids[cell] = BlastBoard.NewId(s);
                if ((s.Pieces[cell] == Piece.Cube || s.Pieces[cell] == Piece.Disco) && s.Colours[cell] == 0)
                    s.Colours[cell] = (byte)(1 + rng.NextInt(level.Colours));
            }

            for (var i = 0; i < level.Goals.Length; i++) s.GoalsLeft[i] = level.Goals[i].Count;
            if (!BlastBoard.HasMove(s)) ShuffleSystem.Shuffle(s, ref rng);
            return s;
        }

        public RuleEngine<BlastState> CreateEngine(BlastLevel level, ulong seed, EventBuffer buffer = null)
        {
            var rng = new RngStreams(seed);
            rng.Register("refill");
            rng.Register("special");
            rng.Register("shuffle");
            return new RuleEngine<BlastState>(CreateState(level, seed), Pipeline, rng, buffer);
        }
    }
}
