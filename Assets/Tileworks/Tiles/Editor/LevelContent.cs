using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;
using UnityEditor;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // A game's content folder: Levels/*.json, sequence.json and the LevelPack asset that ships them.
    public static class LevelContent
    {
        public static string FolderOf(string levelPath) => Path.GetDirectoryName(Path.GetDirectoryName(levelPath))?.Replace('\\', '/');

        public static string PathOf(string folder, string id) => $"{folder}/Levels/{id}.json";

        public static HashSet<string> LevelIds(string folder) =>
            Directory.Exists($"{folder}/Levels") ? new HashSet<string>(Directory.GetFiles($"{folder}/Levels", "*.json").Select(Path.GetFileNameWithoutExtension)) : new HashSet<string>();

        public static List<LevelRef> Sequence(string folder)
        {
            var path = $"{folder}/sequence.json";
            var sequence = File.Exists(path) ? LevelSequence.Parse(File.ReadAllText(path)) : new LevelSequence();
            var refs = new List<LevelRef>(sequence.Count);
            for (var i = 0; i < sequence.Count; i++) refs.Add(sequence.At(i));
            return refs;
        }

        public static void WriteSequence(string folder, IReadOnlyList<LevelRef> levels)
        {
            File.WriteAllText($"{folder}/sequence.json", LevelSequence.Write(Reindexed(levels)));
            AssetDatabase.ImportAsset($"{folder}/sequence.json");
            RefreshPack(folder);
        }

        // For the generators: levels already in the sequence keep their place and reward, levels a designer took out stay out,
        // and levels that didn't exist before this run (`before`) join at the end.
        public static void WriteMerged(string folder, IReadOnlyList<LevelRef> generated, ICollection<string> before)
        {
            var fresh = generated.ToDictionary(r => r.LevelId);
            var exists = LevelIds(folder);
            var merged = new List<LevelRef>();
            foreach (var arranged in Sequence(folder))
            {
                if (fresh.TryGetValue(arranged.LevelId, out var made)) merged.Add(new LevelRef(0, made.LevelId, made.Label, arranged.Reward));
                else if (exists.Contains(arranged.LevelId)) merged.Add(arranged);
            }

            foreach (var made in generated)
                if (!before.Contains(made.LevelId) && merged.All(r => r.LevelId != made.LevelId))
                    merged.Add(made);
            File.WriteAllText($"{folder}/sequence.json", LevelSequence.Write(Reindexed(merged)));
        }

        // Points the pack at every level file and the sequence, as the content builders do.
        public static void RefreshPack(string folder)
        {
            var pack = AssetDatabase.LoadAssetAtPath<LevelPackAsset>($"{folder}/LevelPack.asset");
            if (pack == null) return;
            var levels = Directory.GetFiles($"{folder}/Levels", "*.json").OrderBy(Path.GetFileName)
                .Select(p => AssetDatabase.LoadAssetAtPath<TextAsset>(p.Replace('\\', '/'))).Where(a => a != null).ToArray();
            pack.SetContent(levels, AssetDatabase.LoadAssetAtPath<TextAsset>($"{folder}/sequence.json"));
            pack.SetPublishedRevision(PublishedRevision(folder));
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
        }

        // Written by Tools/publish_levels.py after a publish, so the next build knows which revision its levels match.
        public static string PublishedPath(string folder) => $"{folder}/published.json";

        public static int PublishedRevision(string folder)
        {
            var path = PublishedPath(folder);
            if (!File.Exists(path)) return 0;
            try
            {
                return (int?)JObject.Parse(File.ReadAllText(path))["revision"] ?? 0;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return 0;
            }
        }

        public static string NextId(string folder, string prefix)
        {
            var next = LevelIds(folder).Where(id => id.StartsWith(prefix + "_"))
                .Select(id => int.TryParse(id.Substring(prefix.Length + 1), out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
            return $"{prefix}_{next:0000}";
        }

        // Writes a new level file (the designer's empty level, or a copy of `source`) and returns its id.
        public static string CreateFile(LevelDesigner designer, LevelEnvelope source = null)
        {
            var envelope = source != null ? LevelEnvelope.Parse(source.ToJson()) : new LevelEnvelope { Game = designer.Game, Payload = designer.NewPayload() };
            envelope.Id = NextId(designer.Folder, designer.Prefix);
            envelope.Metrics.Clear();
            if (!envelope.Tags.Contains(LevelEnvelope.EditedTag)) envelope.Tags.Add(LevelEnvelope.EditedTag);
            Directory.CreateDirectory($"{designer.Folder}/Levels");
            var path = PathOf(designer.Folder, envelope.Id);
            File.WriteAllText(path, envelope.ToJson());
            AssetDatabase.ImportAsset(path);
            return envelope.Id;
        }

        // The Assets/Create menu: a new level at the end of the sequence, opened in the level window.
        public static void Create(LevelDesigner designer)
        {
            var id = CreateFile(designer);
            var levels = Sequence(designer.Folder);
            var reward = levels.Count > 0 ? levels[levels.Count - 1].Reward : 0;
            levels.Add(new LevelRef(0, id, string.Empty, reward));
            WriteSequence(designer.Folder, levels);
            LevelSequenceWindow.Open(designer.Game, id);
        }

        public static JObject Payload(string folder, string id) => LevelEnvelope.Parse(File.ReadAllText(PathOf(folder, id))).Payload;

        private static List<LevelRef> Reindexed(IReadOnlyList<LevelRef> levels)
        {
            var result = new List<LevelRef>(levels.Count);
            for (var i = 0; i < levels.Count; i++) result.Add(new LevelRef(i, levels[i].LevelId, levels[i].Label, levels[i].Reward));
            return result;
        }
    }
}
