# ADR 0007: Logic runs ahead of the view

Status: accepted

## Context
Waiting for animations before applying the next move makes input feel sluggish and ties correctness to timing.

## Decision
The rules apply an action at once and return its events; an event player schedules presenters on lanes (one per entity) or in waves (one per resolution pass). The session reports the end of a level only when the player is idle.

## Consequences
- Input stays responsive; animation bugs can't corrupt state.
- Presenters must start from the event's data, never from current view state.

## Alternatives considered
- Awaiting each animation: simpler, slower, and fragile.
