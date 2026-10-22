using System.Linq;
using NUnit.Framework;
using Tileworks.Tiles.Generation;

namespace Tileworks.Tiles.Tests
{
    public sealed class GenerationTests
    {
        [Test]
        public void Curve_PutsHardLevelsFifthAndTenth_WithABreatherAfterEach()
        {
            for (var number = 11; number <= 100; number++)
            {
                var slot = LevelCurve.At(number);
                var position = (number - 1) % 10 + 1;
                if (position == 5 || position == 10) Assert.That(slot.Role, Is.GreaterThanOrEqualTo(LevelRole.Hard), $"level {number}");
                else if (position == 1 || position == 6) Assert.That(slot.Role, Is.EqualTo(LevelRole.Easy), $"level {number}");
                else Assert.That(slot.Role, Is.EqualTo(LevelRole.Normal), $"level {number}");
            }

            Assert.That(Enumerable.Range(1, 100).Where(n => LevelCurve.At(n).Role == LevelRole.SuperHard), Is.EqualTo(new[] { 40, 60, 80, 100 }));
            Assert.That(Enumerable.Range(1, 9).Select(n => LevelCurve.At(n).Role), Has.None.GreaterThanOrEqualTo(LevelRole.Hard), "nothing hard before 10");
        }

        [Test]
        public void Curve_TargetsSitInsideTheirBand_AndNormalLevelsHardenEveryChapter()
        {
            for (var number = 1; number <= 100; number++)
            {
                var slot = LevelCurve.At(number);
                Assert.That(LevelCurve.Classify(slot.WinRate), Is.EqualTo(slot.Role), $"level {number}");
                Assert.That(slot.Low, Is.LessThan(slot.WinRate).And.GreaterThanOrEqualTo(0.1f), $"level {number}");
            }

            Assert.That(LevelCurve.At(93).WinRate, Is.LessThan(LevelCurve.At(13).WinRate));
            Assert.That(LevelCurve.At(95).WinRate, Is.LessThan(LevelCurve.At(15).WinRate));
        }

        [Test]
        public void EveryShape_IsMirrored_AndKeepsACellInMostColumns([Values(7, 8, 9)] int width, [Values(8, 10, 11)] int height)
        {
            foreach (var shape in BoardShapes.All)
            {
                var mask = BoardShapes.Mask(shape, width, height);
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                        Assert.That(mask[y * width + x], Is.EqualTo(mask[y * width + width - 1 - x]), $"{shape} {width}x{height} at {x},{y}");
                Assert.That(mask.Count(c => c), Is.GreaterThan(width * height / 2), $"{shape} keeps most of the board");
            }
        }

        [Test]
        public void EveryPattern_PicksMirroredFreeCells_InsideItsRows([Values(7, 8, 9)] int width)
        {
            const int height = 10;
            foreach (var pattern in BoardPatterns.All)
            {
                foreach (var count in new[] { 3, 8, 15 })
                {
                    var free = new bool[width * height];
                    for (var cell = 0; cell < free.Length; cell++) free[cell] = cell % width != 0 || cell / width != 9;
                    var picked = BoardPatterns.Pick(pattern, width, height, 4, 9, count, free, 7);
                    Assert.That(picked.Count, Is.LessThanOrEqualTo(count).And.GreaterThanOrEqualTo(count - 1), $"{pattern} {count} on {width}");
                    Assert.That(picked.Distinct().Count(), Is.EqualTo(picked.Count));
                    foreach (var cell in picked)
                    {
                        Assert.That(cell / width, Is.InRange(4, 9));
                        Assert.That(free[cell], Is.True);
                        if (cell % width != (width - 1) / 2 || width % 2 == 0)
                            Assert.That(picked, Has.Member(cell / width * width + width - 1 - cell % width), $"{pattern} {count}: {cell} without its mirror");
                    }
                }
            }
        }

