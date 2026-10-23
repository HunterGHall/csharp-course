# 11. Packaging, AOT & distribution

## What & why

Code that only runs on your machine isn't finished. This lesson covers the last mile: designing a stable public API, packaging a library as a **NuGet package**, shipping applications as self-contained single files or **Native AOT** binaries or containers, and automating it all with CI.

## Designing a library's public API

Once published, every `public` member is a promise.

- Keep the surface **small**; make everything else `internal`.
- Seal classes unless designed for inheritance; prefer interfaces and records.
- Use `required`/`init` and factory methods for construction; avoid exposing mutable collections.
- Accept general parameter types (`IEnumerable<T>`, `ReadOnlySpan<char>`), and return specific ones.
- Provide `CancellationToken` overloads for async methods.
- Throw documented exceptions with clear messages. Don't return `null` where an empty collection will do.
- Follow the [.NET Framework Design Guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/): naming, casing, `Async` suffix, `IDisposable` patterns.
- Document with XML comments, and fail the build if public members lack them:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <!-- CS1591: missing XML comment on a publicly visible member -->
  <WarningsAsErrors>$(WarningsAsErrors);CS1591</WarningsAsErrors>
</PropertyGroup>
```

```csharp
/// <summary>Converts a title to a URL-friendly slug.</summary>
/// <param name="title">The text to convert. Must not be <see langword="null"/>.</param>
/// <returns>A lowercase, dash-separated slug.</returns>
/// <exception cref="ArgumentNullException"><paramref name="title"/> is <see langword="null"/>.</exception>
public static string Slugify(string title) { /* ... */ }
```

### Tracking API changes

Add the `Microsoft.CodeAnalysis.PublicApiAnalyzers` package. It keeps `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` files, and fails the build when you add, remove or change a public member without recording it. Reviews then show API changes in plain text. Tools like `Microsoft.DotNet.ApiCompat` (package validation) compare against the previous version:

```xml
<EnablePackageValidation>true</EnablePackageValidation>
<PackageValidationBaselineVersion>1.2.0</PackageValidationBaselineVersion>
```

## Semantic versioning

`MAJOR.MINOR.PATCH`:

- **PATCH**: bug fixes, no API change.
- **MINOR**: backward-compatible additions.
- **MAJOR**: breaking changes.

Breaking includes subtler things: changing a parameter's default value, adding an abstract member to an interface (use default interface methods or a new interface), changing behavior callers depend on, or changing the target framework. Pre-releases look like `2.0.0-beta.1`. Tools such as **MinVer** or **Nerdbank.GitVersioning** derive the version from git tags.

## Creating a NuGet package

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <PackageId>Acme.Slugs</PackageId>
    <Version>1.0.0</Version>
    <Authors>Acme</Authors>
    <Description>Fast, allocation-light slug generation.</Description>
    <PackageTags>slug;url;text</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <RepositoryUrl>https://github.com/acme/slugs</RepositoryUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <IsTrimmable>true</IsTrimmable>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>
  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="/" />
  </ItemGroup>
</Project>
```

```bash
dotnet pack -c Release -o artifacts
dotnet nuget push artifacts/Acme.Slugs.1.0.0.nupkg --source https://api.nuget.org/v3/index.json --api-key $NUGET_KEY
```

Best practices:

- **Multi-target** (`net8.0;net10.0`, and `netstandard2.0` if you need to reach .NET Framework) with `#if` blocks where APIs differ.
- **Source Link + symbols** (`snupkg`) let users step into your code in the debugger.
- **Deterministic builds** (`ContinuousIntegrationBuild`) make the same source produce the same binary.
- **Sign** packages, or use **Trusted Publishing** from GitHub Actions so no long-lived API key sits in a secret.
- Include a README, license, and release notes.
- Never publish a bad version you can't take back: nuget.org lets you only *unlist*, not delete. Test packages from a local feed first: `dotnet nuget add source ./artifacts --name local`.

## Shipping applications

`dotnet publish` produces the files to deploy. Choices:

