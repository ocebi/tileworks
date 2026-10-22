using Blast.Model;
using Tileworks.Tiles.Generation;

namespace Blast.Generation
{
    public enum BlastBand : byte
    {
        Easy,
        Normal,
        Hard,
    }

    public sealed class BlastRecipe
    {
        public string Id = "bl";
        public int Width = 8;
        public int Height = 9;
        public int Colours = 4;
        public int Moves = 25;
        public BlastBand Band = BlastBand.Normal;

        // The place in the curve the moves are tuned to; without one, the middle of Band.
        public LevelSlot? Target;
        public ulong Seed = 1;

        // Cubes of colour 1 to collect; 0 for none.
        public int ColourGoal;
        public int Boxes;
        public bool DoubleBoxes;
        public int Balloons;
        public int Ducks;

        public BoardShape Shape;

        // Cuts the two top corners off the board, as Shape TopCorners does.
        public bool Corners;
        public BlockerPattern BoxPattern;
        public BlockerPattern BalloonPattern;

        // Specials on the board at the start: a mirrored pair of rockets, a bomb, a disco ball.
        public int Rockets;
        public bool Bomb;
        public bool Disco;

        // The casual bot's win rate the level must still reach when it is tuned on the skilled bot.
        public float MinCasualRate;

        // Balloons and boxes go into the cells only slides fill first.
        public bool UnderOverhangs;

        // Box walls sealing empty pens. Other boxes may seal cells only in a pocket shape.
        public PenLayout Pens;

        // The least share of playable cells that hold cubes or specials; blockers and then goals shrink to keep it.
        public float MinMovable;
    }

    public sealed class GeneratedBlast
    {
        public BlastLevel Level;
        public ProbeStats Stats;

        // The casual bot on the final level; the same as Stats when the level was tuned on the casual bot.
        public ProbeStats Casual;
        public TuningKnobs Knobs;
        public BlastBand Band;
        public LevelRole Role;
        public int Attempts;
        public bool HitBand;
    }
}
