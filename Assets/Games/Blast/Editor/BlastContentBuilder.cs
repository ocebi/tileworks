using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Blast.Generation;
using Blast.Model;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Editor;
using Tileworks.Tiles.Generation;
using Tileworks.Tiles.Levels;
using Tileworks.Tiles.Search;
using Tileworks.Tiles.View;
using UnityEditor;
using UnityEngine;

namespace Blast.Editor
{
    // BlastPlanner's recipes → generator → Content/Levels/*.json + sequence.json + report.csv → LevelPack asset.
    public static class BlastContentBuilder
    {
        public const string Root = "Assets/Games/Blast/Content";
        public const string LevelsFolder = Root + "/Levels";
        public const string SequencePath = Root + "/sequence.json";
        public const string ReportPath = Root + "/report.csv";
        public const string PackPath = Root + "/LevelPack.asset";

        public const int LevelCount = 100;

        [MenuItem("Tileworks/Blast/Generate Levels")]
        public static void GenerateAll()
        {
            var before = LevelContent.LevelIds(Root);
            Directory.CreateDirectory(LevelsFolder);
            var recipes = BlastPlanner.Plan(LevelCount);
            var kept = recipes.Select(r => TryKeepEdited(r.Id, out var envelope) ? envelope : null).ToArray();
            LevelBatch.Run("Generating Blast levels", recipes.Length, i => kept[i] != null ? null : new BlastGenerator(new BlastRules(), profile: BotProfile.Skilled).Generate(recipes[i]),
                results => Write(recipes, kept, results, before));
        }

        private static void Write(BlastRecipe[] recipes, LevelEnvelope[] kept, GeneratedBlast[] results, ICollection<string> before)
        {
            var serializer = new BlastLevelSerializer();
            var sequence = new List<LevelRef>();
            var report = new StringBuilder("id,size,shape,pens,movable,colours,moves,goals,boxes,balloons,gifts,target,band,hit,attempts,skilledWinRate,casualWinRate,movesLeftOnWin,goalLeftOnLoss,nearMisses,shufflesPerGame\n");
            for (var i = 0; i < recipes.Length; i++)
            {
                var recipe = recipes[i];
                if (kept[i] != null)
                {
                    var keptRole = LevelCurve.Parse(kept[i].Difficulty);
                    sequence.Add(new LevelRef(i, recipe.Id, LevelCurve.Badge(keptRole), Reward(keptRole)));
                    report.AppendLine($"{recipe.Id},edited by hand,,,,,,,,,,,{kept[i].Difficulty}");
                    continue;
                }

                var result = results[i];
                var level = result.Level;
                var envelope = new LevelEnvelope
                {
                    Id = recipe.Id, Game = serializer.Game, PayloadVersion = serializer.CurrentPayloadVersion, Difficulty = LevelCurve.Label(result.Role),
                    Payload = serializer.Write(level),
                };
                foreach (var goal in level.Goals) envelope.Tags.Add(goal.Kind.ToString().ToLowerInvariant());
                envelope.Tags.Add("shape:" + recipe.Shape.ToString().ToLowerInvariant());
                if (recipe.Pens != PenLayout.None) envelope.Tags.Add("pens:" + recipe.Pens.ToString().ToLowerInvariant());
                var stats = result.Stats;
                envelope.Metrics["winRate"] = stats.WinRate;
                envelope.Metrics["casualWinRate"] = result.Casual.WinRate;
                envelope.Metrics["nearMisses"] = stats.NearMisses;
                File.WriteAllText($"{LevelsFolder}/{recipe.Id}.json", envelope.ToJson());
                sequence.Add(new LevelRef(i, recipe.Id, LevelCurve.Badge(result.Role), Reward(result.Role)));
                var gifts = string.Join(" ", new[] { recipe.Rockets > 0 ? "rockets" : null, recipe.Bomb ? "bomb" : null, recipe.Disco ? "disco" : null }.Where(g => g != null));
                report.AppendLine(string.Join(",", recipe.Id, $"{level.Width}x{level.Height}", recipe.Shape, Pens(recipe), BlastGenerator.MovableShare(level).ToString("0.00"), level.Colours, level.Moves,
                    string.Join(" ", level.Goals.Select(g => g.ToString())), recipe.Boxes > 0 ? recipe.BoxPattern.ToString() : "", recipe.Balloons > 0 ? recipe.BalloonPattern.ToString() : "",
                    gifts, recipe.Target?.ToString(), LevelCurve.Label(result.Role), result.HitBand, result.Attempts, stats.WinRate.ToString("0.00"), result.Casual.WinRate.ToString("0.00"),
                    stats.MovesLeftOnWin.ToString("0.00"), stats.GoalLeftOnLoss.ToString("0.00"), stats.NearMisses.ToString("0.00"), stats.ShufflesPerGame.ToString("0.00")));
            }

            LevelContent.WriteMerged(Root, sequence, before);
            File.WriteAllText(ReportPath, report.ToString());
            AssetDatabase.Refresh();
            UpdatePack();
            Debug.Log($"[Tileworks] Generated {sequence.Count} blast levels, {results.Count(r => r != null && r.HitBand)} on target");
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

        private static string Pens(BlastRecipe recipe) => recipe.Pens != PenLayout.None ? recipe.Pens.ToString() : BoardShapes.HasPockets(recipe.Shape) ? "pockets" : "";

        private static int Reward(LevelRole role) => role switch
        {
            LevelRole.Easy => 20,
            LevelRole.Normal => 30,
            LevelRole.Hard => 50,
            _ => 80,
        };
    }
}
