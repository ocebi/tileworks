# ADR 0009: Undo is a rules action

Status: accepted

## Context
Players expect an animated undo; tools need an exact rewind.

## Decision
Gameplay undo is an action the rules handle (with its own events). Tools such as the level editor rewind with state snapshots.

## Consequences
- Undo animates like any move and is recorded in replays.
- Two mechanisms to maintain, each simple.

## Alternatives considered
- Snapshots for players: no animation, and hard to explain in a replay.
