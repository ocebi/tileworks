using System;
using System.Collections.Generic;
using System.Linq;
using Match3.Model;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;

namespace Match3.Generation
{
    // The play order's recipes. Each blocker arrives alone on an easy level, later levels mix them, no two levels in a row
    // share a board shape or blocker pattern, and hard levels get more goals and two-layer boxes and floors. Easy levels
    // after a hard one start with a special on the board. Level 2 shows a pen behind a box wall, level 5 another, and from
    // level 11 two levels a chapter have pens or pockets. No sixth colour before level 21. The moves are tuned per level
    // afterwards (LevelCurve).
    public static class Match3Planner
    {
        [Flags]
        private enum Goals
        {
            Colour = 1,
            Box = 2,
            Floor = 4,
            Chain = 8,
            Balloon = 16,
            Duck = 32,
        }

        // Goal mixes for levels 11 on: easy levels take one goal, normal one or two, hard two or three, super hard three.
        // Ducks never come alone: a ducks-only level is won quickly or not at all.
        private static readonly Goals[] Mixes =
        {
            Goals.Box, Goals.Floor, Goals.Chain, Goals.Balloon,
            Goals.Colour | Goals.Box, Goals.Floor | Goals.Box, Goals.Floor | Goals.Chain, Goals.Colour | Goals.Floor, Goals.Balloon | Goals.Duck,
            Goals.Chain | Goals.Box, Goals.Floor | Goals.Duck, Goals.Balloon | Goals.Floor, Goals.Colour | Goals.Chain, Goals.Duck | Goals.Box,
            Goals.Floor | Goals.Box | Goals.Chain, Goals.Floor | Goals.Duck | Goals.Balloon, Goals.Colour | Goals.Box | Goals.Duck,
            Goals.Floor | Goals.Chain | Goals.Balloon, Goals.Box | Goals.Balloon | Goals.Duck, Goals.Colour | Goals.Floor | Goals.Box,
        };

        public const int SixthColourFrom = 21;

        private static readonly BoardShape[] OpenShapes = BoardShapes.All.Where(s => !BoardShapes.HasPockets(s)).ToArray();

        public static string IdOf(int number) => $"m3_{number:0000}";

        public static Match3Recipe[] Plan(int count)
        {
            var recipes = new Match3Recipe[count];
            for (var i = 0; i < count; i++)
            {
                var number = i + 1;
                var recipe = number <= 10 ? Opening(number) : Compose(number, recipes[i - 1], recipes[i - 2]);
                recipe.Id = IdOf(number);
                recipe.Seed = StableHash.Hash64(recipe.Id);
                recipe.Target = LevelCurve.SkilledAt(number);
                recipe.MinCasualRate = LevelCurve.MinCasualRate(recipe.Target.Value);
                recipe.MinMovable = LevelCurve.MinMovableShare(number);
                recipe.MaxColours = number < SixthColourFrom ? 5 : Match3Level.MaxColours;
                recipes[i] = recipe;
            }

            return recipes;
        }

        // Levels 1-10 introduce one thing each: a pen behind boxes at 2, boxes as a goal at 3, floors at 4, chains at 6, balloons
        // at 7, ducks at 9.
        private static Match3Recipe Opening(int number) => number switch
        {
            1 => R(7, 8, 4, 20, colourGoal: 20),
            2 => R(7, 9, 4, 20, colourGoal: 25, shape: BoardShape.TopCorners, pens: PenLayout.Intro),
            3 => R(8, 8, 5, 22, colourGoal: 20, boxes: 8, boxPattern: BlockerPattern.Rows, rockets: true),
            4 => R(8, 9, 5, 22, floors: 16, floorPattern: BlockerPattern.Diamond, shape: BoardShape.Bevel),
            5 => R(9, 9, 5, 24, colourGoal: 25, shape: BoardShape.Tower, pens: PenLayout.Corners),
            6 => R(7, 9, 5, 24, chains: 8, chainPattern: BlockerPattern.Checker, shape: BoardShape.Castle, bomb: true),
            7 => R(8, 9, 5, 24, balloons: 8, balloonPattern: BlockerPattern.Chevron),
            8 => R(9, 9, 5, 26, floors: 20, floorPattern: BlockerPattern.Frame, shape: BoardShape.Octagon, disco: true),
            9 => R(8, 9, 5, 26, colourGoal: 25, ducks: 2, shape: BoardShape.Hourglass),
            _ => R(9, 10, 5, 28, boxes: 14, doubleBoxes: true, boxPattern: BlockerPattern.Rows, chains: 6, chainPattern: BlockerPattern.Columns,
                shape: BoardShape.Plus),
        };

