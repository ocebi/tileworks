# ADR 0002: Own dependency-injection container

Status: accepted

## Context
The architecture relies on scopes (App, Home, Game, Level) with lifecycles and validation. The container is also part of what the project demonstrates.

## Decision
A small container in `Tileworks.Kit.Injection`: constructor-only injection, child scopes, `IInitializable`/`ITickable`/`IDisposable` lifecycles, collection injection, and validation that reports missing registrations and cycles before anything is created. Composition tests build every scope with fakes. VContainer is the documented fallback.

## Consequences
- Explicit, small and fully tested; errors are found at scope build, not at first use.
- Maintenance cost is ours; migrating to VContainer stays cheap because only installers touch the container (enforced by an architecture test).

## Alternatives considered
- VContainer or Zenject: mature, but larger and less instructive.
- Service locator or singletons: no scopes, hidden dependencies.
