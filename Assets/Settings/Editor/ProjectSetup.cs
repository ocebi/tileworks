using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tileworlds.Project.Editor
{
    // Idempotent project configuration, so settings live in code review rather than in someone's Editor session.
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string PipelinePath = SettingsFolder + "/URP-2D.asset";
        private const string RendererPath = SettingsFolder + "/URP-2D-Renderer.asset";
        private const string BundleId = "com.ocebi.tileworlds";
        private const string IconPath = "Assets/Games/Hub/Art/app_icon.png";
        private const string IconBackgroundPath = "Assets/Games/Hub/Art/app_icon_background.png";
        private const string LaunchImagePath = "Assets/Games/Hub/Art/launch_background.png";
        private static readonly Color IconBackground = new Color32(0x2B, 0x1C, 0x8F, 255);
        // Block Puzzle's purple; the hub's boot screen uses the same colour.
        internal static readonly Color LaunchBackground = new Color32(0x36, 0x28, 0x7A, 255);

        [MenuItem("Tileworks/Project/Apply Project Settings")]
        public static void Apply()
        {
            ConfigurePipeline();
            ConfigurePlayer();
            ConfigureIcon();
            ConfigureLaunchScreen();
            ConfigureDefines();
            AssetDatabase.SaveAssets();
            Debug.Log("[Tileworks] Project settings applied");
        }

        private static void ConfigurePipeline()
        {
            MoveIfPresent("Assets/DefaultVolumeProfile.asset", SettingsFolder + "/DefaultVolumeProfile.asset");
            MoveIfPresent("Assets/UniversalRenderPipelineGlobalSettings.asset", SettingsFolder + "/UniversalRenderPipelineGlobalSettings.asset");

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            pipeline.useSRPBatcher = true;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Tileworks";
            PlayerSettings.productName = "Tileworlds";
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            // The native launch screens fill with this, so the first frame doesn't flash after them.
            PlayerSettings.SplashScreen.backgroundColor = LaunchBackground;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, BundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = true;

            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;

            PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);

            // The code is over jsDelivr's 20 MB file limit uncompressed, so it ships gzipped (docs/WEB-BUILD.md).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            // Measured peak is about 96 MB; starting above it avoids growing the heap mid-game, which iOS Safari handles badly.
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.maximumMemorySize = 512;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            PlayerSettings.WebGL.template = "PROJECT:Tileworlds";
        }

        // One 1024 px icon; Unity derives the iOS and Android sizes. Android's adaptive icon puts it over a plain colour.
        private static void ConfigureIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogWarning($"[Tileworks] No app icon at {IconPath}");
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            var background = SolidTexture(IconBackgroundPath, IconBackground);
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                if (!kind.ToString().StartsWith("Adaptive")) continue;
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var platformIcon in icons) platformIcon.SetTextures(background, icon);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
        }

        // A solid launch screen. Unity's iOS launch screen always fills with an image (dark grey when none is set), so the image is a
        // swatch of the launch colour. These fields have no public setter.
        private static void ConfigureLaunchScreen()
        {
            var image = SolidTexture(LaunchImagePath, LaunchBackground);
            PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
            PlayerSettings.iOS.SetiPadLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            foreach (var field in new[] { "iOSLaunchScreenPortrait", "iOSLaunchScreenLandscape", "iOSLaunchScreeniPadImage" })
                settings.FindProperty(field).objectReferenceValue = image;
            settings.FindProperty("iOSLaunchScreenBackgroundColor").colorValue = LaunchBackground;
            settings.FindProperty("iOSLaunchScreeniPadBackgroundColor").colorValue = LaunchBackground;
            settings.ApplyModifiedProperties();
        }

        private static Texture2D SolidTexture(string path, Color colour)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var pixels = new Color[64 * 64];
            System.Array.Fill(pixels, colour);
            texture.SetPixels(pixels);
            var png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            if (!File.Exists(path) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(path), png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path);
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Dev builds carry TILEWORKS_DEV (debug panel, cheats); the release build script removes it.
        private static void ConfigureDefines()
        {
            foreach (var target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.WebGL, NamedBuildTarget.iOS })
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out var defines);
                if (System.Array.IndexOf(defines, "TILEWORKS_DEV") >= 0) continue;
                var list = new System.Collections.Generic.List<string>(defines) { "TILEWORKS_DEV" };
                PlayerSettings.SetScriptingDefineSymbols(target, list.ToArray());
            }
        }

        private static void MoveIfPresent(string from, string to)
        {
            if (!File.Exists(from) || File.Exists(to)) return;
            var error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"[Tileworks] Move {from}: {error}");
        }
    }
}
