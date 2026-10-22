using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tileworks.Kit.Content.Addressables;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tileworlds.Project.Editor
{
    // Content groups, profiles and the entry scene, all from code so they are reproducible. Every scene is content; the
    // player's scene list holds only the entry scene, which fetches the first app scene and loads it.
    public static class ContentSetup
    {
        public const string EntryScene = "Assets/Games/Shared/Scenes/Entry.unity";
        public const string HubFirstScene = "HubApp";
        public const string HubGroup = "Hub";
        public const string LocalProfile = "Local";
        public const string RemoteProfile = "Remote";
        public static readonly string[] GameGroups = { "TileMatch", "BlockPuzzle", "Blast", "Match3" };

        private const string SharedGroup = "Shared";
        private const string MusicGroup = "Shared.Music";
        private const string MusicFolder = "Assets/Games/Shared/Audio/Music";
        private const string RemoteLoadPath = "{Tileworks.Kit.Content.ContentUrl.Remote}/[BuildTarget]";
        private static readonly string[] SharedFolders = { "Assets/Games/Shared/Art", "Assets/Games/Shared/Fonts", "Assets/Games/Shared/Audio" };

        [MenuItem("Tileworks/Content/Set Up Groups")]
        public static void SetUp()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            settings.BuildRemoteCatalog = false;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptFastMode);
            SetUpProfiles(settings);
            foreach (var game in GameGroups) SceneGroup(settings, game, $"Assets/Games/{game}/Scenes");
            SceneGroup(settings, HubGroup, "Assets/Games/Hub/Scenes");
            var shared = Group(settings, SharedGroup);
            foreach (var folder in SharedFolders) Entry(settings, shared, folder, folder);
            var music = Group(settings, MusicGroup);
            foreach (var clip in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder }).Select(AssetDatabase.GUIDToAssetPath))
                Entry(settings, music, clip, Path.GetFileNameWithoutExtension(clip));
            settings.activeProfileId = settings.profileSettings.GetProfileId(LocalProfile);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);

            BuildEntryScene(HubFirstScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(EntryScene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Tileworks] Content groups set up");
        }

        // Which app scene the entry scene opens: the hub, or a single game's own app scene.
        public static void BuildEntryScene(string firstScene)
        {
            var open = SceneManager.GetActiveScene().path;
            Directory.CreateDirectory(Path.GetDirectoryName(EntryScene) ?? ".");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ProjectSetup.LaunchBackground;
            camera.orthographic = true;
            camera.cullingMask = 0;

            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            var bar = new GameObject("Progress", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = bar.rectTransform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = new Vector2(0.15f, 0.4f);
            rect.anchorMax = new Vector2(0.85f, 0.4f);
            rect.sizeDelta = new Vector2(0f, 14f);
            bar.color = new Color(1f, 1f, 1f, 0.85f);
            bar.type = Image.Type.Filled;
            bar.fillMethod = Image.FillMethod.Horizontal;
            bar.fillAmount = 0f;
            bar.gameObject.SetActive(false);

            var entry = new GameObject("ContentEntry").AddComponent<ContentEntry>();
            var so = new SerializedObject(entry);
            so.FindProperty("_firstScene").stringValue = firstScene;
            so.FindProperty("_bar").objectReferenceValue = bar;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, EntryScene);
            if (!string.IsNullOrEmpty(open) && open != EntryScene) EditorSceneManager.OpenScene(open);
        }

        public static void SelectProfile(string name)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.activeProfileId = settings.profileSettings.GetProfileId(name);
        }

        // The shared groups always ship; a single-game build leaves the other games and the hub out.
        public static void IncludeGroups(IEnumerable<string> included)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var keep = new HashSet<string>(included);
            foreach (var name in GameGroups.Append(HubGroup))
            {
                var group = settings.FindGroup(name);
                if (group == null) throw new InvalidOperationException($"Content group {name} is missing; run Tileworks/Content/Set Up Groups.");
                group.GetSchema<BundledAssetGroupSchema>().IncludeInBuild = keep.Contains(name);
            }
        }

        // Builds the bundles for the active profile; the Remote profile's output lands in ServerData/<target>.
        public static string BuildContent(BuildTarget target)
        {
            var serverData = Path.Combine("ServerData", target.ToString());
            if (Directory.Exists(serverData)) Directory.Delete(serverData, true);
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException("Content build failed: " + result.Error);
            return Directory.Exists(serverData) ? serverData : null;
        }

        private static void SetUpProfiles(AddressableAssetSettings settings)
        {
            var profiles = settings.profileSettings;
            var local = Profile(profiles, LocalProfile, settings.activeProfileId);
            var remote = Profile(profiles, RemoteProfile, local);
            profiles.SetValue(local, AddressableAssetSettings.kRemoteBuildPath, AddressableAssetSettings.kLocalBuildPathValue);
            profiles.SetValue(local, AddressableAssetSettings.kRemoteLoadPath, AddressableAssetSettings.kLocalLoadPathValue);
            profiles.SetValue(remote, AddressableAssetSettings.kRemoteBuildPath, AddressableAssetSettings.kRemoteBuildPathValue);
            profiles.SetValue(remote, AddressableAssetSettings.kRemoteLoadPath, RemoteLoadPath);
        }

        // GetProfileId answers "" for a missing profile.
        private static string Profile(AddressableAssetProfileSettings profiles, string name, string copyFrom)
        {
            var id = profiles.GetProfileId(name);
            return string.IsNullOrEmpty(id) ? profiles.AddProfile(name, copyFrom) : id;
        }

        private static void SceneGroup(AddressableAssetSettings settings, string name, string folder)
        {
            var group = Group(settings, name);
            foreach (var path in Directory.GetFiles(folder, "*.unity").OrderBy(p => p))
                Entry(settings, group, path.Replace('\\', '/'), Path.GetFileNameWithoutExtension(path));
        }

        // Every group reads the Remote.* variables; the profile decides whether they mean StreamingAssets or a server.
        private static AddressableAssetGroup Group(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name) ?? settings.CreateGroup(name, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.UseAssetBundleCache = true;
            schema.UseAssetBundleCrc = true;
            schema.UseAssetBundleCrcForCachedBundles = false;
            schema.IncludeInBuild = true;
            schema.IncludeGUIDInCatalog = false;
            schema.IncludeLabelsInCatalog = false;
            EditorUtility.SetDirty(schema);
            EditorUtility.SetDirty(group);
            return group;
        }

        private static void Entry(AddressableAssetSettings settings, AddressableAssetGroup group, string assetPath, string address)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException($"No asset at {assetPath}");
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.SetAddress(address, false);
        }
    }
}
