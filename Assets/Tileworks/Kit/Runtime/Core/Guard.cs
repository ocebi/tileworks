using System;
using System.Runtime.CompilerServices;

namespace Tileworks.Kit.Core
{
    public static class Guard
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T NotNull<T>(T value, string name) where T : class
        {
            if (value == null) throw new ArgumentNullException(name);
            return value;
        }

        public static string NotEmpty(string value, string name)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("Value must not be empty.", name);
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InRange(int value, int minInclusive, int maxExclusive, string name)
        {
            if (value < minInclusive || value >= maxExclusive)
                throw new ArgumentOutOfRangeException(name, value, $"Expected [{minInclusive}, {maxExclusive}).");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void That(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
