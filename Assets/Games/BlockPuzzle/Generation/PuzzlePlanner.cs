using System.Linq;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;

namespace BlockPuzzle.Generation
{
    // The play order's recipes. Level 1 is an empty board; from then on every level starts from a picture, each picture
    // used once before any repeats. Score and gem levels alternate, hard levels lean on gems and bigger pieces. The score
    // target or gem count is tuned per level afterwards (LevelCurve).
    public static class PuzzlePlanner
    {
        public static string IdOf(int number) => $"bp_{number:0000}";

        public static PuzzleRecipe[] Plan(int count)
        {
            var order = PictureOrder(count);
            var recipes = new PuzzleRecipe[count];
            for (var i = 0; i < count; i++)
            {
                var number = i + 1;
                var slot = LevelCurve.At(number);
                var recipe = new PuzzleRecipe { Id = IdOf(number), Seed = StableHash.Hash64(IdOf(number)), Target = slot };
                if (number > 1) recipe.Picture = order[i - 1];
                recipe.Tier = number <= 3 ? 1 : slot.Role >= LevelRole.Hard || slot.Role == LevelRole.Normal && number > 30 && number % 3 == 0 ? 3 : 2;
                if (GemsAt(number, slot.Role)) recipe.Gems = 4 + number / 20 + (slot.Role >= LevelRole.Hard ? 2 : 0);
                else recipe.ScoreTarget = 150 + number * 15 + (slot.Role >= LevelRole.Hard ? 300 : 0);
                recipes[i] = recipe;
            }

            return recipes;
        }

        // Gems arrive at 3; after that odd levels collect gems, even ones score, and hard levels mostly collect gems.
        private static bool GemsAt(int number, LevelRole role) =>
            number >= 3 && (role >= LevelRole.Hard ? number % 20 != 5 : number % 2 == 1);

        // Every picture once, shuffled per round, without the same one twice in a row across rounds.
        private static string[] PictureOrder(int count)
        {
            var names = PuzzlePictures.All.Select(p => p.Name).ToArray();
            var order = new string[count];
            var rng = new Pcg32(StableHash.Hash64("bp/pictures"));
            for (var start = 0; start < count; start += names.Length)
            {
                var round = names.ToArray();
                rng.Shuffle<string>(round);
                if (start > 0 && round[0] == order[start - 1]) (round[0], round[round.Length - 1]) = (round[round.Length - 1], round[0]);
                for (var i = 0; i < round.Length && start + i < count; i++) order[start + i] = round[i];
            }

            return order;
        }
    }
}
