using Tileworks.Tiles.View;
using UnityEngine;

namespace Tileworlds.Games.Editor
{
    // The Home's level path: level tiles on a wave with dots between them, and the Home's mascot peeking out behind the current
    // one. The view lays them out at run time, so the band and the mascot it gets here are all a Home decides.
    public static class LevelPathScreen
    {
        private const string ConfigPath = "Assets/Games/Shared/Config/LevelPath.asset";
        private const int Nodes = 7;
        private const int DotsPerGap = 4;
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        // `band` is the rect the path spans. It gets its own canvas, so the breathing node never rebatches the Home around it.
        public static LevelPathView Build(RectTransform band, RectTransform mascot = null)
        {
            UiFactory.NestedCanvas(band, clickable: true);
            if (mascot != null) mascot.SetParent(band, false);
            var dots = new Object[(Nodes - 1) * DotsPerGap];
            for (var i = 0; i < dots.Length; i++)
                dots[i] = UiFactory.Image(UiFactory.Rect("Dot", band, Centre, Centre, Vector2.zero, new Vector2(18, 18)), FxArt.Load("fx_dot"), Color.white);
            var nodes = new Object[Nodes];
            for (var i = 0; i < Nodes; i++) nodes[i] = LevelListScreen.Tile(band);

            var view = band.gameObject.AddComponent<LevelPathView>();
            UiFactory.AssignMotion(view);
            SceneParts.Assign(view, "_config", SceneParts.LoadOrCreate<LevelPathConfig>(ConfigPath));
            SceneParts.Assign(view, "_skin", SceneParts.LoadOrCreate<LevelListSkin>(LevelListScreen.SkinPath));
            SceneParts.AssignArray(view, "_nodes", nodes);
            SceneParts.AssignArray(view, "_dots", dots);
            if (mascot != null) SceneParts.Assign(view, "_mascot", mascot);
            return view;
        }
    }
}
