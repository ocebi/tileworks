using System;
using System.Collections.Generic;
using System.Linq;
using Blast.Model;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;
using Tileworks.Tiles.Search;

namespace Blast.Generation
{
    // Lays out a recipe's board and tunes it on the bot's win rate (LevelTuner): moves first, then the goal, boxes and a
    // fifth colour.
    public sealed class BlastGenerator
    {
        public const int CasualEpsilonPercent = 25;
        public const int MinMoves = 15;
        public const int MaxMoves = 40;
        public const int MaxDucks = 6;
        public const int MaxColours = 5;

        private readonly BlastRules _rules;
        private readonly int _playouts;
        private readonly BotProfile _profile;

        // Tunes on `profile` (casual unless given); the casual bot is measured on the final level either way.
        public BlastGenerator(BlastRules rules, int playouts = 96, BotProfile? profile = null)
        {
            _rules = Guard.NotNull(rules, nameof(rules));
            _playouts = playouts;
            _profile = profile ?? BotProfile.Casual;
        }

        public static BlastBand Classify(float winRate) => winRate >= 0.75f ? BlastBand.Easy : winRate >= 0.4f ? BlastBand.Normal : BlastBand.Hard;

        public static LevelSlot SlotFor(BlastBand band) => band switch
        {
            BlastBand.Easy => new LevelSlot(0, LevelRole.Easy, 0.88f),
            BlastBand.Normal => new LevelSlot(0, LevelRole.Normal, 0.58f),
            _ => new LevelSlot(0, LevelRole.Hard, 0.3f),
        };

        public GeneratedBlast Generate(BlastRecipe recipe, int maxAttempts = 30)
        {
            var slot = recipe.Target ?? SlotFor(recipe.Band);
            var limits = new TuningLimits
            {
                MinMoves = FloorFor(slot.Role), MaxMoves = MaxMoves, MinColours = Math.Min(4, recipe.Colours), MaxColours = MaxColours, MinLoad = 0.5f, MaxLoad = 2f,
                MaxGoal = recipe.ColourGoal > 0 || recipe.Balloons > 0 || recipe.Ducks > 0 ? 3f : 1f, MinCasualRate = recipe.MinCasualRate, MaxAttempts = maxAttempts,
            };
            var start = new TuningKnobs { Moves = recipe.Moves, Colours = recipe.Colours, Load = 1f, Goal = 1f };
            var tuned = LevelTuner.Run(new Tunable(this, recipe), slot, start, limits);
            var win = tuned.Stats.WinRate;
            return new GeneratedBlast
            {
                Level = tuned.Level, Stats = tuned.Stats, Casual = tuned.Casual, Knobs = tuned.Knobs, Band = Classify(win), Role = LevelCurve.Classify(win, _profile),
                Attempts = tuned.Attempts, HitBand = tuned.HitBand,
            };
        }

        // Hard levels leave room to move; a level still too easy at its floor gets a bigger goal, then more blockers.
        public static int FloorFor(LevelRole role) => role switch
        {
            LevelRole.Easy => 16,
            LevelRole.Normal => MinMoves,
            LevelRole.Hard => 20,
            _ => 22,
        };

        public BlastLevel Build(BlastRecipe recipe, int moves) => Build(recipe, moves, recipe.Colours, 1f);

        public BlastLevel Build(BlastRecipe recipe, int moves, int colours, float load) =>
            Build(recipe, new TuningKnobs { Moves = moves, Colours = colours, Load = load, Goal = 1f });

        // Takes boxes, then goal size, off a board that leaves less of it in play than the recipe asks.
        public BlastLevel Build(BlastRecipe recipe, in TuningKnobs knobs)
        {
            var trimmed = knobs;
            while (true)
            {
                var level = Lay(recipe, trimmed);
                if (MovableShare(level) >= recipe.MinMovable || trimmed.Goal < 0.3f) return level;
                if (trimmed.Load > 0.1f) trimmed.Load *= 0.8f;
                else trimmed.Goal *= 0.8f;
            }
        }

        public static float MovableShare(BlastLevel level)
        {
            int movable = 0, playable = 0;
            for (var cell = 0; cell < level.CellCount; cell++)
            {
                if (!level.Playable[cell]) continue;
                playable++;
                if (level.Pieces[cell] == Piece.Cube || Pieces.IsSpecial(level.Pieces[cell])) movable++;
            }

            return playable == 0 ? 0f : (float)movable / playable;
        }

