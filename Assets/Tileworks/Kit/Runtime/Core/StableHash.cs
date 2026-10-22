using System;
using System.Runtime.CompilerServices;

namespace Tileworks.Kit.Core
{
    // Same result on every platform, unlike string.GetHashCode.
    public static class StableHash
    {
        private const uint Prime1 = 2654435761U;
        private const uint Prime2 = 2246822519U;
        private const uint Prime3 = 3266489917U;
        private const uint Prime4 = 668265263U;
        private const uint Prime5 = 374761393U;

        public static uint Hash32(string text, uint seed = 0)
        {
            if (text == null) return seed;
            var h = seed + Prime5 + (uint)(text.Length * 2);
            for (var i = 0; i < text.Length; i++)
            {
                h += text[i] * Prime5;
                h = RotateLeft(h, 11) * Prime1;
            }

            return Avalanche(h);
        }

        public static uint Hash32(ReadOnlySpan<byte> data, uint seed = 0)
        {
            var h = seed + Prime5 + (uint)data.Length;
            var i = 0;
            for (; i + 4 <= data.Length; i += 4)
            {
                var k = (uint)(data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24));
                h += k * Prime3;
                h = RotateLeft(h, 17) * Prime4;
            }

            for (; i < data.Length; i++)
            {
                h += data[i] * Prime5;
                h = RotateLeft(h, 11) * Prime1;
            }

            return Avalanche(h);
        }

        public static ulong Hash64(string text)
        {
            var h = 14695981039346656037UL;
            if (text == null) return h;
            for (var i = 0; i < text.Length; i++)
            {
                h ^= text[i];
                h *= 1099511628211UL;
            }

            return Mix64(h);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Mix64(ulong x)
        {
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Combine(ulong hash, ulong value) => Mix64(hash ^ (value + 0x9E3779B97F4A7C15UL + (hash << 6) + (hash >> 2)));

        public static ulong Combine(ulong hash, ReadOnlySpan<int> values)
        {
            for (var i = 0; i < values.Length; i++) hash = Combine(hash, (ulong)(uint)values[i]);
            return hash;
        }

        public static string ToHex(ulong value) => value.ToString("x16");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft(uint value, int count) => (value << count) | (value >> (32 - count));

        private static uint Avalanche(uint h)
        {
            h ^= h >> 15;
            h *= Prime2;
            h ^= h >> 13;
            h *= Prime3;
            h ^= h >> 16;
            return h;
        }
    }
}
