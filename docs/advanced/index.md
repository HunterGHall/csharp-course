# Advanced

For you if you ship C# and want depth. This level covers the parts of the language and runtime that separate "it works" from "it's fast, safe, and well designed": type design that makes illegal states unrepresentable, advanced generics, metaprogramming, the async model and its pitfalls, threading, channels, low-allocation code, profiling, native interop, and distribution.

## Lessons

| # | Lesson | You'll learn |
| --- | --- | --- |
| 1 | [Modern type design](01-modern-type-design.md) | state records, value objects, `static abstract` members, `IParsable<T>`, API shape |
| 2 | [Advanced generics](02-advanced-generics.md) | variance, constraints, generic math, runtime specialization, phantom types |
| 3 | [Reflection & attributes](03-reflection-and-attributes.md) | `Type`, custom attributes, plugin loading, compiled expression getters, trimming |
| 4 | [Source generators & analyzers](04-source-generators.md) | incremental generators, `partial`, diagnostics, `.editorconfig` severities |
| 5 | [Async/await & the Task model](05-async-await.md) | state machines, `ConfigureAwait`, cancellation, `WhenAll`, `ValueTask`, deadlocks |
| 6 | [Threads & synchronization](06-threads-and-synchronization.md) | races, `Interlocked`, `lock`, `SemaphoreSlim`, concurrent collections, memory model |
| 7 | [Channels & async streams](07-channels-and-async-streams.md) | `IAsyncEnumerable<T>`, `Channel<T>`, back-pressure, pipelines |
| 8 | [Span, Memory & low-allocation code](08-span-and-memory.md) | `Span<T>`, `stackalloc`, `ArrayPool`, `ref struct`, `string.Create`, `u8` |
| 9 | [Performance & profiling](09-performance.md) | BenchmarkDotNet, `dotnet-trace`/`counters`, JIT tiers, allocation hot spots |
| 10 | [Unsafe code & native interop](10-unsafe-and-interop.md) | `LibraryImport`, `SafeHandle`, pointers, `fixed`, function pointers, native memory |
| 11 | [Packaging, AOT & distribution](11-packaging-aot-and-distribution.md) | API design, SemVer, NuGet packages, single-file, Native AOT, containers, CI |
| 12 | [Capstone: parallel file indexer](12-capstone-parallel-indexer.md) | a concurrent, tested, race-free application, end to end |

## How to work through it

- Treat lessons as reference material you revisit. Several (source generators, interop, AOT) reward a second pass after you've used the feature for real.
- Keep nullable reference types and warnings-as-errors on while you work through the concurrency lessons.
- Measure before you optimize; the performance lesson is a method, not a checklist.

## When you're done

Build the indexer capstone to the self-check bar: warning-free, race-free by construction, identical output at any concurrency level, and benchmarked with a measured speedup. Then stop studying and go build something real.
