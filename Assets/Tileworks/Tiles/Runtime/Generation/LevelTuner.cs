using System;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Generation
{
    public struct TuningKnobs
    {
        public int Moves;
        public int Colours;

        // Scales the static blockers (boxes, chains).
        public float Load;

        // Scales the goals that aren't static blockers (a colour, balloons, floors, ducks).
        public float Goal;
    }

    public sealed class TuningLimits
    {
        public int MinMoves;
        public int MaxMoves = 40;
        public int MinColours = 4;
        public int MaxColours = 5;
        public float MinLoad = 0.4f;
        public float MaxLoad = 1.6f;
        public float MinGoal = 0.5f;

        // 1 when the level has no goal that scales.
        public float MaxGoal = 3f;

        // The casual bot's win rate the level must still reach when it is tuned on the skilled bot.
        public float MinCasualRate;
        public int MaxAttempts = 30;
    }

    public sealed class TunedLevel<TLevel>
    {
        public TLevel Level;
        public ProbeStats Stats;

        // The casual bot on the final level; the same as Stats when the level was tuned on the casual bot.
        public ProbeStats Casual;
        public TuningKnobs Knobs;
        public int Attempts;
        public bool HitBand;
    }

    // The game's side of tuning: a level for a set of knobs, and bot playouts on it.
    public interface ITunable<TLevel>
    {
        TLevel Build(in TuningKnobs knobs);
        ProbeStats Probe(TLevel level);
        ProbeStats Casual(TLevel level, in ProbeStats tuned);

        // Static blockers on the board, to tell when more load adds none.
        int Blockers(TLevel level);
    }

    // Searches the move limit until the bot's win rate lands on the slot, with few moves to spare on wins and few
    // dead-board shuffles. A level that wins with moves to spare gets a bigger goal, then fewer moves. When moves alone
    // can't get there, a level that is too easy gets a bigger goal, then more blockers, then another colour; one that is
    // too hard gets those back in reverse. Dead boards take blockers, then colours, away for good.
    public static class LevelTuner
    {
        // Win rate gained per extra move, near the target; only steers the search.
        private const float RatePerMove = 0.04f;

        public static TunedLevel<TLevel> Run<TLevel>(ITunable<TLevel> game, LevelSlot slot, TuningKnobs start, TuningLimits limits)
        {
            Guard.NotNull(game, nameof(game));
            var knobs = start;
            knobs.Moves = Math.Clamp(start.Moves, limits.MinMoves, limits.MaxMoves);
            int low = limits.MinMoves, high = limits.MaxMoves, maxColours = limits.MaxColours, blockersBefore = -1;
            var maxLoad = limits.MaxLoad;
            var spare = LevelCurve.MaxMovesLeftOnWin(slot);
            var shuffles = LevelCurve.MaxShufflesPerGame(slot.Role);
            TunedLevel<TLevel> best = null;
            var bestMiss = float.MaxValue;

            bool Harden()
            {
                if (knobs.Goal < limits.MaxGoal)
                {
                    knobs.Goal = Math.Min(limits.MaxGoal, knobs.Goal * 1.3f);
                    return true;
                }

                if (knobs.Load < maxLoad)
                {
                    knobs.Load = Math.Min(maxLoad, knobs.Load * 1.2f);
                    return true;
                }

                if (knobs.Colours >= maxColours) return false;
                knobs.Colours++;
                return true;
            }

            bool Soften()
            {
                if (knobs.Colours > start.Colours) knobs.Colours--;
                else if (knobs.Load > limits.MinLoad) knobs.Load = Math.Max(limits.MinLoad, knobs.Load * 0.8f);
                else if (knobs.Goal > limits.MinGoal) knobs.Goal = Math.Max(limits.MinGoal, knobs.Goal * 0.8f);
                else if (knobs.Colours > limits.MinColours) knobs.Colours--;
                else return false;
                return true;
            }

            // A cramped board: fewer blockers, then fewer colours, and neither comes back.
            bool Relieve()
            {
                if (knobs.Load > limits.MinLoad) knobs.Load = maxLoad = Math.Max(limits.MinLoad, knobs.Load * 0.8f);
                else if (knobs.Colours > limits.MinColours) knobs.Colours = maxColours = knobs.Colours - 1;
                else return false;
                return true;
            }

            for (var attempt = 1; attempt <= limits.MaxAttempts; attempt++)
            {
                var level = game.Build(knobs);
                var blockers = game.Blockers(level);
                if (blockersBefore >= 0 && blockers <= blockersBefore) maxLoad = knobs.Load;
                blockersBefore = -1;
                var stats = game.Probe(level);
                var win = stats.WinRate;
                var loose = stats.Wins > 0 && stats.MovesLeftOnWin > spare;
                var dead = stats.ShufflesPerGame > shuffles;
                var result = new TunedLevel<TLevel> { Level = level, Stats = stats, Knobs = knobs, Attempts = attempt, HitBand = slot.Accepts(win) && !loose && !dead };
                if (result.HitBand)
                {
                    result.Casual = game.Casual(level, stats);
                    if (LevelCurve.PassesCasual(limits.MinCasualRate, result.Casual.Runs, result.Casual.Wins)) return result;

                    // On target, but out of a casual player's reach: steer it as a level that is too hard.
                    result.HitBand = false;
                    (win, loose, dead) = (slot.Low - 0.01f, false, false);
                }

                // The band comes first: any level inside it beats one outside, however its moves to spare and shuffles compare.
                var miss = (slot.Accepts(win) ? 0f : 1f + slot.Miss(win)) + (loose ? stats.MovesLeftOnWin - spare : 0f) + (dead ? stats.ShufflesPerGame - shuffles : 0f);
                if (best == null || miss < bestMiss) (best, bestMiss) = (result, miss);

                var loadBefore = knobs.Load;
                bool Reset(bool changed)
                {
                    if (!changed) return false;
                    (low, high) = (limits.MinMoves, limits.MaxMoves);
                    if (knobs.Load > loadBefore) blockersBefore = blockers;
                    return true;
                }

                if (dead && Reset(Relieve())) continue;

                // In the band with moves to spare: a bigger goal first, then fewer moves, as for a level that is too easy.
                var inBand = slot.Accepts(win);
                if (inBand && !loose) break;
                if (inBand && knobs.Goal < limits.MaxGoal && Reset(Harden())) continue;
                var tooEasy = win > slot.High || inBand;
                if (tooEasy) high = knobs.Moves - 1;
                else low = knobs.Moves + 1;
                if (low > high)
                {
                    if (Reset(tooEasy ? Harden() : Soften())) continue;
                    break;
                }

                var step = (int)Math.Round(Math.Clamp((slot.WinRate - win) / RatePerMove, -8f, 8f));
                knobs.Moves = Math.Clamp(knobs.Moves + (tooEasy ? Math.Min(step, -1) : Math.Max(step, 1)), low, high);
            }

            if (best.Casual.Runs == 0) best.Casual = game.Casual(best.Level, best.Stats);
            return best;
        }
    }
}
