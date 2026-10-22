using System.IO;
using System.Linq;
using NUnit.Framework;
using Tileworks.Tiles.Editor;
using Tileworks.Tiles.Levels;

namespace Tileworks.Tiles.Tests
{
    public sealed class LevelDesignerTests
    {
        // Opening a shipped level in its designer and saving it untouched must not change it.
        [Test]
        public void EveryShippedLevel_HasADesigner_AndRoundTripsUnchanged()
        {
            var files = Directory.GetDirectories("Assets/Games").SelectMany(game => Directory.Exists($"{game}/Content/Levels")
                ? Directory.GetFiles($"{game}/Content/Levels", "*.json") : new string[0]).ToArray();
            Assert.That(files, Is.Not.Empty);
            foreach (var file in files)
            {
                var envelope = LevelEnvelope.Parse(File.ReadAllText(file));
                var designer = LevelDesigners.Create(envelope.Game);
                Assert.That(designer, Is.Not.Null, $"{file}: no designer for '{envelope.Game}'");
                designer.Load(envelope.Payload);
                Assert.That(LevelEnvelope.ComputeContentHash(designer.Save()), Is.EqualTo(envelope.ContentHash), file);
            }
        }
    }
}
