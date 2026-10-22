# Match 3: game spec

M8 (fourth genre), playable in the Editor. Display name **Gem Swap** (pending approval), folder `Assets/Games/Match3`.
The UI session designs the screens, art and motion from this page; the rules session builds the model.

## The game
Swap two neighbouring pieces to line up three or more of the same colour. Matched pieces clear, the pieces above
fall, new ones drop in from the top, and new matches clear in cascades. Finish the level's goals within its move
limit.

## What it reuses
Most of Blast: the square board with holes, gravity, seeded refill, cascades resolved in waves (ADR 0019), specials
and their combos, box, balloon and duck blockers, goals, the move limit, continues, and levels measured by bot
playouts. TODO M7 already says the Blast board was built so that a swap match-3 is a rules variant. What's new is the
swap input, match finding, static blockers with diagonal slides, two new blockers, the flyer, and the dead-board
shuffle. The shared part of the board moves out of Blast into a shared module (new ADR); each game keeps its own
input and match rules.

## Board
- Square grid, 5–9 cells wide and 5–11 tall (portrait), with holes. The frame follows the level's shape, as in Blast.
- 4–6 colours per level (at most 6).
- Gravity pulls down. New pieces drop in at the top of each column.
- Boxes and chained pieces stay put. A cell below one that nothing can reach straight down is filled by a piece
  sliding in diagonally from a neighbouring column. (In Blast, boxes fall.)
- The starting board has no ready-made matches. Refills can make cascades; that's part of the fun.

## Input
- Drag a piece towards a neighbour (past about a third of a cell), or tap a piece and then a neighbour. The selected
  piece shows a ring.
- A swap that makes no match is refused: the pieces swap and come back, and no move is used.
- Swapping a special with any neighbour fires it, with or without a match. Double-tapping a special fires it where it
  is. Both use a move.
- After 5 seconds without input, a hint nudges two pieces towards each other. It prefers a swap that makes a special.

## Matches and specials
The shape of the match decides the special. It appears in the cell the player moved; for a cascade match, in the
cell of the piece that landed last. The matched pieces slide into that cell, as in Blast.

| Match | Makes | When it fires |
|---|---|---|
| 3 in a line | nothing | — |
| 4 in a line | **Rocket**, crossways to the match (a horizontal 4 makes a column rocket) | clears its row or column |
| 2×2 square (more allowed) | **Flyer** | hits its four neighbours, then flies to a goal piece (or a random piece if no goal is left) and hits it |
| L or T (5 pieces) | **Bomb** | clears the area around it (radius 2, as in Blast) |
| 5 in a line | **Disco** | clears every piece of the colour it was swapped with (a random colour on a double tap) |

When a match fits more than one shape, the stronger special wins: disco > bomb > flyer > rocket.

**Swapping two specials** makes a combo. Blast's combo table carries over:
- rocket + rocket: a row and a column;
- rocket + bomb: 3 rows and 3 columns;
- bomb + bomb: a bigger bomb (radius 3);
- disco + rocket or bomb: every piece of one colour turns into that special and fires;
- disco + disco: clears the board.

The flyer adds its own:
- flyer + rocket or bomb: the flyer carries the special to its target and fires it there;
- flyer + flyer: three flyers;
- disco + flyer: every piece of one colour turns into a flyer.

A special hit by another special fires in the next wave, as in Blast.

## Blockers and goals
| Blocker | How it behaves | Goal counts |
|---|---|---|
| Box (1–2 layers) | stays put; a match next to it or a special hitting it takes a layer | boxes cleared |
| Floor (1–2 layers), new | sits under a piece; a match on top, or a special hitting the cell, takes a layer | floor cells cleared |
| Chain, new | locks the piece under it: it can't be swapped and doesn't fall, but it can still be matched by swapping its neighbours. That match, or a special, breaks the chain. | chains broken |
| Balloon (from Blast) | falls; pops when a match happens next to it | balloons popped |
| Duck (from Blast) | falls; collected when it reaches the bottom of its column | ducks collected |
| Colour | — | pieces of one colour collected |

Up to 4 goals per level, as in Blast.

## Dead boards
When the board settles and no swap makes a match (and no special is on the board), the board shuffles:
a "No moves" banner, then the pieces fly to new cells. Blockers and chained pieces stay. The shuffle is seeded, so
replays stay deterministic, and it always leaves at least one move.

## Turn flow
As in Blast, each resolution pass is a wave:
1. clear the matches;
2. fire the pending specials;
3. gravity, including diagonal slides;
4. refill;
5. find new matches, then start the next wave.

The event player runs in wave mode. Callouts on a second and third cascade use the existing lettering style.

## Levels
- Blast's level format plus a floor layer and a chain layer, and boxes that stay put.
- Generator: 100 levels planned by `Match3Planner` and tuned to the shared difficulty curve by a casual bot (mostly the
  best swap by a simple score, sometimes a random one), as in Blast. See `docs/LEVEL-DESIGN.md`.
- The Levels window supports the new game.

## Screens (UI session)
The view contracts follow Blast's (board, HUD, result, Home), so the design can start from the Blast screens with a
new look:
- **Hub card** for a fourth game, a **loading screen** and **Home**: game switching, Play pulse, lives, coins, and a
  goal preview for the next level.
- **Game:** HUD with the level, up to 4 goal chips, a moves counter and pause. The board has a frame that follows the
  level's shape and checkered cells.
- **Pause, Win** (coin count-up reveal) and **Lose** with a continue (+5 moves for coins).
- **Boosters:** Hammer (level 4) and Shuffle (level 7), shared with Blast; see DECISIONS-LOG "Boosters".

## Art needed
Use the same format as the Blast set: 128 px PNG, transparent background, one centred piece per file, the same glossy
style and Lilita One lettering.
- **6 pieces**, each with its own silhouette as well as its own colour, so colour-blind players can tell them apart.
  They must not look like Blast's cubes. The theme is the designer's choice (for example gems: round, square, drop,
  hexagon, star, heart).
- **Specials:** row and column rocket, bomb, a colourless disco, and a flyer. They may be restyled from Blast's.
- **Blockers:**
  - a box with 2 layers and with 1 layer; it should read as fixed in place;
  - a floor tile with 2 layers and with 1 layer; it is cell-sized and must tile seamlessly;
  - a chain overlay;
  - the balloon and the duck (Blast's, or restyled).
- **Board:** a light and a dark cell for the checkerboard, and a 9-slice board frame.
- **Feedback:** a selection ring and a hint glow. The effects (flash, ring, spark, bit) can be reused from Blast.
- **Hub card art,** a Home backdrop and the title.

## Motion
Presenters read timings from a Feel config, so the motion board should time these new moves:
- a swap;
- a refused swap (there and back, with a small bump);
- a match clear;
- a diagonal slide;
- the flyer: take off, arc, hit;
- the shuffle;
- the hint nudge;
- a chain breaking and a floor layer clearing;
- cascade callouts.

These carry over from Blast and can keep their timings: the group sliding into a special, falls with a landing
squash, rocket halves, the bomb ring, disco rays, the combo merge (gather, charge, burst) and goal flights.

## Open questions
- The display name (the working title is "Match 3").
