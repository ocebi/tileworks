using System.Text;

namespace TileMatch.Service
{
    // The stats screen reads straight from the attempt history in SQLite.
    public sealed class StatsService
    {
        private readonly LevelAttemptDao _attempts;

        public StatsService(LevelAttemptDao attempts) => _attempts = attempts;

        public string Summary(int recent = 8)
        {
            var rows = _attempts.Recent(500);
            var wins = 0;
            var streak = 0;
            var best = 0;
            var current = 0;
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                if (rows[i].Outcome == "won")
                {
                    wins++;
                    current++;
                    best = current > best ? current : best;
                }
                else
                {
                    current = 0;
                }
            }

            streak = current;
            var text = new StringBuilder()
                .AppendLine($"Attempts: {rows.Count}")
                .AppendLine($"Wins: {wins} ({(rows.Count == 0 ? 0 : wins * 100 / rows.Count)}%)")
                .AppendLine($"Win streak: {streak}   Best: {best}")
                .AppendLine();
            for (var i = 0; i < rows.Count && i < recent; i++)
            {
                var row = rows[i];
                text.AppendLine($"Level {row.LevelIndex + 1}: {row.Outcome}, {row.Moves} moves, {row.DurationMs / 1000}s");
            }

            return text.ToString();
        }
    }
}
