using System;
using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    // Builds the pipeline once; creates a state and engine per attempt.
    public sealed class TileMatchRules
    {
        public const int Version = 1;
        public const int ShuffleStream = 0;

        public TileMatchRules(ITileModifier[] modifiers)
        {
            Registry = new EventTypeRegistry();
            Events = new TileMatchEventIds(Registry);
            Modifiers = new ModifierSet(modifiers);
            Pipeline = new RulePipelineBuilder<TileMatchState>()
                .Handle(new SelectTileHandler(Modifiers, Events), new SelectTileCodec())
                .Handle(new BoosterHandler(Modifiers, Events, ShuffleStream), new UseBoosterCodec())
                .Handle(new ContinueHandler(Events), new ContinueCodec())
                .Handle(new TimeUpHandler(Events), new TimeUpCodec())
                .Resolve(new PendingInsertSystem(Events))
                .Resolve(new TripleClearSystem(Events))
                .Resolve(new ModifierRefillSystem(Modifiers, Events))
                .Resolve(new UncoverSystem(Events))
                .Resolve(new ModifierReactSystem(Modifiers, Events))
                .End(new TileMatchEndConditions(Modifiers, Events))
                .Build();
        }

        public EventTypeRegistry Registry { get; }
        public TileMatchEventIds Events { get; }
        public ModifierSet Modifiers { get; }
        public RulePipeline<TileMatchState> Pipeline { get; }

        public static ITileModifier[] BuiltInModifiers() => new ITileModifier[]
        {
            new SpawnerModifier(), new FrozenModifier(), new HiddenModifier(), new LinkedModifier(),
        };

        public static TileMatchRules CreateDefault() => new TileMatchRules(BuiltInModifiers());

        public TileMatchState CreateState(TileMatchLevel level)
        {
            foreach (var spec in level.Modifiers)
            {
                if (!Modifiers.Supports(spec.Type)) throw new ArgumentException($"Level {level.Id} uses unknown modifier {spec.Type}.");
            }

            var s = new TileMatchState(level, Modifiers.CreateState(level));
            Array.Fill(s.Occupant, -1);
            var occupied = new bool[level.SlotCount];
            for (var tile = 0; tile < level.TileCount; tile++)
            {
                s.Kinds[tile] = level.KindOf(tile);
                if (tile < level.BoardTileCount)
                {
                    s.Locations[tile] = TileLocation.Board;
                    s.Occupant[level.SlotOf(tile)] = tile;
                    occupied[level.SlotOf(tile)] = true;
                }
                else
                {
                    s.Locations[tile] = TileLocation.Queued;
                }
            }

            s.Cover = CoverCounts.Create(level.Cover, occupied);
            s.TrayCapacity = level.TrayCapacity;
            return s;
        }

        public TileMatchState CreateScratch(TileMatchLevel level) => new TileMatchState(level, Modifiers.CreateState(level));

        public RuleEngine<TileMatchState> CreateEngine(TileMatchLevel level, ulong seed, EventBuffer buffer = null)
        {
            var rng = new RngStreams(seed);
            rng.Register("shuffle");
            return new RuleEngine<TileMatchState>(CreateState(level), Pipeline, rng, buffer);
        }
    }
}
