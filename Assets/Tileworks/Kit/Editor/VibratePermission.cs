#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace Tileworks.Kit.Editor
{
    // DeviceHaptics calls the Android vibrator directly, so Unity doesn't add this permission by itself.
    public sealed class VibratePermission : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";
        private const string Permission = "android.permission.VIBRATE";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var path = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            var doc = new XmlDocument();
            doc.Load(path);
            var manifest = doc.DocumentElement;
            foreach (XmlElement existing in manifest.GetElementsByTagName("uses-permission"))
            {
                if (existing.GetAttribute("name", AndroidNs) == Permission) return;
            }

            var element = doc.CreateElement("uses-permission");
            element.SetAttribute("name", AndroidNs, Permission);
            manifest.PrependChild(element);
            doc.Save(path);
        }
    }
}
#endif
