#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Tileworks.Kit.Content
{
    // Where remote bundles live. The Remote profile's load path names this property, so the content system reads it at
    // runtime; on the web the page sets window.tileworksContentUrl next to the build folder.
    public static class ContentUrl
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string TileworksContentUrl();

        public static string Remote => TileworksContentUrl() ?? string.Empty;
#else
        public static string Remote => string.Empty;
#endif
    }
}
