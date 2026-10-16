# Benchmarks

Two BenchmarkDotNet suites, both on .NET 10:

| Suite | Cases | Measures |
| --- | --- | --- |
| `RequestFlow.Benchmarks.MediatR` | 224 | RequestFlow against MediatR 12.5.0 in one executable, with matching workloads and service lifetimes. See the [MediatR comparison](RequestFlow.Benchmarks.MediatR/README.md). |
| `RequestFlow.Benchmarks` | 34 | RequestFlow alone: Task vs ValueTask paths and stage chains. |

- A case is one benchmark method with one parameter set.
- Published results: [Benchmarks vs MediatR](../docs/benchmarks/mediatr/README.md), from the 2026-10-15 full run.
- RequestFlow is built from the checked-out source, so record the commit when sharing results.
- BenchmarkDotNet 0.15.8 and Microsoft DI 10.0.9. Package lock files pin the resolved dependencies.
- CI builds both suites but does not run them.

## Quick start

Requires the .NET 10 SDK. Run from the repository root:

```powershell
dotnet restore benchmarks/RequestFlow.Benchmarks.MediatR -p:RestoreLockedMode=true
dotnet build benchmarks/RequestFlow.Benchmarks.MediatR -c Release --no-restore

# Check results, execution order, lifetimes, tokens, and disposal without timing.
dotnet run --project benchmarks/RequestFlow.Benchmarks.MediatR -c Release -f net10.0 --no-build --no-restore -- probe

# Rough timings for one group. Not publishable.
dotnet run --project benchmarks/RequestFlow.Benchmarks.MediatR -c Release -f net10.0 --no-build --no-restore -- --quick --filter '*TaskRetainedScopeBenchmarks*'

# Full run of every case into an unused directory.
dotnet run --project benchmarks/RequestFlow.Benchmarks.MediatR -c Release -f net10.0 --no-build --no-restore -- --runtimes net10.0 --filter '*' --artifacts benchmarks/artifacts/YYYY-MM-DD-mediatr-full
```

- These commands build only the suite and the `net10.0` build of RequestFlow. To build the whole solution, run `dotnet restore -p:RestoreLockedMode=true` and `dotnet build -c Release --no-restore`.
- Single-quoted filters work in PowerShell and bash. In cmd.exe, use double quotes.

## RequestFlow suite

```powershell
dotnet restore benchmarks/RequestFlow.Benchmarks -p:RestoreLockedMode=true
dotnet build benchmarks/RequestFlow.Benchmarks -c Release --no-restore

# Verify the workloads without timing.
dotnet run --project benchmarks/RequestFlow.Benchmarks -c Release -f net10.0 --no-build --no-restore -- probe

# Rough timings for Task and ValueTask void dispatch.
dotnet run --project benchmarks/RequestFlow.Benchmarks -c Release -f net10.0 --no-build --no-restore -- --quick --filter '*VoidCompletionBenchmarks*'
```

- For a full run, use `--filter '*'` without `--quick`.
- Each class uses the Task path as the baseline for the ValueTask path.
- `ChainLevelBenchmarks` bypasses the dispatcher. It enters the Task, ValueTask, and void plans that `AddRequestFlow` froze with 0, 1, or 5 stages.
- Void completion and short-circuit benchmarks dispatch through RequestFlow.
- Rows use the shared fixture handlers and stages, so the handler work matches the comparison suite.

## Run modes

| Argument | What it does |
| --- | --- |
| `probe` | Checks every workload without timing. A pass ends with `Probe passed:` and the number of classes, parameter sets, and cases checked. |
| `--smoke` | Runs each case once to check it executes. |
| `--quick` | Rough timings. Not publishable. |
| `--filter` without `--smoke` or `--quick` | Runs `probe`, then the full profile: two launches, 15 to 50 warm-up and 60 to 100 measured iterations, 5% relative error target. |

- Without arguments, `dotnet run` applies the MediatR suite's first launch profile, **Verify comparison**, so it only runs `probe`.
- The RequestFlow suite has no launch profile. Without arguments it runs `probe`, then shows BenchmarkDotNet's selection prompt.

## Filters

