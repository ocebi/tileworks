using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Blast.Generation;
using Blast.Model;
using NUnit.Framework;
using Tileworks.Tiles.Generation;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Search;

namespace Blast.Tests
{
    public sealed class BlastPocketTests
    {
        private const string Report = "Assets/Games/Blast/Content/report.csv";
        private static readonly BlastRecipe[] Plan = BlastPlanner.Plan(100);

        // Pens start empty and everything else starts full: cells the boxes cut off from every refill are exactly the empty
        // ones, and holes alone cut nothing off.
        [Test]
        public void EveryPlannedLevel_StartsWithItsPensEmpty_AndNothingElse([Values(0.6f, 1f, 1.6f)] float load)
        {
            var generator = new BlastGenerator(new BlastRules());
            foreach (var recipe in Plan)
            {
                var level = generator.Build(recipe, recipe.Moves, recipe.Colours, load);
                var statics = level.Pieces.Select(p => p == Piece.Box).ToArray();
                var pens = BoardReach.Sealed(level.Playable, statics, level.Width, level.Height);
                Assert.That(BoardReach.Unreachable(level.Playable, default, level.Width, level.Height), Is.Zero, recipe.Id);
                for (var cell = 0; cell < level.CellCount; cell++)
                    if (level.Playable[cell] && !statics[cell]) Assert.That(level.Pieces[cell] == Piece.Empty, Is.EqualTo(pens[cell]), $"{recipe.Id} cell {cell}");
                if (!BoardShapes.HasPockets(recipe.Shape)) Assert.That(pens, Is.EqualTo(WallPens(recipe, level)), $"{recipe.Id}: only the walls seal cells");
                Assert.That(BlastGenerator.MovableShare(level), Is.GreaterThanOrEqualTo(recipe.MinMovable), recipe.Id);
                for (var cell = 0; cell < level.CellCount; cell++)
                {
                    if (level.Pieces[cell] != Piece.Duck) continue;
                    Assert.That(BoardReach.FallsToBottom(level.Playable, level.Width, level.Height, cell), Is.True, $"{recipe.Id}: duck at {cell}");
                    for (var below = cell + level.Width; below < level.CellCount; below += level.Width)
                        Assert.That(level.Pieces[below], Is.Not.EqualTo(Piece.Box), $"{recipe.Id}: box under the duck at {cell}");
                }
            }
        }

        [Test]
        public void Level2_ShowsOneSmallPen_BehindOneLayerBoxes_AndEveryChapterHasTwoPenLevels()
        {
            var generator = new BlastGenerator(new BlastRules());
            var intro = generator.Build(Plan[1], 20);
            Assert.That(PenCells(intro), Is.InRange(2, 4));
            Assert.That(intro.Hp.Where((hp, cell) => intro.Pieces[cell] == Piece.Box), Has.All.EqualTo(1));
            Assert.That(BoardShapes.HasPockets(Plan[1].Shape), Is.False);
            for (var chapter = 1; chapter < 10; chapter++)
            {
                var levels = Plan.Skip(chapter * 10).Take(10).ToArray();
                Assert.That(levels.Count(r => r.Pens != PenLayout.None || BoardShapes.HasPockets(r.Shape)), Is.EqualTo(2), $"chapter {chapter + 1}");
                var hard = levels[4];
                Assert.That(hard.Pens != PenLayout.None || hard.UnderOverhangs && hard.Boxes > 0, Is.True, $"chapter {chapter + 1}: the hard level has pens");
                foreach (var recipe in levels.Where(r => r.Pens != PenLayout.None))
                    Assert.That(PenCells(generator.Build(recipe, 25)), Is.GreaterThan(0), recipe.Id);
            }
        }

