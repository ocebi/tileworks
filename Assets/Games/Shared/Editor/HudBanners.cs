using TMPro;
using UnityEngine;

namespace Tileworlds.Games.Editor
{
    // Ribbons that sweep across the board between moments, such as the bonus round after a win.
    public static class HudBanners
    {
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        // "Bonus time!" over a dark band, with a line saying the leftover moves turn into rockets.
        public static RectTransform Bonus(Transform overlay, Color band)
        {
            var root = UiFactory.Rect("Bonus", overlay, Centre, Centre, new Vector2(0, 205), new Vector2(1080, 300));
            UiFactory.Image(UiFactory.Stretch("Band", root), null, band);
            var ribbon = UiFactory.Rect("Ribbon", root, Centre, Centre, new Vector2(0, 30), new Vector2(760, 200));
            ribbon.localRotation = Quaternion.Euler(0f, 0f, -3f);
            UiFactory.Sliced(ribbon, "ui_ribbon");
            var title = UiFactory.Lettered("Label", ribbon, "Bonus time!", 94, UiFactory.RibbonOutline, Vector2.zero, Vector2.one, new Vector2(0, 8), new Vector2(-80, 0));
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Lettered("Line", root, "Leftover moves become rockets", 50, UiFactory.PillOutline, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 46), new Vector2(1000, 70));
            root.gameObject.SetActive(false);
            return root;
        }
    }
}
