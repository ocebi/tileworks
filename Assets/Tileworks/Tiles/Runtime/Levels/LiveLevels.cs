using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Levels
{
    // The levels the game plays: the built-in pack overlaid by the last accepted content update. An update that arrives mid-session
    // waits until the app enters `safeState`, so a level never changes while it's being played.
    public sealed class LiveLevels : ILevelSource, ILevelSequence, IAppStateListener
    {
        private sealed class Content
        {
            public readonly int Revision;
            public readonly ILevelSequence Sequence;
            public readonly Dictionary<string, string> Texts;

            public Content(int revision, ILevelSequence sequence, Dictionary<string, string> texts)
            {
                Revision = revision;
                Sequence = sequence;
                Texts = texts;
            }
        }

        private readonly ILevelSource _bundled;
        private readonly string _safeState;
        private readonly ILog _log;
        private Content _current;
        private Content _pending;

        // `bundledRevision` is the published revision the pack matches: updates at or below it are older than the build.
        // `cache` is null when updates are off; the pack is then all there is.
        public LiveLevels(ILevelSource bundled, ILevelSequence bundledSequence, int bundledRevision, LevelCache cache, string safeState, ILog log)
        {
            _bundled = Guard.NotNull(bundled, nameof(bundled));
            _safeState = safeState;
            _log = Guard.NotNull(log, nameof(log));
            _current = Restore(cache, bundledRevision) ??
                       new Content(bundledRevision, Guard.NotNull(bundledSequence, nameof(bundledSequence)), new Dictionary<string, string>());
        }

        public event Action Changed;

        // The pack's revision while only the built-in levels are in use.
        public int Revision => _current.Revision;
        public int PendingRevision => _pending?.Revision ?? 0;

        public int Count => _current.Sequence.Count;
        public LevelRef At(int index) => _current.Sequence.At(index);

        public IEnumerable<string> Ids
        {
            get
            {
                foreach (var id in _current.Texts.Keys) yield return id;
                foreach (var id in _bundled.Ids)
                    if (!_current.Texts.ContainsKey(id)) yield return id;
            }
        }

        public bool TryGetText(string id, out string json) => _current.Texts.TryGetValue(id, out json) || _bundled.TryGetText(id, out json);

        public bool TryGetBundledHash(string id, out string hash)
        {
            hash = null;
            if (!_bundled.TryGetText(id, out var json)) return false;
            try
            {
                hash = LevelEnvelope.Parse(json).ContentHash;
                return true;
            }
            catch (Exception e) when (e is FormatException || e is Newtonsoft.Json.JsonException)
            {
                return false;
            }
        }

        // From the updater: `texts` holds every level in `index` that doesn't come from the pack.
        public void Offer(LevelManifest index, IReadOnlyDictionary<string, string> texts)
        {
            var copy = new Dictionary<string, string>(texts.Count);
            foreach (var pair in texts) copy[pair.Key] = pair.Value;
            _pending = new Content(index.Revision, index.ToSequence(), copy);
        }

        public void OnStateEntering(string stateId)
        {
            if (stateId == _safeState) ApplyPending();
        }

        public bool ApplyPending()
        {
            if (_pending == null) return false;
            _current = _pending;
            _pending = null;
            _log.Info(LogTag.Content, "Levels revision {0} in use ({1} levels)", _current.Revision, _current.Sequence.Count);
            Changed?.Invoke();
            return true;
        }

        private Content Restore(LevelCache cache, int bundledRevision)
        {
            var index = cache?.ReadIndex();
            // An app update can ship levels newer than the ones downloaded for the old build.
            if (index == null || index.Revision <= bundledRevision) return null;
            var texts = new Dictionary<string, string>();
            foreach (var entry in index.Levels)
            {
                if (texts.ContainsKey(entry.Id)) continue;
                var json = cache.Has(entry.Id, entry.Hash) ? cache.Read(entry.Id, entry.Hash) : null;
                if (json != null) texts[entry.Id] = json;
                else if (!_bundled.TryGetText(entry.Id, out _))
                {
                    _log.Warn(LogTag.Content, "Cached levels revision {0} lack {1}; using the built-in levels", index.Revision, entry.Id);
                    return null;
                }
            }

            return new Content(index.Revision, index.ToSequence(), texts);
        }
    }
}
