# Level design

Every game ships 100 levels built the same way: a planner decides what each level is about, a generator lays out the
board, and a bot tunes it to its place in a shared difficulty curve (a skilled bot for Blast and Gem Swap, a casual one
for Block Puzzle and Tile Match). The Levels window (Tileworks > Levels) shows
the result in play order with the curve; each game's `Content/report.csv` has the numbers.

## The curve (`Tileworks.Tiles.Generation.LevelCurve`)
- Levels 1-4 are near-certain wins; 5-9 ramp up; level 10 is the first hard level.
- From 11, each ten-level chapter has a hard level 5th and 10th, an easy breather right after each, and normal levels
  in between that get a little harder every chapter (bot win rate about 0.72 → 0.47 by level 100).
- Levels 40, 60, 80 and 100 are super hard (bot win rate about 0.15). Nothing ships under 0.1.
- Blast and Gem Swap use the skilled table, with the same roles (ADR 0024): easy 0.97, normal 0.82 → 0.70 over the
  chapters, hard 0.45 → 0.38, super hard 0.22. Nothing ships under a skilled rate of 0.15.
- Hard levels are marked `hard` / `superHard` in the sequence and pay more (Blast, Gem Swap, Block Puzzle: 20 / 30 / 50 /
  80 coins for easy / normal / hard / super hard).

This is common practice in the genre's leading games: hard and super hard levels are tagged and pay more, and a
sawtooth with breathers keeps a rising curve from wearing players out. Boosters (Hammer and Shuffle in Blast and Gem
Swap) are not part of the tuning: levels are tuned without them, so boosters only ever make a level easier.

## How a level is tuned
- The casual bot plays the best-looking move three times in four, a random one otherwise.
- The skilled bot (Blast, Gem Swap) plays a random move one time in twenty. Otherwise it plays its six best-looking
  moves on a copy of the board, judges each position (specials and combo setups on the board, groups or swaps one move
  from a special, goal progress against the moves left, blockers in reach, ducks) and adds half of the best next move.
  The casual bot is then measured on the final level. The first levels are close to unlosable: it must win level 1 at
  least 99% of the time, levels 2-3 95% and level 4 90%, counted as losses over 240 games (at most 2, 12 and 24), since
  fewer games can't tell 0.99 from 0.96. Other easy, normal and hard levels it must win at least 2% of the time; super
  hard levels answer to the skilled bot only. `report.csv` has both rates.
