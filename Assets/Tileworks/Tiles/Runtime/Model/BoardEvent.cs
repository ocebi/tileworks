using System;
using System.Collections.Generic;

namespace Tileworks.Tiles.Model
{
    // One compact shape for every rules event; games expose typed views over it (ADR-0006).
    public readonly struct BoardEvent : IEquatable<BoardEvent>
    {
        public readonly ushort Type;
        public readonly ushort Step;
        public readonly int Entity;
        public readonly int A;
        public readonly int B;
        public readonly int C;

        public BoardEvent(ushort type, ushort step, int entity, int a, int b, int c)
        {
            Type = type;
            Step = step;
            Entity = entity;
            A = a;
            B = b;
            C = c;
        }

        public bool Equals(BoardEvent o) => Type == o.Type && Step == o.Step && Entity == o.Entity && A == o.A && B == o.B && C == o.C;
        public override bool Equals(object obj) => obj is BoardEvent other && Equals(other);
        public override int GetHashCode() => (((Type * 397) ^ Entity) * 397 ^ A) * 397 ^ B ^ (C << 7) ^ (Step << 13);
        public override string ToString() => $"#{Type}@{Step}({Entity},{A},{B},{C})";
    }

    [Flags]
    public enum EntityFields : byte
    {
        None = 0,
        Entity = 1,
        A = 2,
        B = 4,
        C = 8,
    }

    public readonly struct EventTypeInfo
    {
        public readonly ushort Id;
        public readonly string Name;
        public readonly EntityFields Entities;
        public readonly bool Barrier;

        // A preempting event takes over its lanes from an earlier event of a preempting type instead of queueing behind it.
        public readonly bool Preempts;

        public EventTypeInfo(ushort id, string name, EntityFields entities, bool barrier, bool preempts)
        {
            Id = id;
            Name = name;
            Entities = entities;
            Barrier = barrier;
            Preempts = preempts;
        }
    }

    public sealed class EventTypeRegistry
    {
        private readonly List<EventTypeInfo> _types = new List<EventTypeInfo>(32);
        private readonly Dictionary<string, ushort> _byName = new Dictionary<string, ushort>();

        public int Count => _types.Count;

        // Which fields hold entity ids tells the event player which lanes an event occupies.
        public ushort Register(string name, EntityFields entities, bool barrier = false, bool preempts = false)
        {
            if (_byName.ContainsKey(name)) throw new ArgumentException($"Event type '{name}' already registered.");
            if (preempts && barrier) throw new ArgumentException($"Event type '{name}' can't both preempt and be a barrier.");
            var id = (ushort)_types.Count;
            _types.Add(new EventTypeInfo(id, name, entities, barrier, preempts));
            _byName.Add(name, id);
            return id;
        }

        public EventTypeInfo Get(ushort type) => _types[type];
        public bool TryFind(string name, out ushort type) => _byName.TryGetValue(name, out type);
        public string NameOf(ushort type) => type < _types.Count ? _types[type].Name : $"#{type}";
    }

    public readonly struct EventRange
    {
        public readonly int Start;
        public readonly int Count;

        public EventRange(int start, int count)
        {
            Start = start;
            Count = count;
        }
    }

    // Pre-sized and reused per action; grows only during warm-up.
    public sealed class EventBuffer
    {
        private BoardEvent[] _items;

        public EventBuffer(int capacity = 256) => _items = new BoardEvent[capacity];

        public int Count { get; private set; }
        public ref readonly BoardEvent this[int index] => ref _items[index];
        public ReadOnlySpan<BoardEvent> AsSpan() => new ReadOnlySpan<BoardEvent>(_items, 0, Count);
        public ReadOnlySpan<BoardEvent> Slice(EventRange range) => new ReadOnlySpan<BoardEvent>(_items, range.Start, range.Count);

        public void Add(in BoardEvent e)
        {
            if (Count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
            _items[Count++] = e;
        }

        public void Clear() => Count = 0;
    }

    public sealed class EventWriter
    {
        private readonly EventBuffer _buffer;

        public EventWriter(EventBuffer buffer) => _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

        public ushort Step { get; set; }
        public int Count => _buffer.Count;

        public void Emit(ushort type, int entity, int a = 0, int b = 0, int c = 0) => _buffer.Add(new BoardEvent(type, Step, entity, a, b, c));
        public void Emit(in BoardEvent e) => _buffer.Add(e);
    }
}
