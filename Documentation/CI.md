### Continuous integration in detail

Four workflows in `.github/workflows` validate the Roslyn source generator across operating systems and Roslyn compiler versions, enforce UPM package integrity, run cross-platform Unity EditMode/PlayMode tests plus `ManagedStrippingLevel.High` player builds and runtime smoke probes across Unity `2022.3` LTS and Unity `6`, and sweep stale caches and artifacts nightly.

| Workflow | File | Automatic triggers | Manual trigger | Timeout ceiling | Storage per run |
| --- | --- | --- | --- | --- | --- |
| `DotNet` | `dotnet.yml` | Every push and pull request (ignoring docs and images) | `workflow_dispatch` | 20 min per cell + 10 min hygiene | 0 B on pass (`.trx` on failure, 7d) |
| `Continuous integration` | `continuous-integration.yml` | Pull requests and pushes to `main` (ignoring docs and images) | `workflow_dispatch` | 5 min gate + `Unity` | 41 B `passed-files-*` marker (7d) |
| `Unity` | `unity.yml` | Called via `workflow_call` from `continuous-integration.yml` | Via parent workflow | 60 min tests, 90 min builds, 20 min smoke | 1d smoke players, 7d test logs, `Library` cache |
| `Cleanup` | `cleanup.yml` | Nightly at `04:00 UTC` | `workflow_dispatch` (`olderThanHours`) | 10 min | Reclaims stale caches and artifacts |

### DotNet and package-hygiene matrix (`dotnet.yml`)

Runs on `ubuntu-latest`, `windows-latest`, and `macos-latest` on every push, pull request, and manual dispatch across two Roslyn host versions:

1. `Microsoft.CodeAnalysis.CSharp` `4.3.0`: matches the Roslyn `4.3.1` compiler bundled in Unity `2022.3` LTS, `6000.0` LTS, and `6000.3`, and verifies committed [reromanlee.Mocker.Generator.dll](../UnityPackage/Runtime/Plugins/reromanlee.Mocker.Generator.dll) byte parity via `-p:UpdateUnityPackage=true` + `git diff --exit-code`.
2. `Microsoft.CodeAnalysis.CSharp` `4.10.0`: matches the Roslyn `4.10.0` compiler bundled in Unity `6000.5` and `6000.6+`, validating forward binary compatibility of the `netstandard2.0` generator across all `31` diagnostic, edge-case, nullable-enabled, incremental-cache, lifecycle, allocation, and packaging tests.
3. `Package hygiene` (`ubuntu-latest`): verifies `LICENSE.md` byte parity, `UnityPackage/package.json` schema (`"unity": "2022.3"` and registered `Samples~/GettingStarted`), `.meta` 32-hex GUID uniqueness across `UnityPackage/` and `Samples~/`, zero C# 10+ syntax, LF line endings, and `npm pack --dry-run`.

### Tree-SHA-gated Unity matrix and live player probes (`continuous-integration.yml` and `unity.yml`)

The `gate` job skips Unity runners cleanly when either condition holds:

1. `UNITY_LICENSE` secret is absent (logs `::notice::` so forks and unlicensed mirrors stay green while `dotnet.yml` still runs the full 6-cell OS x Roslyn matrix and package hygiene check).
2. On `push` to `main`, an unexpired `passed-files-<tree>` artifact already proves the identical Git tree passed on a pull request.

When `UNITY_LICENSE` is present, `unity.yml` runs:

1. EditMode and PlayMode tests across `2022.3.62f3` (LTS baseline), `6000.0.85f1` (Unity 6.0 LTS), and `6000.6.7f1` (Unity 6.6+), failing on any `warning CS` in `UnityPackage`.
2. Fourteen `ManagedStrippingLevel.High` sample builds covering `WebGL` (IL2CPP), `Android` (IL2CPP ARM64), `iOS` (IL2CPP), `StandaloneLinux64` (IL2CPP and Mono2x), `StandaloneOSX` (Mono2x), and `StandaloneWindows64` (Mono2x).
3. Live headless runtime smoke execution of the compiled `2022.3.62f3` and `6000.6.7f1` `StandaloneLinux64` IL2CPP players and `WebGL` IL2CPP players (via Playwright Chromium), asserting the sample hub initializes asynchronously, runs interface and abstract class mocks, and logs `Mocker GettingStarted ready: saves=profile:checkpoint, bus=SFX, volume=0` with zero runtime errors.

### Nightly storage cleanup (`cleanup.yml`)

Queries active workflow runs before deleting anything and exits untouched while another run is active. When idle, sweeps deleted-branch caches/artifacts and items untouched for `olderThanHours` (`24` by default) while preserving `passed-files-*` and failing `test-results-*` artifacts for their 7-day retention.