| Style | Command / property | Notes |
| --- | --- | --- |
| **Framework-dependent** | `dotnet publish -c Release` | smallest; needs the .NET runtime installed |
| **Self-contained** | `-r linux-x64 --self-contained` | bundles the runtime (~70 MB, trimmable) |
| **Single file** | `-p:PublishSingleFile=true` | one executable, still managed code |
| **Trimmed** | `-p:PublishTrimmed=true` | removes unused framework code |
| **ReadyToRun** | `-p:PublishReadyToRun=true` | pre-compiled native code for faster start-up |
| **Native AOT** | `-p:PublishAot=true` | no JIT, no runtime: tiny, instant start |

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true -o out
```

### Native AOT

Ahead-of-time compilation turns your program into a native executable with no JIT:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>   <!-- optional: drops ICU data -->
</PropertyGroup>
```

Benefits: start-up in milliseconds, small memory footprint, no runtime dependency. Costs:

- **No run-time code generation:** `Reflection.Emit`, `Expression.Compile` (interpreted instead), `Assembly.LoadFrom` don't work.
- **Reflection is limited:** the trimmer must see which members you use. Prefer source generators (`System.Text.Json` contexts, `[GeneratedRegex]`, `LibraryImport`).
- **Per-OS builds:** you compile on the target OS and architecture.
- Libraries need to be AOT-compatible (`IsAotCompatible` + analyzers produce IL3050/IL2026 warnings you must resolve).

Great fits: CLI tools, serverless functions, sidecars, small microservices. Not great: heavy-reflection frameworks, plug-in systems.

## Containers

The official SDK can build an image without a Dockerfile:

```bash
dotnet publish -c Release /t:PublishContainer -p:ContainerRepository=acme/api
```

or write a multi-stage `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Api.dll"]
```

Run as a non-root user, keep images small (use `-alpine` or chiseled variants), and pin versions.

## Continuous integration with GitHub Actions

```yaml
name: CI
on: [push, pull_request]

jobs:
  test:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore --locked-mode
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-build --collect:"XPlat Code Coverage"
      - run: dotnet format --verify-no-changes

  release:
    needs: test
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: 10.0.x }
      - run: dotnet pack -c Release -o artifacts
      - run: dotnet nuget push "artifacts/*.nupkg" --api-key ${{ secrets.NUGET_KEY }} --source https://api.nuget.org/v3/index.json
```

Tips:

- Build on every OS you support. This course's own capstones do exactly that.
- Use **lock files** (`<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`) for reproducible restores.
- Cache the NuGet package folder (`actions/cache` or `setup-dotnet`'s `cache: true`).
- Scan dependencies: `dotnet list package --vulnerable --include-transitive`, Dependabot, or GitHub's dependency review.
- Build docs with DocFX, and publish them to GitHub Pages.

## Supply-chain hygiene

- Use few, well-known dependencies; review what you add.
- Enable **NuGet audit** (on by default) and treat `NU1903` warnings seriously.
- Use package source mapping so internal package names can't be hijacked from nuget.org.
- Prefer SourceLink-enabled, signed, reproducible packages.
- Keep the SDK and runtime patched; subscribe to security announcements.

## Common mistakes

- **Breaking changes in a minor release.**
- **Publishing without a README, license or symbols.**
- **Shipping a Debug build,** or a package that depends on a prerelease dependency by accident.
- **Native AOT without testing the published binary.** Run tests against it, because trimming and AOT warnings are not errors by default.
- **Hard-coded paths and OS assumptions** in tools you distribute.
- **Secrets in CI logs or repo files.**
- **Not testing on each target OS and .NET version.**

## Exercises

1. Turn the link checker capstone's `LinkCheckKit` into a NuGet package with a README, license and symbols; install it from a local feed into a new project.
2. Add the PublicApi analyzers to it; add a method and watch the build tell you to record it.
3. Publish the link checker as a trimmed, self-contained single file; compare its size and start-up time with the framework-dependent build.
4. Publish it with Native AOT. What warnings do you get? Fix them or explain why they can't be fixed.
5. Containerize an ASP.NET minimal API with a multi-stage Dockerfile; run it as a non-root user.
6. Create a GitHub Actions workflow that builds, tests and packs on three operating systems, then pushes on a version tag.

## Recap

- A library's public API is a contract: small, documented, semver-versioned, tracked by analyzers.
- `dotnet pack` creates NuGet packages; Source Link, symbols and deterministic builds make them debuggable and trustworthy.
- `dotnet publish` offers framework-dependent, self-contained, single-file, trimmed, ReadyToRun and Native AOT outputs; each trades size, start-up and compatibility.
- CI on all your target platforms, with audits and lock files, keeps releases boring.

Next: [Capstone: parallel file indexer →](12-capstone-parallel-indexer.md)
