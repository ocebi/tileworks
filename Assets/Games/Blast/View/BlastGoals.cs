using Blast.Model;

namespace Blast.View
{
    public static class BlastGoals
    {
        // The piece whose sprite stands for a goal.
        public static Piece PieceOf(GoalKind kind) => kind switch
        {
            GoalKind.Box => Piece.Box,
            GoalKind.Balloon => Piece.Balloon,
            GoalKind.Duck => Piece.Duck,
            _ => Piece.Cube,
        };
    }
}
