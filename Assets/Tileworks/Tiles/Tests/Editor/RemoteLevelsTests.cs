using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;

namespace Tileworks.Tiles.Tests
{
    public sealed class RemoteLevelsTests
    {
        private const string ManifestUrl = "https://pages.test/levels/test/manifest.json";
        private const string Mirror = "https://pages.test/levels/test";
        private const string Cdn = "https://cdn.test/levels/test";

        private sealed class FakeHttp : IHttpClient
        {
            private readonly Dictionary<string, HttpResponse> _responses = new Dictionary<string, HttpResponse>();
            public readonly List<string> Requests = new List<string>();

            public void Serve(string url, string text) => _responses[url] = new HttpResponse(200, text, null);
            public void Remove(string url) => _responses.Remove(url);

            public UniTask<HttpResponse> GetTextAsync(string url, int timeoutSeconds, CancellationToken ct)
            {
                Requests.Add(url);
                return UniTask.FromResult(_responses.TryGetValue(url, out var response) ? response : new HttpResponse(404, null, "Not Found"));
            }
        }

        private sealed class FakeChecker : ILevelChecker
        {
            public string Game => "test";
            public int PayloadVersion => 1;
            public string Check(LevelEnvelope envelope) => envelope.Payload["bad"] != null ? "bad level" : null;
        }

        private string _folder;
        private FakeHttp _http;
        private MemoryLevelSource _bundled;
        private LevelCache _cache;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tileworks-remote-levels-" + Guid.NewGuid().ToString("N"));
            _http = new FakeHttp();
            _bundled = new MemoryLevelSource();
            foreach (var id in new[] { "l1", "l2", "l3" }) _bundled.Set(id, Level(id, 1));
            _cache = new LevelCache(_folder);
        }

        [TearDown]
        public void TearDown() => _cache.Clear();

        [Test]
        public void AppendedLevel_IsDownloadedAlone_AndAppliedOnEnteringHome()
        {
            var levels = Live();
            Publish(1, ("l1", Level("l1", 1)), ("l2", Level("l2", 1)), ("l3", Level("l3", 1)), ("l4", Level("l4", 1)));

            var result = Run(levels);

            Assert.That(result.Outcome, Is.EqualTo(LevelUpdateOutcome.Updated));
            Assert.That(result.Downloaded, Is.EqualTo(1));
            Assert.That(_http.Requests, Is.EqualTo(new[] { ManifestUrl, $"{Cdn}/l4.json" }));
            levels.OnStateEntering("Level");
            Assert.That(levels.Count, Is.EqualTo(3), "nothing changes during a level");
            levels.OnStateEntering("Home");
            Assert.That(levels.Count, Is.EqualTo(4));
            Assert.That(levels.At(3).LevelId, Is.EqualTo("l4"));
            Assert.That(levels.TryGetText("l4", out _), Is.True);
            Assert.That(levels.Revision, Is.EqualTo(1));
        }

        [Test]
        public void NextSession_RestoresTheUpdate_WithoutDownloadingAgain()
        {
            Publish(1, ("l1", Level("l1", 1)), ("l4", Level("l4", 1)));
            Run(Live());
            _http.Requests.Clear();

            var levels = Live();
            var result = Run(levels);

            Assert.That(levels.Revision, Is.EqualTo(1));
            Assert.That(Ids(levels), Is.EqualTo(new[] { "l1", "l4" }));
            Assert.That(result.Outcome, Is.EqualTo(LevelUpdateOutcome.UpToDate));
            Assert.That(_http.Requests, Is.EqualTo(new[] { ManifestUrl }));
        }

        [Test]
        public void ChangedLevel_ReplacesTheBuiltInOne_AndClearsTheRepository()
        {
            var levels = Live();
            var repository = new LevelRepository(levels);
            levels.Changed += repository.Clear;
            Assert.That(repository.Get("l2").Value.Payload["n"].Value<int>(), Is.EqualTo(1));
            Publish(1, ("l1", Level("l1", 1)), ("l2", Level("l2", 2)));

            Run(levels);
            levels.ApplyPending();

            Assert.That(repository.Get("l2").Value.Payload["n"].Value<int>(), Is.EqualTo(2));
        }

