using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tileworks.Tiles.Levels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tileworks.Tiles.Editor
{
    // One game's levels for the level window: the play order, the files left out of it, and what each file says.
    public sealed class LevelCatalog : IDisposable
    {
        public sealed class Entry
        {
            public string Id;
            public string Path;
            public string Difficulty;
            public float? WinRate;
            public bool Edited;
            public Texture2D Thumbnail;
            public bool ThumbnailBuilt;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        public LevelCatalog(LevelDesigner designer)
        {
            Designer = designer;
            Reload();
        }

        public LevelDesigner Designer { get; }
        public List<LevelRef> Sequence { get; } = new List<LevelRef>();
        public List<string> LeftOut { get; } = new List<string>();

        public Entry this[string id] => id != null && _entries.TryGetValue(id, out var entry) ? entry : null;

        public int IndexOf(string id) => Sequence.FindIndex(r => r.LevelId == id);

        public void Reload()
        {
            Dispose();
            _entries.Clear();
            foreach (var id in LevelContent.LevelIds(Designer.Folder)) _entries[id] = Read(id);
            Sequence.Clear();
            Sequence.AddRange(LevelContent.Sequence(Designer.Folder));
            LeftOut.Clear();
            LeftOut.AddRange(_entries.Keys.Where(id => IndexOf(id) < 0).OrderBy(id => id, StringComparer.Ordinal));
        }

        public Texture2D ThumbnailOf(Entry entry)
        {
            if (entry.ThumbnailBuilt) return entry.Thumbnail;
            entry.ThumbnailBuilt = true;
            try
            {
                entry.Thumbnail = Designer.Thumbnail(LevelContent.Payload(Designer.Folder, entry.Id));
            }
            catch (Exception)
            {
                entry.Thumbnail = null;
            }

            return entry.Thumbnail;
        }

        // Writes the play order; each level's badge follows its difficulty.
        public void Save()
        {
            for (var i = 0; i < Sequence.Count; i++)
            {
                var level = Sequence[i];
                var entry = this[level.LevelId];
                Sequence[i] = new LevelRef(i, level.LevelId, entry != null ? Designer.BadgeOf(entry.Difficulty) : level.Label, level.Reward);
            }

            LevelContent.WriteSequence(Designer.Folder, Sequence);
            Reload();
        }

        public void Dispose()
        {
            foreach (var entry in _entries.Values)
                if (entry.Thumbnail != null)
                    Object.DestroyImmediate(entry.Thumbnail);
        }

        private Entry Read(string id)
        {
            var entry = new Entry { Id = id, Path = LevelContent.PathOf(Designer.Folder, id), Difficulty = "unreadable" };
            try
            {
                var envelope = LevelEnvelope.Parse(File.ReadAllText(entry.Path));
                entry.Difficulty = envelope.Difficulty;
                entry.Edited = envelope.Tags.Contains(LevelEnvelope.EditedTag);
                if (envelope.Metrics.TryGetValue("winRate", out var rate) || envelope.Metrics.TryGetValue("pCasual", out rate)) entry.WinRate = (float)rate;
            }
            catch (Exception)
            {
                // Listed as unreadable; the inspector shows why.
            }

            return entry;
        }
    }
}
