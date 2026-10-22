using UnityEngine;

namespace Tileworks.Tiles.View
{
    public static class CameraFitter
    {
        // Fits `content` into the screen area left after reserving top/bottom fractions (HUD, tray), inside the safe area.
        public static void Fit(Camera camera, Rect content, float topReserved, float bottomReserved, float padding, Vector2 contentCentreOffset = default)
        {
            var safe = Screen.safeArea;
            var screenH = Mathf.Max(1f, camera.pixelHeight);
            var screenW = Mathf.Max(1f, camera.pixelWidth);
            var usableH = safe.height / screenH - topReserved - bottomReserved;
            var usableW = safe.width / screenW;

            var aspect = camera.aspect;
            var sizeForHeight = (content.height + padding * 2f) * 0.5f / Mathf.Max(0.1f, usableH);
            var sizeForWidth = (content.width + padding * 2f) * 0.5f / (aspect * Mathf.Max(0.1f, usableW));
            camera.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);

            // Shift the camera so the content sits in the middle of the usable band, which the HUD and tray bound from the safe edges.
            var viewHeight = camera.orthographicSize * 2f;
            var bandCentre = (safe.yMin / screenH + bottomReserved + safe.yMax / screenH - topReserved) * 0.5f;
            var offsetY = (0.5f - bandCentre) * viewHeight;
            var p = camera.transform.position;
            camera.transform.position = new Vector3(content.center.x + contentCentreOffset.x, content.center.y + offsetY + contentCentreOffset.y, p.z);
        }
    }
}
