using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Tileworlds.Project.Editor
{
    // Command-line and menu builds. Dev builds keep TILEWORKS_DEV; release builds strip it. Showcase builds keep the dev tools
    // (ADR 0016) but are not Development builds.
    public static class BuildScript
    {
        private static readonly GameProfile TileMatch = new GameProfile("TileMatch", "Tile Match", "com.ocebi.tilematch", "App", "TileMatch");
        private static readonly GameProfile BlockPuzzle = new GameProfile("BlockPuzzle", "Block Puzzle", "com.ocebi.blockpuzzle", "BlockApp", "BlockPuzzle");
        private static readonly GameProfile Blast = new GameProfile("Blast", "Blast", "com.ocebi.blast", "BlastApp", "Blast");
        private static readonly GameProfile AllGames = new GameProfile("Tileworlds", "Tileworlds", "com.ocebi.tileworlds", ContentSetup.HubFirstScene,
            ContentSetup.GameGroups.Append(ContentSetup.HubGroup).ToArray());

        [MenuItem("Tileworks/Build/All Games/WebGL (showcase)")]
        public static void AllGamesWebGL() => Build(AllGames, BuildTarget.WebGL, dev: true, development: false);

        [MenuItem("Tileworks/Build/All Games/WebGL (release)")]
        public static void AllGamesWebGLRelease() => Build(AllGames, BuildTarget.WebGL, dev: false, development: false);

        [MenuItem("Tileworks/Build/Tile Match/macOS (dev)")]
        public static void MacDev() => Build(TileMatch, BuildTarget.StandaloneOSX, dev: true, development: true);

        [MenuItem("Tileworks/Build/Tile Match/WebGL (showcase)")]
        public static void WebGL() => Build(TileMatch, BuildTarget.WebGL, dev: true, development: false);

        [MenuItem("Tileworks/Build/Tile Match/Android (dev APK)")]
        public static void AndroidDev() => Build(TileMatch, BuildTarget.Android, dev: true, development: true);

        [MenuItem("Tileworks/Build/Tile Match/Android (release APK)")]
        public static void AndroidRelease() => Build(TileMatch, BuildTarget.Android, dev: false, development: false);

        [MenuItem("Tileworks/Build/Block Puzzle/WebGL (showcase)")]
        public static void PuzzleWebGL() => Build(BlockPuzzle, BuildTarget.WebGL, dev: true, development: false);

        [MenuItem("Tileworks/Build/Block Puzzle/Android (dev APK)")]
        public static void PuzzleAndroidDev() => Build(BlockPuzzle, BuildTarget.Android, dev: true, development: true);

        [MenuItem("Tileworks/Build/Block Puzzle/Android (release APK)")]
        public static void PuzzleAndroidRelease() => Build(BlockPuzzle, BuildTarget.Android, dev: false, development: false);

        [MenuItem("Tileworks/Build/Blast/WebGL (showcase)")]
        public static void BlastWebGL() => Build(Blast, BuildTarget.WebGL, dev: true, development: false);

        [MenuItem("Tileworks/Build/Blast/Android (dev APK)")]
        public static void BlastAndroidDev() => Build(Blast, BuildTarget.Android, dev: true, development: true);

        [MenuItem("Tileworks/Build/Blast/Android (release APK)")]
        public static void BlastAndroidRelease() => Build(Blast, BuildTarget.Android, dev: false, development: false);

        // One project ships several games: each build takes only its game's content, name and bundle id. The content is
        // built first: into StreamingAssets, or for the web into ServerData, copied next to the player as Build/content.
        public static BuildReport Build(GameProfile game, BuildTarget target, bool dev, bool development)
        {
            if (EditorUserBuildSettings.activeBuildTarget != target && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException($"Could not switch the active build target to {target}; switch it in Build Settings and build again.");
            var group = BuildPipeline.GetBuildTargetGroup(target);
            var named = NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.GetScriptingDefineSymbols(named, out var original);
            var productName = PlayerSettings.productName;
            var identifier = PlayerSettings.GetApplicationIdentifier(named);
            var defines = original.Where(d => d != "TILEWORKS_DEV").ToList();
            if (dev) defines.Add("TILEWORKS_DEV");
            PlayerSettings.SetScriptingDefineSymbols(named, defines.ToArray());
            PlayerSettings.productName = game.ProductName;
            PlayerSettings.SetApplicationIdentifier(named, game.BundleId);
            if (target == BuildTarget.Android) EditorUserBuildSettings.buildAppBundle = false;
            if (target == BuildTarget.WebGL) ConfigureWeb();

            try
            {
                var output = game.OutputFor(target, dev);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
                ContentSetup.SelectProfile(target == BuildTarget.WebGL ? ContentSetup.RemoteProfile : ContentSetup.LocalProfile);
                ContentSetup.IncludeGroups(game.Groups);
                ContentSetup.BuildEntryScene(game.FirstScene);
                var serverData = ContentSetup.BuildContent(target);
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ContentSetup.EntryScene },
                    locationPathName = output,
                    target = target,
                    targetGroup = group,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                    // The texture format comes from here; the build settings value alone is ignored.
                    subtarget = target == BuildTarget.WebGL ? (int)WebGLTextureSubtarget.ASTC : 0,
                };
                var report = BuildPipeline.BuildPlayer(options);
                var content = serverData != null && report.summary.result == BuildResult.Succeeded ? CopyContent(serverData, output, target) : null;
                WriteReport(report, game, target, content);
                return report;
            }
            finally
            {
                PlayerSettings.SetScriptingDefineSymbols(named, original);
                PlayerSettings.productName = productName;
                PlayerSettings.SetApplicationIdentifier(named, identifier);
                ContentSetup.IncludeGroups(AllGames.Groups);
                ContentSetup.BuildEntryScene(ContentSetup.HubFirstScene);
                ContentSetup.SelectProfile(ContentSetup.LocalProfile);
                AssetDatabase.SaveAssets();
            }
        }

        // The web player fetches its bundles from Build/content/<target>, published next to the data file.
        private static string CopyContent(string serverData, string output, BuildTarget target)
        {
            var folder = Path.Combine(output, "Build", "content", target.ToString());
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            foreach (var file in Directory.GetFiles(serverData)) File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            return folder;
        }

        // Kept per machine rather than in ProjectSettings, so set on every build. Disk Size with LTO links for over
        // 20 minutes on one core, so it isn't used. See docs/WEB-BUILD.md.
        private static void ConfigureWeb()
        {
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSize;
            EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
        }

        private static void WriteReport(BuildReport report, GameProfile game, BuildTarget target, string content)
        {
            var s = report.summary;
            var text = new StringBuilder()
                .AppendLine($"game: {game.ProductName}")
                .AppendLine($"target: {target}")
                .AppendLine($"result: {s.result}")
                .AppendLine($"size: {OutputBytes(s.outputPath) / (1024f * 1024f):0.0} MB")
                .AppendLine($"time: {s.totalTime.TotalSeconds:0} s")
                .AppendLine($"errors: {s.totalErrors}, warnings: {s.totalWarnings}")
                .AppendLine($"output: {s.outputPath}");
            if (content != null)
            {
                text.AppendLine($"content: {OutputBytes(content) / (1024f * 1024f):0.0} MB in {content}");
                foreach (var file in Directory.GetFiles(content).OrderBy(f => f))
                    text.AppendLine($"  {Path.GetFileName(file),-60} {new FileInfo(file).Length / 1024f:0} KB");
            }

            Directory.CreateDirectory("Artifacts");
            File.WriteAllText($"Artifacts/build-{game.Name}-{target}.txt", text.ToString());
            Debug.Log("[Tileworks] Build finished\n" + text);
        }

        // The summary's totalSize counts uncompressed content, not the shipped file.
        private static long OutputBytes(string path)
        {
            if (File.Exists(path)) return new FileInfo(path).Length;
            return Directory.Exists(path) ? new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
        }
    }

    public sealed class GameProfile
    {
        public GameProfile(string name, string productName, string bundleId, string firstScene, params string[] groups)
        {
            Name = name;
            ProductName = productName;
            BundleId = bundleId;
            FirstScene = firstScene;
            Groups = groups;
        }

        public string Name { get; }
        public string ProductName { get; }
        public string BundleId { get; }

        // The app scene the entry scene opens, and the content groups that ship with this build.
        public string FirstScene { get; }
        public string[] Groups { get; }

        public string OutputFor(BuildTarget target, bool dev)
        {
            var suffix = dev ? "-dev" : string.Empty;
            return target switch
            {
                BuildTarget.Android => $"Builds/Android/{Name}{suffix}.apk",
                BuildTarget.StandaloneOSX => $"Builds/macOS/{Name}{suffix}.app",
                _ => $"Builds/{target}/{Name}{suffix}",
            };
        }
    }
}
