using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Editor;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;
using TileMatch.Generation;
using TileMatch.Model;
using TileMatch.Service;
using UnityEditor;
using UnityEngine;

namespace TileMatch.Editor
{
    // Layouts (kind-less) → generator → Content/Levels/*.json + sequence.json + report.csv → LevelPack asset.
    public static class ContentBuilder
    {
        public const string Root = "Assets/Games/TileMatch/Content";
        public const string LayoutsFolder = Root + "/Layouts";
        public const string LevelsFolder = Root + "/Levels";
        public const string SequencePath = Root + "/sequence.json";
        public const string ReportPath = Root + "/report.csv";
        public const string PackPath = Root + "/LevelPack.asset";

        // Kinds a missed band may add or take away, one per retry, up to MaxKinds.
        private const int KindRetries = 3;
        private const int MaxKinds = 14;

        [MenuItem("Tileworks/Tile Match/Generate Levels")]
        public static void GenerateAll()
        {
            var before = LevelContent.LevelIds(Root);
            Directory.CreateDirectory(LevelsFolder);
            var layouts = Directory.GetFiles(LayoutsFolder, "*.txt").OrderBy(Path.GetFileName).ToArray();
            var names = layouts.Select(Path.GetFileNameWithoutExtension).ToArray();
            var files = layouts.Select((path, i) => LayoutParser.Parse(names[i], File.ReadAllText(path))).ToArray();
            var ids = names.Select((_, i) => $"tm_{i + 1:0000}").ToArray();
            var kept = ids.Select(id => File.Exists($"{LevelsFolder}/{id}.json") ? LevelEnvelope.Parse(File.ReadAllText($"{LevelsFolder}/{id}.json")) : null)
                .Select(e => e != null && e.Tags.Contains(LevelEnvelope.EditedTag) ? e : null).ToArray();
            LevelBatch.Run("Generating Tile Match levels", files.Length, i => kept[i] != null ? null : Generate(ids[i], names[i], files[i]),
                results => Write(ids, names, files, kept, results, before));
        }

        // Tries the layout's kind count first; a level off its band gets one more kind if too easy, one fewer if too hard.
        private static GenerationResult Generate(string id, string name, LayoutFile file)
        {
            var generator = new LevelGenerator(TileMatchRules.CreateDefault(), new DifficultyBands(), new AnalyzerSettings { Playouts = 150, SolverNodes = 150_000 });
            GenerationResult best = null;
            var kinds = file.Kinds;
            for (var retry = 0; retry <= KindRetries; retry++)
            {
                var result = generator.Generate(new GenerationRequest
                {
                    Id = id, Layout = file.Layout, Assigner = AssignerFor(file.Band, kinds), TargetBand = file.Band, Seed = StableHash.Hash64(name), MaxAttempts = 40,
                });
                if (result == null) break;
                if (best == null || Off(result.Band, file.Band) < Off(best.Band, file.Band)) best = result;
                if (result.HitTarget) break;
                kinds += result.Band < file.Band ? 1 : -1;
                if (kinds < 3 || kinds > Math.Min(MaxKinds, file.Layout.TileCount / 3)) break;
            }

            return best;
        }

        private static void Write(string[] ids, string[] names, LayoutFile[] files, LevelEnvelope[] kept, GenerationResult[] results, ICollection<string> before)
        {
            var serializer = new TileMatchLevelSerializer();
            var sequence = new List<LevelRef>();
            var report = new StringBuilder("id,layout,tiles,kinds,band,target,hit,attempts,pRandom,pGreedy,pCasual,minPeak,deadEnds,visibleKinds,solvable\n");
            for (var i = 0; i < files.Length; i++)
            {
                string id = ids[i], name = names[i];
                if (kept[i] != null)
                {
                    var keptBand = DifficultyBands.Parse(kept[i].Difficulty);
                    sequence.Add(new LevelRef(i, id, keptBand >= DifficultyBand.Hard ? kept[i].Difficulty : string.Empty, Reward(keptBand)));
                    report.AppendLine($"{id},{name},edited by hand");
                    continue;
                }

                var result = results[i];
                if (result == null)
                {
                    Debug.LogError($"[Tileworks] {name}: no valid kind assignment found");
                    continue;
                }

                var envelope = new LevelEnvelope
                {
                    Id = id, Game = serializer.Game, PayloadVersion = serializer.CurrentPayloadVersion,
                    Difficulty = DifficultyBands.Label(result.Band), Payload = serializer.Write(result.Level),
                };
                envelope.Tags.Add("layout:" + name);
                foreach (var spec in result.Level.Modifiers)
                {
                    var tag = TileMatchLevelSerializer.NameOf(spec.Type);
                    if (!envelope.Tags.Contains(tag)) envelope.Tags.Add(tag);
                }

                if (result.Level.Spawners.Length > 0) envelope.Tags.Add("spawner");
                var m = result.Metrics;
                envelope.Metrics["pRandom"] = m.PRandom;
                envelope.Metrics["pGreedy"] = m.PGreedy;
                envelope.Metrics["pCasual"] = m.PCasual;
                envelope.Metrics["minPeak"] = m.MinPeak;
                envelope.Metrics["deadEndRatio"] = m.DeadEndRatio;
                envelope.Metrics["visibleKinds"] = m.VisibleKindsAtStart;
                File.WriteAllText($"{LevelsFolder}/{id}.json", envelope.ToJson());

                sequence.Add(new LevelRef(i, id, result.Band >= DifficultyBand.Hard ? DifficultyBands.Label(result.Band) : string.Empty, Reward(result.Band)));
                report.AppendLine(string.Join(",", id, name, result.Level.TileCount, result.Level.KindCount, DifficultyBands.Label(result.Band),
                    DifficultyBands.Label(files[i].Band), result.HitTarget, result.Attempts, m.PRandom.ToString("0.00"), m.PGreedy.ToString("0.00"), m.PCasual.ToString("0.00"),
                    m.MinPeak, m.DeadEndRatio.ToString("0.00"), m.VisibleKindsAtStart, m.Solvable));
            }

            LevelContent.WriteMerged(Root, sequence, before);
            File.WriteAllText(ReportPath, report.ToString());
            AssetDatabase.Refresh();
            UpdatePack();
            Debug.Log($"[Tileworks] Generated {sequence.Count} levels, {results.Count(r => r != null && r.HitTarget)} on their band");
        }