        private static Match3Recipe Compose(int number, Match3Recipe previous, Match3Recipe before)
        {
            var slot = LevelCurve.At(number);
            var rng = new Pcg32(StableHash.Hash64(IdOf(number) + "/plan"));
            var chapter = (number - 1) / 10;
            var role = slot.Role;

            // Walls are boxes or chains, so their goals hold one of those; hard pocket boards get boxes to seal pens with. A level
            // before with the only such mix may leave it the only choice.
            var size = role == LevelRole.Easy ? 1 : role == LevelRole.Normal ? 1 + (number + chapter) % 2 : role == LevelRole.Hard ? 2 + number % 2 : 3;
            var (pens, pockets) = BoardPens.ForLevel(number);
            var walls = pens != PenLayout.None;
            var mixes = Mixes.Where(m => Count(m) == size && (!walls || (m & (Goals.Box | Goals.Chain)) != 0) && (!pockets || (m & Goals.Box) != 0)).ToArray();
            var fresh = mixes.Where(m => m != Kinds(previous)).ToArray();
            var goals = fresh.Length > 0 ? fresh[rng.NextInt(fresh.Length)] : mixes[rng.NextInt(mixes.Length)];

            var gift = slot.IsBreather || role == LevelRole.Normal && rng.Chance(1, 4);
            var giftKind = (number / 3) % 3;
            var width = role == LevelRole.Easy ? 7 + 2 * rng.NextInt(2) : role == LevelRole.Normal ? 8 + rng.NextInt(2) : 9;
            if (gift && giftKind > 0 && width % 2 == 0) width++;
            var height = role == LevelRole.Easy ? 9 : role == LevelRole.Normal ? 9 + rng.NextInt(2) : 10;
            var cells = width * height;
            var shapes = pockets ? BoardShapes.Pockets : OpenShapes.Where(s => !walls || BoardPens.Fits(pens, BoardShapes.Mask(s, width, height), width, height));
            var shape = Pick(shapes.Where(s => s != Shape(previous) && s != Shape(before)).ToArray(), ref rng);
            var wallChains = walls && (goals & Goals.Box) == 0;

            var recipe = new Match3Recipe
            {
                Width = width, Height = height, Colours = role == LevelRole.Easy ? 4 : 5, Shape = shape, Pens = pens, WallChains = wallChains,
                UnderOverhangs = pockets && role >= LevelRole.Hard,
                Moves = role >= LevelRole.Hard ? 28 : role == LevelRole.Normal ? 25 : 22,
                DoubleBoxes = role == LevelRole.SuperHard || role == LevelRole.Hard && rng.Chance(2, 3) || role == LevelRole.Normal && rng.Chance(1, 3),
                DoubleFloors = number >= 12 && (role >= LevelRole.Hard && rng.Chance(1, 2) || role == LevelRole.Normal && rng.Chance(1, 4)),
            };

            var weight = (role <= LevelRole.Normal ? 1f : role == LevelRole.Hard ? 1.25f : 1.4f) * (1f + chapter * 0.03f);
            if ((goals & Goals.Colour) != 0) recipe.ColourGoal = Math.Min(40, (int)Math.Round((22 + chapter * 2) * weight));
            // Walls count towards their blocker.
            if ((goals & Goals.Box) != 0) recipe.Boxes = (int)Math.Round(cells * 0.14f * weight * (walls ? 0.4f : 1f));
            if ((goals & Goals.Floor) != 0) recipe.Floors = (int)Math.Round(cells * 0.22f * weight);
            if ((goals & Goals.Chain) != 0) recipe.Chains = (int)Math.Round((7 + chapter / 2) * weight * (wallChains ? 0.4f : 1f));
            if ((goals & Goals.Balloon) != 0) recipe.Balloons = (int)Math.Round((7 + chapter / 2) * weight);
            if ((goals & Goals.Duck) != 0) recipe.Ducks = role >= LevelRole.Hard ? 3 : 2;

            var used = new List<BlockerPattern> { previous.BoxPattern, previous.FloorPattern };
            var patterns = BoardPatterns.All.Where(p => !used.Contains(p)).ToArray();
            recipe.BoxPattern = Pick(patterns, ref rng);
            recipe.FloorPattern = Pick(patterns.Where(p => p != recipe.BoxPattern).ToArray(), ref rng);
            recipe.ChainPattern = Pick(patterns.Where(p => p != recipe.BoxPattern && p != BlockerPattern.Frame).ToArray(), ref rng);
            recipe.BalloonPattern = Pick(patterns.Where(p => p != recipe.BoxPattern && p != BlockerPattern.Frame).ToArray(), ref rng);
            if (gift)
            {
                recipe.Rockets = giftKind == 0 ? 2 : 0;
                recipe.Bomb = giftKind == 1;
                recipe.Disco = giftKind == 2;
            }
            else if (recipe.Boxes > 0 && recipe.BoxPattern == BlockerPattern.Frame && width % 2 == 1)
            {
                // A frame of boxes holds a bomb in its pocket.
                recipe.Bomb = true;
            }

            return recipe;
        }

