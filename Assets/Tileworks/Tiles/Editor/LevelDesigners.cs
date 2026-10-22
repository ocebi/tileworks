using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Tileworks.Tiles.Editor
{
    public static class LevelDesigners
    {
        public static LevelDesigner Create(string game) => All().FirstOrDefault(d => d.Game == game);

        public static List<LevelDesigner> All() => TypeCache.GetTypesDerivedFrom<LevelDesigner>().Where(t => !t.IsAbstract)
            .Select(t => (LevelDesigner)Activator.CreateInstance(t)).OrderBy(d => d.Title).ToList();
    }
}