        [Test]
        public void EveryShape_IsReachedByRefills_AndPocketShapesHaveSlideFedCells([Values(7, 8, 9)] int width, [Values(8, 9, 10, 11)] int height)
        {
            foreach (var shape in BoardShapes.All)
            {
                var mask = BoardShapes.Mask(shape, width, height);
                Assert.That(BoardReach.Unreachable(mask, default, width, height), Is.Zero, $"{shape} {width}×{height}");
                var fed = BoardReach.SlideFed(mask, width, height).Count(f => f);
                if (BoardShapes.HasPockets(shape)) Assert.That(fed, Is.GreaterThanOrEqualTo(2), $"{shape} {width}×{height}");
                else Assert.That(fed, Is.Zero, $"{shape} {width}×{height}");
            }
        }

        [Test]
        public void EveryPenLayout_SealsMirroredCells_BehindItsWalls([Values(7, 8, 9)] int width, [Values(9, 10)] int height)
        {
            var full = BoardShapes.Mask(BoardShape.Full, width, height);
            foreach (var layout in BoardPens.Layouts.Append(PenLayout.Intro))
            {
                Assert.That(BoardPens.Fits(layout, full, width, height), Is.True, $"{layout} {width}×{height}");
                var walls = new bool[full.Length];
                foreach (var cell in BoardPens.Walls(layout, width, height)) walls[cell] = true;
                var pens = BoardReach.Sealed(full, walls, width, height);
                for (var cell = 0; cell < pens.Length; cell++)
                    Assert.That(pens[cell / width * width + width - 1 - cell % width], Is.EqualTo(pens[cell]), $"{layout} {width}×{height}: mirror of {cell}");
                Assert.That(pens.Where((p, cell) => p && walls[cell]), Is.Empty);
            }
        }

        [Test]
        public void IntroPen_IsTwoOrThreeCells_OnTheBottomRow([Values(7, 8, 9)] int width)
        {
            const int height = 9;
            var full = BoardShapes.Mask(BoardShape.Full, width, height);
            var walls = new bool[full.Length];
            foreach (var cell in BoardPens.Walls(PenLayout.Intro, width, height)) walls[cell] = true;
            var sealedCells = BoardReach.Sealed(full, walls, width, height);
            var pens = Enumerable.Range(0, full.Length).Where(cell => sealedCells[cell]).ToArray();
            Assert.That(pens.Length, Is.InRange(2, 4));
            Assert.That(pens.All(cell => cell / width == height - 1), Is.True);
        }

        [Test]
        public void PenSchedule_GivesEveryChapterTwoPenLevels_IncludingTheHardFifth()
        {
            for (var chapter = 1; chapter < 10; chapter++)
            {
                var kinds = Enumerable.Range(chapter * 10 + 1, 10).Select(BoardPens.ForLevel).ToArray();
                Assert.That(kinds.Count(k => k.Walls != PenLayout.None || k.Pockets), Is.EqualTo(2), $"chapter {chapter + 1}");
                Assert.That(kinds[4].Walls != PenLayout.None || kinds[4].Pockets, Is.True, $"chapter {chapter + 1}");
                Assert.That(kinds[0].Walls == PenLayout.None && kinds[5].Walls == PenLayout.None, Is.True, "no pens on a breather");
            }
        }

        [Test]
        public void FallsToBottom_StopsAtHoles()
        {
            const int width = 7, height = 8;
            var bridge = BoardShapes.Mask(BoardShape.Bridge, width, height);
            Assert.That(BoardReach.FallsToBottom(bridge, width, height, 1), Is.False, "an arch under column 1");
            Assert.That(BoardReach.FallsToBottom(bridge, width, height, 0), Is.True);
        }

        [Test]
        public void Pick_TakesPreferredCellsFirst()
        {
            const int width = 7, height = 8;
            var free = Enumerable.Repeat(true, width * height).ToArray();
            var prefer = new bool[width * height];
            prefer[2 * width + 1] = prefer[2 * width + 5] = true;
            var picked = BoardPatterns.Pick(BlockerPattern.Rows, width, height, 0, height - 1, 2, free, 7, prefer);
            Assert.That(picked, Is.EquivalentTo(new[] { 2 * width + 1, 2 * width + 5 }));
        }
    }
}
