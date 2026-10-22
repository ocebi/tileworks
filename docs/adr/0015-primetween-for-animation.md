# ADR 0015: PrimeTween for animation

Status: accepted

## Context
Presenters start many short tweens per action; allocations there show up as hitches.

## Decision
PrimeTween for all tweens, with struct targets and delayed starts instead of nested sequences where possible.

## Consequences
- Zero-allocation tweens; installed through the package manager.
- A smaller community than DOTween.

## Alternatives considered
- DOTween: popular, but allocates and is installed as copied source.
