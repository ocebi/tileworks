using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Tileworlds.ArchitectureTests
{
    // Enforces ARCHITECTURE.md §6.7. Each test reports every violation at once.
    public sealed class ArchitectureTests
    {
        private const int AssemblyBudget = 80;
        private const int MaxFileLines = 400;

        private static readonly string[] Layers = { "Api", "Model", "Generation", "Service", "Controller", "View", "Installer", "Debug", "Editor", "Tests" };

        private static readonly Dictionary<string, string[]> LayerMatrix = new Dictionary<string, string[]>
        {
            ["Api"] = new string[0],
            ["Model"] = new[] { "Api" },
            ["Generation"] = new[] { "Api", "Model" },
            ["Service"] = new[] { "Api", "Model", "Generation" },
            ["Controller"] = new[] { "Api", "Model", "Service" },
            ["View"] = new[] { "Api", "Model", "Controller" },
            ["Debug"] = new[] { "Api", "Model", "Service", "Controller", "Generation" },
        };

        private static readonly Dictionary<Tier, Tier[]> TierRules = new Dictionary<Tier, Tier[]>
        {
            [Tier.Kit] = new[] { Tier.Kit },
            [Tier.Tiles] = new[] { Tier.Kit, Tier.Tiles },
            [Tier.Meta] = new[] { Tier.Kit, Tier.Meta },
            [Tier.DevTools] = new[] { Tier.Kit, Tier.DevTools },
            [Tier.Game] = new[] { Tier.Kit, Tier.Tiles, Tier.Meta, Tier.DevTools, Tier.Game },
        };

        private static readonly string[] EngineFree = { "Tileworks.Kit.Core", "Tileworks.Kit.Injection", "Tileworks.Tiles.Model", "Tileworks.Tiles.Search" };

        private static List<AsmdefInfo> _all;
        private static List<AsmdefInfo> All => _all ??= AssemblyCatalog.Load();
        private static IEnumerable<AsmdefInfo> Ours => All.Where(a => a.Tier != Tier.Project);
        private static Dictionary<string, AsmdefInfo> ByName => All.ToDictionary(a => a.Name);
        private static bool UnderGames(AsmdefInfo a) => a.Directory.Replace('\\', '/').Contains("/Assets/Games/");

        [Test]
        public void TierRules_DependenciesPointDownward_AndGamesAreIndependent()
        {
            var problems = new List<string>();
            foreach (var asm in Ours)
            {
                foreach (var reference in asm.References)
                {
                    if (!ByName.TryGetValue(reference, out var target) || target.Tier == Tier.Project) continue;
                    if (!TierRules[asm.Tier].Contains(target.Tier))
                        problems.Add($"{asm.Name} ({asm.Tier}) → {target.Name} ({target.Tier})");
                    else if (asm.Tier == Tier.Game && target.Tier == Tier.Game && asm.Game != target.Game)
                        problems.Add($"{asm.Name} references another game: {target.Name}");
                }
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void LayerMatrix_IsRespectedWithinAFeature_AndOnlyApiAcrossFeatures()
        {
            var problems = new List<string>();
            foreach (var asm in Ours.Where(a => Layers.Contains(a.LastSegment)))
            {
                foreach (var reference in asm.References)
                {
                    if (!ByName.TryGetValue(reference, out var target) || !Layers.Contains(target.LastSegment)) continue;
                    if (target.FeaturePrefix == asm.FeaturePrefix)
                    {
                        if (LayerMatrix.TryGetValue(asm.LastSegment, out var allowed) && !allowed.Contains(target.LastSegment))
                            problems.Add($"{asm.Name} → {target.Name}: {asm.LastSegment} may not reference {target.LastSegment}");
                    }
                    else if (target.Tier == asm.Tier && target.LastSegment != "Api" && asm.IsRuntime && asm.LastSegment != "Installer")
                    {
                        problems.Add($"{asm.Name} → {target.Name}: across features only Api may be referenced");
                    }
                }
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void ModelApiAndCoreAssemblies_HaveNoEngineReferences()
        {
            var problems = Ours
                .Where(a => EngineFree.Contains(a.Name) || a.LastSegment == "Model" || a.LastSegment == "Api" || a.LastSegment == "Generation")
                .Where(a => !a.NoEngineReferences)
                .Select(a => a.Name)
                .ToList();
            Assert.That(problems, Is.Empty, "Must set noEngineReferences: " + string.Join(", ", problems));
        }

        private static readonly string[] ViewFreeLayers = { "Api", "Model", "Service", "Generation" };

        // The layer matrix covers the game's own View; this also catches framework views such as Tileworks.Tiles.View.
        [Test]
        public void GameLogicLayers_ReferenceNoViewAssembly()
        {
            var problems = Ours
                .Where(a => a.Tier == Tier.Game && ViewFreeLayers.Contains(a.LastSegment))
                .SelectMany(a => a.References.Where(r => r.EndsWith(".View")).Select(r => $"{a.Name} → {r}"))
                .ToList();
            Assert.That(problems, Is.Empty, "Logic layers may not reference a View assembly:\n" + string.Join("\n", problems));
        }

        [Test]
        public void DebugAndTestCode_IsCompiledOutOfRelease()
        {
            var problems = new List<string>();
            foreach (var asm in Ours)
            {
                if ((asm.LastSegment == "Debug" || asm.Tier == Tier.DevTools) && asm.IsRuntime && !asm.DefineConstraints.Contains("TILEWORKS_DEV"))
                    problems.Add($"{asm.Name} needs defineConstraints TILEWORKS_DEV");
                if (asm.IsTests && !asm.DefineConstraints.Contains("UNITY_INCLUDE_TESTS"))
                    problems.Add($"{asm.Name} needs defineConstraints UNITY_INCLUDE_TESTS");
                if (asm.LastSegment == "Editor" && !asm.IsEditor)
                    problems.Add($"{asm.Name} must be Editor-only");
                if (asm.AutoReferenced)
                    problems.Add($"{asm.Name} must set autoReferenced: false");
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void OnlyInstallersAndTheAppShell_SeeTheContainer()
        {
            var problems = Ours
                .Where(a => a.References.Contains("Tileworks.Kit.Injection"))
                .Where(a => a.LastSegment != "Installer" && a.Name != "Tileworks.Kit.App" && !a.IsTests && !a.IsEditor && a.Tier != Tier.DevTools)
                .Select(a => a.Name)
                .ToList();
            Assert.That(problems, Is.Empty, "Container API used outside installers: " + string.Join(", ", problems));
        }

        [Test]
        public void OnlyServicesAndKitSave_TouchSqlite()
        {
            var problems = Ours
                .Where(a => a.References.Contains("Gilzoide.SqliteNet"))
                .Where(a => a.LastSegment != "Service" && a.Name != "Tileworks.Kit.Save" && !a.IsTests)
                .Select(a => a.Name)
                .ToList();
            Assert.That(problems, Is.Empty, "sqlite-net referenced from: " + string.Join(", ", problems));
        }

        private static readonly string[] AddressablesAssemblies = { "Unity.Addressables", "Unity.ResourceManager", "UniTask.Addressables" };

        [Test]
        public void OnlyTheContentAdapterAndInstallers_SeeAddressables()
        {
            var problems = Ours
                .Where(a => a.References.Any(r => AddressablesAssemblies.Contains(r)))
                .Where(a => a.Name != "Tileworks.Kit.Content.Addressables" && a.LastSegment != "Installer" && !a.IsTests && !a.IsEditor)
                .Select(a => a.Name)
                .ToList();
            Assert.That(problems, Is.Empty, "Addressables referenced from: " + string.Join(", ", problems));
        }

        [Test]
        public void RuntimeAssemblies_StayWithinBudget()
        {
            var count = Ours.Count(a => a.IsRuntime);
            Assert.That(count, Is.LessThanOrEqualTo(AssemblyBudget), $"{count} runtime assemblies");
        }

        private static readonly (Regex Pattern, string Reason, Func<AsmdefInfo, bool> Exempt)[] BannedApis =
        {
            (new Regex(@"\bFind(Object|Objects|AnyObject|FirstObject)s?(OfType|ByType)\b"), "scene search", null),
            (new Regex(@"\bGameObject\.Find\b"), "scene search", null),
            (new Regex(@"\bSendMessage\s*\("), "SendMessage", null),
            (new Regex(@"\basync\s+void\b"), "async void", null),
            (new Regex(@"\bDateTime\.Now\b"), "use IClock", null),
            (new Regex(@"\bPlayerPrefs\b"), "use the database", null),
            (new Regex(@"WHERE[^""]*""\s*\+"), "SQL concatenation; use parameters", null),
            (new Regex(@"\bResources\.Load"), "use AssetScope", a => a.Name == "Tileworks.Kit.App"),
            (new Regex(@"\bUnityEngine\.Random\b|\bRandom\.Range\b"), "use Pcg32", a => a.LastSegment == "View" || a.LastSegment == "Debug"),
        };

        [Test]
        public void BannedApis_AreNotUsed()
        {
            var problems = new List<string>();
            foreach (var asm in Ours.Where(a => a.IsRuntime))
            {
                foreach (var file in asm.SourceFiles())
                {
                    var lines = File.ReadAllLines(file);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        var line = StripComment(lines[i]).TrimStart();
                        if (line.Length == 0) continue;
                        foreach (var (pattern, reason, exempt) in BannedApis)
                        {
                            if (exempt != null && exempt(asm)) continue;
                            if (pattern.IsMatch(line)) problems.Add($"{Relative(file)}:{i + 1} {reason}");
                        }
                    }
                }
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void SourceFiles_StayUnderSizeBudget()
        {
            var problems = new List<string>();
            // Assets/Games/Shared sits in the Project tier for the layer rules but its code still counts.
            foreach (var asm in All.Where(a => !a.IsTests && (a.Tier != Tier.Project || UnderGames(a))))
            {
                foreach (var file in asm.SourceFiles())
                {
                    var lines = File.ReadAllLines(file).Length;
                    if (lines > MaxFileLines) problems.Add($"{Relative(file)}: {lines} lines");
                }
            }

            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void NoStaticMutableState_OutsideTheAllowList()
        {
            var problems = new List<string>();
            foreach (var type in OurTypes())
            {
                if (type.IsDefined(typeof(CompilerGeneratedAttribute), false)) continue;
                foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsLiteral || field.IsInitOnly) continue;
                    var member = (MemberInfo)field;
                    if (field.Name.StartsWith("<", StringComparison.Ordinal))
                    {
                        var property = type.GetProperty(field.Name.Substring(1, field.Name.IndexOf('>') - 1),
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (property == null) continue;
                        member = property;
                    }

                    if (member.GetCustomAttributes(true).Any(a => a.GetType().Name == "AllowStaticStateAttribute")) continue;
                    problems.Add($"{type.FullName}.{member.Name}");
                }
            }

            Assert.That(problems, Is.Empty, "Static mutable state: " + string.Join("\n", problems));
        }

        [Test]
        public void MonoBehaviours_HaveNoPublicFields()
        {
            var problems = OurTypes()
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t))
                .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Select(f => $"{t.Name}.{f.Name}"))
                .ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        // Unity binds a script file to the one class named like it; any other component in that file goes missing on reload.
        [Test]
        public void UnityObjects_LiveInAFileNamedAfterThem()
        {
            var bound = new HashSet<Type>(UnityEditor.AssetDatabase.FindAssets("t:MonoScript")
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.MonoScript>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                .Where(script => script != null)
                .Select(script => script.GetClass())
                .Where(type => type != null));
            var problems = OurTypes()
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition && !t.IsNested)
                .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) || typeof(ScriptableObject).IsAssignableFrom(t))
                .Where(t => !bound.Contains(t))
                .Select(t => t.FullName)
                .ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void ServicesAndControllers_HaveOnePublicConstructor()
        {
            var problems = OurTypes(a => a.LastSegment == "Service" || a.LastSegment == "Controller")
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsDefined(typeof(CompilerGeneratedAttribute), false) && !t.IsNested)
                .Where(t => t.GetConstructors().Length > 1)
                .Select(t => t.FullName)
                .ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        private static IEnumerable<Type> OurTypes(Func<AsmdefInfo, bool> filter = null)
        {
            var names = new HashSet<string>(Ours.Where(a => !a.IsTests && (filter == null || filter(a))).Select(a => a.Name));
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!names.Contains(assembly.GetName().Name)) continue;
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types) yield return type;
            }
        }

        private static string StripComment(string line)
        {
            var inString = false;
            for (var i = 0; i < line.Length - 1; i++)
            {
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\')) inString = !inString;
                if (!inString && line[i] == '/' && line[i + 1] == '/') return line.Substring(0, i);
            }

            return line;
        }

        private static string Relative(string path) => path.Substring(AssemblyCatalog.ProjectRoot.Length + 1);
    }
}
