using Tileworks.Tiles.Levels;
using UnityEditor;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // Where the level sits in the progression, and a button to append it when it isn't there yet.
    public sealed class SequencePanel
    {
        private string _cachedFolder;
        private string _cachedId;
        private int _cachedIndex = -1;
        private int _cachedCount;

        public void Draw(string levelPath, LevelEnvelope envelope)
        {
            var folder = LevelContent.FolderOf(levelPath);
            if (folder != _cachedFolder || envelope.Id != _cachedId) Refresh(folder, envelope.Id);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Sequence");
                if (_cachedIndex >= 0)
                {
                    GUILayout.Label($"level {_cachedIndex + 1} of {_cachedCount}");
                    if (GUILayout.Button(new GUIContent("Levels window", "Every level in play order, to reorder and add levels"), GUILayout.Width(110f)))
                        LevelSequenceWindow.Open(envelope.Game, envelope.Id);
                    return;
                }

                GUILayout.Label("not played yet", EditorStyles.miniLabel);
                if (!GUILayout.Button("Add to sequence", GUILayout.Width(120f))) return;
                var levels = LevelContent.Sequence(folder);
                var reward = levels.Count > 0 ? levels[levels.Count - 1].Reward : 0;
                levels.Add(new LevelRef(levels.Count, envelope.Id, envelope.Difficulty == "hard" || envelope.Difficulty == "superHard" ? envelope.Difficulty : string.Empty, reward));
                LevelContent.WriteSequence(folder, levels);
                Refresh(folder, envelope.Id);
            }
        }

        private void Refresh(string folder, string id)
        {
            var levels = LevelContent.Sequence(folder);
            _cachedFolder = folder;
            _cachedId = id;
            _cachedCount = levels.Count;
            _cachedIndex = levels.FindIndex(l => l.LevelId == id);
        }
    }
}
