<p align="center">
<img src="https://raw.githubusercontent.com/sp-tarkov/bento/main/.github/images/banner.png" alt="Bento">
</p>

[![Build](https://github.com/sp-tarkov/bento/actions/workflows/tests.yml/badge.svg)](https://github.com/sp-tarkov/bento/actions/workflows/tests.yml)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/)
[![Licence](https://img.shields.io/badge/licence-AGPL--3.0-blue.svg)](../LICENSE)

Bento is a .NET 10 command-line tool that builds and packages a Single Player Tarkov release. It builds the server, modules, and launcher, assembles them with static assets, and writes the archive alongside a `manifest.json`.

Supported SPT versions: `> 4.1.0`

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [git](https://git-scm.com/install/)
- [git lfs](https://git-lfs.com/)
- [7-Zip](https://www.7-zip.org/download.html)

### Building from Source

Clone the repository and build it with the .NET SDK:

```
git clone https://github.com/sp-tarkov/bento.git
cd bento
dotnet build
```

The compiled executable lands in `bin/Debug/net10.0/`:

- Windows: `bin\Debug\net10.0\bento.exe`
- Linux / macOS: `bin/Debug/net10.0/bento`

For an optimised build, pass `-c Release`; the output then lives under `bin/Release/net10.0/` instead.

#### Running directly with `dotnet run`

To compile and run in a single step without locating the binary use `dotnet run`. Restore is implicit, but you can run it explicitly first, if you're into that.

### Usage

#### Development Usage

Builds your local working copies. The first run walks you through a short setup (repo paths, module package source, output directory) and saves it. Every run asks for the version and build type, pre-filled with your last-used values, and shows a summary to confirm before building.

Any value can be supplied as a flag:

```
bento --version 4.1.0 --build-type DEBUG --server ..\server-csharp --modules ..\modules --launcher ..\launcher
```

#### Fresh Mode (`--fresh`, or via Docker)

Builds a clean release straight from GitHub at a given tag, with no local repos involved. Intended to run in Docker for distribution builds.

```
docker build -t bento .
docker run --rm \
  -v ./dist:/out \
  -v bento-cache:/root/.bento/cache \
  -v bento-nuget:/root/.nuget/packages \
  -e MODULE_PACKAGE=https://example.com \
  bento --fresh --tag 4.1.0 --output /out
```

### Options

| Flag                            | Description                                                                                                       |
|---------------------------------|-------------------------------------------------------------------------------------------------------------------|
| `--fresh`                       | Clone all three repos from GitHub at `--tag` and build there. Requires `--tag`.                                   |
| `--tag <TAG>`                   | Git tag to build. Must exist in all three repos; also supplies the version and build type.                        |
| `--version <VERSION>`           | SPT version of the build (e.g. `4.1.0`).                                                                          |
| `--build-type <TYPE>`           | `RELEASE`, `DEBUG`, `BLEEDINGEDGE`, or `BLEEDINGEDGEMODS`.                                                        |
| `--server <DIR>`                | Path to the local server-csharp repo. Not needed for `--fresh`.                                                   |
| `--modules <DIR>`               | Path to the local modules repo. Not needed for `--fresh`.                                                         |
| `--launcher <DIR>`              | Path to the local launcher repo. Not needed for `--fresh`.                                                        |
| `--module-package <URL_OR_DIR>` | Module package source: an http base URL or a local directory. Overrides `MODULE_PACKAGE` and config.              |
| `--output <DIR>`                | Where the `.7z`, `manifest.json`, and logs land. Default: `./dist`.                                               |
| `--no-config`                   | Ignore the user config file (automatic in containers); every value must come from flags or environment variables. |
| `--help`                        | List every flag.                                                                                                  |

### Environment

| Variable         | Description                                                                                                                                          |
|------------------|------------------------------------------------------------------------------------------------------------------------------------------------------|
| `MODULE_PACKAGE` | Module package source used when `--module-package` is not passed: an http base URL or a local directory. Takes precedence over the configured value. |

### Output

A successful build writes the `.7z` archive, a `manifest.json`, and per-stage build logs to the output directory.

### Notes

- **Build on Linux (Docker) for distribution.** A `.7z` created on Windows can't carry the Unix executable bit for the Linux binaries. Windows builds are fine for personal and dev use; for releases, build in the container.
- **Config and cache** live under `~/.bento`. Both are safe to delete at any time.
