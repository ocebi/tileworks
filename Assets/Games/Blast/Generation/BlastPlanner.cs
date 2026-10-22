using System;
using System.Collections.Generic;
using System.Linq;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Generation;

namespace Blast.Generation
{
    // The play order's recipes. Each blocker arrives alone on an easy level, later levels mix them, no two levels in a row
    // share a board shape or blocker pattern, and hard levels get more goals and two-layer boxes. Easy levels after a hard
    // one start with a special on the board. Level 2 shows a pen behind a box wall, level 4 another, and from level 11 two
    // levels a chapter have pens or pockets. The moves are tuned per level afterwards (LevelCurve).
    public static class BlastPlanner
    {
        public const int BoxesFrom = 3;
        public const int BalloonsFrom = 5;
        public const int DucksFrom = 7;
        public const int DoubleBoxesFrom = 8;

        [Flags]
        private enum Goals
        {
            Colour = 1,
            Box = 2,
            Balloon = 4,
            Duck = 8,
        }

        // Goal mixes for levels 11 on: easy levels take one goal, normal one or two, hard two or three, super hard three.
        // Ducks never come alone: a ducks-only level is won quickly or not at all.
        private static readonly Goals[] Mixes =
        {
            Goals.Box, Goals.Balloon,
            Goals.Colour | Goals.Box, Goals.Box | Goals.Balloon, Goals.Duck | Goals.Box, Goals.Colour | Goals.Balloon, Goals.Balloon | Goals.Duck,
            Goals.Duck | Goals.Colour,
            Goals.Box | Goals.Balloon | Goals.Duck, Goals.Colour | Goals.Box | Goals.Duck, Goals.Colour | Goals.Box | Goals.Balloon,
            Goals.Colour | Goals.Balloon | Goals.Duck,
        };

        private static readonly BoardShape[] OpenShapes = BoardShapes.All.Where(s => !BoardShapes.HasPockets(s)).ToArray();

        public static string IdOf(int number) => $"bl_{number:0000}";

        public static BlastRecipe[] Plan(int count)
        {
            var recipes = new BlastRecipe[count];
            for (var i = 0; i < count; i++)
            {
                var number = i + 1;
                var recipe = number <= 10 ? Opening(number) : Compose(number, recipes[i - 1], recipes[i - 2]);
                recipe.Id = IdOf(number);
                recipe.Seed = StableHash.Hash64(recipe.Id);
                recipe.Target = LevelCurve.SkilledAt(number);
                recipe.MinCasualRate = LevelCurve.MinCasualRate(recipe.Target.Value);
                recipe.MinMovable = LevelCurve.MinMovableShare(number);
                recipes[i] = recipe;
            }

            return recipes;
        }

        // Levels 1-10 introduce one thing each.
        private static BlastRecipe Opening(int number) => number switch
        {
            1 => R(7, 8, 3, 18, colourGoal: 20),
            2 => R(7, 9, 3, 20, colourGoal: 25, shape: BoardShape.TopCorners, pens: PenLayout.Intro),
            3 => R(8, 8, 4, 20, colourGoal: 20, boxes: 8, boxPattern: BlockerPattern.Rows, rockets: true),
            4 => R(8, 9, 4, 22, colourGoal: 25, shape: BoardShape.Castle, pens: PenLayout.Corners),
            5 => R(9, 9, 4, 22, balloons: 8, balloonPattern: BlockerPattern.Band, shape: BoardShape.TopCorners),
            6 => R(7, 9, 4, 22, colourGoal: 30, balloons: 6, balloonPattern: BlockerPattern.Chevron, shape: BoardShape.Castle, bomb: true),
            7 => R(8, 9, 4, 24, colourGoal: 25, ducks: 2),
            8 => R(9, 9, 4, 24, boxes: 16, doubleBoxes: true, boxPattern: BlockerPattern.Frame, shape: BoardShape.Octagon, disco: true),
            9 => R(8, 9, 4, 26, balloons: 8, ducks: 2, balloonPattern: BlockerPattern.Diamond, shape: BoardShape.Hourglass),
            _ => R(9, 10, 5, 28, colourGoal: 35, boxes: 18, doubleBoxes: true, boxPattern: BlockerPattern.Rows, shape: BoardShape.Plus),
        };

