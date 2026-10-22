# ADR 0019: Blast resolves in waves

Status: accepted

## Context
In a blast game one tap can trigger chains of specials, falls and refills that must read clearly on screen and replay exactly.

## Decision
Each rules resolution pass is one wave: pending specials fire (their hits queue further specials for the next pass), and only when nothing is pending do gravity, collection, refill and shuffle run. Every piece keeps an id while it falls, refills come from a seeded stream, and the event player runs in wave mode so each wave plays after the previous one.

## Consequences
- Chains animate in order without presenters knowing the rules; replays are exact; taps allocate nothing.
- Boxes fall like other pieces for now; static blockers would be a rules variant.

## Alternatives considered
- Resolving everything at once and animating by distance: harder to read and to replay.
