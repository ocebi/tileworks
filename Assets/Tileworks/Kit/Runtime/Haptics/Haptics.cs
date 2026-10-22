using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Tileworks.Kit.Haptics
{
    public enum HapticKind : byte
    {
        Selection,
        Light,
        Medium,
        Success,
        Warning,
        Failure,
    }

    public interface IHaptics
    {
        bool Enabled { get; set; }
        void Play(HapticKind kind);
    }

    // Rate-limited to one pulse per 40 ms. Android uses predefined effects (API 29+) or one-shots (API 26+); iOS uses the
    // Taptic Engine's feedback generators (Plugins/iOS/TileworksHaptics.mm).
    public sealed class DeviceHaptics : IHaptics
    {
        private const float MinInterval = 0.04f;
        private float _last = -1f;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private int _sdk;
#endif

        public bool Enabled { get; set; } = true;

        public void Play(HapticKind kind)
        {
            if (!Enabled || Time.unscaledTime - _last < MinInterval) return;
            _last = Time.unscaledTime;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Vibrate(kind);
            }
            catch (Exception)
            {
                Enabled = false;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            TileworksHapticsPlay((int)kind);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void TileworksHapticsPlay(int kind);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private void Vibrate(HapticKind kind)
        {
            if (_vibrator == null)
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                _sdk = version.GetStatic<int>("SDK_INT");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }

            using var effects = new AndroidJavaClass("android.os.VibrationEffect");
            AndroidJavaObject effect;
            if (_sdk >= 29)
            {
                var id = kind switch { HapticKind.Selection => 2, HapticKind.Light => 0, HapticKind.Success => 1, _ => 5 };
                effect = effects.CallStatic<AndroidJavaObject>("createPredefined", id);
            }
            else
            {
                var (ms, amplitude) = kind switch { HapticKind.Selection => (10L, 60), HapticKind.Light => (15L, 90), HapticKind.Failure => (60L, 255), _ => (30L, 180) };
                effect = effects.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
            }

            _vibrator.Call("vibrate", effect);
            effect.Dispose();
        }

        // Never called: makes the build add the VIBRATE permission.
        private static void RequestPermission() => Handheld.Vibrate();
#endif
    }
}
