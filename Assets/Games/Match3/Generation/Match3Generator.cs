using System;
using System.Collections.Generic;
using System.Linq;
using Match3.Model;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;
using Tileworks.Tiles.Search;

namespace Match3.Generation
{
    // Lays out a recipe's board and tunes it on the bot's win rate (LevelTuner): moves first, then the goal, blockers and a
    // sixth colour.
    public sealed class Match3Generator
    {
        public const int CasualEpsilonPercent = 25;
        public const int MinMoves = 14;
        public const int MaxMoves = 40;
        public const int MaxDucks = 6;


        private readonly Match3Rules _rules;
        private readonly int _playouts;
        private readonly BotProfile _profile;

        // Tunes on `profile` (casual unless given); the casual bot is measured on the final level either way.
        public Match3Generator(Match3Rules rules, int playouts = 48, BotProfile? profile = null)
        {
            _rules = Guard.NotNull(rules, nameof(rules));
            _playouts = playouts;
            _profile = profile ?? BotProfile.Casual;
        }

        public static Match3Band Classify(float winRate) => winRate >= 0.75f ? Match3Band.Easy : winRate >= 0.4f ? Match3Band.Normal : Match3Band.Hard;

        public static LevelSlot SlotFor(Match3Band band) => band switch
        {
            Match3Band.Easy => new LevelSlot(0, LevelRole.Easy, 0.88f),
            Match3Band.Normal => new LevelSlot(0, LevelRole.Normal, 0.58f),
            _ => new LevelSlot(0, LevelRole.Hard, 0.3f),
        };

