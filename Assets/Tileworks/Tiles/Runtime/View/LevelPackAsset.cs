using System.Collections.Generic;
using Tileworks.Tiles.Levels;
using UnityEngine;

namespace Tileworks.Tiles.View
{
    // Levels shipped inside the build; the file name is the level id. Any tile game can use one.
    [CreateAssetMenu(menuName = "Tileworks/Level Pack")]
    public sealed class LevelPackAsset : ScriptableObject, ILevelSource
    {
        [SerializeField] private TextAsset[] _levels = new TextAsset[0];
        [SerializeField] private TextAsset _sequence;
        [SerializeField, Tooltip("The published level revision these files match (Content/published.json); 0 if never published.")]
        private int _publishedRevision;

        private Dictionary<string, TextAsset> _byId;

        public IEnumerable<string> Ids
        {
            get
            {
                foreach (var level in _levels) yield return level.name;
            }
        }

        public bool TryGetText(string id, out string json)
        {
            _byId ??= Index();
            json = _byId.TryGetValue(id, out var asset) ? asset.text : null;
            return json != null;
        }

        public int PublishedRevision => _publishedRevision;

        public void SetPublishedRevision(int revision) => _publishedRevision = revision;

        public LevelSequence LoadSequence() => _sequence != null ? LevelSequence.Parse(_sequence.text) : new LevelSequence();

        public void SetContent(TextAsset[] levels, TextAsset sequence)
        {
            _levels = levels;
            _sequence = sequence;
            _byId = null;
        }

        private Dictionary<string, TextAsset> Index()
        {
            var index = new Dictionary<string, TextAsset>(_levels.Length);
            foreach (var level in _levels) index[level.name] = level;
            return index;
        }
    }
}
