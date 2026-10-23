# 9. Performance & profiling

## What & why

"Premature optimization is the root of all evil," but so is guessing. The professional approach is a loop: **measure, find the bottleneck, change one thing, measure again.** This lesson is a method, not a checklist: how to benchmark correctly, how to profile, what the runtime does for you, and the handful of changes that usually matter.

## Rule zero: measure in Release

```bash
dotnet run -c Release
```

Debug builds disable optimizations and tiered compilation tricks. Timing a Debug build, or timing a method once with a `Stopwatch`, tells you almost nothing. The first call includes JIT time, caches are cold, and the tiered JIT hasn't optimized yet.

## BenchmarkDotNet

[BenchmarkDotNet](https://benchmarkdotnet.org/) handles warm-up, repetition, statistics and allocation counts:

```bash
dotnet new console -o bench && cd bench
dotnet add package BenchmarkDotNet
```

```csharp
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<JoinBenchmarks>();

[MemoryDiagnoser]
public class JoinBenchmarks
{
    private string[] _words = null!;

    [Params(10, 1000)]
    public int Count;

    [GlobalSetup]
    public void Setup() => _words = Enumerable.Range(0, Count).Select(i => $"word{i}").ToArray();

    [Benchmark(Baseline = true)]
    public string Concat()
    {
        string result = "";
        foreach (var w in _words) result += w + ",";
        return result;
    }

    [Benchmark]
    public string Builder()
    {
        var sb = new StringBuilder();
        foreach (var w in _words) sb.Append(w).Append(',');
        return sb.ToString();
    }

    [Benchmark]
    public string Join() => string.Join(',', _words);
}
```

```bash
dotnet run -c Release
```

The report shows mean time, error, ratio against the baseline, and allocated bytes plus GC counts per operation. Read the **allocated** column as carefully as the time column.

Tips:

- Benchmark the code path *realistically*: representative input sizes and data.
- Return the result (or use a `Consumer`/`Blackhole`) so the JIT can't delete dead code.
- Beware micro-benchmarks that prove nothing about the whole program. Always confirm in a realistic run.
- For quick checks: `Stopwatch` after warm-up with many iterations, or `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime(start)`.

## Profiling a real program

Benchmarks answer "which version is faster?". A **profiler** answers "where is my time going?".

| Tool | Use |
| --- | --- |
| `dotnet-counters` | live metrics: CPU, GC counts, allocation rate, thread-pool queue length |
| `dotnet-trace` | collect a CPU sampling trace (`.nettrace`/speedscope) |
| `dotnet-gcdump`, `dotnet-dump` | managed heap snapshots and crash/hang analysis |
| `dotnet-monitor` | the same in containers |
| Visual Studio / Rider profilers | CPU, allocation, memory snapshots with source navigation |
| PerfView | deep ETW analysis (Windows) |
| `perf` + `DOTNET_PerfMapEnabled=1` | Linux native sampling with managed symbols |

```bash
dotnet tool install -g dotnet-counters
dotnet-counters monitor --process-id <pid> System.Runtime
```

Watch: **% time in GC**, **allocation rate (MB/s)**, **gen 2 collection count**, **thread pool queue length** (a growing queue means blocked threads).

A typical session:

1. Reproduce a realistic workload.
2. Look at the CPU flame graph: the widest bars are where the time is.
3. Look at allocations: the types allocated most are usually the cause of GC time.
4. Fix the top item. Re-measure. Stop when it's fast enough.

## What the runtime does for you

- **Tiered JIT:** methods start in a fast-to-compile tier 0, and hot methods are recompiled with full optimization (tier 1). With **dynamic PGO** (default since .NET 8) the runtime profiles code while it runs and re-optimizes using real branch/type data, including **guarded devirtualization** of interface calls and inlined delegate calls.
- **Inlining:** small methods disappear into their callers. Don't mark everything `[MethodImpl(AggressiveInlining)]`; trust the JIT unless measurements say otherwise.
- **Bounds-check elimination:** `for (int i = 0; i < arr.Length; i++)` over an array lets the JIT remove checks; `foreach` over arrays and spans is equally fast.
- **Vectorization:** many BCL methods (`IndexOf`, `SequenceEqual`, `Sum` on arrays...) use SIMD automatically.
- **Escape analysis (growing):** the JIT can stack-allocate short-lived objects and delegates that don't escape.
- **ReadyToRun / Native AOT** trade peak speed for start-up speed (see the packaging lesson).

## Where the time usually goes

### 1. Algorithms and data structures

An O(n²) loop beats any micro-optimization:

```csharp
// O(n * m): List.Contains scans every time
foreach (var id in ids) if (known.Contains(id)) hits++;

// O(n + m): a HashSet
var set = known.ToHashSet();
foreach (var id in ids) if (set.Contains(id)) hits++;
```

Know the costs: `List<T>` index O(1), insert at front O(n); `Dictionary` lookup ~O(1); `SortedDictionary` O(log n); `LinkedList` is rarely faster in practice because of cache misses.

### 2. Allocation and the GC

- Avoid per-item allocations in hot loops: LINQ chains, closures that capture variables, `string` concatenation, boxing, `params object[]`, `Substring`/`Split`.
- Reuse buffers (`ArrayPool<T>`, `StringBuilder` caches), use spans, presize collections (`new List<T>(capacity)`), use `struct` for small value-like items and `readonly struct` to avoid defensive copies.
- Prefer `Dictionary<TKey, TValue>` with a struct key that implements `IEquatable<TKey>` over a string-concatenated key.
- For lookup tables built once and read a lot, `FrozenDictionary`/`FrozenSet` (`ToFrozenDictionary()`) are faster to read than `Dictionary`.
- `SearchValues<char>` makes "find any of these characters" very fast:

```csharp
private static readonly SearchValues<char> Separators = SearchValues.Create(" ,;\t");
int index = text.AsSpan().IndexOfAny(Separators);
```

### 3. I/O

- Use async I/O, buffering (`FileStream` with a larger buffer, `BufferedStream`), and `File.ReadLines` for streaming.
- Batch network and database round trips; reuse `HttpClient`; avoid N+1 queries.
- Compress or avoid sending data you don't need.

### 4. Concurrency

- Parallelize CPU-bound work with `Parallel.For`/PLINQ **after** making the sequential version fast.
- Contention on a single lock or a shared counter can make "parallel" code slower than serial. Aggregate locally.
- Too many threads → context switching; too few → idle cores. Let the pool decide.
- Thread-pool starvation (blocking inside async code) shows as high latency with low CPU.

### 5. LINQ and abstractions

LINQ is wonderful for clarity and fine for most code. In a proven hot loop, a `for` over an array or span may be 3-10× faster and allocation-free. Convert only after profiling.

### 6. Exceptions, reflection and logging

- Exceptions cost microseconds each, so don't use them for expected control flow in loops.
- Cache reflection results; use source generators.
- Use `[LoggerMessage]` source-generated logging, and check `logger.IsEnabled(level)` before building expensive messages.

## Structs: faster or slower?

```csharp
readonly record struct Point(double X, double Y);       // 16 bytes: great as a struct
struct Huge { public long A, B, C, D, E, F, G, H; }      // 64 bytes: copied on every call: use a class or `in`
```

Structs avoid allocation and improve locality (arrays of structs sit contiguously), but large structs are expensive to copy, and mutable structs invite bugs. Benchmark both.

## Caching and laziness

Compute once, reuse: `Lazy<T>`, `ConcurrentDictionary.GetOrAdd`, `MemoryCache` with expiry/size limits. Caching adds invalidation bugs, so cache the expensive and stable thing and measure the hit rate.

## Server settings worth knowing

- **Server GC** (`<ServerGarbageCollector>true</ServerGarbageCollector>`) uses a heap per core for throughput; **Concurrent GC** (default) reduces pause times; `GCHeapHardLimit` in containers.
- `TieredPGO`, `TieredCompilation` and `ReadyToRun` can be tuned in the `.csproj` or `runtimeconfig.json`.
- In containers, set CPU/memory limits; .NET respects them for thread counts and heap sizing.

## Performance regression tests

Keep a small BenchmarkDotNet project in the repo and run it before big changes. Some teams run benchmarks in CI and compare to a baseline with a generous threshold. At minimum, **assert allocation counts** in unit tests for hot paths:

```csharp
long before = GC.GetAllocatedBytesForCurrentThread();
Parse(line);
long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
Assert.Equal(0, allocated);
```

(Run it twice and assert on the second call, so one-time JIT allocations don't count.)

## Common mistakes

- **Optimizing without measuring,** or measuring Debug builds.
- **Micro-benchmarks that the JIT optimizes away.**
- **Benchmarking the wrong data size,** so cache behavior differs from production.
- **Reducing time while increasing allocations** (or vice versa) without noticing the trade.
- **Parallelizing before fixing the algorithm.**
- **Replacing clear code with clever code for no measurable gain.**
- **Forgetting the network and database:** the slow part is rarely the CPU.
- **Tuning for the benchmark machine** rather than production hardware.

## Exercises

1. Benchmark `string +=`, `StringBuilder`, `string.Join` and `string.Concat` for 10, 1,000 and 100,000 items.
2. Compare `List<int>.Contains` in a loop against `HashSet<int>` and `FrozenSet<int>` for several sizes.
3. Write the same word count with LINQ `GroupBy`, with a `Dictionary`, and with `CollectionsMarshal.GetValueRefOrAddDefault` plus spans. Compare time and allocations.
4. Run `dotnet-counters` against a program that allocates heavily in a loop and watch gen 0 collections climb. Then fix the program.
5. Find the hot spot in the file indexer capstone with `dotnet-trace` or your IDE's profiler.
6. Show that a 64-byte struct passed by value is slower than passing it with `in` or as a class.
7. Add a unit test that asserts a hot method performs zero allocations.

## Recap

- Measure in Release, with BenchmarkDotNet for micro-benchmarks and profilers (`dotnet-trace`, `dotnet-counters`, IDE tools) for real programs.
- The runtime already inlines, vectorizes, profiles and re-optimizes; algorithms, allocations and I/O dominate.
- Reduce allocations with pooling, spans, presizing and struct-vs-class decisions that you've measured.
- Don't parallelize or micro-optimize until the profile says so, and keep the evidence.

Next: [Unsafe code & native interop →](10-unsafe-and-interop.md)
