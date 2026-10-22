using UnityEditor;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // What a designer calls before it changes the level, so the change can be undone and gets saved.
    public sealed class LevelEdit
    {
        private readonly Object _draft;

        public LevelEdit(Object draft) => _draft = draft;

        public bool Changed { get; private set; }

        // The level's difficulty label, for tools that aim at it.
        public string Difficulty { get; set; }

        // Width the designer may draw across.
        public float Width { get; set; }

        public void Change(string label)
        {
            Undo.RecordObject(_draft, label);
            Changed = true;
        }

        public void Reset() => Changed = false;
    }
}
