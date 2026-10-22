using System;
using System.Reflection;
using UnityEditor;

namespace Tileworlds.Games.Editor
{
    // Puts the Game view at a portrait phone resolution (1080×1920) for play-testing and screenshots.
    public static class GameViewSize
    {
        [MenuItem("Tileworks/Project/Game View Portrait 1080x1920")]
        public static void Portrait() => Set(1080, 1920, "Tileworlds Portrait");

        public static void Set(int width, int height, string label)
        {
            var sizesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
            var groupType = group.GetType();
            var count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            var index = -1;
            for (var i = 0; i < count && index < 0; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((string)size.GetType().GetProperty("baseText").GetValue(size) == label) index = i;
            }

            if (index < 0)
            {
                var sizeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize");
                var kindType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new object[] { Enum.ToObject(kindType, 1), width, height, label });
                groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = count;
            }

            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var window = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(window, new object[] { index, null });
        }
    }
}
