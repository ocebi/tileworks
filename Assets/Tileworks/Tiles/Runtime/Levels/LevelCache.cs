using System;
using System.Collections.Generic;
using System.IO;

namespace Tileworks.Tiles.Levels
{
    // Downloaded levels kept between sessions. Files are named by id and hash and never rewritten; index.json is written last and
    // names the current ones, so an interrupted update leaves the previous content whole.
    public sealed class LevelCache
    {
        private const string IndexFile = "index.json";

        public LevelCache(string folder) => Folder = folder;

        public string Folder { get; }

        public static string FolderFor(string profileDirectory, string game) => Path.Combine(profileDirectory, "levels", game);

        // Null when there is no index or it can't be read.
        public LevelManifest ReadIndex()
        {
            var path = Path.Combine(Folder, IndexFile);
            try
            {
                return File.Exists(path) ? LevelManifest.Parse(File.ReadAllText(path)) : null;
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is Newtonsoft.Json.JsonException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public bool Has(string id, string hash) => File.Exists(PathOf(id, hash));

        public string Read(string id, string hash)
        {
            try
            {
                return File.ReadAllText(PathOf(id, hash));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        public void Write(string id, string hash, string json) => WriteReplacing(PathOf(id, hash), json);

        // Makes `index` current, then deletes the level files it doesn't name.
        public void Commit(LevelManifest index)
        {
            WriteReplacing(Path.Combine(Folder, IndexFile), index.ToJson());
            var keep = new HashSet<string>(StringComparer.Ordinal) { IndexFile };
            foreach (var entry in index.Levels) keep.Add(Path.GetFileName(PathOf(entry.Id, entry.Hash)));
            foreach (var file in Directory.GetFiles(Folder))
            {
                if (keep.Contains(Path.GetFileName(file))) continue;
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // Left for the next commit.
                }
            }
        }

        public void Clear()
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
        }

        private string PathOf(string id, string hash) => Path.Combine(Folder, $"{id}.{hash}.json");

        // File.Replace isn't available on every platform's file system (WebGL's in-memory one), so this writes, deletes, then renames.
        private void WriteReplacing(string path, string text)
        {
            Directory.CreateDirectory(Folder);
            var temp = path + ".tmp";
            File.WriteAllText(temp, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }
}
