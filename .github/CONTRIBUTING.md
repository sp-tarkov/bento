# Contributing to Bento

Thanks for your interest in contributing. Bento is built in the open and welcomes contributions of all kinds: code, documentation, bug reports, feature discussions, and testing.

## Getting started

1. Fork the repository and clone your fork.
2. Run `dotnet tool restore` to install the local tools (CSharpier).
3. Run `dotnet build` to build the CLI.
4. Run `dotnet test` to make sure the suite passes before you start.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [git](https://git-scm.com/install/)
- [git lfs](https://git-lfs.com/)
- [7-Zip](https://www.7-zip.org/download.html)

## Finding work

- Issues labelled `good first issue` are scoped for newcomers.
- Issues labelled `help wanted` are ready for contribution but may require more context.

## Pull request guidelines

- One concern per pull request. Don't mix refactors with features.
- Write tests for new functionality. The pure logic (`BuildRules`, `BentoConfig`, `BuildSettings`, `ModulePackageSource`, `ModulePackageCache`) is unit-tested under `tests/Bento.Tests`; add cases there.
- Run `dotnet test` and `dotnet csharpier check .` before submitting.
- Write a clear PR description explaining what changed and why.
- Reference the issue number if one exists.

## Code style

- Formatting is handled by CSharpier (print width 120) and enforced via `.editorconfig`. Run `dotnet csharpier format .` before committing, and follow the patterns established in the codebase.

## Reporting bugs

Open a GitHub issue with:
- Steps to reproduce
- Expected behaviour
- Actual behaviour
- Bento version, host OS, and how you ran it (local dev build or Docker `--fresh`)

## Code of conduct

Be constructive, be patient, and be kind. Harassment and discrimination are not tolerated. See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for details.
