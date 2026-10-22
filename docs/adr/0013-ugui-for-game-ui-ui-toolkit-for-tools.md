# ADR 0013: uGUI for game UI, UI Toolkit for tools

Status: accepted

## Context
Game UI needs artist-friendly layout and effects; tools need productivity.

## Decision
Game screens and popups use uGUI with TextMesh Pro, built by scene builders. Editor tools and the future debug panel use UI Toolkit.

## Consequences
- Artists can work with familiar UI; tools are quick to write.
- Two UI systems in one project.

## Alternatives considered
- UI Toolkit everywhere: weaker for in-game effects today.