        // Load scales the boxes, Goal the colour goal, balloons and ducks (as many as have a column to fall down).
        private static BlastLevel Lay(BlastRecipe recipe, in TuningKnobs knobs)
        {
            float load = knobs.Load, goalSize = knobs.Goal;
            int Boxes(int count) => count == 0 ? 0 : Math.Max(2, (int)Math.Round(count * load));
            int Goal(int count) => count == 0 ? 0 : Math.Max(2, (int)Math.Round(count * goalSize));
            int width = recipe.Width, height = recipe.Height, count = width * height;
            var shape = recipe.Shape == BoardShape.Full && recipe.Corners ? BoardShape.TopCorners : recipe.Shape;
            var playable = BoardShapes.Mask(shape, width, height);
            if (BoardReach.Unreachable(playable, default, width, height) > 0) throw new InvalidOperationException($"{recipe.Id}: {shape} at {width}×{height} has cells no refill reaches.");
            var overhangs = recipe.UnderOverhangs ? BoardReach.SlideFed(playable, width, height) : null;
            var pieces = new Piece[count];
            var tints = new byte[count];
            var hp = new byte[count];
            var statics = new bool[count];
            var layers = recipe.DoubleBoxes ? (byte)2 : (byte)1;
            for (var cell = 0; cell < count; cell++) pieces[cell] = playable[cell] ? Piece.Cube : Piece.Empty;
            bool[] FreeCells()
            {
                var free = new bool[playable.Length];
                for (var c = 0; c < free.Length; c++) free[c] = playable[c] && pieces[c] == Piece.Cube;
                return free;
            }

            void PutBox(int cell)
            {
                (pieces[cell], hp[cell]) = (Piece.Box, layers);
                statics[cell] = true;
            }

            // Cells the boxes cut off start empty.
            void EmptyPens()
            {
                var sealedCells = BoardReach.Sealed(playable, statics, width, height);
                for (var cell = 0; cell < count; cell++)
                    if (sealedCells[cell]) (pieces[cell], hp[cell]) = (Piece.Empty, 0);
            }

            foreach (var cell in BoardPens.Walls(recipe.Pens, width, height))
                if (playable[cell]) PutBox(cell);
            EmptyPens();

            // Boxes sit in the lower half (a frame or diamond gets the bottom six rows and keeps its pocket clear), balloons
            // anywhere below the top two rows, ducks at the top of their columns so they have a way to fall. Pens come from the
            // walls, or from boxes in a pocket shape's pockets; elsewhere a pair of boxes that would seal a cell is passed over.
            var ringed = recipe.BoxPattern == BlockerPattern.Frame || recipe.BoxPattern == BlockerPattern.Diamond;
            var boxTop = ringed ? Math.Max(2, height - 6) : height / 2;
            var pocket = ringed && recipe.Boxes > 0 ? (boxTop + height - 1) / 2 : height / 2 - 1;
            PlaceGifts(recipe, pieces, playable, width, pocket);
            var pens = BoardShapes.HasPockets(shape);
            var sealedBefore = BoardReach.Unreachable(playable, statics, width, height);
            bool Fits(int cell, int mirror)
            {
                statics[cell] = statics[mirror] = true;
                if (pens || BoardReach.Unreachable(playable, statics, width, height) <= sealedBefore) return true;
                statics[cell] = statics[mirror] = false;
                return false;
            }

            foreach (var cell in BoardPatterns.Pick(recipe.BoxPattern, width, height, boxTop, height - 1, Boxes(recipe.Boxes), FreeCells(), recipe.Seed ^ 0xB0, overhangs, Fits))
                PutBox(cell);

            EmptyPens();
            foreach (var cell in BoardPatterns.Pick(recipe.BalloonPattern, width, height, 2, height - 1, Goal(recipe.Balloons), FreeCells(), recipe.Seed ^ 0xBA, overhangs))
                pieces[cell] = Piece.Balloon;
            var ducks = PlaceDucks(pieces, playable, statics, width, height, recipe.Ducks == 0 ? 0 : Math.Clamp((int)Math.Round(recipe.Ducks * goalSize), 1, MaxDucks));

            var goals = new List<BlastGoal>();
            if (recipe.ColourGoal > 0) goals.Add(new BlastGoal(GoalKind.Colour, Goal(recipe.ColourGoal), 1));
            var boxes = pieces.Count(p => p == Piece.Box);
            var balloons = pieces.Count(p => p == Piece.Balloon);
            if (boxes > 0) goals.Add(new BlastGoal(GoalKind.Box, boxes));
            if (balloons > 0) goals.Add(new BlastGoal(GoalKind.Balloon, balloons));
            if (ducks > 0) goals.Add(new BlastGoal(GoalKind.Duck, ducks));
            return new BlastLevel(recipe.Id, width, height, knobs.Moves, knobs.Colours, playable, pieces, tints, hp, goals.ToArray());
        }

        public PlayoutStats Measure(BlastLevel level, ulong seed)
        {
            var domain = new BlastSearchDomain(_rules, level, seed);
            var runner = new PlayoutRunner<BlastState, Tap>(domain);
            var start = _rules.CreateState(level, seed);
            return runner.Run(start, new EpsilonGreedyPolicy<BlastState, Tap>(CasualEpsilonPercent), _playouts, seed);
        }

        // Up to 12 starting boards, as many runs on each as the playout budget allows.
        public ProbeStats Probe(BlastLevel level, ulong seed) => Probe(level, seed, BotProfile.Casual);

