using Match3.Model;
using Tileworks.Tiles.Generation;

namespace Match3.Generation
{
    public enum Match3Band : byte
    {
        Easy,
        Normal,
        Hard,
    }

    public sealed class Match3Recipe
    {
        public string Id = "m3";
        public int Width = 8;
        public int Height = 9;
        public int Colours = 5;
        public int Moves = 25;
        public Match3Band Band = Match3Band.Normal;

        // The place in the curve the moves are tuned to; without one, the middle of Band.
        public LevelSlot? Target;
        public ulong Seed = 1;

        // Gems of colour 1 to collect; 0 for none.
        public int ColourGoal;
        public int Boxes;
        public bool DoubleBoxes;
        public int Balloons;
        public int Ducks;
        public int Floors;
        public bool DoubleFloors;
        public int Chains;

        public BoardShape Shape;

        // Cuts the two top corners off the board, as Shape TopCorners does.
        public bool Corners;
        public BlockerPattern BoxPattern;
        public BlockerPattern BalloonPattern;
        public BlockerPattern FloorPattern;
        public BlockerPattern ChainPattern;

        // Specials on the board at the start: a mirrored pair of rockets, a bomb, a disco ball.
        public int Rockets;
        public bool Bomb;
        public bool Disco;

        // The casual bot's win rate the level must still reach when it is tuned on the skilled bot.
        public float MinCasualRate;

        // Chains, balloons and floors go into the cells only slides fill first.
        public bool UnderOverhangs;

        // Walls sealing empty pens, of chains instead of boxes when WallChains is set. Other boxes may seal cells only in a
        // pocket shape; other chains never do.
        public PenLayout Pens;
        public bool WallChains;

        // The least share of playable cells that hold gems or specials free to move; blockers and then goals shrink to keep it.
        public float MinMovable;

        // A sixth colour is held back on early levels.
        public int MaxColours = Match3Level.MaxColours;
    }

    public sealed class GeneratedMatch3
    {
        public Match3Level Level;
        public ProbeStats Stats;

        // The casual bot on the final level; the same as Stats when the level was tuned on the casual bot.
        public ProbeStats Casual;
        public TuningKnobs Knobs;
        public Match3Band Band;
        public LevelRole Role;
        public int Attempts;
        public bool HitBand;
    }
}
