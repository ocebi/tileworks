using System;
using System.Collections.Generic;
using System.Linq;
using BlockPuzzle.Model;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;
using Tileworks.Tiles.Search;

namespace BlockPuzzle.Generation
{
    public enum PuzzleBand : byte
    {
        Easy,
        Normal,
        Hard,
    }

    public sealed class PuzzleRecipe
    {
        public string Id = "bp";
        public int Width = 8;
        public int Height = 8;
        public BlockGoal Goal = BlockGoal.Score;
        public int ScoreTarget = 300;
        public int Gems;

        // A PuzzlePictures name to start from; without one, Fill decides how much rubble there is.
        public string Picture;

        // Share of cells filled before the first move.
        public float Fill;

        // Highest piece tier in the pool (1 = small pieces only).
        public int Tier = 1;

        // Extra weight on pieces above tier 1, so big ones come more often; 0 for the usual mix.
        public int Heavy;
        public PuzzleBand Band = PuzzleBand.Normal;

        // The place in the curve the goal is tuned to; without one, the middle of Band.
        public LevelSlot? Target;
        public ulong Seed = 1;
    }

    public sealed class GeneratedPuzzle
    {
        public BlockLevel Level;
        public ProbeStats Stats;
        public PuzzleBand Band;
        public LevelRole Role;
        public int Attempts;
        public bool HitBand;
    }

    // Builds a recipe's board and tunes its goal until the casual bot's win rate over several deals lands on the recipe's
    // target: the score target for a score level, the gem count for a gem level, then the piece tier and how often big pieces
    // come. A picture board is
    // the same picture every attempt; a rubble board without one tries a new seed instead. A hard level still needs a 10%
    // win rate.
    public sealed class PuzzleGenerator
    {
        public const int CasualEpsilonPercent = 25;
        public const int MinScore = 100;
        public const int MaxScore = 4000;

        // More gems than this covers most of a picture; past it the pieces get bigger instead.
        public const int MaxGems = 16;

        private readonly BlockRules _rules;
        private readonly int _playouts;

        public PuzzleGenerator(BlockRules rules, int playouts = 60)
        {
            _rules = Guard.NotNull(rules, nameof(rules));
            _playouts = playouts;
        }

        public static PuzzleBand Classify(float winRate) => winRate >= 0.75f ? PuzzleBand.Easy : winRate >= 0.4f ? PuzzleBand.Normal : PuzzleBand.Hard;

        public static LevelSlot SlotFor(PuzzleBand band) => band switch
        {
            PuzzleBand.Easy => new LevelSlot(0, LevelRole.Easy, 0.88f),
            PuzzleBand.Normal => new LevelSlot(0, LevelRole.Normal, 0.58f),
            _ => new LevelSlot(0, LevelRole.Hard, 0.3f),
        };

        public GeneratedPuzzle Generate(PuzzleRecipe recipe, int maxAttempts = 14)
        {
            var slot = recipe.Target ?? SlotFor(recipe.Band);
            var gemGoal = recipe.Gems > 0;
            var tuned = new PuzzleRecipe
            {
                Id = recipe.Id, Width = recipe.Width, Height = recipe.Height, Picture = recipe.Picture, Fill = recipe.Fill, Tier = recipe.Tier, Heavy = recipe.Heavy,
                Seed = recipe.Seed, ScoreTarget = recipe.ScoreTarget / 10 * 10, Gems = recipe.Gems,
            };
            // Gems come in mirrored pairs on a board without a middle column.
            var unit = recipe.Picture != null && recipe.Width % 2 == 0 ? 2 : 1;
            var maxGems = Math.Max(unit, Math.Min(MaxGems, Build(tuned, recipe.Seed).Colours.Count(c => c != 0)) / unit * unit);
            if (gemGoal) tuned.Gems = Math.Clamp(tuned.Gems / unit * unit, unit, maxGems);
            int low = gemGoal ? unit : MinScore, high = gemGoal ? maxGems : MaxScore;
            GeneratedPuzzle best = null;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var seed = recipe.Picture != null ? recipe.Seed : recipe.Seed + (ulong)(attempt - 1) * 7919UL;
                var level = Build(tuned, seed);
                var stats = Probe(level, seed);
                var win = stats.WinRate;
                var result = new GeneratedPuzzle
                {
                    Level = level, Stats = stats, Band = Classify(win), Role = LevelCurve.Classify(win), Attempts = attempt, HitBand = slot.Accepts(win),
                };
                if (result.HitBand) return result;
                if (best == null || slot.Miss(win) < slot.Miss(best.Stats.WinRate)) best = result;
                if (recipe.Picture == null) continue;

                var value = gemGoal ? tuned.Gems : tuned.ScoreTarget;
                var stride = gemGoal ? unit : 10;
                if (win > slot.High) low = value + stride;
                else high = value - stride;
                if (low > high)
                {
                    // The goal alone can't reach the target: bigger pieces make the board jam sooner, smaller ones later.
                    if (win > slot.High && tuned.Tier < 3) tuned.Tier++;
                    else if (win > slot.High && tuned.Heavy < 8) tuned.Heavy++;
                    else if (win < slot.Low && tuned.Heavy > 0) tuned.Heavy--;
                    else if (win < slot.Low && tuned.Tier > 1) tuned.Tier--;
                    else break;
                    (low, high) = gemGoal ? (unit, maxGems) : (MinScore, MaxScore);
                    continue;
                }

                var next = gemGoal ? value + (win > slot.High ? unit : -unit) : (int)Math.Round(value * (win > slot.High ? 1.3f : 0.75f) / 10f) * 10;
                next = Math.Clamp(next, low, high);
                if (gemGoal) tuned.Gems = next;
                else tuned.ScoreTarget = next;
            }

