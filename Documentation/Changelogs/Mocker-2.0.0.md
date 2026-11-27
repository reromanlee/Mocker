### What's new

1. Support `abstract partial class` components alongside `partial interface` components, preserving concrete and `virtual` base logic in generated mocks while overriding only unimplemented `abstract` members with matching accessibility.
2. Expand minimum Unity support from Unity `6000.0` down to Unity `2022.3` LTS through Unity `6.6+` (`6.7+`), verified across EditMode, PlayMode, and WebGL IL2CPP with `ManagedStrippingLevel.High`.
3. Add user partial mock override detection so hand-written members inside `partial class {Component}Mock` compile cleanly without `CS0111` duplicate-member collisions.
4. Add `MOCK013` (unconstructible or ambiguous-constructor implementor), `MOCK014` (class component must be abstract), and `MOCK015` (abstract component parameterless constructor contract for `None` mocks).
5. Add optional constructor defaults on `{Composite}Composite(choices = null, dependencies = null)`, default `CancellationToken = default` on `InitializeAsync`, public `IsDisposed` getter, `Dependencies.TryGet<T>`, and post-dispose `ObjectDisposedException` guards on cached `Node` references.
6. Migrate the Roslyn `4.3.0` generator into `DotNet/` with deterministic embedded `/Mocker/` PDBs, SHA-256 `SourceFingerprint` sync to `UnityPackage/Runtime/Plugins/reromanlee.Mocker.Generator.dll`, NUnit `3.14.0` tests, Unity EditMode/PlayMode tests, a `Samples~/GettingStarted` scene, minimal `UnityProject/` harness, and GitHub Actions CI.
7. Rename `ImplementorAttribute.InterfaceType` to `ImplementorAttribute.ComponentType`, seal `ComponentAttribute` and `NodeAttribute`, and reorganize `UnityPackage/Runtime/` into `Attributes/` and `Contracts/` while preserving `.meta` GUIDs.

### Bugfixes

1. Cancel linked cancellation tokens and await all started sibling tasks on partial `InitializeAsync` failure so no sibling tasks leak uncancelled or raise unobserved task exceptions.
2. Roll back and dispose already-constructed `IDisposable` implementors if a later dependency or constructor fails during composite creation, avoiding leaked `CancellationTokenSource` and native handles.
3. Make `Dispose()` atomically idempotent via `Interlocked.Exchange`, prevent in-flight `InitializeAsync` completion from overwriting `Disposed` state, and guarantee full teardown even when `OnBeforeDispose` or an implementor throws.
4. Release `_dependencies`, `_resolving`, and completed `_init*` task references immediately after construction and initialization instead of pinning them until `Dispose()`.
5. Fix Roslyn incremental cache invalidation in `ProviderExtensions.CollectAll` via structural `EquatableArray<T>` comparison and emit deterministic LF line endings in generated scripts.
6. Fix `where T : unmanaged` (`CS0449`), `where T : notnull`, `init` property accessors, `ref`-return diagnostics, and diamond interface member deduplication (`CS0111`) in generated mocks.
