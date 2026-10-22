# ADR 0011: Online services deferred behind seams

Status: accepted

## Context
A portfolio project gains more from gameplay quality than from a backend, but should show where one would plug in.

## Decision
Everything runs locally. Seams are in place: `ITimeSource` (server time), `IConfigSource` (remote config), `IEconomy` (authoritative economy) and planned ad, store and analytics interfaces. (`ILiveEventSchedule` went with the race on 22 Oct.)

## Consequences
- Effort goes to gameplay; online features can arrive without touching game code.
- No real multiplayer or server-verified economy yet.

## Alternatives considered
- Build a backend now: large cost, little gameplay value.
