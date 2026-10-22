using System;
using Tileworks.Tiles.Search;

namespace Tileworks.Tiles.Generation
{
    public enum LevelRole : byte
    {
        Easy,
        Normal,
        Hard,
        SuperHard,
    }

    // A level's place in the difficulty curve: its role and the bot win rate it is tuned to, on the casual table unless
    // made for the skilled one.
    public readonly struct LevelSlot
    {
        public readonly int Number;
        public readonly LevelRole Role;
        public readonly float WinRate;
        public readonly bool Skilled;

        public LevelSlot(int number, LevelRole role, float winRate, bool skilled = false)
        {
            Number = number;
            Role = role;
            WinRate = winRate;
            Skilled = skilled;
        }

        public float Low => Math.Max(LevelCurve.Floor(Role, Skilled), WinRate - LevelCurve.Tolerance);
        public float High => Math.Min(LevelCurve.Ceiling(Role, Skilled), WinRate + LevelCurve.Tolerance);
        public bool Accepts(float winRate) => winRate >= Low && winRate <= High;
        public float Miss(float winRate) => winRate < Low ? Low - winRate : winRate > High ? winRate - High : 0f;
        public string Label => LevelCurve.Label(Role);

        // The easy level that follows a hard one.
        public bool IsBreather => Number > 10 && Role == LevelRole.Easy;

        public override string ToString() => $"{Number} {Label} {WinRate:0.00}";
    }

    // The play order's difficulty. Levels 1-4 are near-certain wins and 5-9 ramp up to the first hard level at 10. From
    // there each ten-level chapter has hard levels 5th and 10th, an easy breather after each, and normal levels that get a
    // little harder every chapter. Every 20th level from 40 is super hard. Two tables share those roles: the casual bot's,
    // and the skilled bot's for games whose players build specials (Blast, Gem Swap).
    public static class LevelCurve
    {
        public const float Tolerance = 0.07f;

        private static readonly float[] Opening = { 0.97f, 0.95f, 0.93f, 0.9f, 0.72f, 0.86f, 0.66f, 0.62f, 0.58f, 0.33f };
        private static readonly float[] SkilledOpening = { 0.99f, 0.98f, 0.98f, 0.97f, 0.88f, 0.97f, 0.85f, 0.83f, 0.8f, 0.48f };

        // Normal levels' offsets from the chapter's normal rate, by position in the chapter.
        private static readonly float[] Offsets = { 0f, 0f, 0.06f, 0f, -0.05f, 0f, 0f, 0.03f, -0.02f, -0.07f, 0f };

        public static LevelSlot At(int number)
        {
            if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
            if (number <= Opening.Length) return new LevelSlot(number, Classify(Opening[number - 1]), Opening[number - 1]);
            var chapter = Math.Min((number - 1) / 10, 9);
            var position = (number - 1) % 10 + 1;
            var hard = 0.33f - 0.01f * chapter;
            switch (position)
            {
                case 1: return new LevelSlot(number, LevelRole.Easy, 0.88f);
                case 6: return new LevelSlot(number, LevelRole.Easy, 0.85f);
                case 5: return new LevelSlot(number, LevelRole.Hard, hard);
                case 10:
                    return number >= 40 && number % 20 == 0 ? new LevelSlot(number, LevelRole.SuperHard, 0.15f) : new LevelSlot(number, LevelRole.Hard, hard - 0.02f);
                default: return new LevelSlot(number, LevelRole.Normal, 0.66f - 0.015f * (chapter - 1) + Offsets[position]);
            }
        }

        public static LevelSlot At(int number, BotProfile profile) => profile.LooksAhead ? SkilledAt(number) : At(number);

        // Normal levels from 0.82 at chapter 2 to 0.70 at chapter 10, hard ones from 0.45 to 0.38, super hard 0.22.
        public static LevelSlot SkilledAt(int number)
        {
            var casual = At(number);
            if (number <= SkilledOpening.Length) return new LevelSlot(number, casual.Role, SkilledOpening[number - 1], true);
            var chapter = Math.Min((number - 1) / 10, 9);
            var position = (number - 1) % 10 + 1;
            var hard = 0.45f - 0.00875f * (chapter - 1);
            var rate = casual.Role switch
            {
                LevelRole.Easy => 0.97f,
                LevelRole.Hard => position == 10 ? hard - 0.02f : hard,
                LevelRole.SuperHard => 0.22f,
                _ => 0.82f - 0.015f * (chapter - 1) + Offsets[position],
            };
            return new LevelSlot(number, casual.Role, rate, true);
        }

