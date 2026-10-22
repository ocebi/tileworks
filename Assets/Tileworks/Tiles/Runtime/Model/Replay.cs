using System;
using System.IO;

namespace Tileworks.Tiles.Model
{
    public sealed class Replay
    {
        public const int FormatVersion = 1;

        public string LevelId = string.Empty;
        public string ContentHash = string.Empty;
        public int RulesVersion;
        public ulong Seed;
        public ActionRecord[] Actions = Array.Empty<ActionRecord>();
        public ulong FinalHash;

        public ulong Play<TState>(RuleEngine<TState> engine) where TState : class, IRuleState<TState>
        {
            foreach (var record in Actions)
            {
                var result = engine.ApplyRecord(record);
                if (!result.Accepted) throw new InvalidOperationException($"Replay of {LevelId} rejected {record}: {result.Rejected}");
            }

            return engine.State.ComputeHash();
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream(64 + Actions.Length * 10);
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)FormatVersion);
            writer.Write(LevelId);
            writer.Write(ContentHash);
            writer.Write(RulesVersion);
            writer.Write(Seed);
            writer.Write(FinalHash);
            writer.Write(Actions.Length);
            foreach (var action in Actions)
            {
                writer.Write(action.Type);
                writer.Write(action.A);
                writer.Write(action.B);
            }

            return stream.ToArray();
        }

        public static Replay FromBytes(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes));
            var version = reader.ReadByte();
            if (version != FormatVersion) throw new InvalidDataException($"Unknown replay format {version}.");
            var replay = new Replay
            {
                LevelId = reader.ReadString(),
                ContentHash = reader.ReadString(),
                RulesVersion = reader.ReadInt32(),
                Seed = reader.ReadUInt64(),
                FinalHash = reader.ReadUInt64(),
            };
            replay.Actions = new ActionRecord[reader.ReadInt32()];
            for (var i = 0; i < replay.Actions.Length; i++)
                replay.Actions[i] = new ActionRecord(reader.ReadUInt16(), reader.ReadInt32(), reader.ReadInt32());
            return replay;
        }
    }

    // Pre-allocated state copies for tooling undo (debug rewind, editor step-back, solver).
    public sealed class SnapshotRing<TState> where TState : class, IRuleState<TState>
    {
        private readonly TState[] _states;
        private int _start;

        // Owns the given scratch states; capacity is their count.
        public SnapshotRing(TState[] states) => _states = states;

        public int Count { get; private set; }

        public void Push(TState source)
        {
            var index = (_start + Count) % _states.Length;
            source.CopyTo(_states[index]);
            if (Count < _states.Length) Count++;
            else _start = (_start + 1) % _states.Length;
        }

        public bool TryPop(TState target)
        {
            if (Count == 0) return false;
            Count--;
            _states[(_start + Count) % _states.Length].CopyTo(target);
            return true;
        }

        public void Clear()
        {
            _start = 0;
            Count = 0;
        }
    }
}
