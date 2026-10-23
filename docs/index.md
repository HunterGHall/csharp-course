# The C# Course

A complete, self-paced C# curriculum in Markdown, from your first `Console.WriteLine` to async pipelines, `Span<T>` and Native AOT. Work through it top to bottom, or jump to the level that matches your experience.

## How this course is organized

| Folder | For you if... | Outcome |
| --- | --- | --- |
| [Beginner](beginner/index.md) | You have never written C# (or never finished a course) | Write and run real programs, understand the core language, build a command-line app that saves data |
| [Intermediate](intermediate/index.md) | You know the syntax but classes vs structs vs records, interfaces, generics, LINQ and testing still feel shaky | Object design, pattern matching, generics, LINQ, resource management, JSON, projects and NuGet, tests, a real project |
| [Advanced](advanced/index.md) | You ship C# and want depth | Type design, advanced generics, reflection, source generators, async, threading, channels, `Span<T>`, profiling, interop, AOT, a concurrent capstone |

Each lesson is one file and follows the same shape:

1. **What & why**: the idea in plain language
2. **Examples**: code you should type out yourself
3. **Common mistakes**: the traps everyone hits
4. **Exercises**: do these; reading is not learning
5. **Recap**

Each level ends with a **capstone** project. The complete, buildable source for all three lives in the [`capstones/`](https://github.com/HunterGHall/csharp-course/tree/main/capstones) folder of the repository, and CI builds and tests them on Linux, Windows and macOS.

## Setup (do this first)

You need the **.NET SDK 10.0 or newer** (C# 14). It runs on Windows, macOS and Linux, and you do not need Visual Studio.

```bash
dotnet --version
```

- **Windows**: `winget install Microsoft.DotNet.SDK.10`
- **macOS**: `brew install --cask dotnet-sdk`, or the installer from [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
- **Linux**: your package manager (for example `sudo apt install dotnet-sdk-10.0`) or the [install guide](https://learn.microsoft.com/dotnet/core/install/linux)

A good editor helps a lot: [VS Code](https://code.visualstudio.com/) with the *C# Dev Kit* extension works on every platform; Visual Studio and JetBrains Rider are full IDEs.

Every example in this course is meant to be run either as a one-file script or inside a project:

```bash
dotnet run scratch.cs               # run a single file (.NET 10+)
dotnet new console -o demo          # create a project
dotnet run                          # build and run it
dotnet test                         # run its tests
```

## How to study

- **Type every example.** Copy-paste teaches you nothing.
- **Read the compiler errors.** C#'s messages are precise; start at the *first* one.
- **Break things on purpose.** Change a line, predict the result, compile, run.
- **Do the exercises before looking anything up.**
- **One lesson per sitting** is plenty. Consistency beats marathons.

## Curriculum at a glance

### Beginner
1. [Setup & your first program](beginner/01-setup-and-first-program.md)
2. [Variables & types](beginner/02-variables-and-types.md)
3. [Numbers & math](beginner/03-numbers-and-math.md)
4. [Strings & characters](beginner/04-strings.md)
5. [Booleans & conditionals](beginner/05-booleans-and-conditionals.md)
6. [Loops](beginner/06-loops.md)
7. [Arrays & lists](beginner/07-arrays-and-lists.md)
8. [Dictionaries & sets](beginner/08-dictionaries-and-sets.md)
9. [Methods](beginner/09-methods.md)
10. [Nullable values](beginner/10-nullable.md)
11. [Files & exceptions](beginner/11-files-and-exceptions.md)
12. [Capstone: command-line task tracker](beginner/12-capstone-task-tracker.md)

### Intermediate
1. [Classes, structs & records](intermediate/01-classes-structs-records.md)
2. [Properties & members](intermediate/02-properties-and-members.md)
3. [Enums & pattern matching](intermediate/03-enums-and-pattern-matching.md)
4. [Interfaces & inheritance](intermediate/04-interfaces-and-inheritance.md)
5. [Generics](intermediate/05-generics.md)
6. [Delegates, lambdas & LINQ](intermediate/06-delegates-lambdas-linq.md)
7. [Memory, GC & disposal](intermediate/07-memory-gc-and-disposal.md)
8. [Exceptions in depth](intermediate/08-exceptions-in-depth.md)
9. [JSON & serialization](intermediate/09-json-and-serialization.md)
10. [Projects, solutions & NuGet](intermediate/10-projects-and-nuget.md)
11. [Testing](intermediate/11-testing.md)
12. [Capstone: Markdown link checker](intermediate/12-capstone-link-checker.md)

### Advanced
1. [Modern type design](advanced/01-modern-type-design.md)
2. [Advanced generics](advanced/02-advanced-generics.md)
3. [Reflection & attributes](advanced/03-reflection-and-attributes.md)
4. [Source generators & analyzers](advanced/04-source-generators.md)
5. [Async/await & the Task model](advanced/05-async-await.md)
6. [Threads & synchronization](advanced/06-threads-and-synchronization.md)
7. [Channels & async streams](advanced/07-channels-and-async-streams.md)
8. [Span, Memory & low-allocation code](advanced/08-span-and-memory.md)
9. [Performance & profiling](advanced/09-performance.md)
10. [Unsafe code & native interop](advanced/10-unsafe-and-interop.md)
11. [Packaging, AOT & distribution](advanced/11-packaging-aot-and-distribution.md)
12. [Capstone: parallel file indexer](advanced/12-capstone-parallel-indexer.md)

## License

Use freely for learning and teaching.
