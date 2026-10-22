# ADR 0001: Platform-first monorepo

Status: accepted

## Context
Tileworlds is meant to host several tile and grid puzzle games and to be reused in case studies. Copying a project per game would fork the shared code on day one.

## Decision
One Unity project hosts every game. Shared code lives in four embedded UPM packages (Tileworks Kit, Tiles, Meta, and dev tools later). Each game lives in `Assets/Games/<Game>` and has its own build profile (scenes, product name, bundle id).

Update 21 Oct: the framework moved from UPM packages to the source folders `Assets/Tileworks/{Kit,Tiles,Meta}` (no `package.json`), so another project copies a folder instead of taking a git dependency; see `ARCHITECTURE.md` §5.

## Consequences
- A fix in a package reaches every game at once; the second and third games prove the packages are generic.
- Packages can be tagged and consumed by git URL in another project.
- Builds must pick their game's scenes, which `BuildScript` does per game.

## Alternatives considered
- One project per game: simple builds, but shared code forks.
- A single assembly for everything: fastest to start, impossible to keep boundaries.