        private ProbeStats Casual(BlastLevel level, ulong seed, int playouts, ProbeStats tuned) =>
            _profile.LooksAhead ? Probe(level, seed, BotProfile.Casual, Math.Max(playouts, _playouts)) : tuned;

        public ProbeStats Probe(BlastLevel level, ulong seed, BotProfile profile) => Probe(level, seed, profile, _playouts);

        public ProbeStats Probe(BlastLevel level, ulong seed, BotProfile profile, int playouts)
        {
            var seeds = Math.Min(12, playouts);
            var probe = new LevelProbe<BlastState, Tap>(new ProbeDomain(_rules, level));
            return probe.Run(profile.CreatePolicy(new BlastEvaluator()), seed, seeds, Math.Max(1, playouts / seeds));
        }

        private static float GoalLeft(BlastState s)
        {
            int left = 0, total = 0;
            for (var i = 0; i < s.Level.Goals.Length; i++)
            {
                left += Math.Max(0, s.GoalsLeft[i]);
                total += s.Level.Goals[i].Count;
            }

            return total == 0 ? 0f : (float)left / total;
        }

        // Rockets in a mirrored pair on the pocket row, a disco ball in its middle and a bomb on top of it.
        private static void PlaceGifts(BlastRecipe recipe, Piece[] pieces, bool[] playable, int width, int row)
        {
            void Put(int x, int y, Piece piece)
            {
                var cell = y * width + x;
                if (y >= 0 && playable[cell] && pieces[cell] == Piece.Cube) pieces[cell] = piece;
            }

            var centre = (width - 1) / 2;
            if (recipe.Rockets > 0)
            {
                Put(Math.Max(0, centre - 2), row, Piece.RocketColumn);
                Put(width - 1 - Math.Max(0, centre - 2), row, Piece.RocketColumn);
            }

            if (recipe.Disco) Put(centre, row, Piece.Disco);
            if (recipe.Bomb) Put(centre, recipe.Disco ? row - 1 : row, Piece.Bomb);
        }

        // One duck in the middle column, two in a mirrored pair, three both.
        private static int PlaceDucks(Piece[] pieces, bool[] playable, bool[] statics, int width, int height, int count)
        {
            if (width % 2 == 0 && count % 2 == 1) count = Math.Max(2, count - 1);

            // The highest cube at least three rows above the bottom that falls straight there with nothing static under it.
            int Spot(int x)
            {
                for (var y = 0; y < height - 3; y++)
                {
                    var cell = y * width + x;
                    if (!playable[cell] || pieces[cell] != Piece.Cube || !BoardReach.FallsToBottom(playable, width, height, cell)) continue;
                    for (var below = cell + width; below < pieces.Length; below += width)
                        if (statics[below]) return -1;
                    return cell;
                }

                return -1;
            }

            // Without a clear middle column, an odd count rounds up to pairs.
            var placed = 0;
            var middle = width % 2 == 1 ? Spot((width - 1) / 2) : -1;
            if (count % 2 == 1 && middle < 0) count++;
            else if (count % 2 == 1)
            {
                pieces[middle] = Piece.Duck;
                placed++;
            }

            foreach (var x in new[] { 1, 3, 2, 0 })
            {
                if (placed + 2 > count) break;
                if (x >= (width - 1) / 2) continue;
                int left = Spot(x), right = Spot(width - 1 - x);
                if (left < 0 || right < 0) continue;
                pieces[left] = pieces[right] = Piece.Duck;
                placed += 2;
            }

            return placed;
        }

        private sealed class ProbeDomain : IProbeDomain<BlastState, Tap>
        {
            private readonly BlastRules _rules;
            private readonly BlastLevel _level;

            public ProbeDomain(BlastRules rules, BlastLevel level)
            {
                _rules = rules;
                _level = level;
            }

            public (ISearchDomain<BlastState, Tap> Domain, BlastState Start) Create(ulong seed) =>
                (new BlastSearchDomain(_rules, _level, seed), _rules.CreateState(_level, seed));

            public float GoalLeft(BlastState state) => BlastGenerator.GoalLeft(state);
            public float MovesLeft(BlastState state) => (float)state.MovesLeft / state.Level.Moves;
            public int Shuffles(BlastState state) => state.Shuffles;
        }

        private sealed class Tunable : ITunable<BlastLevel>
        {
            private readonly BlastGenerator _generator;
            private readonly BlastRecipe _recipe;

            public Tunable(BlastGenerator generator, BlastRecipe recipe)
            {
                _generator = generator;
                _recipe = recipe;
            }

            public BlastLevel Build(in TuningKnobs knobs) => _generator.Build(_recipe, knobs);
            public ProbeStats Probe(BlastLevel level) => _generator.Probe(level, _recipe.Seed, _generator._profile);
            public ProbeStats Casual(BlastLevel level, in ProbeStats tuned) =>
                _generator.Casual(level, _recipe.Seed, _recipe.Target is { } target ? LevelCurve.MinCasualPlayouts(target) : 0, tuned);
            public int Blockers(BlastLevel level) => level.Pieces.Count(p => p == Piece.Box);
        }
    }
}
