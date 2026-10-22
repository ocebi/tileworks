using System.IO;

namespace Tileworks.Kit.Content
{
    public static class SceneKey
    {
        // Scenes are addressed by name; catalogs and scene roots carry asset paths, which map to the same key.
        public static string Of(string pathOrName)
        {
            if (string.IsNullOrEmpty(pathOrName) || pathOrName.IndexOf('/') < 0) return pathOrName;
            return Path.GetFileNameWithoutExtension(pathOrName);
        }
    }
}
