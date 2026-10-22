using System;

namespace Tileworks.Kit.Audio
{
    public readonly struct VoiceRequest
    {
        public VoiceRequest(int cue, int priority, int maxVoices, double minGap, double start, double length)
        {
            Cue = cue;
            Priority = priority;
            MaxVoices = Math.Max(1, maxVoices);
            MinGap = minGap;
            Start = start;
            Length = length;
        }

        public int Cue { get; }
        public int Priority { get; }
        public int MaxVoices { get; }
        public double MinGap { get; }
        public double Start { get; }
        public double Length { get; }
    }

    // Where a sound goes: `Voice` (a new one when `IsNew`), and the voice to stop at `CutAt`, if any (`Cut` is -1 when none).
    public readonly struct VoicePlan
    {
        public VoicePlan(int voice, bool isNew, int cut, double cutAt)
        {
            Voice = voice;
            IsNew = isNew;
            Cut = cut;
            CutAt = cutAt;
        }

        public int Voice { get; }
        public bool IsNew { get; }
        public int Cut { get; }
        public double CutAt { get; }
    }

    // Hands out voices from what each one is playing or has scheduled; all times are on one clock.
    // A voice is free once its sound ends. A cue over its cap cuts its own oldest copy when the new one starts.
    // With every voice busy, a sound may take over one that has started and is the same cue or a lower priority;
    // a sound still waiting to start is never taken over by another cue.
    public sealed class VoiceTable
    {
        private readonly int[] _cue;
        private readonly int[] _priority;
        private readonly double[] _start;
        private readonly double[] _end;

        public VoiceTable(int capacity)
        {
            _cue = new int[capacity];
            _priority = new int[capacity];
            _start = new double[capacity];
            _end = new double[capacity];
        }

        public int Count { get; private set; }
        public int Capacity => _cue.Length;

        public int CueOf(int voice) => _cue[voice];
        public double StartOf(int voice) => _start[voice];
        public double EndOf(int voice) => _end[voice];

        public bool TryAssign(in VoiceRequest request, double now, out VoicePlan plan)
        {
            plan = default;
            var oldest = -1;
            var sounding = 0;
            for (var v = 0; v < Count; v++)
            {
                if (_cue[v] != request.Cue || _end[v] <= now) continue;
                if (Math.Abs(_start[v] - request.Start) < request.MinGap) return false;
                if (_start[v] > request.Start || request.Start >= _end[v]) continue;
                sounding++;
                if (oldest < 0 || _start[v] < _start[oldest]) oldest = v;
            }

            var cut = sounding >= request.MaxVoices ? oldest : -1;
            var voice = Free(now, cut, request.Start);
            var isNew = voice < 0 && Count < Capacity;
            if (isNew) voice = Count;
            else if (voice < 0) voice = Steal(request, now);
            if (voice < 0) return false;

            if (isNew) Count++;
            if (cut == voice) cut = -1;
            if (cut >= 0) _end[cut] = request.Start;
            _cue[voice] = request.Cue;
            _priority[voice] = request.Priority;
            _start[voice] = request.Start;
            _end[voice] = request.Start + request.Length;
            plan = new VoicePlan(voice, isNew, cut, request.Start);
            return true;
        }

        // A voice whose sound has ended, or the capped copy when the new sound starts right away.
        private int Free(double now, int cut, double start)
        {
            for (var v = 0; v < Count; v++)
            {
                if (_end[v] <= now || (v == cut && start <= now)) return v;
            }

            return -1;
        }

        private int Steal(in VoiceRequest request, double now)
        {
            var best = -1;
            for (var v = 0; v < Count; v++)
            {
                if (_start[v] > now) continue;
                if (_cue[v] != request.Cue && _priority[v] >= request.Priority) continue;
                if (best < 0 || _start[v] < _start[best]) best = v;
            }

            return best;
        }
    }
}
