### What's new

1. Introduce core attributes (`[Composite]`, `[Node]`, `[Component]`, `[Implementor]`, `[assembly: MockerRoot]`) and compile-time Roslyn `IIncrementalGenerator` code generation.
2. Emit nested property chains, `{Composite}Composite` runnable subclasses, `{Composite}Choices` configuration trees, and `{Component}Implementor` selection enums starting at `None = 0`.
3. Resolve component constructor dependencies at compile time as literal `new` calls with runtime cycle detection and external `Dependencies` registry lookups.
4. Generate partial do-nothing mocks for `None` slots supporting properties, indexers, events, generic methods, `ref`/`in`/`out`/`params` parameters, and completed `Task`, `ValueTask`, `UniTask`, and `Awaitable` returns.
5. Support per-implementor `IAsyncInitializable<TAwaitable>` overlapped initialization, reverse-order `IDisposable` teardown, and `OnBeforeInitialize`, `OnInitialized`, `OnBeforeDispose`, and `OnDisposed` virtual hooks.
6. Support cross-assembly discovery via `[MockerExport]` assembly attributes and compile-time validation diagnostics `MOCK001` through `MOCK012`.
