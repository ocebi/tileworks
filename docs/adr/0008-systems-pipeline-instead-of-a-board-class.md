# ADR 0008: Systems pipeline instead of a board class

Status: accepted

## Context
A single board class with every rule becomes a god class that is hard to test and extend.

## Decision
Rules are a pipeline: action handlers, then resolution systems that repeat until a full pass changes nothing, then end conditions. Each system is small and tested on its own; a game is its list of handlers and systems.

## Consequences
- New mechanics are new systems; order is explicit in one place.
- The resolution loop needs a pass limit, which it has.

## Alternatives considered
- One board class: quick to start, hard to grow.
