using System.Collections.Generic;
using System;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;
using TileMatch.Generation;
using TileMatch.Model;

namespace TileMatch.Tests
{
    public sealed class LevelFileTests
    {
        [Test]
        public void Level_RoundTripsThroughTheEnvelope()
        {
            var level = TileMatchLevelBuilder.FromAscii(@"
                layer 0:
                A A A B
                layer 1:
                 B B").Frozen(0, 2).Link(1, 3).Spawner(4, 2, 2, 2).Build("tm_0001");
            var serializer = new TileMatchLevelSerializer();
            var envelope = new LevelEnvelope { Id = level.Id, Game = serializer.Game, Payload = serializer.Write(level) };

            var json = envelope.ToJson();
            var parsed = LevelEnvelope.Parse(json);
            var copy = serializer.Read(parsed);

            Assert.That(parsed.HashMatches(), Is.True);
            Assert.That(copy.TileCount, Is.EqualTo(level.TileCount));
            Assert.That(copy.Modifiers.ToArray(), Is.EqualTo(level.Modifiers.ToArray()));
            Assert.That(RulesFixture.Rules.CreateState(copy).ComputeHash(), Is.EqualTo(RulesFixture.Rules.CreateState(level).ComputeHash()));
            StringAssert.Contains("[0, 0, 0, 0]", json, "tiles stay one per line");
        }

        [Test]
        public void ContentHash_ChangesWhenThePayloadChanges()
        {
            var a = new LevelEnvelope { Payload = new Newtonsoft.Json.Linq.JObject { ["tray"] = 7 } };
            var b = new LevelEnvelope { Payload = new Newtonsoft.Json.Linq.JObject { ["tray"] = 8 } };
            a.ToJson();
            b.ToJson();
            Assert.That(a.ContentHash, Is.Not.EqualTo(b.ContentHash));
        }

        [Test]
        public void LayoutParser_ReadsMarksAndDirectives()
        {
            var file = LayoutParser.Parse("L1", @"
                tray 7
                kinds 5
                band hard
                spawner 2
                layer 0:
                x f 1 h
                x x 1
                layer 1:
                 s F x");

            Assert.That(file.Band, Is.EqualTo(DifficultyBand.Hard));
            Assert.That(file.Kinds, Is.EqualTo(5));
            Assert.That(file.Layout.BoardTileCount, Is.EqualTo(10));
            Assert.That(file.Layout.TileCount, Is.EqualTo(12));
            Assert.That(file.Layout.Modifiers.Length, Is.EqualTo(4));
        }

        [Test]
        public void Sequence_WrapsPastTheEnd()
        {
            var sequence = LevelSequence.Parse(@"{ ""levels"": [ {""id"":""a""}, {""id"":""b""}, {""id"":""c""}, {""id"":""d""}, {""id"":""e""} ] }");
            Assert.That(sequence.At(4).LevelId, Is.EqualTo("e"));
            Assert.That(sequence.At(5).LevelId, Is.EqualTo("e"));
            var report = new ValidationReport();
            sequence.Validate(new AllBut("c"), report);
            Assert.That(report.ErrorCount, Is.EqualTo(1));
        }

        private sealed class AllBut : ILevelSource
        {
            private readonly string _missing;

            public AllBut(string missing) => _missing = missing;

            public IEnumerable<string> Ids => Array.Empty<string>();

            public bool TryGetText(string id, out string json)
            {
                json = null;
                return id != _missing;
            }
        }
    }
}
