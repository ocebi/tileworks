using System.Linq;
using Tileworks.Kit.App;
using UnityEditor;
using UnityEngine;

namespace Tileworlds.Games.Editor
{
    // Shared pieces of the code-built scenes.
    public static class SceneParts
    {
        public static Camera Camera(string name, Color background)
        {
            var go = new GameObject(name);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            return camera;
        }

        public static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void Assign(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Tileworks] {target.GetType().Name} has no field {field}");
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void AssignArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Scenes are content (Tileworks/Content/Set Up Groups); the player's scene list holds only the entry scene, so a
        // game's scenes leave it if an older build list still has them.
        public static void SetBuildScenes(string folder, params string[] paths)
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => !s.path.StartsWith(folder + "/")).ToArray();
        }

        public static GameCatalog RegisterGame(string title, string bootScene)
        {
            var catalog = LoadOrCreate<GameCatalog>("Assets/Games/Shared/GameCatalog.asset");
            catalog.Set(title, bootScene);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        public static SceneRoot AddSceneRoot(GameObject go, string bootScene, params SceneInstaller[] installers)
        {
            var root = go.AddComponent<SceneRoot>();
            var so = new SerializedObject(root);
            so.FindProperty("_bootScene").stringValue = bootScene;
            var list = so.FindProperty("_installers");
            list.arraySize = installers.Length;
            for (var i = 0; i < installers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = installers[i];
            if (UiFactory.Motion != null) so.FindProperty("_uiMotion").objectReferenceValue = UiFactory.Motion;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }
    }
}
