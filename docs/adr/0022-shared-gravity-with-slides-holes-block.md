# ADR 0022: Shared gravity with slides; holes block

Status: accepted

## Context
Blast's pieces fell straight past holes and refilled every column from the top; Match-3 (ADR 0021) had its own loop
with diagonal slides under static blockers. Levels are now designed around slides in both games, and the genre's
leading games treat a hole as the end of a column, feeding the cells below it from the side.

## Decision
- One `Gravity` in `Tileworks.Tiles.Model`, generic over a small grid interface (`IGravityGrid`) that both states
  implement. It loops until nothing moves: straight falls within a column segment, a refill of each column's top
  segment, then diagonal slides into cells nothing above can reach.
- A segment is a run of playable cells bounded by holes, static pieces (Blast and Match-3 boxes, chained gems) or the
  board edge. Only the top segment refills; a static piece on a column's top cell stops that column's refill.
- A slide takes a resting piece from up-left or up-right; the side tried first alternates by `(x + y)` parity.
- Each piece's path goes out as legs, straight runs merged, with the landing on the last leg only. Every moved or new
  piece is left in flight at its destination (ADR 0023 covers flight).
- The legs and per-column scratch live in the state (`GravityLegs`): pipelines are shared across engines, and level
  generation runs playouts in parallel.
- `Gravity.Reachable(playable, statics)` gives the cells falls and slides can reach; generation and the Levels window
  reject boards that leave a playable cell unreachable.

## Consequences
- Blast and Match-3 settle the same way, and the shared-board extraction ADR 0021 deferred has its first piece.
- Blast's rules version went to 2 and its golden replays were rebaselined; shapes with mid-column holes (Window,
  Pillars, Hourglass, Plus) now make pockets fed by slides. Window at odd widths leaves one cell unreachable and is
  rejected until the planner fixes it.
- With landings recorded as actions, a replay holds several hundred records per attempt instead of tens. That is
  still small for the attempts table in SQLite.
- Parity is mirror-symmetric only on even widths; on odd widths a mirrored pocket may be fed from the same side.

## Alternatives considered
- Pieces passing holes (Blast's old rule): it makes holes cosmetic and slides rare, against the level design.
- Refilling every segment from the top: pieces would appear out of nowhere under an overhang.
- Keeping a copy of the loop per game: two rules to keep in step for every change to slides.
