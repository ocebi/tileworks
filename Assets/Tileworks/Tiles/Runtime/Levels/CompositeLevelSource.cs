using System.Collections.Generic;

namespace Tileworks.Tiles.Levels
{
    // Priority chain (§9.8): the first source that has the id wins, e.g. debug override, then the embedded pack.
    public sealed class CompositeLevelSource : ILevelSource
    {
        private readonly ILevelSource[] _sources;

        public CompositeLevelSource(params ILevelSource[] sources) => _sources = sources;

        public IEnumerable<string> Ids
        {
            get
            {
                var seen = new HashSet<string>();
                foreach (var source in _sources)
                foreach (var id in source.Ids)
                    if (seen.Add(id)) yield return id;
            }
        }

        public bool TryGetText(string id, out string json)
        {
            foreach (var source in _sources)
            {
                if (source.TryGetText(id, out json)) return true;
            }

            json = null;
            return false;
        }
    }

    public sealed class MemoryLevelSource : ILevelSource
    {
        private readonly Dictionary<string, string> _levels = new Dictionary<string, string>();

        public IEnumerable<string> Ids => _levels.Keys;
        public void Set(string id, string json) => _levels[id] = json;
        public bool TryGetText(string id, out string json) => _levels.TryGetValue(id, out json);
    }
}
