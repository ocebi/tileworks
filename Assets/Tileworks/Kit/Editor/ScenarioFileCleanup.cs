using Tileworks.Kit.App;
using UnityEditor;

namespace Tileworks.Kit.Editor
{
    // A scenario request lives for one Play session: every app launched in it reads the profile, and leaving Play ends it.
    [InitializeOnLoad]
    public static class ScenarioFileCleanup
    {
        static ScenarioFileCleanup() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) ScenarioFile.Delete();
        }
    }
}
