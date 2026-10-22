# ADR 0012: Picking by maths, not colliders

Status: accepted

## Context
Tile and cell picking needs to be exact and cheap, including stacked layers.

## Decision
Picking maps a world point to the model's layout with arithmetic (top layer wins for Tile Match, grid cells for the other games). No physics colliders.

## Consequences
- Deterministic and cheap; picking matches the rules exactly.
- Shapes outside the grid would need their own maths.

## Alternatives considered
- Colliders: easy to set up, but slower and can disagree with the model.
