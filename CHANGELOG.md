# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-21

First release. Requires Unity 6000.0 or newer.

### Added

**Declaring a hub**

- `[Composite]` marks the root of a hub, on an `abstract partial class`.
- `[Node(name, parent)]` groups things under a composite or another node, nesting as deep as needed.
- `[Component(name, parent)]` marks the interface that gets swapped.
- `[Implementor(component)]` marks a class that can fill a component.
- `[assembly: MockerRoot(...)]` marks where the wiring is generated, optionally naming the composites that assembly owns.

**Generated API**

- A property on every composite and node for each child, so a hub reads as `services.Player.Inventory.Items`.
- A constructor on every composite and node, taking the children it holds.
- `{Composite}Composite`, the runnable subclass that knows the implementors, generated in the root assembly.
- `{Component}Implementor` enums listing everything found, each starting at `None = 0`.
- `{Composite}Choices`, a tree of plain classes mirroring the API, so `choices.Player.Saves` sits where `Player.Saves` does.
- A `Choices` property on the composite, for branching on what was actually selected.

**Construction**

- Components are built on demand and cached, so declaration order never matters and a shared dependency is built once.
- Constructor parameters that are components are resolved by the generator and written out as literal `new` calls, with no reflection or container at runtime.
- Constructor parameters that are not components come from `Dependencies`, a registry filled before construction. Only selected implementors are ever asked for theirs.
- A constructor loop is reported at runtime naming the exact chain, rather than overflowing the stack.

**Mocks**

- A do-nothing implementation is generated for every component and used when a slot is `None`, so an absent service is silent rather than fatal.
- Covers properties, indexers, events, methods, generic methods with constraints, and `ref`, `in`, `out` and `params` parameters.
- Returns completed awaitables for `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`, `UniTask` and `Awaitable`, so awaiting a service that is not there continues instead of hanging.
- Generated as `partial`, so a mock that needs real behaviour can have it written by hand.

**Lifecycle**

- `IAsyncInitializable<TAwaitable>` lets each implementor pick its own awaitable type, so `Task`, `UniTask` and `Awaitable` can be mixed across a single hub.
- `InitializeAsync` overlaps services that do not depend on each other, while a service whose constructor takes another waits for that one first. Components with nothing to initialize generate no state machine at all.
- `IDisposable` tears down in reverse of the order things were built, keeps going after a failure and reports them together, cancels an initialization still in flight, and releases every reference so a disposed hub keeps nothing alive.
- `OnBeforeInitialize`, `OnInitialized`, `OnBeforeDispose` and `OnDisposed` are `virtual`, and run after a derived constructor has finished.

**Assemblies**

- Implementors can live in any number of separate assemblies, discovered through generated assembly attributes rather than by scanning types.
- Any number of composites, each isolated from the others; sharing between them goes through `Dependencies`.
- Implementor assemblies excluded by platform or define constraints drop out of the enums with no further configuration.

**Diagnostics**

- Twelve rules, `MOCK001` through `MOCK012`, reported against the attribute that caused them so they appear in the Unity console and the IDE with a line to click.
