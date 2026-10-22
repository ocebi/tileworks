# ADR 0021: Match-3 settles in legs, with static blockers

Status: accepted

## Context
Match-3 reuses Blast's waves (ADR 0019) but adds what Blast doesn't have: swaps that can be refused, boxes and chained
gems that stay put, cells under them filled by diagonal slides, a floor layer under pieces, and a flyer that crosses the
board. The spec planned to move the shared board out of Blast first, but both sessions had uncommitted Blast work.

## Decision
- Match-3 gets its own rules assembly on the same engine. Each pass is a wave: specials fire, then the board settles in
  the wave after a clear, then the matches the falls made clear in the wave after that. Systems read the wave number
  from the event writer's step, so no system depends on another's position in the pipeline.
- Settling loops straight falls, refills and diagonal slides until nothing moves, then sends each piece's path as legs
  on its own lane, with the landing on the last leg only.
- A refused swap is a rejected action with a reason (no match, chained, fixed) and no events; the view plays the reason.
- A hit takes the top layer first: the chain, else the box, else the piece and one floor layer. Floors have their own
  ids, so a flyer's landing can hold back the floor it hits.
- The shared board module waits until that Blast work is committed. Match-3's model is the superset, so Blast moves
  onto it then.

## Consequences
- Falls with diagonal steps animate as one path per piece; the presenters need no rules knowledge.
- Until the move, gravity, refill and the special effects exist in both games' assemblies.

## Alternatives considered
- Extracting the shared module first: it would have meant reworking uncommitted Blast code in a shared working tree.
- One leg per event without merging straight runs: more events, and a stutter on every row.
