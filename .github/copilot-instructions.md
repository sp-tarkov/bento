# Copilot Coding Agent Instructions

## Project Summary

Bento is a .NET 10 command-line tool (`bento`) that builds and packages a Single Player Tarkov (SPT) release. It builds three repositories — server (`server-csharp`), modules, and launcher, assembles their outputs with static assets from `assets/`, and writes a `.7z` archive plus a `manifest.json` to the output directory.

**Stack:** .NET 10, C# (nullable enabled, implicit usings), Spectre.Console + Spectre.Console.Cli for the CLI, NUnit for tests, CSharpier for formatting. Ships a `Dockerfile` for fresh/distribution builds.

## Build, Test, and Validate

Always run commands from the repository root.

### Restore tools (once)

```
dotnet tool restore
```

Installs the local CSharpier tool pinned in `dotnet-tools.json`.

### Build

```
dotnet build
```

### Run

```
dotnet run -- --version 4.1.0 --build-type DEBUG --server ..\server-csharp --modules ..\modules --launcher ..\launcher
```

### Test

```
dotnet test
```

Runs the NUnit suite under `tests/Bento.Tests`. Filter by class or name:

```
dotnet test --filter FullyQualifiedName~BuildRulesTests
dotnet test --filter Name~ArchiveBaseName
```

### Format

```
dotnet csharpier format .   # apply formatting
dotnet csharpier check .    # verify formatting (CI runs this)
```

Print width is 120, enforced via `.editorconfig`. Always format before committing.

### Full validation sequence (matches CI)

Run these in order before considering a change complete:

```
dotnet csharpier check .
dotnet build -c Release -warnaserror
dotnet test
```

## CI Workflows (`.github/workflows/`)

| Workflow | File | What it checks |
|---|---|---|
| **Format** | `format.yml` | `dotnet csharpier check .` |
| **Quality** | `quality.yml` | Release build with warnings treated as errors |
| **Tests** | `tests.yml` | `dotnet test` |
| **Vulnerability** | `vulnerability.yml` | `dotnet list package --vulnerable --include-transitive` |
| **CodeQL** | `codeql.yml` | CodeQL analysis (`csharp`, `actions`) |
| **Release** | `release.yml` | On a `v*` tag: build & push the Docker image to GHCR and create a GitHub Release |

## Project Layout

```
Program.cs                 Entry point; hosts BuildCommand as the default Spectre.Console.Cli command
Bento.csproj               Main project (assets ship via CopyToOutputDirectory)
Dockerfile                 Multi-stage image for fresh/distribution builds
assets/                    Static payload (BepInEx, ConfigurationManager, doorstop) overlaid onto every release
src/
  BuildCommand.cs          Orchestrates the whole flow (RunAsync)
  BuildSettings.cs         All CLI flags
  BuildContext.cs          The bag passed to every step; exposes derived paths and values
  BuildRules.cs            Pure logic: tag -> build type, build type -> MSBuild config, version validation, archive names
  BentoConfig.cs           User config persisted under ~/.bento
  RuntimeEnv.cs            Run-mode detection (interactive vs fully-flagged / container)
  InteractiveSetup.cs      Prompts (interactive mode only)
  Preflight.cs             Tool checks (.NET SDK, git, git lfs, 7-Zip)
  RepoScanner.cs           Repo inspection and marker-file heuristics
  ProcessRunner.cs         Streaming and capturing process runners (never a shell)
  BuildLogger.cs           Per-stage log files plus a StatusSink for the progress display
  Fs.cs                    Directory copy/delete/size helpers
  SevenZip.cs              .7z archive creation
  ModulePackageSource.cs   Module package source parsing (http URL or local dir)
  ModulePackageCache.cs    Module package cache under ~/.bento/cache, keyed by client version
  Manifest.cs              manifest.json model
  Steps/
    FreshCloneStep.cs      --fresh: clone all three repos at --tag into a temp dir
    ServerStep.cs          Build server (publishes win-x64 then linux-x64; they share obj/)
    ModulesStep.cs         Build modules
    LauncherStep.cs        Build launcher
    AssembleStep.cs        Merge outputs into the release tree, prune strays, set exec bits, overlay assets/
    PackageStep.cs         Produce the .7z and manifest.json
tests/Bento.Tests/         NUnit tests for the pure logic
```

## Architecture and Conventions

- **Single command**: `Program.cs` hosts `BuildCommand` as the default Spectre.Console.Cli command; all flags live in `BuildSettings`.
- **Flow** (`BuildCommand.RunAsync`): detect runtime env -> load config -> preflight tool checks -> resolve build inputs -> show summary (confirm if interactive) -> run the pipeline -> report.
- **Controlled failure**: throw `BentoException` (with an optional `Hint`); it is caught at the top and rendered as `error:` / `hint:` with exit code 1. Use it for all expected failures.
- **Run modes** (`RuntimeEnv.Detect`): *interactive* (a TTY, not a container) prompts and persists choices to the config file under `~/.bento`; *fully-flagged* (`--no-config`, or `DOTNET_RUNNING_IN_CONTAINER` set by the Docker base image) never prompts and fails fast on missing input.
- **Input resolution** (`ResolveAndInspectAsync`): fixed priority orders — version and build type are `flag > tag > prompt`; module package is `flag > MODULE_PACKAGE env > config`; repo paths are `flag > config > prompt`. Everything resolved lands in a single `BuildContext`.
- **Pipeline**: `ServerStep`, `ModulesStep`, and `LauncherStep` run concurrently (`RunBuildStagesAsync`); each exposes a `const string Stage` used as its log channel and progress-bar key. Then `AssembleStep`, then `PackageStep`.
- **Build-type rules live in `BuildRules`** — the most logic-heavy, pure, and well-tested code. Start there for naming or tag changes, and add cases to `BuildRulesTests`.
- **Static assets**: reference them via `AppContext.BaseDirectory`, never by source path. They ship next to the binary through `CopyToOutputDirectory` and are overlaid onto every release by `AssembleStep`.
- **Build on Linux (Docker) for distribution**: a `.7z` made on Windows can't carry the Unix exec bit for the Linux binaries. Windows builds are fine for dev and personal use.
- **Tests** cover the pure logic (`BuildRules`, `BentoConfig`, `BuildSettings`, `ModulePackageSource`, `ModulePackageCache`); the build steps themselves are not unit-tested. The test project lives under `tests/` and is excluded from the main project's compile globs.

## Trust These Instructions

These instructions reflect the current repository layout. Trust them and only search the codebase if the information here is incomplete or produces an error.
