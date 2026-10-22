# ADR 0005: UniTask with owned scopes

Status: accepted

## Context
Async flows (boot, scene loads, popups, presentation waits) need cancellation and must not leak when a scene unloads.

## Decision
UniTask for async work; every task belongs to a `TaskScope` that cancels with its owner. No `async void`, and no fire-and-forget without an error sink.

## Consequences
- Cancellation follows scopes; zero-allocation awaits; works on WebGL.
- A third-party dependency in Kit.

## Alternatives considered
- Coroutines: no return values, awkward cancellation.
- `System.Threading.Tasks`: allocations and WebGL limits.