        public GeneratedMatch3 Generate(Match3Recipe recipe, int maxAttempts = 30)
        {
            var slot = recipe.Target ?? SlotFor(recipe.Band);
            var limits = new TuningLimits
            {
                MinMoves = FloorFor(slot.Role), MaxMoves = MaxMoves, MinColours = Math.Min(4, recipe.Colours), MaxColours = Math.Max(recipe.Colours, recipe.MaxColours),
                MinLoad = 0.4f, MaxLoad = 2f, MaxGoal = recipe.ColourGoal > 0 || recipe.Balloons > 0 || recipe.Floors > 0 || recipe.Ducks > 0 ? 3f : 1f,
                MinCasualRate = recipe.MinCasualRate, MaxAttempts = maxAttempts,
            };
            var start = new TuningKnobs { Moves = recipe.Moves, Colours = recipe.Colours, Load = 1f, Goal = 1f };
            var tuned = LevelTuner.Run(new Tunable(this, recipe), slot, start, limits);
            var win = tuned.Stats.WinRate;
            return new GeneratedMatch3
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

        public Match3Level Build(Match3Recipe recipe, int moves) => Build(recipe, moves, 1f);

        public Match3Level Build(Match3Recipe recipe, int moves, float load) => Build(recipe, moves, recipe.Colours, load);

        public Match3Level Build(Match3Recipe recipe, int moves, int colours, float load) =>
            Build(recipe, new TuningKnobs { Moves = moves, Colours = colours, Load = load, Goal = 1f });

        // Takes blockers, then goal size, off a board that leaves less of it in play than the recipe asks.
        public Match3Level Build(Match3Recipe recipe, in TuningKnobs knobs)
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

        public static float MovableShare(Match3Level level)
        {
            int movable = 0, playable = 0;
            for (var cell = 0; cell < level.CellCount; cell++)
            {
                if (!level.Playable[cell]) continue;
                playable++;
                if ((level.Pieces[cell] == Piece.Gem || Pieces.IsSpecial(level.Pieces[cell])) && !level.Chains[cell]) movable++;
            }

            return playable == 0 ? 0f : (float)movable / playable;
        }

        // Load scales the boxes and chains, Goal the colour goal, balloons, floors and ducks (as many as have a column to fall
        // down).
        private static Match3Level Lay(Match3Recipe recipe, in TuningKnobs knobs)
        {
            float load = knobs.Load, goalSize = knobs.Goal;
            int Blockers(int n) => n == 0 ? 0 : Math.Max(2, (int)Math.Round(n * load));
            int Goal(int n) => n == 0 ? 0 : Math.Max(2, (int)Math.Round(n * goalSize));
            int width = recipe.Width, height = recipe.Height, count = width * height;
            var shape = recipe.Shape == BoardShape.Full && recipe.Corners ? BoardShape.TopCorners : recipe.Shape;
            var playable = BoardShapes.Mask(shape, width, height);
            if (BoardReach.Unreachable(playable, default, width, height) > 0) throw new InvalidOperationException($"{recipe.Id}: {shape} at {width}×{height} has cells no refill reaches.");
            var overhangs = recipe.UnderOverhangs ? BoardReach.SlideFed(playable, width, height) : null;
            var pieces = new Piece[count];
            var tints = new byte[count];
            var hp = new byte[count];
            var floors = new byte[count];
            var chains = new bool[count];
            var statics = new bool[count];
            var layers = recipe.DoubleBoxes ? (byte)2 : (byte)1;
            for (var cell = 0; cell < count; cell++) pieces[cell] = playable[cell] ? Piece.Gem : Piece.Empty;

            // Boxes hold still, so they stay off each column's lowest cell for ducks to reach it, and in the lower half (a
            // frame or diamond gets the six rows above the bottom one and keeps its pocket clear).
            var lowest = new int[width];
            for (var x = 0; x < width; x++)
                for (var y = height - 1; y >= 0; y--)
                    if (playable[y * width + x]) { lowest[x] = y; break; }
            bool[] GemCells()
            {
                var free = new bool[playable.Length];
                for (var c = 0; c < free.Length; c++) free[c] = playable[c] && pieces[c] == Piece.Gem && !chains[c];
                return free;
            }

            // Chains hold still, so they stay out of the columns under ducks.
            bool[] ChainCells()
            {
                var free = GemCells();
                for (var c = 0; c < free.Length; c++)
                    for (var above = c - width; above >= 0 && free[c]; above -= width)
                        if (pieces[above] == Piece.Duck) free[c] = false;
                return free;
            }

            bool[] BoxCells()
            {
                var free = GemCells();
                for (var c = 0; c < free.Length; c++) free[c] &= c / width < lowest[c % width];
                return free;
            }

            // Cells the walls and boxes cut off start empty.
            void EmptyPens()
            {
                var sealedCells = BoardReach.Sealed(playable, statics, width, height);
                for (var cell = 0; cell < count; cell++)
                    if (sealedCells[cell]) pieces[cell] = Piece.Empty;
            }

            foreach (var cell in BoardPens.Walls(recipe.Pens, width, height))
            {
                if (!playable[cell]) continue;
                if (recipe.WallChains) chains[cell] = true;
                else (pieces[cell], hp[cell]) = (Piece.Box, layers);
                statics[cell] = true;
            }

            EmptyPens();

            // Pens come from the walls, or from boxes in a pocket shape's pockets; elsewhere a pair of boxes that would seal a
            // cell is passed over. Chains come after the pens and never seal one.
            var pens = BoardShapes.HasPockets(shape);
            var sealedBefore = BoardReach.Unreachable(playable, statics, width, height);
            bool Fits(int cell, int mirror, bool seals)
            {
                statics[cell] = statics[mirror] = true;
                if (seals || BoardReach.Unreachable(playable, statics, width, height) <= sealedBefore) return true;
                statics[cell] = statics[mirror] = false;
                return false;
            }

            var ringed = recipe.BoxPattern == BlockerPattern.Frame || recipe.BoxPattern == BlockerPattern.Diamond;
            var boxTop = ringed ? Math.Max(2, height - 7) : height / 2;
            var pocket = ringed && recipe.Boxes > 0 ? (boxTop + height - 2) / 2 : height / 2 - 1;
            PlaceGifts(recipe, pieces, playable, width, pocket);
            foreach (var cell in BoardPatterns.Pick(recipe.BoxPattern, width, height, boxTop, height - 2, Blockers(recipe.Boxes), BoxCells(), recipe.Seed ^ 0xB0, overhangs,
                         (c, m) => Fits(c, m, pens)))
                (pieces[cell], hp[cell]) = (Piece.Box, layers);

            EmptyPens();
            sealedBefore = BoardReach.Unreachable(playable, statics, width, height);

            foreach (var cell in BoardPatterns.Pick(recipe.BalloonPattern, width, height, 2, height - 1, Goal(recipe.Balloons), GemCells(), recipe.Seed ^ 0xBA, overhangs))
                pieces[cell] = Piece.Balloon;
            var ducks = PlaceDucks(pieces, playable, statics, width, height, recipe.Ducks == 0 ? 0 : Math.Clamp((int)Math.Round(recipe.Ducks * goalSize), 1, MaxDucks));
            foreach (var cell in BoardPatterns.Pick(recipe.ChainPattern, width, height, 1, height - 1, Blockers(recipe.Chains), ChainCells(), recipe.Seed ^ 0xC4, overhangs,
                         (c, m) => Fits(c, m, false)))
                chains[cell] = true;

            bool[] FloorCells()
            {
                var free = new bool[playable.Length];
                for (var c = 0; c < free.Length; c++)
                    free[c] = playable[c] && floors[c] == 0 && pieces[c] != Piece.Empty && pieces[c] != Piece.Box && pieces[c] != Piece.Duck;
                return free;
            }
            foreach (var cell in BoardPatterns.Pick(recipe.FloorPattern, width, height, height / 3, height - 1, Goal(recipe.Floors), FloorCells(), recipe.Seed ^ 0xF1, overhangs))
                floors[cell] = recipe.DoubleFloors ? (byte)2 : (byte)1;

            var goals = new List<Match3Goal>();
            if (recipe.ColourGoal > 0) goals.Add(new Match3Goal(GoalKind.Colour, Goal(recipe.ColourGoal), 1));
            AddGoal(goals, GoalKind.Box, pieces.Count(p => p == Piece.Box));
            AddGoal(goals, GoalKind.Balloon, pieces.Count(p => p == Piece.Balloon));
            AddGoal(goals, GoalKind.Duck, ducks);
            AddGoal(goals, GoalKind.Floor, floors.Count(f => f > 0));
            AddGoal(goals, GoalKind.Chain, chains.Count(c => c));
            if (goals.Count > Match3Level.MaxGoals) goals.RemoveRange(Match3Level.MaxGoals, goals.Count - Match3Level.MaxGoals);
            return new Match3Level(recipe.Id, width, height, knobs.Moves, knobs.Colours, playable, pieces, tints, hp, floors, chains, goals.ToArray());
        }

        public PlayoutStats Measure(Match3Level level, ulong seed)
        {
            var domain = new Match3SearchDomain(_rules, level, seed);
            var runner = new PlayoutRunner<Match3State, Swap>(domain);
            var start = _rules.CreateState(level, seed);
            return runner.Run(start, new EpsilonGreedyPolicy<Match3State, Swap>(CasualEpsilonPercent), _playouts, seed);
        }

        // Up to 8 starting boards, as many runs on each as the playout budget allows.
        public ProbeStats Probe(Match3Level level, ulong seed) => Probe(level, seed, BotProfile.Casual);

        private ProbeStats Casual(Match3Level level, ulong seed, int playouts, ProbeStats tuned) =>
            _profile.LooksAhead ? Probe(level, seed, BotProfile.Casual, Math.Max(playouts, _playouts)) : tuned;

        public ProbeStats Probe(Match3Level level, ulong seed, BotProfile profile) => Probe(level, seed, profile, _playouts);

        public ProbeStats Probe(Match3Level level, ulong seed, BotProfile profile, int playouts)
        {
            var seeds = Math.Min(8, playouts);
            var probe = new LevelProbe<Match3State, Swap>(new ProbeDomain(_rules, level));
            return probe.Run(profile.CreatePolicy(new Match3Evaluator()), seed, seeds, Math.Max(1, playouts / seeds));
        }

        private static float GoalLeft(Match3State s)
        {
            int left = 0, total = 0;
            for (var i = 0; i < s.Level.Goals.Length; i++)
            {
                left += Math.Max(0, s.GoalsLeft[i]);
                total += s.Level.Goals[i].Count;
            }

            return total == 0 ? 0f : (float)left / total;
        }

        private static void AddGoal(List<Match3Goal> goals, GoalKind kind, int count)
        {
            if (count > 0) goals.Add(new Match3Goal(kind, count));
        }

        // Rockets in a mirrored pair on the pocket row, a disco ball in its middle and a bomb on top of it.
        private static void PlaceGifts(Match3Recipe recipe, Piece[] pieces, bool[] playable, int width, int row)
        {
            void Put(int x, int y, Piece piece)
            {
                var cell = y * width + x;
                if (y >= 0 && playable[cell] && pieces[cell] == Piece.Gem) pieces[cell] = piece;
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

        // One duck in the middle column, two in a mirrored pair, three both. An even board has no middle column, so its
        // ducks come in pairs.
        private static int PlaceDucks(Piece[] pieces, bool[] playable, bool[] statics, int width, int height, int count)
        {
            if (width % 2 == 0 && count % 2 == 1) count = Math.Max(2, count - 1);

            // The highest gem at least three rows above the bottom that falls straight there with nothing static under it.
            int Spot(int x)
            {
                for (var y = 0; y < height - 3; y++)
                {
                    var cell = y * width + x;
                    if (!playable[cell] || pieces[cell] != Piece.Gem || !BoardReach.FallsToBottom(playable, width, height, cell)) continue;
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

        private sealed class ProbeDomain : IProbeDomain<Match3State, Swap>
        {
            private readonly Match3Rules _rules;
            private readonly Match3Level _level;

            public ProbeDomain(Match3Rules rules, Match3Level level)
            {
                _rules = rules;
                _level = level;
            }

            public (ISearchDomain<Match3State, Swap> Domain, Match3State Start) Create(ulong seed) =>
                (new Match3SearchDomain(_rules, _level, seed), _rules.CreateState(_level, seed));

            public float GoalLeft(Match3State state) => Match3Generator.GoalLeft(state);
            public float MovesLeft(Match3State state) => (float)state.MovesLeft / state.Level.Moves;
            public int Shuffles(Match3State state) => state.Shuffles;
        }

        private sealed class Tunable : ITunable<Match3Level>
        {
            private readonly Match3Generator _generator;
            private readonly Match3Recipe _recipe;

            public Tunable(Match3Generator generator, Match3Recipe recipe)
            {
                _generator = generator;
                _recipe = recipe;
            }

            public Match3Level Build(in TuningKnobs knobs) => _generator.Build(_recipe, knobs);
            public ProbeStats Probe(Match3Level level) => _generator.Probe(level, _recipe.Seed, _generator._profile);
            public ProbeStats Casual(Match3Level level, in ProbeStats tuned) =>
                _generator.Casual(level, _recipe.Seed, _recipe.Target is { } target ? LevelCurve.MinCasualPlayouts(target) : 0, tuned);
            public int Blockers(Match3Level level) => level.Pieces.Count(p => p == Piece.Box) + level.Chains.Count(c => c);
        }
    }
}
