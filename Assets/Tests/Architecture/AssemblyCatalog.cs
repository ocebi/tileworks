using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Tileworlds.ArchitectureTests
{
    public enum Tier
    {
        Kit,
        Tiles,
        Meta,
        DevTools,
        Game,
        Project,
    }

    public sealed class AsmdefInfo
    {
        public string Name;
        public string Directory;
        public Tier Tier;
        public string Game;
        public string[] References;
        public bool NoEngineReferences;
        public bool AutoReferenced;
        public string[] DefineConstraints;
        public string[] IncludePlatforms;

        public string LastSegment => Name.Substring(Name.LastIndexOf('.') + 1);
        public string FeaturePrefix => Name.Contains('.') ? Name.Substring(0, Name.LastIndexOf('.')) : Name;
        public bool IsTests => LastSegment == "Tests" || Name.EndsWith("Tests", StringComparison.Ordinal);
        public bool IsEditor => IncludePlatforms.Length == 1 && IncludePlatforms[0] == "Editor";
        public bool IsRuntime => !IsTests && !IsEditor;

        public IEnumerable<string> SourceFiles()
        {
            var nested = new HashSet<string>(System.IO.Directory
                .GetFiles(Directory, "*.asmdef", SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .Where(d => d != Directory));
            foreach (var file in System.IO.Directory.GetFiles(Directory, "*.cs", SearchOption.AllDirectories))
            {
                var dir = Path.GetDirectoryName(file);
                if (nested.Any(n => dir == n || dir.StartsWith(n + Path.DirectorySeparatorChar))) continue;
                yield return file;
            }
        }

        public override string ToString() => Name;
    }

    public static class AssemblyCatalog
    {
        [Serializable]
        private sealed class AsmdefJson
        {
            public string name;
            public string[] references = new string[0];
            public bool noEngineReferences;
            public bool autoReferenced = true;
            public string[] defineConstraints = new string[0];
            public string[] includePlatforms = new string[0];
        }

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        public static List<AsmdefInfo> Load()
        {
            var result = new List<AsmdefInfo>();
            var roots = new List<string>();
            var tileworks = Path.Combine(Application.dataPath, "Tileworks");
            roots.AddRange(new[] { "Kit", "Tiles", "Meta" }.Select(tier => Path.Combine(tileworks, tier)));
            var games = Path.Combine(Application.dataPath, "Games");
            if (System.IO.Directory.Exists(games)) roots.Add(games);
            roots.Add(Path.Combine(Application.dataPath, "Tests"));

            foreach (var root in roots)
            {
                if (!System.IO.Directory.Exists(root)) continue;
                foreach (var path in System.IO.Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories))
                {
                    var json = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(path));
                    var info = new AsmdefInfo
                    {
                        Name = json.name,
                        Directory = Path.GetDirectoryName(path),
                        References = json.references ?? new string[0],
                        NoEngineReferences = json.noEngineReferences,
                        AutoReferenced = json.autoReferenced,
                        DefineConstraints = json.defineConstraints ?? new string[0],
                        IncludePlatforms = json.includePlatforms ?? new string[0],
                    };
                    (info.Tier, info.Game) = Classify(path);
                    result.Add(info);
                }
            }

            return result;
        }

        private static (Tier, string) Classify(string path)
        {
            var p = path.Replace('\\', '/');
            if (p.Contains("/Assets/Tileworks/Kit/")) return (Tier.Kit, null);
            if (p.Contains("/Assets/Tileworks/Tiles/")) return (Tier.Tiles, null);
            if (p.Contains("/Assets/Tileworks/Meta/")) return (Tier.Meta, null);
            var marker = "/Assets/Games/";
            var index = p.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                var rest = p.Substring(index + marker.Length);
                var game = rest.Substring(0, rest.IndexOf('/'));
                return game == "Shared" ? (Tier.Project, null) : (Tier.Game, game);
            }

            return (Tier.Project, null);
        }
    }
}