            best.Attempts = maxAttempts;
            return best;
        }

        public BlockLevel Build(PuzzleRecipe recipe, ulong seed)
        {
            var rng = new Pcg32(seed);
            var cells = recipe.Width * recipe.Height;
            byte[] colours;
            bool[] gems;
            if (recipe.Picture != null)
            {
                colours = Draw(PuzzlePictures.All[PuzzlePictures.IndexOf(recipe.Picture)].Rows, recipe.Width, recipe.Height);
                gems = PlaceGemsMirrored(colours, recipe.Width, recipe.Gems);
            }
            else
            {
                colours = new byte[cells];
                var target = (int)Math.Round(recipe.Fill * cells);
                for (var filled = 0; filled < target;)
                {
                    var cell = PickRubbleCell(colours, recipe.Width, recipe.Height, ref rng);
                    if (colours[cell] != 0) continue;
                    colours[cell] = (byte)rng.NextInt(1, OfferDealer.Colours + 1);
                    filled++;
                }

                BreakFullLines(colours, recipe.Width, recipe.Height, ref rng);
                gems = PlaceGems(colours, recipe.Gems, ref rng);
            }

            var (pool, weights) = Pool(recipe.Tier, recipe.Heavy);
            var goal = recipe.Gems > 0 ? BlockGoal.Gems : BlockGoal.Score;
            return new BlockLevel(recipe.Id, recipe.Width, recipe.Height, colours, gems, goal, goal == BlockGoal.Score ? recipe.ScoreTarget : 0, pool, weights);
        }

        public PlayoutStats Measure(BlockLevel level, ulong seed)
        {
            var domain = new BlockSearchDomain(_rules, level, seed);
            var runner = new PlayoutRunner<BlockState, PlacePiece>(domain);
            var start = _rules.CreateEngine(level, seed).State;
            return runner.Run(start, new EpsilonGreedyPolicy<BlockState, PlacePiece>(CasualEpsilonPercent), _playouts, seed);
        }

        // Up to 10 deals, as many runs on each as the playout budget allows.
        public ProbeStats Probe(BlockLevel level, ulong seed)
        {
            var seeds = Math.Min(10, _playouts);
            var probe = new LevelProbe<BlockState, PlacePiece>(new ProbeDomain(_rules, level));
            return probe.Run(new EpsilonGreedyPolicy<BlockState, PlacePiece>(CasualEpsilonPercent), seed, seeds, Math.Max(1, _playouts / seeds));
        }

        private static float GoalLeft(BlockState s) => s.Level.Goal == BlockGoal.Gems
            ? (float)s.GemsLeft / Math.Max(1, s.Level.GemCount)
            : Math.Max(0f, 1f - (float)s.Score / Math.Max(1, s.Level.ScoreTarget));

        // The picture centred on the board (one that sits on the bottom row stays there).
        private static byte[] Draw(string[] rows, int width, int height)
        {
            int top = rows.Length, bottom = -1;
            for (var y = 0; y < rows.Length; y++)
            {
                if (rows[y].Trim('.').Length == 0) continue;
                top = Math.Min(top, y);
                bottom = y;
            }

            var shift = bottom >= height - 1 ? 0 : (height - (bottom - top + 1)) / 2 - top;
            var colours = new byte[width * height];
            for (var y = 0; y < rows.Length; y++)
                for (var x = 0; x < Math.Min(width, rows[y].Length); x++)
                    if (rows[y][x] != '.' && y + shift >= 0 && y + shift < height) colours[(y + shift) * width + x] = (byte)(rows[y][x] - '0');
            return colours;
        }