        [Test]
        public void TamperedFile_IsLeftOut_OrKeptAtItsBuiltInVersion()
        {
            var levels = Live();
            Publish(1, ("l1", Level("l1", 1)), ("l2", Level("l2", 2)), ("l4", Level("l4", 1)));
            _http.Serve($"{Cdn}/l2.json", Level("l2", 3));
            _http.Serve($"{Cdn}/l4.json", Level("l4", 9));

            var result = Run(levels);
            levels.ApplyPending();

            Assert.That(result.Skipped, Is.EqualTo(2));
            Assert.That(Ids(levels), Is.EqualTo(new[] { "l1", "l2" }));
            levels.TryGetText("l2", out var json);
            Assert.That(json, Is.EqualTo(Level("l2", 1)));
        }

        [Test]
        public void LevelsThisBuildCantPlay_AreLeftOut()
        {
            var levels = Live();
            var bad = Envelope("l5", 1);
            bad.Payload["bad"] = true;
            Publish(1, ("l1", Level("l1", 1)), ("l4", Level("l4", 1, payloadVersion: 2)), ("l5", bad.ToJson()));

            var result = Run(levels);
            levels.ApplyPending();

            Assert.That(result.Skipped, Is.EqualTo(2));
            Assert.That(Ids(levels), Is.EqualTo(new[] { "l1" }));
            Assert.That(_http.Requests, Has.None.EndsWith("l4.json"), "a newer payload isn't downloaded");
        }

        [Test]
        public void Offline_KeepsTheBuiltInLevels()
        {
            var levels = Live();

            var result = Run(levels);
            levels.ApplyPending();

            Assert.That(result.Outcome, Is.EqualTo(LevelUpdateOutcome.Unreachable));
            Assert.That(Ids(levels), Is.EqualTo(new[] { "l1", "l2", "l3" }));
            Assert.That(Directory.Exists(_folder), Is.False);
        }

        [Test]
        public void OversizedFile_IsLeftOutUnread()
        {
            var levels = Live();
            var big = Envelope("l4", 1);
            big.Payload["padding"] = new string('x', 300 << 10);
            Publish(1, ("l1", Level("l1", 1)), ("l4", big.ToJson()));

            var result = Run(levels);
            levels.ApplyPending();

            Assert.That(result.Skipped, Is.EqualTo(1));
            Assert.That(Ids(levels), Is.EqualTo(new[] { "l1" }));
        }

        [Test]
        public void OlderRevision_IsIgnored()
        {
            var levels = Live();
            Publish(2, ("l1", Level("l1", 1)), ("l4", Level("l4", 1)));
            Run(levels);
            Publish(1, ("l1", Level("l1", 1)));

            var result = Run(levels);
            levels.ApplyPending();

            Assert.That(result.Outcome, Is.EqualTo(LevelUpdateOutcome.Rejected));
            Assert.That(levels.Revision, Is.EqualTo(2));
        }

        [Test]
        public void MissingCdnFile_LoadsFromTheMirror()
        {
            var levels = Live();
            Publish(1, ("l1", Level("l1", 1)), ("l4", Level("l4", 1)));
            _http.Remove($"{Cdn}/l4.json");
            _http.Serve($"{Mirror}/l4.json", Level("l4", 1));

            var result = Run(levels);

            Assert.That(result.Downloaded, Is.EqualTo(1));
            Assert.That(_http.Requests.Last(), Is.EqualTo($"{Mirror}/l4.json"));
        }

