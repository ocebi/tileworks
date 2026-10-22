# ADR 0016: Dev tools stay reachable in the showcase build

Status: accepted (the debug panel itself is not built yet; Editor menus cover scenarios and time travel today)

## Context
Reviewers should be able to launch scenarios, watch replays and time-travel lives in the public WebGL demo.

## Decision
The WebGL showcase build keeps dev tools behind a hidden gesture; the Android release build strips them (Debug assemblies require `TILEWORKS_DEV`).

## Consequences
- Reviewers see the systems at work.
- The showcase build is documented as such.

## Alternatives considered
- No dev tools in public builds: scenarios, replays and lives timers would be hard to show.
