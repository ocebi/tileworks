using Tileworks.Kit.App;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TileMatch.Editor
{
    public static class EditorScenario
    {
        public const string AppScene = "Assets/Games/TileMatch/Scenes/App.unity";

        public static void PlayLevel(string levelJson) => Play(new ScenarioRequest { kind = "play-level", payload = levelJson });

        public static void Play(ScenarioRequest request)
        {
            if (EditorApplication.isPlaying) return;
            ScenarioFile.Write(request);
            EditorSceneManager.OpenScene(AppScene);
            EditorApplication.isPlaying = true;
        }
    }
}