        [MenuItem("Tileworks/Tile Match/Update Level Pack")]
        public static LevelPackAsset UpdatePack()
        {
            var pack = AssetDatabase.LoadAssetAtPath<LevelPackAsset>(PackPath);
            if (pack == null)
            {
                pack = ScriptableObject.CreateInstance<LevelPackAsset>();
                AssetDatabase.CreateAsset(pack, PackPath);
            }

            var levels = Directory.GetFiles(LevelsFolder, "*.json").OrderBy(Path.GetFileName)
                .Select(path => AssetDatabase.LoadAssetAtPath<TextAsset>(path)).Where(asset => asset != null).ToArray();
            pack.SetContent(levels, AssetDatabase.LoadAssetAtPath<TextAsset>(SequencePath));
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            return pack;
        }

        // Re-validates every shipped level: schema, content hash, kind counts and solvability.
        [MenuItem("Tileworks/Tile Match/Validate All Levels")]
        public static bool ValidateAll()
        {
            var rules = TileMatchRules.CreateDefault();
            var serializer = new TileMatchLevelSerializer();
            var ok = true;
            foreach (var path in Directory.GetFiles(LevelsFolder, "*.json").OrderBy(Path.GetFileName))
            {
                var envelope = LevelEnvelope.Parse(File.ReadAllText(path));
                var report = new ValidationReport();
                report.Require(envelope.HashMatches(), "content hash does not match the payload");
                var level = serializer.Read(envelope);
                serializer.Validate(level, report);
                var metrics = DifficultyAnalyzer.Measure(rules, level, new AnalyzerSettings { Playouts = 20, SolverNodes = 150_000, PeakNodes = 1 });
                report.Require(metrics.Solvable || metrics.SolverExhausted, "proven unsolvable");
                if (!report.HasErrors) continue;
                ok = false;
                Debug.LogError($"[Tileworks] {envelope.Id}:\n{report}");
            }

            Debug.Log(ok ? "[Tileworks] All levels valid" : "[Tileworks] Level validation failed");
            return ok;
        }

        // Harder targets interleave groups more and leave no tray headroom in the witness line.
        internal static AssignerSettings AssignerFor(LayoutFile file) => AssignerFor(file.Band, file.Kinds);

        private static AssignerSettings AssignerFor(DifficultyBand band, int kinds) => band switch
        {
            DifficultyBand.Easy => new AssignerSettings { KindCount = kinds, Margin = 2, CloseBiasPercent = 70 },
            DifficultyBand.Normal => new AssignerSettings { KindCount = kinds, Margin = 1, CloseBiasPercent = 40 },
            DifficultyBand.Hard => new AssignerSettings { KindCount = kinds, Margin = 0, CloseBiasPercent = 20 },
            _ => new AssignerSettings { KindCount = kinds, Margin = 0, CloseBiasPercent = 10 },
        };

        private static int Off(DifficultyBand a, DifficultyBand b) => Math.Abs((int)a - (int)b);

        private static int Reward(DifficultyBand band) => band switch
        {
            DifficultyBand.Easy => 10,
            DifficultyBand.Normal => 15,
            DifficultyBand.Hard => 25,
            _ => 40,
        };
    }
}