        public static LevelRole Classify(float winRate) =>
            winRate >= 0.75f ? LevelRole.Easy : winRate >= 0.4f ? LevelRole.Normal : winRate > 0.2f ? LevelRole.Hard : LevelRole.SuperHard;

        public static LevelRole Classify(float winRate, BotProfile profile) =>
            !profile.LooksAhead ? Classify(winRate) :
            winRate >= Floor(LevelRole.Easy, true) ? LevelRole.Easy : winRate >= Floor(LevelRole.Normal, true) ? LevelRole.Normal :
            winRate >= Floor(LevelRole.Hard, true) ? LevelRole.Hard : LevelRole.SuperHard;

        public static float Floor(LevelRole role, bool skilled = false) => role switch
        {
            LevelRole.Easy => skilled ? 0.9f : 0.75f,
            LevelRole.Normal => skilled ? 0.6f : 0.4f,
            LevelRole.Hard => skilled ? 0.3f : 0.2f,
            _ => skilled ? 0.15f : 0.1f,
        };

        // Just under the next role's floor.
        public static float Ceiling(LevelRole role, bool skilled = false) => role switch
        {
            LevelRole.Easy => 1f,
            LevelRole.Normal => skilled ? 0.9f : 0.75f,
            LevelRole.Hard => skilled ? 0.6f : 0.4f,
            _ => skilled ? 0.3f : 0.2f,
        };

        // Acceptance beyond the win rate: wins shouldn't come with many moves to spare, and dead boards stay rare.
        public static float MaxMovesLeftOnWin(LevelRole role) => role switch
        {
            LevelRole.Easy => 0.5f,
            LevelRole.Normal => 0.35f,
            _ => 0.25f,
        };

        // Levels 1-4 are close to unlosable, so their wins leave moves over.
        public static float MaxMovesLeftOnWin(in LevelSlot slot) => slot.Number <= 4 ? 0.6f : MaxMovesLeftOnWin(slot.Role);

        // The casual bot's floor: levels 1-4 close to unlosable, other levels won now and then, super hard ones left to the
        // skilled bot.
        public static float MinCasualRate(in LevelSlot slot) => slot.Number switch
        {
            1 => 0.99f,
            <= 3 => 0.95f,
            4 => 0.9f,
            _ => slot.Role == LevelRole.SuperHard ? 0f : 0.02f,
        };

        // Enough casual games on levels 1-4 to tell 0.99 from 0.96.
        public static int MinCasualPlayouts(in LevelSlot slot) => slot.Number <= 4 ? 240 : 0;

        // A floor is a loss budget: at 240 games, 0.99 allows two losses and 0.95 twelve.
        public static bool PassesCasual(float minRate, int runs, int wins) => runs - wins <= (int)Math.Floor(runs * (1f - minRate) + 1e-3f);

        public static float MaxShufflesPerGame(LevelRole role) => role >= LevelRole.Hard ? 0.3f : 0.15f;

        // Levels under 30 keep most of the board in play: the share of playable cells holding pieces a player can move.
        public static float MinMovableShare(int number) => number < 30 ? 0.6f : 0f;

        public static string Label(LevelRole role) => role switch
        {
            LevelRole.Easy => "easy",
            LevelRole.Normal => "normal",
            LevelRole.Hard => "hard",
            _ => "superHard",
        };

        public static LevelRole Parse(string label) => label switch
        {
            "easy" => LevelRole.Easy,
            "hard" => LevelRole.Hard,
            "superHard" => LevelRole.SuperHard,
            _ => LevelRole.Normal,
        };

        // What a sequence shows next to the level: only the hard ones are marked.
        public static string Badge(LevelRole role) => role >= LevelRole.Hard ? Label(role) : string.Empty;
    }
}
