using Tileworks.Tiles.View;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Tileworks.Tiles.Editor
{
    // Each build ships the published revision its levels match (Content/published.json), so it never takes an older update.
    public sealed class LevelPackBuildStamp : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            foreach (var designer in LevelDesigners.All())
            {
                var pack = AssetDatabase.LoadAssetAtPath<LevelPackAsset>($"{designer.Folder}/LevelPack.asset");
                var revision = LevelContent.PublishedRevision(designer.Folder);
                if (pack == null || pack.PublishedRevision == revision) continue;
                pack.SetPublishedRevision(revision);
                EditorUtility.SetDirty(pack);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
