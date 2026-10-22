using Tileworks.Kit.Core;

namespace TileMatch.Api
{
    public interface ILevelProgress
    {
        // Zero-based index of the next level to play.
        IReadOnlyObservable<int> CurrentIndex { get; }
    }

    public sealed class LevelStartArgs
    {
        public LevelStartArgs(int levelIndex, string levelId = null)
        {
            LevelIndex = levelIndex;
            LevelId = levelId;
        }

        public int LevelIndex { get; }

        // Plays this level instead of the sequence entry (editor scenarios, debug).
        public string LevelId { get; }
    }

    public readonly struct LevelCompleted
    {
        public readonly string LevelId;
        public readonly int LevelIndex;
        public readonly bool Won;
        public readonly int Moves;

        public LevelCompleted(string levelId, int levelIndex, bool won, int moves)
        {
            LevelId = levelId;
            LevelIndex = levelIndex;
            Won = won;
            Moves = moves;
        }
    }
}
