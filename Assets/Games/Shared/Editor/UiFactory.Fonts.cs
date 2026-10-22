using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Tileworlds.Games.Editor
{
    public static partial class UiFactory
    {
        private const string DisplayCharacters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~ÇĞİÖŞÜçğıöşü×…–—−‘’“”•";
        // Glyphs the display face lacks, such as the lives timer's "∞", cut from Liberation Sans into a tiny static fallback.
        private const string SymbolCharacters = "∞♥★";
        private const string SymbolsSourcePath = "Assets/ThirdParty/TextMesh Pro/Fonts/LiberationSans.ttf";

        // The rounded display face, baked once into a static atlas so it never changes at runtime. Its only fallback is the
        // symbols face, so the Liberation Sans atlas and font file stay out of the build.
        public static TMP_FontAsset DisplayFont()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayFontPath);
            if (asset == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/LilitaOne/LilitaOne-Regular.ttf");
                if (font == null) return null;
                asset = StaticFont(font, "LilitaOne SDF", DisplayCharacters, 1024, 1024, DisplayFontPath);
            }

            var symbols = SymbolsFont();
            var fallbacks = asset.fallbackFontAssetTable;
            if (symbols != null && (fallbacks == null || fallbacks.Count != 1 || fallbacks[0] != symbols))
            {
                asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { symbols };
                EditorUtility.SetDirty(asset);
            }

            return asset;
        }

        private static TMP_FontAsset SymbolsFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SymbolsFontPath);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>(SymbolsSourcePath);
            return font == null ? null : StaticFont(font, "Symbols SDF", SymbolCharacters, 256, 128, SymbolsFontPath);
        }

        private static TMP_FontAsset StaticFont(Font font, string name, string characters, int width, int height, string path)
        {
            var asset = TMP_FontAsset.CreateFontAsset(font, 72, 10, GlyphRenderMode.SDFAA, width, height, AtlasPopulationMode.Dynamic, false);
            asset.name = name;
            asset.TryAddCharacters(characters, out _);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            return SaveFont(asset, path);
        }

        // White display text with an outline and a drop of the same colour, the way casual games letter buttons and titles.
        // One material per colour and width, saved next to the font, so labels that share it still batch.
        public static void Outline(TMP_Text text, Color colour, float width = 0.2f)
        {
            var path = $"{FontsFolder}/{text.font.name} Outline {ColorUtility.ToHtmlStringRGB(colour)} {Mathf.RoundToInt(width * 100)}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(text.font.material) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                material.shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
                material.EnableKeyword("OUTLINE_ON");
                material.SetColor("_OutlineColor", colour);
                material.SetFloat("_OutlineWidth", width);
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor", colour);
                material.SetFloat("_UnderlayOffsetY", -0.75f);
                material.SetFloat("_UnderlayDilate", width * 2f);
                AssetDatabase.CreateAsset(material, path);
            }

            text.fontSharedMaterial = material;
        }

        private static TMP_FontAsset SaveFont(TMP_FontAsset asset, string path)
        {
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var atlas in asset.atlasTextures)
            {
                atlas.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }

            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
