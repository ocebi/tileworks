using System;
using Tileworks.Kit.UI;
using UnityEditor;
using UnityEngine;
using static Tileworlds.Games.Editor.SceneParts;

namespace Tileworlds.Games.Editor
{
    // Screen-space particle layers, shared by every game. A config gets its preset values only when it is first created, so
    // tuning in the Inspector survives rebuilds.
    public static class UiBursts
    {
        private const string Config = "Assets/Games/Shared/Config";

        private static readonly Color[] Party =
        {
            new Color32(255, 92, 92, 255), new Color32(255, 201, 46, 255), new Color32(84, 210, 102, 255), new Color32(62, 160, 255, 255),
            new Color32(186, 120, 255, 255), new Color32(255, 140, 200, 255),
        };

        private static readonly Color[] Gold = { new Color32(255, 236, 140, 255), new Color32(255, 255, 255, 255), new Color32(255, 205, 70, 255) };

        // Paper from both bottom corners for a win.
        public static UiBurst Confetti(Transform parent) => Build(parent, "Confetti", so =>
        {
            Set(so, "_count", 90);
            Set(so, "_spread", 34f);
            Set(so, "_speed", new Vector2(1500f, 2300f));
            Set(so, "_gravity", -1300f);
            Set(so, "_drag", 1.6f);
            Set(so, "_sway", 260f);
            Set(so, "_life", new Vector2(1.8f, 2.6f));
            Set(so, "_fadeFrom", 0.8f);
            Set(so, "_size", new Vector2(40f, 64f));
            Set(so, "_spin", new Vector2(180f, 540f));
            SetColours(so, Party);
        }, "fx_confetti_a", "fx_confetti_b", "fx_confetti_c");

        // Small gold stars where a goal piece lands in its chip.
        public static UiBurst GoalSparkle(Transform parent) => Build(parent, "GoalSparkle", so =>
        {
            Set(so, "_count", 7);
            Set(so, "_speed", new Vector2(260f, 520f));
            Set(so, "_gravity", -500f);
            Set(so, "_drag", 4f);
            Set(so, "_life", new Vector2(0.3f, 0.5f));
            Set(so, "_size", new Vector2(22f, 38f));
            SetColours(so, Gold);
        }, "fx_star", "fx_dot");

        // A thin wake of glints behind a goal piece flying to its chip.
        public static UiBurst GoalTrail(Transform parent) => Build(parent, "GoalTrail", so =>
        {
            Set(so, "_count", 2);
            Set(so, "_speed", new Vector2(40f, 140f));
            Set(so, "_gravity", -250f);
            Set(so, "_drag", 3f);
            Set(so, "_life", new Vector2(0.25f, 0.4f));
            Set(so, "_size", new Vector2(16f, 28f));
            Set(so, "_trailEvery", 0.03f);
            SetColours(so, Gold);
        }, "fx_star", "fx_dot");

        // A ring of stars thrown out when a result star or badge pops in.
        public static UiBurst StarBurst(Transform parent) => Build(parent, "StarBurst", so =>
        {
            Set(so, "_count", 16);
            Set(so, "_speed", new Vector2(700f, 1200f));
            Set(so, "_gravity", -900f);
            Set(so, "_drag", 3f);
            Set(so, "_life", new Vector2(0.5f, 0.8f));
            Set(so, "_size", new Vector2(30f, 56f));
            SetColours(so, Gold);
        }, "fx_star");

        // A sparkle or two drifting up a menu screen now and then; the jitter spreads them over the middle of the screen.
        public static UiBurst HomeSparkle(Transform parent) => Build(parent, "HomeSparkle", so =>
        {
            Set(so, "_count", 2);
            Set(so, "_speed", new Vector2(30f, 70f));
            Set(so, "_jitter", 460f);
            Set(so, "_gravity", 30f);
            Set(so, "_drag", 0.5f);
            Set(so, "_sway", 30f);
            Set(so, "_life", new Vector2(1.2f, 2f));
            Set(so, "_fadeFrom", 0.5f);
            Set(so, "_size", new Vector2(26f, 44f));
            Set(so, "_spin", new Vector2(20f, 80f));
            Set(so, "_growIn", 0.3f);
            SetColours(so, Gold);
        }, "fx_star");

        // Coins hopping out of a reward as it counts up.
        public static UiBurst Coins(Transform parent) => Build(parent, "CoinBurst", so =>
        {
            Set(so, "_count", 10);
            Set(so, "_spread", 120f);
            Set(so, "_speed", new Vector2(600f, 1000f));
            Set(so, "_gravity", -2600f);
            Set(so, "_drag", 0.8f);
            Set(so, "_life", new Vector2(0.55f, 0.75f));
            Set(so, "_size", new Vector2(44f, 60f));
            Set(so, "_spin", new Vector2(0f, 120f));
        }, KitArt.Load("icon_coin"));

        private static UiBurst Build(Transform parent, string name, Action<SerializedObject> preset, params string[] fxSprites) =>
            Build(parent, name, preset, Array.ConvertAll(fxSprites, FxArt.Load));

        private static UiBurst Build(Transform parent, string name, Action<SerializedObject> preset, params Sprite[] sprites)
        {
            var rect = UiFactory.Stretch(name, parent);
            UiFactory.NestedCanvas(rect, false);
            var burst = rect.gameObject.AddComponent<UiBurst>();
            UiFactory.AssignMotion(burst);
            Assign(burst, "_config", ConfigFor(name, preset));
            AssignArray(burst, "_sprites", sprites);
            return burst;
        }

        private static UiBurstConfig ConfigFor(string name, Action<SerializedObject> preset)
        {
            var path = $"{Config}/Burst{name}.asset";
            var config = Load<UiBurstConfig>(path);
            if (config != null) return config;
            config = LoadOrCreate<UiBurstConfig>(path);
            var so = new SerializedObject(config);
            preset(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static void Set(SerializedObject so, string field, int value) => so.FindProperty(field).intValue = value;

        private static void Set(SerializedObject so, string field, float value) => so.FindProperty(field).floatValue = value;

        private static void Set(SerializedObject so, string field, Vector2 value) => so.FindProperty(field).vector2Value = value;

        private static void SetColours(SerializedObject so, Color[] colours)
        {
            var property = so.FindProperty("_palette");
            property.arraySize = colours.Length;
            for (var i = 0; i < colours.Length; i++) property.GetArrayElementAtIndex(i).colorValue = colours[i];
        }
    }
}
