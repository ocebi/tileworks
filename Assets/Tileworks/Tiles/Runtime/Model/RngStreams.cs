using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Model
{
    // Independent named streams from one seed: adding a draw to one system never shifts another's sequence.
    public sealed class RngStreams
    {
        private readonly List<string> _names = new List<string>(4);
        private Pcg32[] _streams = new Pcg32[4];

        public RngStreams(ulong seed) => Seed = seed;

        public ulong Seed { get; private set; }
        public int Count => _names.Count;

        public int Register(string name)
        {
            var existing = _names.IndexOf(name);
            if (existing >= 0) return existing;
            if (_names.Count == _streams.Length) Array.Resize(ref _streams, _streams.Length * 2);
            _streams[_names.Count] = Create(Seed, name);
            _names.Add(name);
            return _names.Count - 1;
        }

        public ref Pcg32 Get(int stream) => ref _streams[stream];

        public void Reseed(ulong seed)
        {
            Seed = seed;
            for (var i = 0; i < _names.Count; i++) _streams[i] = Create(seed, _names[i]);
        }

        public void CopyTo(RngStreams target)
        {
            if (target._names.Count != _names.Count) throw new InvalidOperationException("Streams differ.");
            target.Seed = Seed;
            Array.Copy(_streams, target._streams, _names.Count);
        }

        public ulong ComputeHash()
        {
            var hash = Seed;
            for (var i = 0; i < _names.Count; i++) hash = StableHash.Combine(hash, _streams[i].State);
            return hash;
        }

        private static Pcg32 Create(ulong seed, string name) => new Pcg32(seed, StableHash.Hash32(name));
    }
}
