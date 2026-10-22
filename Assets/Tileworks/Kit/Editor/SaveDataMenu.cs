using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Tileworks.Kit.Editor
{
    public static class SaveDataMenu
    {
        // Every game's save folder: "save", "save-blockpuzzle", ...
        private static string[] SaveRoots => Directory.Exists(Application.persistentDataPath)
            ? Directory.GetDirectories(Application.persistentDataPath, "save-*").Append(SaveRoot).Where(Directory.Exists).ToArray()
            : new string[0];

        private static string SaveRoot => Path.Combine(Application.persistentDataPath, "save");

        [MenuItem("Tileworks/Save/Reset Player Data")]
        private static void ResetWithConfirm()
        {
            if (EditorUtility.DisplayDialog("Reset player data", $"Delete every local profile under\n{Application.persistentDataPath}?", "Delete", "Cancel"))
                ResetPlayerData();
        }

        [MenuItem("Tileworks/Save/Reset Player Data", true)]
        private static bool CanReset() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tileworks/Save/Open Save Folder")]
        private static void OpenFolder()
        {
            Directory.CreateDirectory(SaveRoot);
            EditorUtility.RevealInFinder(SaveRoot);
        }

        public static void ResetPlayerData()
        {
            foreach (var root in SaveRoots) Directory.Delete(root, true);
            Debug.Log("[Save] Player data reset");
        }
    }
}
