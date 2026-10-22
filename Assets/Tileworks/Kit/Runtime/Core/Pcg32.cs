using System;

namespace Tileworks.Kit.Core
{
    // PCG-XSH-RR 64/32. A value type, so copying it snapshots the sequence.
    public struct Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private ulong _increment;

        public Pcg32(ulong seed, ulong sequence = 0xDA3E39CB94B95BDBUL)
        {
            _state = 0UL;
            _increment = (sequence << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public ulong State => _state;

        public uint NextUInt()
        {
            var old = _state;
            _state = old * Multiplier + _increment;
            var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            var rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        // Lemire's method: no modulo bias.
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            var range = (uint)maxExclusive;
            var m = (ulong)NextUInt() * range;
            var low = (uint)m;
            if (low < range)
            {
                var threshold = (0u - range) % range;
                while (low < threshold)
                {
                    m = (ulong)NextUInt() * range;
                    low = (uint)m;
                }
            }

            return (int)(m >> 32);
        }

        public int NextInt(int minInclusive, int maxExclusive) => minInclusive + NextInt(maxExclusive - minInclusive);

        public double NextDouble() => NextUInt() * (1.0 / 4294967296.0);

        public bool Chance(int numerator, int denominator) => NextInt(denominator) < numerator;

        public void Shuffle<T>(Span<T> items)
        {
            for (var i = items.Length - 1; i > 0; i--)
            {
                var j = NextInt(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
