using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Levels
{
    public sealed class LevelUpdateOptions
    {
        public string ManifestUrl = string.Empty;
        public int TimeoutSeconds = 8;
        public int[] RetryDelaysMs = { 1000, 3000 };
        public int MaxParallel = 4;

        // Anything bigger is refused unread, so a bad or hostile server can't make the client parse megabytes.
        public int MaxManifestChars = 1 << 20;
        public int MaxLevelChars = 256 << 10;
    }

    public enum LevelUpdateOutcome
    {
        Unreachable,
        Rejected,
        UpToDate,
        Updated,
    }

    public readonly struct LevelUpdateResult
    {
        public readonly LevelUpdateOutcome Outcome;
        public readonly int Revision;
        public readonly int Downloaded;
        public readonly int Skipped;
        public readonly string Reason;

        public LevelUpdateResult(LevelUpdateOutcome outcome, int revision, int downloaded = 0, int skipped = 0, string reason = null)
        {
            Outcome = outcome;
            Revision = revision;
            Downloaded = downloaded;
            Skipped = skipped;
            Reason = reason;
        }

        public override string ToString() =>
            $"{Outcome} r{Revision}" + (Outcome == LevelUpdateOutcome.Updated ? $": {Downloaded} downloaded, {Skipped} skipped" : "") +
            (Reason != null ? $" ({Reason})" : "");
    }

    // Fetches a game's manifest, downloads the levels this device lacks, checks each one, and offers the result to LiveLevels
    // (docs/REMOTE-LEVELS.md). A level that fails a check is left out, or kept at its built-in version when the pack has it.
    public sealed class LevelUpdater
    {
        private readonly struct Download
        {
            public readonly LevelManifest.Entry Entry;
            public readonly string Json;
            public readonly string Problem;

            public Download(LevelManifest.Entry entry, string json, string problem)
            {
                Entry = entry;
                Json = json;
                Problem = problem;
            }
        }

        private readonly LevelUpdateOptions _options;
        private readonly IHttpClient _http;
        private readonly LiveLevels _levels;
        private readonly LevelCache _cache;
        private readonly ILevelChecker _checker;
        private readonly ILog _log;

        public LevelUpdater(LevelUpdateOptions options, IHttpClient http, LiveLevels levels, LevelCache cache, ILevelChecker checker, ILog log)
        {
            _options = Guard.NotNull(options, nameof(options));
            _http = Guard.NotNull(http, nameof(http));
            _levels = Guard.NotNull(levels, nameof(levels));
            _cache = Guard.NotNull(cache, nameof(cache));
            _checker = Guard.NotNull(checker, nameof(checker));
            _log = Guard.NotNull(log, nameof(log));
        }

        public async UniTask<LevelUpdateResult> RunAsync(CancellationToken ct)
        {
            var response = await GetAsync(_options.ManifestUrl, ct);
            if (!response.Ok) return new LevelUpdateResult(LevelUpdateOutcome.Unreachable, 0, reason: response.ToString());
            if (response.Text.Length > _options.MaxManifestChars) return Rejected(0, $"manifest is {response.Text.Length} characters");

            LevelManifest manifest;
            try
            {
                manifest = LevelManifest.Parse(response.Text);
            }
            catch (Exception e) when (e is FormatException || e is Newtonsoft.Json.JsonException)
            {
                return Rejected(0, "unreadable manifest: " + e.Message);
            }

            if (manifest.Schema > LevelManifest.CurrentSchema) return Rejected(manifest.Revision, $"manifest schema {manifest.Schema} is newer than this build");
            if (manifest.Game != _checker.Game) return Rejected(manifest.Revision, $"manifest is for {manifest.Game}");
            var installed = Math.Max(_levels.Revision, _levels.PendingRevision);
            if (manifest.Revision < installed) return Rejected(manifest.Revision, $"older than revision {installed}");
            if (manifest.Revision == installed) return new LevelUpdateResult(LevelUpdateOutcome.UpToDate, installed);

            var texts = new Dictionary<string, string>();
            var usable = new Dictionary<string, bool>();
            var downloads = new List<LevelManifest.Entry>();
            var skipped = 0;
            foreach (var entry in manifest.Levels)
            {
                if (usable.ContainsKey(entry.Id) || downloads.Exists(d => d.Id == entry.Id)) continue;
                string json = null;
                if (entry.PayloadVersion > _checker.PayloadVersion) usable[entry.Id] = KeepBundled(entry, $"needs payload v{entry.PayloadVersion}", ref skipped);
                else if (_cache.Has(entry.Id, entry.Hash) && (json = _cache.Read(entry.Id, entry.Hash)) != null)
                {
                    texts[entry.Id] = json;
                    usable[entry.Id] = true;
                }
                else if (_levels.TryGetBundledHash(entry.Id, out var bundledHash) && bundledHash == entry.Hash) usable[entry.Id] = true;
                else downloads.Add(entry);
            }

            var mirror = FolderOf(_options.ManifestUrl);
            var files = manifest.Files.Length > 0 ? manifest.Files.TrimEnd('/') : mirror;
            var downloaded = 0;
            for (var start = 0; start < downloads.Count; start += Math.Max(1, _options.MaxParallel))
            {
                var batch = new List<UniTask<Download>>();
                for (var i = start; i < Math.Min(downloads.Count, start + Math.Max(1, _options.MaxParallel)); i++)
                    batch.Add(DownloadAsync(downloads[i], files, mirror, ct));
                foreach (var result in await UniTask.WhenAll(batch))
                {
                    if (result.Problem != null)
                    {
                        usable[result.Entry.Id] = KeepBundled(result.Entry, result.Problem, ref skipped);
                        continue;
                    }

                    _cache.Write(result.Entry.Id, result.Entry.Hash, result.Json);
                    texts[result.Entry.Id] = result.Json;
                    usable[result.Entry.Id] = true;
                    downloaded++;
                }
            }

            var index = new LevelManifest { Game = manifest.Game, Revision = manifest.Revision };
            foreach (var entry in manifest.Levels)
                if (usable[entry.Id]) index.Levels.Add(entry);
            if (index.Levels.Count == 0) return Rejected(manifest.Revision, "no playable levels");

            _cache.Commit(index);
            _levels.Offer(index, texts);
            return new LevelUpdateResult(LevelUpdateOutcome.Updated, manifest.Revision, downloaded, skipped);
        }

        private async UniTask<Download> DownloadAsync(LevelManifest.Entry entry, string files, string mirror, CancellationToken ct)
        {
            var response = await GetAsync($"{files}/{entry.Id}.json", ct);
            if (!response.Ok && files != mirror) response = await GetAsync($"{mirror}/{entry.Id}.json", ct);
            if (!response.Ok) return new Download(entry, null, response.ToString());
            if (response.Text.Length > _options.MaxLevelChars) return new Download(entry, null, $"file is {response.Text.Length} characters");
            return new Download(entry, response.Text, Verify(entry, response.Text));
        }

        private string Verify(LevelManifest.Entry entry, string json)
        {
            LevelEnvelope envelope;
            try
            {
                envelope = LevelEnvelope.Parse(json);
            }
            catch (Exception e) when (e is FormatException || e is Newtonsoft.Json.JsonException || e is InvalidCastException)
            {
                return "unreadable: " + e.Message;
            }

            if (envelope.Id != entry.Id) return $"the file says it is {envelope.Id}";
            if (envelope.Game != _checker.Game) return $"the file is for {envelope.Game}";
            if (envelope.ContentHash != entry.Hash || !envelope.HashMatches()) return "content hash mismatch";
            if (envelope.PayloadVersion > _checker.PayloadVersion) return $"needs payload v{envelope.PayloadVersion}";
            return _checker.Check(envelope);
        }

        private async UniTask<HttpResponse> GetAsync(string url, CancellationToken ct)
        {
            var response = await _http.GetTextAsync(url, _options.TimeoutSeconds, ct);
            for (var i = 0; i < _options.RetryDelaysMs.Length && response.Transient; i++)
            {
                await UniTask.Delay(_options.RetryDelaysMs[i], DelayType.Realtime, PlayerLoopTiming.Update, ct);
                response = await _http.GetTextAsync(url, _options.TimeoutSeconds, ct);
            }

            return response;
        }

        private bool KeepBundled(LevelManifest.Entry entry, string problem, ref int skipped)
        {
            skipped++;
            var bundled = _levels.TryGetBundledHash(entry.Id, out _);
            _log.Warn(LogTag.Content, $"Skipped downloaded level {entry.Id}: {problem}{(bundled ? "; the built-in one stays" : "")}");
            return bundled;
        }

        private LevelUpdateResult Rejected(int revision, string reason)
        {
            _log.Warn(LogTag.Content, "Level update rejected: {0}", reason);
            return new LevelUpdateResult(LevelUpdateOutcome.Rejected, revision, reason: reason);
        }

        private static string FolderOf(string url)
        {
            var query = url.IndexOfAny(new[] { '?', '#' });
            var path = query >= 0 ? url.Substring(0, query) : url;
            var slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(0, slash) : path;
        }
    }
}
