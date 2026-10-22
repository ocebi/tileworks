# ADR 0010: Generated, measured content

Status: accepted

## Context
Hand-authoring dozens of levels per game is slow, and a level's difficulty is a guess until measured.

## Decision
Levels come from recipes: a generator builds the board (Tile Match assigns kinds along a witness solution, so every level is solvable), then casual-bot playouts measure the win rate and place the level in an Easy, Normal or Hard band. Results are written as canonical JSON with a report.

## Consequences
- Every shipped level is solvable and its difficulty is a number.
- The bots model a casual player, not a real one; play-testing still matters.

## Alternatives considered
- Hand-authored levels only: slow and unmeasured.
