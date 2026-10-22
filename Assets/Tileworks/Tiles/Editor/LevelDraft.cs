using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // The level being edited, held in an object so Unity's Undo can record it.
    public sealed class LevelDraft : ScriptableObject
    {
        [SerializeField] private string _json;

        public string Json
        {
            get => _json;
            set => _json = value;
        }
    }
}
