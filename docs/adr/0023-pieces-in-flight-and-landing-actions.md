# ADR 0023: Pieces in flight and landing actions

Status: accepted

## Context
Players tap and swipe while a refill is still falling. Until now every board waited for the whole presentation to
settle before taking a move, which felt sluggish, and the one attempt to let moves through early (15 Oct) cleared and
stacked pieces the player hadn't seen land, because the rules had them resting while the view still had them in the
air. Both games' references let you play on what has landed while the rest falls.

## Decision
- **Flight is rules state.** Gravity (ADR 0022) leaves every piece it moves or spawns *in flight* at its destination
  (`FlightMarks`, part of `CopyTo` and the hash). A piece in flight takes no part in groups, matches, swaps, hints,
  shuffles or the end of the level.
- **The view reports landings as actions.** When a fall's last leg touches down, the view reports `(id, cell)` and the
  session applies `Land(cell, id)`; the rules then resolve as after any action (a landed gem that completes a line
  clears on that landing, its special on the landed gem). `LandAll` lands everything one piece per quiet pass in
  canonical order (lowest row first, then left to right); bots, replays and the session's watchdog use it. Neither
  costs a move; both are recorded, so replays stay exact.
- **No loss while anything flies.** A landing may still win; the landing that settles the board decides the loss. With
  no moves left and pieces in flight, moves and boosters are refused `NoMovesLeft`. Once the level has ended, landing
  reports are dropped.
- **Moves act on landed pieces only.** A swap, tap or activation touching a piece in flight is refused `Falling`. The
  session's `CanActOn(cell)` adds that the piece's event lane is idle; the board view refuses moves while a special,
  combo, shuffle or booster holds it. Plain falls never hold the board.
- **Falls preempt falls.** The event player lets a fall from a later action take over a lane an earlier fall still
  holds (same-type preemption); legs of one action queue as before. A per-frame `FallMover` moves falling pieces by
  speed (start speed, acceleration, top speed; slides at a clamped even pace), so an extended fall keeps its position
  and speed, and a piece never comes within a cell of a falling piece below it.
- **The view must not lose a landing.** The session lands everything if the presentation has been idle for 0.5 s with
  pieces still in flight, and logs a warning.

## Consequences
- The 15 Oct phantom clears and stacked gems can't happen: the rules never act on a piece the player hasn't seen land.
- A replay holds one record per landing; several hundred per attempt, still small for the attempts table.
- Presenters return a fall's planned duration to the event player; the follow rule may make the real arrival a little
  later, which the watchdog's grace and the landing reports absorb.
- A swipe onto a gem a few frames from landing is buffered for `TapBuffer` and retried on each landing.

## Alternatives considered
- Timed falls in the rules (pieces land by model time): every presenter and motion test would have to follow model
  time, and the view's real motion would still drift from it.
- Gating input on the whole board settling (the old rule): correct but sluggish, and against the brief.
- Letting the view guess what is safe to touch: the cause of the 15 Oct bug.
