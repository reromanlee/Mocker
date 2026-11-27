### Continuous integration in detail

Four workflows in `.github/workflows` validate the Roslyn source generator across operating systems, run Unity EditMode/PlayMode tests and sample player builds across Unity `2022.3` LTS and Unity `6`, and sweep stale caches and artifacts nightly.

| Workflow | File | Automatic triggers | Manual trigger | Timeout ceiling | Storage per run |
| --- | --- | --- | --- | --- | --- |
| `DotNet` | `dotnet.yml` | Every push and pull request (ignoring docs and images) | `workflow_dispatch` | 15 min per OS | 0 B on pass (`.trx` on failure, 7d) |
| `Continuous integration` | `continuous-integration.yml` | Pull requests and pushes to `main` (ignoring docs and images) | `workflow_dispatch` | 5 min gate + `Unity` | 41 B `passed-files-*` marker (7d) |
| `Unity` | `unity.yml` | Called via `workflow_call` from `continuous-integration.yml` | Via parent workflow | 60 min tests, 90 min builds | Test logs (7d) + `Library` cache |
| `Cleanup` | `cleanup.yml` | Nightly at `04:00 UTC` | `workflow_dispatch` (`olderThanHours`) | 10 min | Reclaims stale caches and artifacts |

### DotNet fast lane (`dotnet.yml`)

Runs `dotnet build DotNet/Mocker.slnx --configuration Release` on `ubuntu-latest` and `windows-latest` on pushes, adding `macos-latest` on pull requests and manual runs. Every job rebuilds with `-p:UpdateUnityPackage=true` and runs `git diff --exit-code --stat -- UnityPackage/Runtime/Plugins` so any drift between `DotNet/Source/Mocker.Generator/` and `UnityPackage/Runtime/Plugins/reromanlee.Mocker.Generator.dll` fails CI immediately before running the NUnit `3.14.0` test suite.

### Tree-SHA-gated Unity suite (`continuous-integration.yml` and `unity.yml`)

The `gate` job checks two conditions before launching Unity runners:

1. License guard: when `UNITY_LICENSE` is not configured (such as on forks), the job logs a `::notice::` and skips `unity.yml` cleanly.
2. Tree-SHA deduplication: on `push` to `main`, if a `passed-files-<tree>` artifact already exists from an earlier pull request with the identical Git tree SHA, Unity jobs are skipped without re-billing runner minutes.

When executed, `unity.yml` runs EditMode and PlayMode tests on `2022.3.62f3` and `6000.6.7f1`, enforces zero `warning CS` diagnostics in `UnityPackage`, and builds `Samples~/GettingStarted` with `ManagedStrippingLevel.High` for `WebGL`, `Android`, and `StandaloneLinux64`.

### Nightly storage cleanup (`cleanup.yml`)

Before deleting anything, `cleanup.yml` queries active workflow runs and exits untouched whenever any other run is queued or in progress. When idle, it removes caches and artifacts belonging to deleted branches and entries older than `olderThanHours` (`24` by default) while preserving `passed-files-*` markers and `test-results-*` failure logs for their 7-day retention.
