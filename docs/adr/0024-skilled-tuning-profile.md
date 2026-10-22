# ADR 0024: Blast and Gem Swap are tuned on a skilled bot

Status: accepted

## Context
Blast and Gem Swap levels were tuned on the casual bot (a random move one time in four, otherwise the best-looking move
one ply deep). That bot fires specials as soon as it has them but doesn't build towards them, save them for combos or
plan around goals, so a player who does all three won about 90% of normal levels and 63% of hard ones.

## Decision
- `Tileworks.Tiles.Search` gets bot profiles. Casual is unchanged. Skilled plays a random move one time in twenty;
  otherwise `LookaheadPolicy` plays the six best moves (by the game's `IPositionEvaluator.Score`) on a scratch state
  and keeps the one with the highest score + position value + half the best follow-up move's score. Choices are
  deterministic for a seed.
- Each game's evaluator (`BlastEvaluator`, `Match3Evaluator`) scores special creation by size and shape, combos,
  specials and combo setups on the board, groups or swaps one move from a special, goal progress weighted by how far it
  lags the moves left, blockers within reach, and ducks (depth, and whether the piece under one can go next).
- `LevelCurve` keeps the casual table for Block Puzzle and Tile Match and adds a skilled table with the same roles:
  easy 0.97, normal 0.82 → 0.70 over the chapters, hard 0.45 → 0.38, super hard 0.22, with skilled floors of 0.9, 0.6,
  0.3 and 0.15.
- Blast and Gem Swap tune the move limit, blockers and colours on the skilled bot, then measure the casual bot on the
  final level. The casual bot must still win levels 1-4 at least 80% of the time and other easy, normal and hard levels
  at least 2% of the time. Super hard levels answer to the skilled band only. `report.csv` carries both rates, and a
  content test checks both.

## Consequences
- Normal levels now take a player who builds specials about 1.3 attempts, hard ones about 2.5 and super hard ones
  about 5. The casual bot wins about 0.3 of normal levels, 0.06-0.08 of hard ones and almost no super hard ones.
- A tuning run takes minutes per game instead of seconds: skilled playouts cost about eight times as much.
- If play-testing shows the levels are too hard or too easy, the fix is the skilled table, not the planners.
- Golden replays still record the casual bot's playouts.

## Alternatives considered
- Keep the casual bot and lower its targets: that makes levels harder without valuing what skilled players do
  differently (setups, combos, goal focus), so the difficulty lands on the wrong levels.
- Today's targets on the skilled bot: casual win rates of about 0.15 on normal levels and 0.03 on hard ones, too steep
  for most players.
- Deeper search (two plies, Monte Carlo rollouts): stronger than a human, and tens of times slower to tune with.
