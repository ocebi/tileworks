using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.UnityLinker;

namespace Tileworlds.Project.Editor
{
    // The container finds constructors by reflection and sqlite-net maps rows the same way, so managed stripping keeps
    // our own assemblies and sqlite-net whole and strips only the engine and other libraries.
    public sealed class PreserveOwnCode : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var names = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)
                .Where(a => a.sourceFiles.Any(f => f.StartsWith("Assets/Games/") || f.StartsWith("Assets/Tileworks/")))
                .Select(a => a.name)
                .Append("Gilzoide.SqliteNet");
            var xml = new StringBuilder("<linker>\n");
            foreach (var name in names) xml.AppendLine($"  <assembly fullname=\"{name}\" preserve=\"all\" ignoreIfMissing=\"1\"/>");
            xml.AppendLine("</linker>");
            var path = Path.GetFullPath("Temp/TileworksLink.xml");
            File.WriteAllText(path, xml.ToString());
            return path;
        }
    }
}
