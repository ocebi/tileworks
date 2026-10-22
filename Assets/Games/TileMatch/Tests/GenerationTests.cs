using NUnit.Framework;
using Tileworks.Tiles.Model;
using Tileworks.Tiles.Search;
using TileMatch.Generation;
using TileMatch.Model;

namespace TileMatch.Tests
{
    public sealed class GenerationTests
    {
        private static TileMatchLevel Pyramid()
        {
            var b = new TileMatchLevelBuilder();
            for (var layer = 0; layer < 3; layer++)
            for (var y = 0; y < 5 - layer; y++)
            for (var x = 0; x < 6 - layer; x++)
                b.Add(x * 2 + layer, y * 2 + layer, layer, 0);
            while (b.TileCount % 3 != 0) b.Add(20 + b.TileCount * 2, 0, 0, 0);
            return b.Build("layout");
        }

        [Test]
        public void Assignment_WitnessOrder_IsALegalWinningLine()
        {
            var layout = Pyramid();
            var settings = new AssignerSettings { KindCount = 6, Margin = 2 };
            Assignment assignment = null;
            for (ulong seed = 1; assignment == null && seed < 50; seed++) assignment = new KindAssigner().TryAssign(layout, settings, seed);
            Assert.That(assignment, Is.Not.Null);

            var level = layout.WithKinds("generated", assignment.Kinds, settings.KindCount);
            var report = new Tileworks.Kit.Core.ValidationReport();
            TileMatchLevelBuilder.Validate(level, report);
            Assert.That(report.HasErrors, Is.False, report.ToString());

            var engine = RulesFixture.Rules.CreateEngine(level, 1);
            foreach (var tile in assignment.WitnessOrder)
            {
                if (engine.State.Locations[tile] != TileLocation.Board) continue;
                var result = engine.Apply(new SelectTile(tile));
                Assert.That(result.Accepted, Is.True, $"witness step {tile}: {result.Rejected}");
                Assert.That(engine.State.TrayCount, Is.LessThanOrEqualTo(level.TrayCapacity - settings.Margin));
            }

            Assert.That(engine.State.Outcome, Is.EqualTo(Outcome.Won));
        }

        [Test]
        public void Assignment_RespectsFrozenAndLinkedTiles()
        {
            var builder = new TileMatchLevelBuilder();
            for (var x = 0; x < 12; x++) builder.Add(x * 2, 0, 0, 0);
            builder.Frozen(5, 2).Link(1, 8);
            var layout = builder.Build("modded");

            var assigner = new KindAssigner();
            Assignment assignment = null;
            for (ulong seed = 1; assignment == null && seed < 100; seed++)
                assignment = assigner.TryAssign(layout, new AssignerSettings { KindCount = 4, Margin = 1 }, seed);
            Assert.That(assignment, Is.Not.Null);

            var engine = RulesFixture.Rules.CreateEngine(layout.WithKinds("modded", assignment.Kinds, 4), 1);
            foreach (var tile in assignment.WitnessOrder)
            {
                if (engine.State.Locations[tile] == TileLocation.Board) Assert.That(engine.Apply(new SelectTile(tile)).Accepted, Is.True);
            }

            Assert.That(engine.State.Outcome, Is.EqualTo(Outcome.Won));
        }

        [Test]
        public void Solver_SolvesGeneratedLevels_AndProvesTinyUnsolvableOnes()
        {
            var stack = new TileMatchLevelBuilder().Tray(3);
            for (var layer = 0; layer < 9; layer++) stack.Add(0, 0, layer, (ushort)((8 - layer) % 3));
            var unsolvable = stack.Build("forced-order");
            var solver = new DepthFirstSolver<TileMatchState, SelectTile>(new TileMatchSearchDomain(RulesFixture.Rules, unsolvable));
            var result = solver.Solve(RulesFixture.Rules.CreateState(unsolvable), new SearchBudget(100_000));
            Assert.That(result.ProvenUnsolvable, Is.True);

            var generated = new LevelGenerator(RulesFixture.Rules, new DifficultyBands(), new AnalyzerSettings { Playouts = 50 })
                .Generate(new GenerationRequest { Id = "g", Layout = Pyramid(), Seed = 3 });
            Assert.That(generated.Metrics.Solvable, Is.True, generated.Metrics.ToString());
            Assert.That(generated.Metrics.MinPeak, Is.InRange(1, generated.Level.TrayCapacity));
        }
    }
}