- `--filter '*'` selects every case.
- When narrowing, keep both libraries selected within each scenario.
- Pass several patterns to one `--filter` to combine scenarios: `--filter '*MediatRStream*' '*MediatRVoidRequest*'`.

| MediatR scenario | Filter | Cases |
| --- | --- | --- |
| Task requests | `*MediatRTask*Scope*` | 36 |
| Void requests | `*MediatRVoidRequest*` | 24 |
| Streams | `*MediatRStream*` | 72 |
| Sequential events | `*MediatREventSequential*` | 16 |
| Parallel events | `*MediatREventParallel*` | 16 |
| Catalog size | `*MediatRCatalogSize*` | 18 |
| Mixed traffic | `*MediatRMixedTraffic*` | 18 |
| Short circuit | `*MediatRShortCircuit*` | 18 |
| Direct-handler reference | `*MediatRTaskDirectHandler*` | 6 |

## Reports

- `--artifacts <dir>` sets the output directory. Without it, reports go to `BenchmarkDotNet.Artifacts/<suite>/<tier>/<run>/` under the current directory, where `<suite>` is `mediatr` or `requestflow`.
- Readable tables are the `results/*-report-github.md` files. `run-manifest.json` records the run status and host details.
- Git ignores `BenchmarkDotNet.Artifacts/` and `benchmarks/artifacts/`.

## Publishing a run

Publish only a run whose `run-manifest.json` shows `status: completed` and `publicationCandidate: true`. That requires:

- the full profile, with no measurement overrides
- a passing probe
- Git can read HEAD and the status of every build input
- a clean Git status for every build input, with HEAD and that status unchanged from start to end of the run
- no `DOTNET_*`, `COMPlus_*`, or `CORECLR_*` environment variables, except the SDK and host settings the harness ignores

Before the run:

- Build the suite after your last commit. A full run fails before timing anything when the build's commit differs from HEAD.
- Delete stray files under `src/`, the suite folder, and each benchmark project the suite references, such as old reports and `.csproj.user` files. The source check includes ignored files and skips only `bin` and `obj`.
- Start without a processor affinity mask. The jobs set none, but child processes inherit any mask set with `start /affinity`, `taskset`, or a similar tool.
- Leave the machine otherwise idle.

After the run:

- Check that `hostEnvironment.processorCount` in the manifest equals the logical core count in the report header. `publicationCandidate` does not check affinity, and parallel event and suspending rows depend on how many cores the process can use.
- A run stopped with Ctrl+C stays at `status: running` and is not publishable.

## Visual Studio

Use Visual Studio for exploration only. Publish from the command line.

1. Open `RequestFlow.slnx` in a Visual Studio version that supports the .NET 10 SDK and `.slnx` solutions.
2. In the `benchmarks` solution folder, set `RequestFlow.Benchmarks.MediatR` as the startup project.
3. Select the **Release** configuration.
4. Pick a launch profile: **Verify comparison** (`probe`), **Smoke test** (`--smoke` on the Task retained-scope class only), or **Full comparison** (`--filter *`).
5. Start without debugging (**Ctrl+F5**).

- Reports go under `BenchmarkDotNet.Artifacts/mediatr/` at the repository root, because the profiles set `workingDirectory` to `../..`. `dotnet run` ignores that setting.
- A Visual Studio run is never a publication candidate. Visual Studio saves the selected profile in `RequestFlow.Benchmarks.MediatR.csproj.user`, which the source check flags.

## Projects

All five are in the root solution.

| Project | Contents |
| --- | --- |
| `RequestFlow.Benchmarks` | Stage-chain measurements, Task vs ValueTask dispatch, and probes |
| `RequestFlow.Benchmarks.MediatR` | MediatR fixtures, paired benchmarks, and probes. Does not depend on `RequestFlow.Benchmarks`. |
| `RequestFlow.Benchmarks.Harness` | BenchmarkDotNet configuration, runner, run manifest, and provenance records. Knows nothing about workloads. |
| `RequestFlow.Benchmarks.Core` | Probe engine, trace names, provider hosts, shared workloads, and the short-circuit dependency graph |
| `RequestFlow.Benchmarks.RequestFlowFixtures` | RequestFlow handlers, stages, registration, and message catalogs |

- Benchmark classes are public and inheritable so BenchmarkDotNet can generate its harness.