        private static int Count(Goals goals)
        {
            var count = 0;
            for (var bits = (int)goals; bits != 0; bits &= bits - 1) count++;
            return count;
        }

        private static Goals Kinds(Match3Recipe r) => (r.ColourGoal > 0 ? Goals.Colour : 0) | (r.Boxes > 0 ? Goals.Box : 0) | (r.Floors > 0 ? Goals.Floor : 0) |
                                                     (r.Chains > 0 ? Goals.Chain : 0) | (r.Balloons > 0 ? Goals.Balloon : 0) | (r.Ducks > 0 ? Goals.Duck : 0);

        private static BoardShape Shape(Match3Recipe recipe) => recipe.Shape == BoardShape.Full && recipe.Corners ? BoardShape.TopCorners : recipe.Shape;

        private static T Pick<T>(IReadOnlyList<T> items, ref Pcg32 rng) => items[rng.NextInt(items.Count)];

        private static Match3Recipe R(int width, int height, int colours, int moves, int colourGoal = 0, int boxes = 0, bool doubleBoxes = false,
            int balloons = 0, int ducks = 0, int floors = 0, int chains = 0, BoardShape shape = BoardShape.Full,
            BlockerPattern boxPattern = BlockerPattern.Scatter, BlockerPattern balloonPattern = BlockerPattern.Scatter,
            BlockerPattern floorPattern = BlockerPattern.Scatter, BlockerPattern chainPattern = BlockerPattern.Scatter, bool rockets = false, bool bomb = false,
            bool disco = false, PenLayout pens = PenLayout.None) =>
            new Match3Recipe
            {
                Width = width, Height = height, Colours = colours, Moves = moves, ColourGoal = colourGoal, Boxes = boxes, DoubleBoxes = doubleBoxes,
                Balloons = balloons, Ducks = ducks, Floors = floors, Chains = chains, Shape = shape, BoxPattern = boxPattern,
                BalloonPattern = balloonPattern, FloorPattern = floorPattern, ChainPattern = chainPattern, Rockets = rockets ? 2 : 0, Bomb = bomb, Disco = disco,
                Pens = pens,
            };
    }
}