        [Test]
        public void ManifestNamingAPath_IsRejected()
        {
            var levels = Live();
            var manifest = new JObject
            {
                ["schema"] = 1, ["game"] = "test", ["revision"] = 1,
                ["levels"] = new JArray(new JObject { ["id"] = "../../save", ["hash"] = "0123456789abcdef" }),
            };
            _http.Serve(ManifestUrl, manifest.ToString());

            Assert.That(Run(levels).Outcome, Is.EqualTo(LevelUpdateOutcome.Rejected));
        }

        [Test]
        public void BuildStampedWithARevision_IgnoresThatRevisionAndOlder()
        {
            var levels = Live(bundledRevision: 2);
            Publish(2, ("l1", Level("l1", 1)), ("l4", Level("l4", 1)));

            var result = Run(levels);

            Assert.That(result.Outcome, Is.EqualTo(LevelUpdateOutcome.UpToDate));
            Assert.That(Ids(levels), Is.EqualTo(new[] { "l1", "l2", "l3" }));
        }

        [Test]
        public void CacheFromBeforeAnAppUpdate_GivesWayToTheNewBuild()
        {
            Publish(1, ("l1", Level("l1", 1)), ("l4", Level("l4", 1)));
            Run(Live());

            Assert.That(Ids(Live(bundledRevision: 1)), Is.EqualTo(new[] { "l1", "l2", "l3" }));
            Assert.That(Ids(Live(bundledRevision: 0)), Is.EqualTo(new[] { "l1", "l4" }));
        }

        [Test]
        public void UnreadableCache_FallsBackToTheBuiltInLevels()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, "index.json"), "{ not json");

            Assert.That(Ids(Live()), Is.EqualTo(new[] { "l1", "l2", "l3" }));
        }

        [Test]
        public void NewRevision_DeletesFilesItNoLongerUses()
        {
            var levels = Live();
            Publish(1, ("l1", Level("l1", 1)), ("l4", Level("l4", 1)));
            Run(levels);
            Publish(2, ("l1", Level("l1", 1)), ("l5", Level("l5", 1)));

            Run(levels);

            var files = Directory.GetFiles(_folder).Select(Path.GetFileName).ToArray();
            Assert.That(files, Has.Some.StartsWith("l5."));
            Assert.That(files, Has.None.StartsWith("l4."));
        }

        private LiveLevels Live(int bundledRevision = 0) =>
            new LiveLevels(_bundled, LevelSequence.Parse("{\"levels\":[{\"id\":\"l1\"},{\"id\":\"l2\"},{\"id\":\"l3\"}]}"), bundledRevision, _cache, "Home",
                new NullLog());

        private LevelUpdateResult Run(LiveLevels levels)
        {
            var options = new LevelUpdateOptions { ManifestUrl = ManifestUrl, RetryDelaysMs = Array.Empty<int>() };
            return new LevelUpdater(options, _http, levels, _cache, new FakeChecker(), new NullLog()).RunAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        private void Publish(int revision, params (string Id, string Json)[] levels)
        {
            var manifest = new LevelManifest { Game = "test", Revision = revision, Files = Cdn };
            foreach (var (id, json) in levels)
            {
                var envelope = LevelEnvelope.Parse(json);
                manifest.Levels.Add(new LevelManifest.Entry { Id = id, Hash = envelope.ContentHash, PayloadVersion = envelope.PayloadVersion });
                _http.Serve($"{Cdn}/{id}.json", json);
            }

            _http.Serve(ManifestUrl, manifest.ToJson());
        }

        private static string[] Ids(ILevelSequence sequence) => Enumerable.Range(0, sequence.Count).Select(i => sequence.At(i).LevelId).ToArray();

        private static string Level(string id, int n, int payloadVersion = 1)
        {
            var envelope = Envelope(id, n);
            envelope.PayloadVersion = payloadVersion;
            return envelope.ToJson();
        }

        private static LevelEnvelope Envelope(string id, int n) => new LevelEnvelope { Id = id, Game = "test", Payload = new JObject { ["n"] = n } };
    }
}