        // Gems in mirrored pairs, first on the cells whose row and column are fullest, which clear soonest.
        private static bool[] PlaceGemsMirrored(byte[] colours, int width, int count)
        {
            var height = colours.Length / width;
            var rowFill = new int[height];
            var columnFill = new int[width];
            for (var cell = 0; cell < colours.Length; cell++)
            {
                if (colours[cell] == 0) continue;
                rowFill[cell / width]++;
                columnFill[cell % width]++;
            }

            var gems = new bool[colours.Length];
            var placed = 0;
            var order = Enumerable.Range(0, colours.Length).Where(c => colours[c] != 0 && c % width <= (width - 1) / 2)
                .OrderByDescending(c => rowFill[c / width] + columnFill[c % width]).ThenByDescending(c => c);
            foreach (var cell in order)
            {
                if (placed >= count) break;
                var mirror = cell / width * width + (width - 1 - cell % width);
                var pair = mirror != cell && colours[mirror] != 0;
                if (pair && placed + 2 > count) continue;
                gems[cell] = true;
                placed++;
                if (!pair) continue;
                gems[mirror] = true;
                placed++;
            }

            // An odd count on a board without a middle column: the last gem goes alone.
            foreach (var cell in order)
            {
                if (placed >= count) break;
                if (gems[cell]) continue;
                gems[cell] = true;
                placed++;
            }

            return gems;
        }

        // Smaller pieces weigh more, so low tiers stay forgiving even when bigger pieces are allowed.
        private (int[] Pool, int[] Weights) Pool(int tier, int heavy)
        {
            var pool = new List<int>();
            var weights = new List<int>();
            foreach (var shape in _rules.Shapes.All)
            {
                if (shape.Tier > tier) continue;
                pool.Add(shape.Id);
                weights.Add(shape.Name == "dot" ? 1 : 5 - shape.Tier + (shape.Tier > 1 ? heavy * shape.Tier : 0));
            }

            return (pool.ToArray(), weights.ToArray());
        }

        // Rubble settles at the bottom: lower rows are likelier, and cells next to rubble likelier still.
        private static int PickRubbleCell(byte[] colours, int width, int height, ref Pcg32 rng)
        {
            for (var tries = 0; ; tries++)
            {
                var x = rng.NextInt(width);
                var y = height - 1 - Math.Min(rng.NextInt(height), rng.NextInt(height));
                var cell = y * width + x;
                if (colours[cell] != 0) continue;
                var supported = y == height - 1 || colours[cell + width] != 0 || (x > 0 && colours[cell - 1] != 0) || (x < width - 1 && colours[cell + 1] != 0);
                if (supported || tries > 8) return cell;
            }
        }

        private static void BreakFullLines(byte[] colours, int width, int height, ref Pcg32 rng)
        {
            for (var y = 0; y < height; y++)
            {
                var full = true;
                for (var x = 0; x < width; x++) full &= colours[y * width + x] != 0;
                if (full) colours[y * width + rng.NextInt(width)] = 0;
            }

            for (var x = 0; x < width; x++)
            {
                var full = true;
                for (var y = 0; y < height; y++) full &= colours[y * width + x] != 0;
                if (full) colours[rng.NextInt(height) * width + x] = 0;
            }
        }

        private static bool[] PlaceGems(byte[] colours, int count, ref Pcg32 rng)
        {
            var gems = new bool[colours.Length];
            var filled = new List<int>();
            for (var cell = 0; cell < colours.Length; cell++)
            {
                if (colours[cell] != 0) filled.Add(cell);
            }

            for (var placed = 0; placed < count && filled.Count > 0; placed++)
            {
                var pick = rng.NextInt(filled.Count);
                gems[filled[pick]] = true;
                filled.RemoveAt(pick);
            }

            return gems;
        }

        // Block Puzzle has no move limit, so a state is never close to running out.
        private sealed class ProbeDomain : IProbeDomain<BlockState, PlacePiece>
        {
            private readonly BlockRules _rules;
            private readonly BlockLevel _level;

            public ProbeDomain(BlockRules rules, BlockLevel level)
            {
                _rules = rules;
                _level = level;
            }

            public (ISearchDomain<BlockState, PlacePiece> Domain, BlockState Start) Create(ulong seed) =>
                (new BlockSearchDomain(_rules, _level, seed), _rules.CreateEngine(_level, seed).State);

            public float GoalLeft(BlockState state) => PuzzleGenerator.GoalLeft(state);
            public float MovesLeft(BlockState state) => 0f;
        }
    }
}
