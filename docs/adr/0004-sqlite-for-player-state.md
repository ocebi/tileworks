# ADR 0004: SQLite for player state

Status: accepted

## Context
Player state spans features that must change together (a level result writes progress, coins, lives and whatever a level-result listener records), and the stats screen needs queryable history.

## Decision
Each profile is a SQLite database (sqlite-net) with DAOs, cached DAOs, named transactions (nested ones become savepoints) and per-module, append-only migrations. Authored content stays JSON. On WebGL the database persists through the browser's IndexedDB file system.

## Consequences
- One transaction per level result: all of it or none of it.
- History can be queried (attempts, durations, boosters).
- A native plugin per platform and SQL migrations to maintain.

## Alternatives considered
- JSON or PlayerPrefs: simple, but no atomic cross-feature writes and no queries.
- A document database: heavier and less portable.
