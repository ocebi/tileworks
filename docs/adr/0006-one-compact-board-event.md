# ADR 0006: One compact board event

Status: accepted

## Context
Rules emit many events per action, and presenters, replays and digests all consume them.

## Decision
A single `BoardEvent` struct (type, step, entity, three ints) in a reusable buffer, with an event-type registry and typed read-only views per event (`PieceFell.From(e)`), so nothing outside the event file reads raw fields.

## Consequences
- No allocation per event; events serialize and hash trivially.
- Packing values into ints needs care, kept in one file per game.

## Alternatives considered
- A class per event: readable, but allocates.
- C# events from the rules: couples rules to listeners.