- Each level is measured on 8-12 starting boards (as players' attempts differ) and tuned until its win rate lands
  within 0.07 of its target:
  - Blast and Gem Swap (`LevelTuner`): the move limit (up to 40; at least 16 / 14-15 / 20 / 22 on easy / normal / hard /
    super hard levels), then the goal size (the colour goal, balloons, floors), then the static blockers (boxes,
    chains), then a colour more (Blast up to five, Gem Swap up to six, and no sixth before level 21). A level that is
    too hard gets those back in reverse order. On the skilled bot a level is only on target when wins leave at most 50%
    / 35% / 25% of the moves (easy / normal / hard and super hard; 60% on levels 1-4, where near-unlosable means moves
    left over), so a win isn't decided in the first few moves, and
    when dead-board shuffles average at most 0.15 a game (0.3 on hard and super hard). Too many shuffles take blockers
    away, then a colour, for good. Levels under 30 keep at least 60% of the board in movable pieces: the generator trims
    blockers, then goals, to stay there.
  - Block Puzzle: the score target, or the gem count (in mirrored pairs, at most 16), then the piece tier and how often
    big pieces come.
  - Tile Match: the kind assignment (the solver proves every level solvable), then one kind more or fewer.
- The report also records how close games were (moves left on wins, goal left on losses), shuffles per game, the
  share of movable pieces, and the pen layout.

## What each game's levels look like
- **Blast and Gem Swap** (`BlastPlanner`, `Match3Planner`): boards take one of 20 mirrored shapes (heart, castle,
  bridge, keyhole, chimney…) and never the same shape twice in a row. Holes and static blockers stop falls, and pieces
  slide diagonally into cells nothing can fall into (ADR 0022).
  - **Pens** (`BoardPens`): empty cells sealed behind a wall of boxes (or chains in Gem Swap), shown as `_` in the
    level files. Pieces fall and slide in the moment a wall piece breaks. Walls step in two cells thick, since slides
    come in diagonally. The layouts:
    - Intro: a short shelf over the bottom row's middle, sealing 2-3 cells.
    - Corners: walls stepping in from each side to a column that runs to the bottom.
    - Funnel: walls narrowing to a chute over open bottom rows, so pieces slide outwards as they arrive.
    - Sides: a hook on each side wall.
    - Shelf: a box shelf across the middle.
  - **Where slides appear:** level 2 shows a pen behind one-layer boxes on an otherwise open board, and Blast 4 / Gem
    Swap 5 have corner pens. From level 11, two levels a chapter have pens: the hard 5th (walls in even chapters, a
    pocket shape with boxes in its pockets in odd ones) and one normal level with walls. The wall layouts rotate
    through Corners, Funnel, Sides and Shelf. Pocket shapes (bridge, keyhole,
    cave, shelves, goblet, chimney, hourglass, pillars, window) are the secondary source of slides.
  - **Pen rules:** a pen holds no pieces or goals at the start, and only static blockers seal it, never holes alone.
    Outside walls and pocket shapes, no blocker may seal a cell. The Levels window has a pen brush and flags a board
    with an empty cell that a refill reaches, or a sealed cell holding a piece or a floor.
  - **Blockers** form mirrored patterns (rows, frames, checkers, chevrons, diamonds, mounds) rather than random scatter.
    Ducks only go where they can fall straight to the bottom. Each blocker arrives alone on an easy level, then comes
    back mixed with others: Blast boxes 2 (as the pen wall), balloons 5, ducks 7, two-layer boxes 8; Gem Swap boxes 2
    (as the pen wall; as a goal at 3), floors 4, chains 6, balloons 7, ducks 9. Ducks never come alone: they share a
    level with a colour goal or balloons, and their count grows with the goal size (at most six).
  - **Gifts:** breathers start with a rocket pair, bomb or disco ball on the board, and a frame of boxes holds a bomb in
    its pocket.
- **Block Puzzle** (`PuzzlePlanner`, `PuzzlePictures`): level 1 is an empty board; every other level starts from one of
  26 mirrored 8×8 pictures (heart, rocket, crown, ghost…), each shown once before any repeats. Gem and score levels
  alternate; hard ones mostly collect gems.
- **Tile Match** (`Tools/make_layouts.py`): levels 1-10, 12 and 15 use the hand recipes, put in curve order; the others
  are mirrored shapes stacked in pyramids, brick offsets or straight piles. Tile count, kinds and layers grow with the
  level, hard levels are larger and breathers smaller. Ice from 7, hidden tiles from 9, linked pairs (a tile and its
  mirror) from 14, a spawner in the middle column from 18.

## Regenerating
- Block Puzzle: Tileworks > Block Puzzle > Generate Levels (seconds).
- Blast, Gem Swap: Tileworks > Blast / Match3 > Generate Levels (about 8 minutes for Blast and 12 for Gem Swap, run
  side by side, since the skilled bot is about eight times slower), then Rebaseline Replays.
- Tile Match: `python3 Tools/make_layouts.py`, then Tileworks > Tile Match > Generate Levels (a few minutes), then
  Rebaseline Replays.
- Generation runs on every core but one behind a cancellable progress bar. A level saved from the level designer is
  marked edited and is kept as it is.

## Where it stands (18 Oct)
| Game | On target | Misses |
|---|---|---|
| Blast | 94/100 | none outside the win-rate band; levels 37, 42, 50, 63 and 70 leave a few more moves to spare on wins than the cap (0.26-0.41), and 32 is flagged by the tuner though its row is inside the band and caps. By design, level 3 asks for 6 of its 8 boxes |
| Gem Swap | 94/100 | none outside the win-rate band; levels 12, 39 and 57 leave 0.36-0.40 of the moves to spare, 71 (easy) 0.58 with 0.25 shuffles a game, 75 averages 0.52 dead-board shuffles a game (known, the worst), and 20 sits exactly on its band's ceiling (0.50) |
| Block Puzzle | 95/100 | four levels a few points off target (three easy ones slightly harder, one normal slightly easier); level 80 lands hard, not super hard |
| Tile Match | 99/100 | level 14 (links intro) is easy rather than normal |

On the skilled bot, by role (easy / normal / hard / super hard):
- Blast wins 0.96 / 0.75 / 0.40 / 0.20 (targets 0.97 / 0.76 / 0.42 / 0.22). The casual bot wins 0.55 / 0.20 / 0.07 /
  0.01, wins leave 0.49 / 0.31 / 0.21 / 0.17 of the moves, and no level shuffles.
- Gem Swap wins 0.97 / 0.76 / 0.42 / 0.20. The casual bot wins 0.70 / 0.25 / 0.07 / 0.03, wins leave 0.46 / 0.27 /
  0.17 / 0.15 of the moves, and shuffles average 0.01 / 0.00 / 0.05 / 0.01 a game.
- Levels 1-4 are close to unlosable. The casual bot loses, out of 240 games each: Blast 0 / 1 / 6 / 0, Gem Swap 1 / 6 /
  5 / 11 (allowed 2 / 12 / 12 / 24). Their wins leave 0.51-0.59 of the moves.
- Levels 1-10 of both games meet every rule; a test keeps it so, and keeps every level inside its band.
- Retuned by hand after the run, boards unchanged:
  - Blast 68 and Gem Swap 24 got a small colour goal (10 and 5) next to their blockers.
  - Levels 1-4 got smaller goals or more moves: Blast 1 has 22 moves and Gem Swap 4 has 30 for its floors.
  - Blast 3's goal asks for 6 of its 8 boxes, the one goal under its board's count: the 8-box goal kept the casual bot
    losing even at 40 moves. Goals under the board count are common in the genre; the two left over are there for a
    player who clears them anyway.

Bot win rates are a stand-in for players. Once real attempt data exists, the curve's targets are the numbers to adjust.
