using Match3.Model;
using Tileworks.Kit.Haptics;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.View;

namespace Match3.View
{
    // The hammer flies in from its button and swings onto the cell, whose piece, chain and floor wait on their lanes until
    // impact. The dim around the board lifts at impact.
    public sealed class HammerStruckPresenter : IEventPresenter
    {
        private readonly Match3PresenterKit _kit;
        public HammerStruckPresenter(Match3PresenterKit kit) => _kit = kit;
        public ushort EventType => _kit.Ids.HammerStruck;

        public float Present(in BoardEvent e, float delay)
        {
            var struck = HammerStruck.From(e);
            var boosters = _kit.Boosters;
            var bar = _kit.BoosterBar;
            var impact = BoosterFx.Hammer(boosters, boosters.Config, _kit.Clock, bar.ButtonScreenPoint(BoardBoosters.Hammer), _kit.Board.CellPosition(struck.Cell),
                delay);
            _kit.Feedback(Match3Cue.Hammer, HapticKind.Medium, delay + impact);
            _kit.At(bar, delay + impact, b => b.SetPicked(-1, default));
            return _kit.Hold(delay, impact);
        }
    }
}
