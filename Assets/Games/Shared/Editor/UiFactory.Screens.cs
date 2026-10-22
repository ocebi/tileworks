using Tileworks.Kit.App;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworlds.Games.Editor
{
    public static partial class UiFactory
    {
        // Full-screen cover with its own camera, shown while the app boots or changes scene.
        public static LoadingScreen LoadingScreen(string title, Color background, Sprite backdrop = null, Color? titleOutline = null)
        {
            var root = new GameObject("LoadingScreen");
            var camera = SceneParts.Camera("LoadingCamera", background);
            camera.transform.SetParent(root.transform, false);
            camera.depth = -100;
            camera.cullingMask = 0;
            var canvas = Canvas("LoadingCanvas", 1000);
            canvas.transform.SetParent(root.transform, false);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            if (backdrop != null) Cover(Stretch("Background", canvas.transform), backdrop).raycastTarget = true;
            else Image(Stretch("Background", canvas.transform), null, background, raycast: true);
            HomeTitle(canvas.transform, title, titleOutline);
            Text("Loading", canvas.transform, "Loading", 56, new Color(1f, 1f, 1f, 0.7f), new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), Vector2.zero,
                new Vector2(800, 120));
            var bar = Rect("Progress", canvas.transform, new Vector2(0.15f, 0.4f), new Vector2(0.85f, 0.4f), Vector2.zero, new Vector2(0f, 14f));
            bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = new Vector2(0f, 0f);
            Image(bar, null, new Color(1f, 1f, 1f, 0.85f));
            bar.gameObject.SetActive(false);
            var screen = root.AddComponent<LoadingScreen>();
            SceneParts.Assign(screen, "_group", group);
            SceneParts.Assign(screen, "_camera", camera);
            SceneParts.Assign(screen, "_bar", bar);
            return screen;
        }

        // A painted full-screen backdrop behind everything the camera draws. It has its own canvas that never changes, so it
        // is batched once and never rebuilt.
        public static Canvas Backdrop(Camera camera, Sprite sprite)
        {
            var go = new GameObject("Backdrop", typeof(RectTransform), typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 20f;
            canvas.sortingOrder = -100;
            Cover(Stretch("Image", go.transform), sprite);
            return canvas;
        }

        // A full-screen picture; TextureImportRules sets its sizes and formats from its Backgrounds folder.
        public static Sprite BackdropSprite(string path) => Sprite(path);

        // Fills the parent with the sprite, cropping instead of stretching on other aspect ratios.
        private static Image Cover(RectTransform rect, Sprite sprite)
        {
            var image = Image(rect, sprite, Color.white);
            var fitter = rect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            return image;
        }

        // Shared by Home and the loading screen, so the title stays put while one fades into the other.
        public static TMP_Text HomeTitle(Transform parent, string title, Color? outline = null)
        {
            var text = Text("Title", parent, title, 160, Color.white, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1000, 260));
            Outline(text, outline ?? PillOutline, 0.25f);
            text.transform.localRotation = Quaternion.Euler(0f, 0f, -3f);
            return text;
        }
    }
}
