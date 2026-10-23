# 10. Projects, solutions & NuGet

## What & why

A one-file program grows into several: a library with the logic, an executable that's a thin shell, a test project. This lesson covers how .NET organizes code (**projects**, **solutions**, **assemblies**, **namespaces**) and how to use other people's code with **NuGet**. It's also the "library plus thin executable" structure that every capstone from now on uses.

## Vocabulary

- **Project** (`.csproj`): one buildable unit; produces one **assembly**.
- **Assembly**: a `.dll` (library) or `.exe`/`.dll` with an entry point (application).
- **Solution** (`.sln` or `.slnx`): a group of projects built together.
- **Namespace**: a name prefix that organizes types (`System.IO`); independent of the folder layout, though the two usually match.
- **Package**: a versioned bundle of assemblies from NuGet (`Newtonsoft.Json`, `Serilog`).
- **Target framework (TFM)**: which .NET you build for, such as `net10.0`.

## The project file

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Serilog" Version="4.2.0" />
    <ProjectReference Include="..\MyLib\MyLib.csproj" />
  </ItemGroup>

</Project>
```

SDK-style projects automatically include every `.cs` file beneath the project folder; no file lists. Omit `<OutputType>` for a library.

## A multi-project layout

```bash
mkdir shop && cd shop
dotnet new sln -n Shop                             # creates Shop.slnx (or Shop.sln)
dotnet new classlib -o src/Shop.Core
dotnet new console  -o src/Shop.Cli
dotnet new xunit    -o tests/Shop.Core.Tests

dotnet sln add src/Shop.Core src/Shop.Cli tests/Shop.Core.Tests
dotnet add src/Shop.Cli reference src/Shop.Core
dotnet add tests/Shop.Core.Tests reference src/Shop.Core
```

```
shop/
├── Shop.slnx
├── src/
│   ├── Shop.Core/        # pure logic: no Console, no file access if possible
│   └── Shop.Cli/         # thin executable: arguments, output
└── tests/
    └── Shop.Core.Tests/
```

Now `dotnet build`, `dotnet test` and `dotnet run --project src/Shop.Cli` work from the root.

Keep the dependency arrows pointing one way: **Cli → Core**, **Tests → Core**; never Core → Cli.

## Accessibility across projects

Types are `internal` by default, which means visible only inside their own assembly. To use a class from another project, make it `public`. To let a test project see `internal` types:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Shop.Core.Tests" />
</ItemGroup>
```

Prefer testing through the public API; use this sparingly.

## Namespaces

```csharp
namespace Shop.Core;                // file-scoped: applies to the whole file

public class Cart { }
```

```csharp
using Shop.Core;                    // bring a namespace into scope
using Json = System.Text.Json.JsonSerializer;   // alias
using static System.Math;           // call Sqrt(...) without Math.

global using System.Text;           // visible in every file of the project
```

`ImplicitUsings` supplies a hidden `global using` list. Make namespaces match the folder structure; tooling and readers expect it.

## NuGet

```bash
dotnet add package Serilog                       # latest stable
dotnet add package Serilog --version 4.2.0
dotnet remove package Serilog
dotnet list package                              # what's installed
dotnet list package --outdated
dotnet list package --vulnerable                 # known security advisories
dotnet restore                                   # download everything
```

Packages come from [nuget.org](https://www.nuget.org/) by default. Choose them carefully: look at download counts, maintenance, license and whether they pull in many dependencies. Every package is code you're running.

### Versions

NuGet versions follow SemVer: `MAJOR.MINOR.PATCH`. A `Version="4.2.0"` means "4.2.0 or higher" (the lowest applicable version is chosen). Pin or float deliberately; `*` and ranges exist but make builds less reproducible. Consider **central package management**: a `Directory.Packages.props` file with all versions in one place.

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="xunit" Version="2.9.2" />
  </ItemGroup>
</Project>
```

and `<PackageReference Include="xunit" />` in each project, with no version.

## Shared build settings: `Directory.Build.props`

Put settings every project should share in one file at the repo root:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

MSBuild picks it up automatically for every project below it.

## Analyzers and formatting

Add a `.editorconfig` for style rules, and turn on analyzers:

```xml
<PropertyGroup>
  <AnalysisMode>Recommended</AnalysisMode>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
</PropertyGroup>
```

```bash
dotnet format                  # apply formatting and fixable style rules
dotnet format --verify-no-changes   # check only (good for CI)
```

## Configurations and output

- `dotnet build` is `Debug` by default (no optimization, full debug info). `dotnet build -c Release` is the optimized build.
- Output lands in `bin/<Configuration>/<TFM>/`; intermediates in `obj/`. Both belong in `.gitignore`.
- `dotnet publish -c Release -o out` produces a folder you can deploy. Add `-r linux-x64 --self-contained` to bundle the runtime, or `-p:PublishSingleFile=true` for one file.

## Useful `dotnet` commands

| Command | Purpose |
| --- | --- |
| `dotnet new list` | available templates |
| `dotnet build` / `dotnet run` / `dotnet test` | the daily loop |
| `dotnet watch run` | rebuild and rerun on save |
| `dotnet clean` | remove build output |
| `dotnet tool install -g dotnet-counters` | install a global tool |
| `dotnet nuget locals all --clear` | clear package caches |

## Common mistakes

- **Putting all code in the executable project,** which makes it untestable. Move logic into a library.
- **Circular project references** (not allowed; extract a third project).
- **Committing `bin/` and `obj/`.**
- **Different packages versions across projects** causing odd runtime behavior. Centralize them.
- **Forgetting `public`** on a type used from another project.
- **Adding a package for a single helper** you could write in five lines.

## Exercises

1. Create the `shop` layout above; put a `Cart` class in Core and print a total from Cli.
2. Add a `Directory.Build.props` that enables nullable and warnings-as-errors for all projects.
3. Add a NuGet package, use one of its APIs, then run `dotnet list package --vulnerable`.
4. Switch to central package management and verify that the build still works.
5. Publish the CLI as a self-contained single file for your platform and run it from another folder.
6. Add a `.editorconfig`, run `dotnet format`, and review the diff.

## Recap

- Projects produce assemblies; solutions group projects; namespaces organize types.
- The library-plus-thin-executable split keeps logic testable.
- NuGet brings packages; `dotnet add package`, `list package --vulnerable` and central package management keep them safe and consistent.
- `Directory.Build.props`, `.editorconfig` and `dotnet format` share settings across a repo.

Next: [Testing →](11-testing.md)
