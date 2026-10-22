# ADR 0003: Feature × layer assemblies

Status: accepted

## Context
Without enforced boundaries, game logic leaks into views and every change recompiles everything.

## Decision
Assemblies are named `<Root>.<Feature>.<Layer>` with layers Api, Model, Generation, Service, Controller, View, Installer, Debug, Editor and Tests. A layer exists only if it has code. Architecture tests check the layer matrix, that Model and Api assemblies are engine-free, that only Service assemblies touch SQLite, and a budget of at most 80 runtime assemblies.

## Consequences
- Rules stay testable without Unity; views can be replaced.
- Incremental compiles stay small.
- Adding a feature means a few asmdefs, kept honest by the tests.

## Alternatives considered
- One assembly per game: fewer files, no enforcement.
- One assembly per class family: asmdef sprawl.
