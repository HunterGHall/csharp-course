# Intermediate

For you if you know the syntax but classes vs structs vs records, interfaces, generics, LINQ and resource management still feel shaky. By the end you'll design your own types, write generic and interface-based code, manage resources correctly, parse JSON, organize code into projects, test it, and have built a real tool.

## Lessons

| # | Lesson | You'll learn |
| --- | --- | --- |
| 1 | [Classes, structs & records](01-classes-structs-records.md) | reference vs value semantics, primary constructors, `init`, `required`, `with` |
| 2 | [Properties & members](02-properties-and-members.md) | properties, the `field` keyword, indexers, operators, extension methods and blocks, events |
| 3 | [Enums & pattern matching](03-enums-and-pattern-matching.md) | enums, `[Flags]`, record hierarchies, property, positional and list patterns |
| 4 | [Interfaces & inheritance](04-interfaces-and-inheritance.md) | contracts, `virtual`/`override`, `abstract`, composition, dependency injection |
| 5 | [Generics](05-generics.md) | generic methods and types, constraints, generic math, variance basics |
| 6 | [Delegates, lambdas & LINQ](06-delegates-lambdas-linq.md) | `Func`/`Action`, closures, `Where`/`Select`/`GroupBy`, deferred execution, `yield` |
| 7 | [Memory, GC & disposal](07-memory-gc-and-disposal.md) | stack vs heap, generations, `IDisposable`, `using`, boxing, leaks |
| 8 | [Exceptions in depth](08-exceptions-in-depth.md) | guard clauses, custom exceptions, wrapping, filters, `Try` patterns, result types |
| 9 | [JSON & serialization](09-json-and-serialization.md) | `System.Text.Json`, options, attributes, `JsonNode`, converters, source generation |
| 10 | [Projects, solutions & NuGet](10-projects-and-nuget.md) | `.csproj`, solutions, references, packages, `Directory.Build.props`, publishing |
| 11 | [Testing](11-testing.md) | xUnit, `[Fact]`/`[Theory]`, fakes, temp directories, what makes a good test |
| 12 | [Capstone: link checker](12-capstone-link-checker.md) | a tested, modular Markdown link checker |

## How to work through it

- Build each example in a scratch project, and keep your own notes on **why** each design choice was made.
- Exercises here are bigger. It's fine to spend a whole sitting on one.
- Write a test for every non-trivial method from lesson 11 onward.

## When you're done

You should be able to explain every line of the capstone, add a feature to it, and write a test for it. Then go to [Advanced](../advanced/index.md).