        // Every shipped level is winnable by the skilled bot, which wins at least as often as the casual one (within noise).
        // The casual bot must win easy, normal and hard levels now and then, and levels 1-4 nearly always; super hard levels
        // are meant to be out of a weak player's reach, so there its rate is only reported (LevelCurve.MinCasualRate).
        [Test]
        public void EveryShippedLevel_IsWinnableByBothBots()
        {
            var lines = File.ReadAllLines(Report).Where(l => l.Length > 0).ToArray();
            var header = lines[0].Split(',');
            int skilledAt = Array.IndexOf(header, "skilledWinRate"), casualAt = Array.IndexOf(header, "casualWinRate");
            if (skilledAt < 0 || casualAt < 0) Assert.Ignore("report.csv predates the skilled tuning profile; regenerate the levels.");
            for (var row = 1; row < lines.Length; row++)
            {
                var cells = lines[row].Split(',');
                if (cells[1] == "edited by hand") continue;
                var skilled = float.Parse(cells[skilledAt], CultureInfo.InvariantCulture);
                var casual = float.Parse(cells[casualAt], CultureInfo.InvariantCulture);
                Assert.That(skilled, Is.GreaterThanOrEqualTo(0.15f), $"{cells[0]}: skilled");
                Assert.That(casual, Is.GreaterThanOrEqualTo(LevelCurve.MinCasualRate(LevelCurve.SkilledAt(row)) - 0.005f), $"{cells[0]}: casual");
                Assert.That(skilled, Is.GreaterThanOrEqualTo(casual - 0.1f), $"{cells[0]}: skilled below casual");
            }
        }

        // Every shipped level sits inside its win-rate band. Moves to spare on wins and shuffles are soft caps a few later
        // levels may miss, but levels 1-10 meet every rule; levels under 30 keep 60% of the board movable. Levels 1-4 are
        // replayed on enough casual games to count their losses.
        [Test]
        public void EveryShippedLevel_IsInsideItsBand_AndTheFirstTenMeetEveryRule()
        {
            var lines = File.ReadAllLines(Report).Where(l => l.Length > 0).ToArray();
            var header = lines[0].Split(',');
            int At(string column) => Array.IndexOf(header, column);
            if (At("shufflesPerGame") < 0) Assert.Ignore("report.csv predates the round-2 tuning rules; regenerate the levels.");
            for (var row = 1; row < lines.Length; row++)
            {
                var cells = lines[row].Split(',');
                if (cells[1] == "edited by hand") continue;
                float Read(string column) => float.Parse(cells[At(column)], CultureInfo.InvariantCulture);
                var slot = LevelCurve.SkilledAt(row);
                const float rounding = 0.005f;
                Assert.That(slot.Miss(Read("skilledWinRate")), Is.LessThanOrEqualTo(rounding), $"{cells[0]}: outside its band");
                if (row < 30) Assert.That(Read("movable"), Is.GreaterThanOrEqualTo(LevelCurve.MinMovableShare(row) - rounding), $"{cells[0]}: movable");
                if (row > 10) continue;
                if (row <= 4) AssertCloseToUnlosable(row, slot);
                Assert.That(Read("movesLeftOnWin"), Is.LessThanOrEqualTo(LevelCurve.MaxMovesLeftOnWin(slot) + rounding), $"{cells[0]}: moves to spare");
                Assert.That(Read("shufflesPerGame"), Is.LessThanOrEqualTo(LevelCurve.MaxShufflesPerGame(slot.Role) + rounding), $"{cells[0]}: shuffles");
            }
        }

        private static void AssertCloseToUnlosable(int number, in LevelSlot slot)
        {
            var recipe = Plan[number - 1];
            var level = new BlastLevelSerializer().Read(LevelEnvelope.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Report), "Levels", recipe.Id + ".json"))));
            var casual = new BlastGenerator(new BlastRules()).Probe(level, recipe.Seed, BotProfile.Casual, LevelCurve.MinCasualPlayouts(slot));
            Assert.That(LevelCurve.PassesCasual(LevelCurve.MinCasualRate(slot), casual.Runs, casual.Wins), Is.True,
                $"{recipe.Id}: {casual.Runs - casual.Wins} casual losses in {casual.Runs}");
        }

        private static int PenCells(BlastLevel level) => Enumerable.Range(0, level.CellCount).Count(cell => level.Playable[cell] && level.Pieces[cell] == Piece.Empty);

        private static bool[] WallPens(BlastRecipe recipe, BlastLevel level)
        {
            var walls = new bool[level.CellCount];
            foreach (var cell in BoardPens.Walls(recipe.Pens, level.Width, level.Height)) walls[cell] = level.Playable[cell];
            return BoardReach.Sealed(level.Playable, walls, level.Width, level.Height);
        }
    }
}
