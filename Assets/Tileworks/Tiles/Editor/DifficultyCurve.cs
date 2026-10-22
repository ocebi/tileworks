using UnityEditor;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // The play order as bars, one per level: taller is harder (the measured loss rate, or the band when unmeasured).
    public static class DifficultyCurve
    {
        public static Color Colour(string difficulty) => difficulty switch
        {
            "easy" => new Color(0.42f, 0.75f, 0.38f),
            "normal" => new Color(0.36f, 0.6f, 0.92f),
            "hard" => new Color(0.93f, 0.43f, 0.32f),
            "superHard" => new Color(0.7f, 0.42f, 0.93f),
            _ => new Color(0.5f, 0.5f, 0.5f),
        };

        public static float Hardness(LevelCatalog.Entry entry)
        {
            if (entry == null) return 0.05f;
            if (entry.WinRate is float rate) return Mathf.Clamp(1f - rate, 0.06f, 1f);
            return entry.Difficulty switch
            {
                "easy" => 0.25f,
                "normal" => 0.5f,
                "hard" => 0.75f,
                "superHard" => 0.95f,
                _ => 0.05f,
            };
        }

        // Returns the level clicked, or -1; `hovered` is the level under the mouse.
        public static int Draw(Rect rect, LevelCatalog catalog, int selected, out int hovered)
        {
            var e = Event.current;
            var count = catalog.Sequence.Count;
            hovered = -1;
            if (e.type == EventType.Repaint) EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.2f));
            if (count == 0) return -1;
            var step = rect.width / count;
            var gap = step > 4f ? 1f : 0f;
            for (var i = 0; i < count; i++)
            {
                var column = new Rect(rect.x + i * step, rect.y, step, rect.height);
                if (column.Contains(e.mousePosition)) hovered = i;
                if (e.type != EventType.Repaint) continue;
                var entry = catalog[catalog.Sequence[i].LevelId];
                var height = (rect.height - 4f) * Hardness(entry);
                var colour = Colour(entry?.Difficulty);
                if (i == selected) EditorGUI.DrawRect(column, new Color(1f, 1f, 1f, 0.18f));
                EditorGUI.DrawRect(new Rect(column.x + gap, rect.yMax - 2f - height, Mathf.Max(1f, step - 2f * gap), height), i == hovered ? Color.Lerp(colour, Color.white, 0.35f) : colour);
            }

            if (hovered >= 0 && e.type == EventType.Repaint)
            {
                var entry = catalog[catalog.Sequence[hovered].LevelId];
                var rate = entry?.WinRate is float w ? $", bots win {w:P0}" : string.Empty;
                GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 12f, 18f), $"Level {hovered + 1}: {catalog.Sequence[hovered].LevelId}, {entry?.Difficulty}{rate}",
                    EditorStyles.whiteMiniLabel);
            }

            if (hovered < 0 || e.type != EventType.MouseDown || e.button != 0) return -1;
            e.Use();
            return hovered;
        }
    }
}