        private static BlastRecipe Compose(int number, BlastRecipe previous, BlastRecipe before)
        {
            var slot = LevelCurve.At(number);
            var rng = new Pcg32(StableHash.Hash64(IdOf(number) + "/plan"));
            var chapter = (number - 1) / 10;
            var role = slot.Role;

            var (pens, pockets) = BoardPens.ForLevel(number);
            // Pens and pockets hold boxes, so their goals do too; a box-only level before one may leave its mix the only choice.
            var mixes = Mixes.Where(m => Count(m) == (role == LevelRole.Easy ? 1 : role == LevelRole.Normal ? 1 + (number + chapter) % 2 : role == LevelRole.Hard ? 2 + number % 2 : 3))
                .Where(m => pens == PenLayout.None && !pockets || (m & Goals.Box) != 0).ToArray();
            var fresh = mixes.Where(m => m != Kinds(previous)).ToArray();
            var goals = fresh.Length > 0 ? fresh[rng.NextInt(fresh.Length)] : mixes[rng.NextInt(mixes.Length)];

            var gift = slot.IsBreather || role == LevelRole.Normal && rng.Chance(1, 4);
            var giftKind = (number / 3) % 3;
            var width = role == LevelRole.Easy ? 7 + 2 * rng.NextInt(2) : role == LevelRole.Normal ? 8 + rng.NextInt(2) : 9;
            if (gift && giftKind > 0 && width % 2 == 0) width++;
            var height = role == LevelRole.Easy ? 9 : role == LevelRole.Normal ? 9 + rng.NextInt(2) : 10;
            var cells = width * height;
            var shapes = pockets ? BoardShapes.Pockets : OpenShapes.Where(s => pens == PenLayout.None || BoardPens.Fits(pens, BoardShapes.Mask(s, width, height), width, height));
            var shape = Pick(shapes.Where(s => s != Shape(previous) && s != Shape(before)).ToArray(), ref rng);
            var colours = role >= LevelRole.Hard || role == LevelRole.Normal && chapter >= 3 && rng.Chance(1, 3) ? 5 : 4;

            var recipe = new BlastRecipe
            {
                Width = width, Height = height, Colours = colours, Shape = shape, Pens = pens, UnderOverhangs = pockets && role >= LevelRole.Hard, Moves = role >= LevelRole.Hard ? 30 : role == LevelRole.Normal ? 26 : 22,
                DoubleBoxes = role == LevelRole.SuperHard || role == LevelRole.Hard && rng.Chance(2, 3) || role == LevelRole.Normal && rng.Chance(1, 3),
            };

            var weight = role == LevelRole.Easy ? 0.7f : role == LevelRole.Normal ? 1f : role == LevelRole.Hard ? 1.25f : 1.4f;
            weight *= 1f + chapter * 0.03f;
            if ((goals & Goals.Colour) != 0) recipe.ColourGoal = Math.Min(50, (int)Math.Round((25 + chapter * 2) * weight));
            // Walls are boxes already.
            if ((goals & Goals.Box) != 0) recipe.Boxes = (int)Math.Round(cells * 0.17f * weight * (pens != PenLayout.None ? 0.4f : 1f));
            if ((goals & Goals.Balloon) != 0) recipe.Balloons = (int)Math.Round((7 + chapter / 2) * weight);
            if ((goals & Goals.Duck) != 0) recipe.Ducks = role >= LevelRole.Hard ? 3 : 2;

            var patterns = BoardPatterns.All.Where(p => p != previous.BoxPattern && p != previous.BalloonPattern).ToArray();
            recipe.BoxPattern = Pick(patterns, ref rng);
            recipe.BalloonPattern = Pick(patterns.Where(p => p != recipe.BoxPattern && p != BlockerPattern.Frame).ToArray(), ref rng);
            if (gift)
            {
                recipe.Rockets = giftKind == 0 ? 2 : 0;
                recipe.Bomb = giftKind == 1;
                recipe.Disco = giftKind == 2;
            }
            else if (recipe.BoxPattern == BlockerPattern.Frame && width % 2 == 1)
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

        private static Goals Kinds(BlastRecipe recipe) => (recipe.ColourGoal > 0 ? Goals.Colour : 0) | (recipe.Boxes > 0 ? Goals.Box : 0) |
                                                         (recipe.Balloons > 0 ? Goals.Balloon : 0) | (recipe.Ducks > 0 ? Goals.Duck : 0);

        private static BoardShape Shape(BlastRecipe recipe) => recipe.Shape == BoardShape.Full && recipe.Corners ? BoardShape.TopCorners : recipe.Shape;

        private static T Pick<T>(IReadOnlyList<T> items, ref Pcg32 rng) => items[rng.NextInt(items.Count)];

        private static BlastRecipe R(int width, int height, int colours, int moves, int colourGoal = 0, int boxes = 0, bool doubleBoxes = false,
            int balloons = 0, int ducks = 0, BoardShape shape = BoardShape.Full, BlockerPattern boxPattern = BlockerPattern.Scatter,
            BlockerPattern balloonPattern = BlockerPattern.Scatter, bool rockets = false, bool bomb = false, bool disco = false, PenLayout pens = PenLayout.None) =>
            new BlastRecipe
            {
                Width = width, Height = height, Colours = colours, Moves = moves, ColourGoal = colourGoal, Boxes = boxes, DoubleBoxes = doubleBoxes,
                Balloons = balloons, Ducks = ducks, Shape = shape, BoxPattern = boxPattern, BalloonPattern = balloonPattern, Rockets = rockets ? 2 : 0,
                Bomb = bomb, Disco = disco, Pens = pens,
            };
    }
}
