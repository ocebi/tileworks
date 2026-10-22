using Tileworks.Tiles.Model;

namespace BlockPuzzle.Model
{
    // Builds the pipeline once; creates a state and engine per attempt.
    public sealed class BlockRules
    {
        public const int Version = 1;
        public const int OfferStream = 0;

        public BlockRules()
        {
            Registry = new EventTypeRegistry();
            Events = new BlockEventIds(Registry);
            Shapes = new ShapeCatalog();
            Dealer = new OfferDealer(Shapes, Events);
            Pipeline = new RulePipelineBuilder<BlockState>()
                .Handle(new PlacePieceHandler(Shapes, Events), new PlacePieceCodec())
                .Handle(new RefreshOfferHandler(Dealer), new RefreshOfferCodec())
                .Resolve(new LineClearSystem(Events))
                .Resolve(new OfferRefillSystem(Dealer))
                .End(new BlockEndConditions(Shapes, Events))
                .Build();
        }

        public EventTypeRegistry Registry { get; }
        public BlockEventIds Events { get; }
        public ShapeCatalog Shapes { get; }
        public OfferDealer Dealer { get; }
        public RulePipeline<BlockState> Pipeline { get; }

        public BlockState CreateState(BlockLevel level)
        {
            var s = new BlockState(level);
            System.Array.Copy(level.Colours, s.Cells, level.CellCount);
            System.Array.Copy(level.Gems, s.Gems, level.CellCount);
            s.GemsLeft = level.GemCount;
            return s;
        }

        public BlockState CreateScratch(BlockLevel level) => new BlockState(level);

        // The first offer is dealt here, before any action, so it has no events: the view draws it from the state.
        public RuleEngine<BlockState> CreateEngine(BlockLevel level, ulong seed, EventBuffer buffer = null)
        {
            var rng = new RngStreams(seed);
            rng.Register("offer");
            var state = CreateState(level);
            Dealer.Deal(state, ref rng.Get(OfferStream), allMustFit: false, events: null);
            return new RuleEngine<BlockState>(state, Pipeline, rng, buffer);
        }
    }
}
