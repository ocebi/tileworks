using System;
using UnityEditor;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // Drawing and painting helpers shared by the level designers.
    public static class LevelGui
    {
        private const float Margin = 20f;

        private static readonly Lazy<GUIStyle> CentredStyle = new Lazy<GUIStyle>(() => new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });

        public static GUIStyle Centred => CentredStyle.Value;

        public static void Sprite(Rect rect, Sprite sprite, Color tint)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            var r = sprite.textureRect;
            var uv = new Rect(r.x / texture.width, r.y / texture.height, r.width / texture.width, r.height / texture.height);
            var old = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
            GUI.color = old;
        }

        // A point-filtered texture, `pixels` row by row from the top.
        public static Texture2D Pixels(int width, int height, Color32[] pixels)
        {
            var flipped = new Color32[pixels.Length];
            for (var y = 0; y < height; y++) Array.Copy(pixels, y * width, flipped, (height - 1 - y) * width, width);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            texture.SetPixels32(flipped);
            texture.Apply(false, true);
            return texture;
        }

        // Reserves a centred area of `columns` × `rows` square cells as large as the Inspector allows, up to `maxCell`.
        public static Rect Board(float width, float columns, float rows, float maxCell, out float cell)
        {
            var available = width - 2f * Margin;
            cell = Mathf.Floor(Mathf.Min(maxCell, available / columns));
            var area = GUILayoutUtility.GetRect(available, rows * cell + 8f);
            return new Rect(area.x + (area.width - columns * cell) * 0.5f, area.y + 4f, columns * cell, rows * cell);
        }

        // A row of square brush buttons that wraps; `icon` draws brush i into its button.
        public static int Palette(float width, int selected, GUIContent[] brushes, Action<int, Rect> icon, float size = 40f)
        {
            var perRow = Mathf.Max(1, (int)((width - 2f * Margin) / (size + 4f)));
            for (var start = 0; start < brushes.Length; start += perRow)
            {
                var row = GUILayoutUtility.GetRect(width - 2f * Margin, size + 4f);
                for (var i = start; i < Mathf.Min(start + perRow, brushes.Length); i++)
                {
                    var rect = new Rect(row.x + (i - start) * (size + 4f), row.y + 2f, size, size);
                    if (GUI.Toggle(rect, i == selected, new GUIContent(string.Empty, brushes[i].tooltip), GUI.skin.button)) selected = i;
                    icon(i, new Rect(rect.x + 4f, rect.y + 4f, size - 8f, size - 8f));
                    if (!string.IsNullOrEmpty(brushes[i].text)) GUI.Label(rect, brushes[i].text, Centred);
                }
            }

            return selected;
        }
    }
}
