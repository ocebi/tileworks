using Tileworks.Tiles.Model;

namespace TileMatch.Model
{
    public sealed class ModifierRefillSystem : IRuleSystem<TileMatchState>
    {
        private readonly ModifierSet _modifiers;
        private readonly TileMatchEventIds _ids;

        public ModifierRefillSystem(ModifierSet modifiers, TileMatchEventIds ids)
        {
            _modifiers = modifiers;
            _ids = ids;
        }

        public bool Run(TileMatchState s, RuleContext ctx) => _modifiers.OnRefill(s, ctx, _ids);
    }

    public sealed class ModifierReactSystem : IRuleSystem<TileMatchState>
    {
        private readonly ModifierSet _modifiers;
        private readonly TileMatchEventIds _ids;

        public ModifierReactSystem(ModifierSet modifiers, TileMatchEventIds ids)
        {
            _modifiers = modifiers;
            _ids = ids;
        }

        public bool Run(TileMatchState s, RuleContext ctx) => _modifiers.OnReact(s, ctx, _ids);
    }
}
