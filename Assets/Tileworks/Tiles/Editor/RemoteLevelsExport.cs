using System.Collections.Generic;
using System.IO;
using System.Text;
using Tileworks.Tiles.Levels;
using UnityEditor;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // Checks every game's levels and writes what Tools/publish_levels.py uploads: Builds/Levels/<game>/manifest.json and the level
    // files in the sequence (docs/REMOTE-LEVELS.md). A game with a failing level isn't written at all.
    public static class RemoteLevelsExport
    {
        public const string OutputFolder = "Builds/Levels";

        [MenuItem("Tileworks/Levels/Export for Publishing")]
        private static void ExportFromMenu()
        {
            var report = Export(OutputFolder, out var ok);
            if (ok) Debug.Log("[Tileworks] " + report);
            else Debug.LogError("[Tileworks] " + report);
        }

        public static string Export(string outputFolder, out bool ok)
        {
            ok = true;
            var report = new StringBuilder($"Levels for publishing in {outputFolder}:\n");
            foreach (var designer in LevelDesigners.All())
            {
                var problems = new List<string>();
                var manifest = ExportGame(designer, outputFolder, problems);
                if (manifest != null)
                {
                    report.AppendLine($"  {designer.Game}: {manifest.Levels.Count} levels in the sequence");
                    continue;
                }

                ok = false;
                report.AppendLine($"  {designer.Game}: not exported, {problems.Count} problem(s)");
                foreach (var problem in problems) report.AppendLine("    " + problem);
            }

            return report.ToString();
        }

        // Writes <outputFolder>/<game>/; null, with nothing written, when a level fails.
        public static LevelManifest ExportGame(LevelDesigner designer, string outputFolder, List<string> problems)
        {
            var manifest = Check(designer, problems, out var files);
            var folder = Path.Combine(outputFolder, designer.Game);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            if (problems.Count > 0) return null;

            Directory.CreateDirectory(folder);
            foreach (var pair in files) File.WriteAllText(Path.Combine(folder, pair.Key + ".json"), pair.Value);
            File.WriteAllText(Path.Combine(folder, "manifest.json"), manifest.ToJson());
            // Tells the publish script where to record the published revision.
            File.WriteAllText(Path.Combine(folder, "source.json"), new Newtonsoft.Json.Linq.JObject { ["content"] = designer.Folder }.ToString());
            return manifest;
        }

        private static LevelManifest Check(LevelDesigner designer, List<string> problems, out Dictionary<string, string> files)
        {
            files = new Dictionary<string, string>();
            var manifest = new LevelManifest { Game = designer.Game };
            var sequence = LevelContent.Sequence(designer.Folder);
            if (sequence.Count == 0) problems.Add("the sequence is empty");
            foreach (var level in sequence)
            {
                var path = LevelContent.PathOf(designer.Folder, level.LevelId);
                if (!files.TryGetValue(level.LevelId, out var json))
                {
                    if (!File.Exists(path))
                    {
                        problems.Add($"{level.LevelId}: no file");
                        continue;
                    }

                    json = File.ReadAllText(path);
                    var before = problems.Count;
                    var envelope = Read(level.LevelId, json, problems);
                    if (envelope == null) continue;
                    if (envelope.Id != level.LevelId) problems.Add($"{level.LevelId}: the file says it is {envelope.Id}");
                    if (envelope.Game != designer.Game) problems.Add($"{level.LevelId}: the file is for {envelope.Game}");
                    if (!envelope.HashMatches()) problems.Add($"{level.LevelId}: content hash is stale; open and save it in the level editor");
                    var invalid = new List<string>();
                    // Designers check the level they have open, as the level inspector does.
                    designer.Load(envelope.Payload);
                    designer.Validate(envelope, invalid);
                    foreach (var problem in invalid) problems.Add($"{level.LevelId}: {problem}");
                    if (problems.Count > before) continue;
                    files[level.LevelId] = json;
                }

                var parsed = LevelEnvelope.Parse(json);
                manifest.Levels.Add(new LevelManifest.Entry
                {
                    Id = level.LevelId, Hash = parsed.ContentHash, PayloadVersion = parsed.PayloadVersion, Reward = level.Reward, Label = level.Label ?? string.Empty,
                });
            }

            return manifest;
        }

        private static LevelEnvelope Read(string id, string json, List<string> problems)
        {
            try
            {
                return LevelEnvelope.Parse(json);
            }
            catch (System.Exception e)
            {
                problems.Add($"{id}: unreadable ({e.Message})");
                return null;
            }
        }
    }
}
