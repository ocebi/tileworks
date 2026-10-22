# ADR 0025: Showcase games keep their own presentation, session and persistence code

Status: accepted

## Context
Four demo games run on one framework: Tile Match, Block Puzzle, Blast and Gem Swap. Blast and Gem Swap have the same
shape for home, result, HUD, idle, special presenters, session and persistence, and an architecture review (22 Oct,
`docs/GRAPH-REVIEW.md` item 2) found about 20 files that are near copies of each other. Some have already drifted:
Blast's special presenters count specials and show the cascade on the HUD, Gem Swap's don't, and the sessions decide
"settled" differently. The obvious fix is to lift the common shape into Tiles.

Tileworks will be copied into other projects that each hold a single game. Whatever it carries, every such project
carries too.

## Decision
- Tileworks holds only what any single imported game needs: the rules engine, events and the event player, boosters,
  economy, saves, level files, and views every game can host as they are (`BoosterBarView`).
- Game-specific flows, UI composition, per-game SQLite tables and session skeletons stay in the game folder, even when
  two demo games look alike. Looking alike in two demo games is not a reason to move code into Tileworks; a single
  imported game needing it is.
- The booster-slot constants (`BoardBoosters.Hammer`, `Shuffle`) left Tileworks for the same reason: slot order is
  each game's booster config, not the framework's.

## Consequences
- Blast and Gem Swap duplicate presentation, session and persistence code on purpose.
- A case project copies `Assets/Tileworks` and writes its own game folder; it inherits no demo-game flows it would have
  to work around.
- Drift between demo games is acceptable where their rules differ. A bug fixed in one copy should be checked in the
  other.
- Reviewers should read one game folder as a self-contained example of a game on Tileworks.

## Alternatives considered
- Move the shared shape into Tiles (generic home presenter, result view, special-idle loop, persistence DAOs, session
  skeleton): less code in the repo, but every single-game project would carry flows shaped by two demo games.
- A demo-only runtime assembly in `Assets/Games/Shared`: keeps Tileworks lean, but no game folder would stand on its
  own as an example, and the two games' differences would turn into options on shared types.
