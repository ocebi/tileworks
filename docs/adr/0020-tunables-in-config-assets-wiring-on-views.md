# ADR 0020: Tunables in config assets, wiring on views

Status: accepted

## Context
Inspector values on view components get edited by mistake, live on scene objects that builders regenerate, and are hard to review.

## Decision
View MonoBehaviours hold only references set by the scene builders, shown read-only. Every tunable (layout, timings, sounds, colours) lives in a per-game ScriptableObject with private fields, read-only properties and `Range`/`Min` limits.

## Consequences
- One home per tunable; designers can't break a scene by editing a component.
- Builders must create and wire the config assets, which they do.

## Alternatives considered
- Serialized fields on views: quick, but fragile and scattered.
