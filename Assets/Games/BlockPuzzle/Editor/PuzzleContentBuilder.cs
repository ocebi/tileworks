using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BlockPuzzle.Generation;
using BlockPuzzle.Model;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Editor;
using Tileworks.Tiles.Generation;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.View;
using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.Editor
{
    // PuzzlePlanner's recipes → generator → Content/Levels/*.json + sequence.json + report.csv → LevelPack asset.
    public static class PuzzleContentBuilder
    {
        public const string Root = "Assets/Games/BlockPuzzle/Content";
        public const string LevelsFolder = Root + "/Levels";
        public const string SequencePath = Root + "/sequence.json";
        public const string ReportPath = Root + "/report.csv";
        public const string PackPath = Root + "/LevelPack.asset";

        public const int LevelCount = 100;

        [MenuItem("Tileworks/Block Puzzle/Generate Levels")]
        public static void GenerateAll()
        {
            var before = LevelContent.LevelIds(Root);
            Directory.CreateDirectory(LevelsFolder);
            var recipes = PuzzlePlanner.Plan(LevelCount);
            var kept = recipes.Select(r => TryKeepEdited(r.Id, out var envelope) ? envelope : null).ToArray();
            LevelBatch.Run("Generating Block Puzzle levels", recipes.Length, i => kept[i] != null ? null : new PuzzleGenerator(new BlockRules()).Generate(recipes[i]),
                results => Write(recipes, kept, results, before));
        }

        private static void Write(PuzzleRecipe[] recipes, LevelEnvelope[] kept, GeneratedPuzzle[] results, ICollection<string> before)
        {
            var shapes = new BlockRules().Shapes;
            var serializer = new BlockLevelSerializer(shapes);
            var sequence = new List<LevelRef>();
            var report = new StringBuilder("id,picture,goal,target,tier,curve,band,hit,attempts,winRate,goalLeftOnLoss,nearMisses\n");
            for (var i = 0; i < recipes.Length; i++)
            {
                var recipe = recipes[i];
                if (kept[i] != null)
                {
                    var keptRole = LevelCurve.Parse(kept[i].Difficulty);
                    sequence.Add(new LevelRef(i, recipe.Id, LevelCurve.Badge(keptRole), Reward(keptRole)));
                    report.AppendLine($"{recipe.Id},edited by hand,,,,,{kept[i].Difficulty}");
                    continue;
                }

                var result = results[i];
                var level = result.Level;
                var envelope = new LevelEnvelope
                {
                    Id = recipe.Id, Game = serializer.Game, PayloadVersion = serializer.CurrentPayloadVersion, Difficulty = LevelCurve.Label(result.Role),
                    Payload = serializer.Write(level),
                };
                envelope.Tags.Add(level.Goal == BlockGoal.Gems ? "gems" : "score");
                if (recipe.Picture != null) envelope.Tags.Add("picture:" + recipe.Picture);
                var stats = result.Stats;
                envelope.Metrics["winRate"] = stats.WinRate;
                envelope.Metrics["nearMisses"] = stats.NearMisses;
                File.WriteAllText($"{LevelsFolder}/{recipe.Id}.json", envelope.ToJson());
                sequence.Add(new LevelRef(i, recipe.Id, LevelCurve.Badge(result.Role), Reward(result.Role)));
                report.AppendLine(string.Join(",", recipe.Id, recipe.Picture ?? "empty", level.Goal == BlockGoal.Gems ? "gems" : "score",
                    level.Goal == BlockGoal.Gems ? level.GemCount : level.ScoreTarget, level.Pool.Max(id => shapes[id].Tier), recipe.Target?.ToString(),
                    LevelCurve.Label(result.Role), result.HitBand, result.Attempts, stats.WinRate.ToString("0.00"), stats.GoalLeftOnLoss.ToString("0.00"),
                    stats.NearMisses.ToString("0.00")));
            }

            LevelContent.WriteMerged(Root, sequence, before);
            File.WriteAllText(ReportPath, report.ToString());
            AssetDatabase.Refresh();
            UpdatePack();
            Debug.Log($"[Tileworks] Generated {sequence.Count} block puzzle levels, {results.Count(r => r != null && r.HitBand)} on target");
        }

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

        // A level someone saved from the level designer is kept as it is.
        private static bool TryKeepEdited(string id, out LevelEnvelope envelope)
        {
            var path = $"{LevelsFolder}/{id}.json";
            envelope = File.Exists(path) ? LevelEnvelope.Parse(File.ReadAllText(path)) : null;
            return envelope != null && envelope.Tags.Contains(LevelEnvelope.EditedTag);
        }

        private static int Reward(LevelRole role) => role switch
        {
            LevelRole.Easy => 20,
            LevelRole.Normal => 30,
            LevelRole.Hard => 50,
            _ => 80,
        };
    }
}
