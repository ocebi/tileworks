using System;
using Tileworks.Kit.App;
using Tileworks.Kit.Time;
using UnityEditor;
using UnityEngine;

namespace Tileworks.Meta.Editor
{
    // Moves only the running app's clock, for anything timed such as lives regeneration.
    public static class TimeTravelMenu
    {
        private const string Travel = "Tileworks/Time Travel/";

        [MenuItem(Travel + "+1 Minute")] private static void Minute() => Shift(TimeSpan.FromMinutes(1));
        [MenuItem(Travel + "+10 Minutes")] private static void TenMinutes() => Shift(TimeSpan.FromMinutes(10));
        [MenuItem(Travel + "+1 Hour")] private static void Hour() => Shift(TimeSpan.FromHours(1));
        [MenuItem(Travel + "+1 Day")] private static void Day() => Shift(TimeSpan.FromDays(1));

        [MenuItem(Travel + "Reset")]
        private static void Reset()
        {
            var clock = Resolve<TrustedClock>();
            clock.DebugOffset = TimeSpan.Zero;
            Debug.Log($"[Tileworks] Clock back to device time {clock.UtcNow:u}");
        }

        [MenuItem(Travel + "+1 Minute", true), MenuItem(Travel + "+10 Minutes", true), MenuItem(Travel + "+1 Hour", true), MenuItem(Travel + "+1 Day", true),
         MenuItem(Travel + "Reset", true)]
        private static bool CanTravel() => EditorApplication.isPlaying && App() != null;

        private static void Shift(TimeSpan by)
        {
            var clock = Resolve<TrustedClock>();
            clock.DebugOffset += by;
            Debug.Log($"[Tileworks] Clock now {clock.UtcNow:u} (offset {clock.DebugOffset})");
        }

        private static AppRoot App() => UnityEngine.Object.FindAnyObjectByType<AppRoot>();

        private static T Resolve<T>() => App().Scope.Resolve<T>();
    }
}
