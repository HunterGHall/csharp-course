# 7. Memory, GC & disposal

## What & why

C# manages memory for you with a **garbage collector (GC)**: you create objects with `new`, and the runtime frees them when nothing can reach them any more. That removes whole classes of bugs (use-after-free, double-free), but it doesn't remove *resource* management: files, sockets, database connections and native handles still need deterministic cleanup. This lesson explains both halves.

## Stack and heap

- The **stack** holds method call frames: local variables, parameters, and values of value types declared locally. It is fast and automatically popped when a method returns.
- The **managed heap** holds objects of reference types (and anything captured by closures, or boxed). The GC reclaims it.

```csharp
void Demo()
{
    int n = 5;                  // value on the stack
    var list = new List<int>(); // reference on the stack, object on the heap
}                               // frame popped; the list becomes garbage
```

Value types stored *inside* a class live on the heap as part of that object. "Structs live on the stack" is a simplification: they live wherever their container does.

## How the GC works (the useful version)

- The GC finds **reachable** objects starting from *roots* (static fields, locals on active stacks, CPU registers) and reclaims everything else.
- Objects are grouped into **generations**: gen 0 (new, short-lived), gen 1, and gen 2 (long-lived). Most objects die young, so collecting gen 0 is cheap and frequent.
- Large objects (85,000 bytes or more) go to the **large object heap**, collected only with gen 2.
- Collections briefly pause managed threads; the GC is concurrent and compacting, and tuned for throughput by default.

You normally never call `GC.Collect()`. Your job is to avoid creating needless garbage in hot paths and to avoid keeping things reachable longer than needed.

## Memory leaks still exist

A "leak" in C# means an object is still *reachable* when you no longer need it:

- **Static collections** that only grow (caches without eviction).
- **Event handlers never unsubscribed.** The publisher references the subscriber.
- **Long-lived closures** capturing big objects.
- **Timers and tasks** that are never stopped.

```csharp
publisher.Changed += subscriber.OnChanged;     // publisher now keeps subscriber alive
// ... later
publisher.Changed -= subscriber.OnChanged;     // required if the publisher outlives the subscriber
```

## Deterministic cleanup: `IDisposable`

The GC decides *when* memory is reclaimed, which can be much later than when you finish with a file. Types that own unmanaged resources implement `IDisposable`:

```csharp
public interface IDisposable { void Dispose(); }
```

Calling `Dispose()` releases the resource immediately. The `using` statement calls it for you, even when an exception is thrown:

```csharp
using (var stream = File.OpenRead("data.bin"))
{
    // use stream
}                                           // stream.Dispose() runs here

using var connection = new SqlConnection(connectionString);   // disposed at end of scope
```

**Rule:** if you create an `IDisposable`, you dispose it (unless something else explicitly took ownership). Analyzers warn about this (CA2000).

### Implementing `IDisposable`

Most classes only *contain* disposable things. Then the pattern is simple:

```csharp
sealed class Report : IDisposable
{
    private readonly StreamWriter _writer;
    private bool _disposed;

    public Report(string path) => _writer = new StreamWriter(path);

    public void WriteLine(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _writer.WriteLine(text);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _writer.Dispose();
        _disposed = true;
    }
}
```

`Dispose` should be safe to call more than once. For an **unsealed** class that might be inherited, use the full pattern with a `protected virtual void Dispose(bool disposing)` and `GC.SuppressFinalize(this)`. Sealing the class avoids that complexity.

### Finalizers

A finalizer (`~MyClass()`) is a last-resort safety net that the GC calls on unreachable objects. It runs at an unpredictable time and slows collection down. You almost never need one: wrap native handles in `SafeHandle` subclasses (which have finalizers) and your own types need none.

### `IAsyncDisposable`

For cleanup that is itself asynchronous (flushing a network stream):

```csharp
await using var writer = new StreamWriter("out.txt");
await writer.WriteLineAsync("hello");
```

## Value types and boxing

Treating a value type as `object` (or an interface it implements) **boxes** it: allocates a heap object and copies the value in.

```csharp
int n = 42;
object boxed = n;          // allocation
int back = (int)boxed;     // unbox

var list = new System.Collections.ArrayList();
list.Add(n);               // boxes every int: why generics matter
```

Boxing in tight loops produces garbage; generic collections and `readonly struct` + `IEquatable<T>` avoid it.

## Reducing allocations

- Don't build strings in loops with `+`; use `StringBuilder` or interpolation once.
- Reuse buffers: `ArrayPool<byte>.Shared.Rent(...)`.
- Prefer `Span<T>`/`ReadOnlySpan<T>` slices over `Substring`/`Array` copies (Advanced level).
- Size collections up front: `new List<int>(capacity)`.
- Avoid LINQ and lambdas that capture variables in the hottest loops.
- Use `struct` for tiny, short-lived data; but not as a reflex.

## Weak references and caches

`WeakReference<T>` lets you refer to an object without keeping it alive, useful for caches that should not prevent collection:

```csharp
var weak = new WeakReference<byte[]>(new byte[1_000_000]);
if (weak.TryGetTarget(out var data)) { /* still alive */ }
```

For general caching, use `MemoryCache` (with size limits and expiry) instead of rolling your own.

## Observing memory

```csharp
long before = GC.GetTotalMemory(forceFullCollection: true);
// ... do work ...
Console.WriteLine($"gen0 collections: {GC.CollectionCount(0)}");
Console.WriteLine($"allocated so far: {GC.GetTotalAllocatedBytes():N0} bytes");
```

For real investigation use `dotnet-counters`, `dotnet-trace` and `dotnet-gcdump`, or Visual Studio's/Rider's memory profilers.

## Common mistakes

- **Not disposing** streams, `HttpClient` responses, `DbConnection`s, `Process`es, timers.
- **Creating a new `HttpClient` per request,** which exhausts sockets. Create one and reuse it (or use `IHttpClientFactory`).
- **Subscribing to events and never unsubscribing.**
- **Calling `GC.Collect()`** as a "fix".
- **Writing a finalizer** when `SafeHandle` or composition would do.
- **Using a disposed object:** `ObjectDisposedException`.
- **Assuming structs are always faster.** Large structs are copied constantly.

## Exercises

1. Write a class `TempFile : IDisposable` that creates a temp file in its constructor and deletes it in `Dispose`. Use it with `using` and prove the file is gone afterwards.
2. Write code that leaks via an event subscription, then use `WeakReference` plus `GC.Collect()` to demonstrate that the subscriber is still alive; fix it by unsubscribing.
3. Measure the difference between summing 10 million ints in an `ArrayList` vs `List<int>` (time and `GC.CollectionCount(0)`).
4. Build a `StringBuilder`-based and a `+=`-based version of joining 100,000 strings and compare the time.
5. Implement `IAsyncDisposable` on a class that flushes pending items to a file on `DisposeAsync`.
6. Use `ArrayPool<byte>` to read a file in 4 KB chunks without allocating a new array per chunk.

## Recap

- The GC reclaims unreachable objects automatically, in generations; you control reachability, not timing.
- Leaks mean "still referenced": static collections, event handlers, closures, timers.
- `IDisposable` + `using` gives deterministic release of files, sockets and handles; dispose what you create.
- Boxing and needless allocation cost GC time; measure before optimizing.

Next: [Exceptions in depth →](08-exceptions-in-depth.md)
