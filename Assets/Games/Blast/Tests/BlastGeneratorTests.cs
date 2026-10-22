using Blast.Generation;
using Blast.Model;
using NUnit.Framework;
using Tileworks.Kit.Core;

namespace Blast.Tests
{
    public sealed class BlastGeneratorTests
    {
        [Test]
        public void Generate_PlacesBlockersAndGoals_AndMeasuresTheLevel()
        {
            var recipe = new BlastRecipe { Id = "gen", Seed = 3, Moves = 22, Boxes = 4, Balloons = 3, Ducks = 1, ColourGoal = 20, Corners = true };
            var result = new BlastGenerator(new BlastRules(), playouts: 8).Generate(recipe, maxAttempts: 2);
            var level = result.Level;

            Assert.That(level.Goals.Length, Is.EqualTo(4));
            Assert.That(level.Playable[0], Is.False, "top corners cut");
            var report = new ValidationReport();
            new BlastLevelSerializer().Validate(level, report);
            Assert.That(report.HasErrors, Is.False, report.ToString());
            Assert.That(result.Stats.Runs, Is.EqualTo(8));
        }

        [Test]
        public void TheCasualBot_WinsAnEasyLevel()
        {
            var level = new BlastGenerator(new BlastRules()).Build(new BlastRecipe { Seed = 5, Colours = 3, ColourGoal = 15 }, moves: 30);
            Assert.That(new BlastGenerator(new BlastRules(), playouts: 10).Measure(level, 5).WinRate, Is.GreaterThan(0.8f));
        }
    }
}
