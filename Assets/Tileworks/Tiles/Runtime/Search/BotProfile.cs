namespace Tileworks.Tiles.Search
{
    // How a tuning bot plays. Casual: the best-looking move three times in four, a random one otherwise. Skilled: a
    // random move one time in twenty, otherwise a look one move ahead that values specials, setups and goal progress.
    public readonly struct BotProfile
    {
        public const int MaxBreadth = 6;

        public static readonly BotProfile Casual = new BotProfile("casual", 25, 0, 0);
        public static readonly BotProfile Skilled = new BotProfile("skilled", 5, MaxBreadth, 50);

        public readonly string Name;
        public readonly int EpsilonPercent;

        // Moves looked at one move ahead; 0 plays one ply on the domain's own score.
        public readonly int Breadth;

        // Share of the best follow-up move's score added to a candidate's value.
        public readonly int FollowUpPercent;

        public BotProfile(string name, int epsilonPercent, int breadth, int followUpPercent)
        {
            Name = name;
            EpsilonPercent = epsilonPercent;
            Breadth = breadth;
            FollowUpPercent = followUpPercent;
        }

        public bool LooksAhead => Breadth > 0;

        // A new policy per thread: the lookahead keeps scratch state.
        public IPlayoutPolicy<TState, TMove> CreatePolicy<TState, TMove>(IPositionEvaluator<TState, TMove> evaluator) where TMove : struct =>
            LooksAhead
                ? new LookaheadPolicy<TState, TMove>(evaluator, EpsilonPercent, Breadth, FollowUpPercent)
                : new EpsilonGreedyPolicy<TState, TMove>(EpsilonPercent);

        public override string ToString() => Name;
    }
}
