# ADR 0018: Local live events with deterministic bots

Status: withdrawn 22 Oct 2026: the race was removed before the first public release; no live event ships. The level-result listener seam stays.

## Context
A live event needs a schedule, other racers and time that keeps passing while the app is closed, without a server.

## Decision
A local schedule turns config rules (every N hours, lasting M hours) into windows with deterministic ids. In the race, each bot's win times are drawn once from a seeded generator starting at the join time; standings at any moment are counts of those times, evaluated against the trusted clock. Wins are recorded inside the level-result transaction. Scenarios start on a throwaway profile and Editor time travel shifts only the running clock.

## Consequences
- Nothing ticks in the background; the same race always plays out the same way, so tests run the whole lifecycle on a fake clock.
- Bots don't react to the player; a server version would replace the schedule and bots behind the same seams.

## Alternatives considered
- Ticking bots in a background service: needs the app open and is hard to test.
- Server-run races: the right end state, not needed offline.
