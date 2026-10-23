# 6. Threads & synchronization

## What & why

`async`/`await` handles waiting. This lesson handles **sharing**: when two pieces of code run at the same time and touch the same data, you get **race conditions**: bugs that appear one run in a thousand, vanish under the debugger, and corrupt data. The cure is either *not sharing* (the best option), or controlling access with the right primitive.

## A race, demonstrated

```csharp
int counter = 0;

Parallel.For(0, 1_000_000, _ => counter++);

Console.WriteLine(counter);     // not 1,000,000 (maybe 612,345). Different every run.
```

`counter++` is really three steps (read, add, write). Two threads can read the same value and both write back the same result, losing an increment.

## Strategy 1: don't share

Everything in the right column avoids the problem entirely:

| Shared mutable state | Alternative |
| --- | --- |
| a global counter updated by many threads | each worker keeps a local total; combine at the end |
| a list that workers append to | each worker returns its own result; one thread merges |
| a cache everyone mutates | immutable snapshot swapped atomically |

```csharp
// Each partition has its own local sum: no contention at all.
long total = 0;
Parallel.For(0, 1_000_000,
    localInit: () => 0L,
    body: (i, _, local) => local + i,
    localFinally: local => Interlocked.Add(ref total, local));
```

Or just use PLINQ / LINQ: `Enumerable.Range(0, 1_000_000).AsParallel().Sum(i => (long)i)`.

## Strategy 2: atomic operations

`Interlocked` performs a read-modify-write as one indivisible hardware step:

```csharp
int counter = 0;
Parallel.For(0, 1_000_000, _ => Interlocked.Increment(ref counter));
Console.WriteLine(counter);         // always 1,000,000
```

Also `Interlocked.Add`, `Exchange`, `CompareExchange` (the building block of lock-free algorithms):

```csharp
// "set max" without a lock
static void UpdateMax(ref int target, int value)
{
    int current = Volatile.Read(ref target);
    while (value > current)
    {
        int seen = Interlocked.CompareExchange(ref target, value, current);
        if (seen == current) return;     // we won
        current = seen;                  // someone else changed it; retry
    }
}
```

## Strategy 3: mutual exclusion with `lock`

```csharp
class Account
{
    private readonly Lock _gate = new();        // .NET 9+: System.Threading.Lock
    private decimal _balance;

    public void Deposit(decimal amount)
    {
        lock (_gate)
        {
            _balance += amount;
        }
    }

    public bool TryWithdraw(decimal amount)
    {
        lock (_gate)
        {
            if (_balance < amount) return false;
            _balance -= amount;
            return true;
        }
    }
}
```

Only one thread at a time can be inside `lock (_gate)`. Rules:

- **Lock on a private object** you own (`private readonly Lock _gate = new()` or, before .NET 9, `new object()`). Never lock on `this`, on a `Type`, or on a string; other code could lock the same thing.
- **Keep the critical section tiny** and never call unknown code (callbacks, events) while holding it.
- **Never `await` inside a `lock`** (the compiler forbids it). Use `SemaphoreSlim` for async mutual exclusion.
- Check-then-act must be **inside** the same lock (see `TryWithdraw`).
- **Lock ordering:** if code ever takes lock A then B, and elsewhere B then A, you can **deadlock**. Always acquire multiple locks in a fixed order, or avoid nesting.

### Async-friendly: `SemaphoreSlim`

```csharp
private readonly SemaphoreSlim _mutex = new(1, 1);

public async Task UpdateAsync()
{
    await _mutex.WaitAsync();
    try
    {
        await SaveAsync();               // awaiting while holding is fine here
    }
    finally
    {
        _mutex.Release();                // always release in finally
    }
}
```

`new SemaphoreSlim(n, n)` limits concurrency to *n* instead of 1, which is perfect for "at most 4 uploads at once".

### Other primitives

| Primitive | Use |
| --- | --- |
| `ReaderWriterLockSlim` | many readers or one writer (only worth it for read-heavy, long reads) |
| `Monitor` | what `lock` compiles to; offers `Wait/Pulse` |
| `ManualResetEventSlim`, `AutoResetEvent` | signal between threads |
| `Barrier`, `CountdownEvent` | wait for N participants |
| `Mutex` | cross-process locking |
| `SpinLock` | extremely short, uncontended sections (rarely right) |

## Thread-safe collections

```csharp
using System.Collections.Concurrent;

var counts = new ConcurrentDictionary<string, int>();
Parallel.ForEach(words, w => counts.AddOrUpdate(w, 1, (_, old) => old + 1));

var cache = new ConcurrentDictionary<int, string>();
string value = cache.GetOrAdd(42, key => Expensive(key));    // factory may run more than once!

var queue = new ConcurrentQueue<int>();      // also ConcurrentStack, ConcurrentBag
```

Each individual call is atomic, but **sequences of calls are not**:

```csharp
if (!dict.ContainsKey(k)) dict[k] = Compute();      // race between the check and the write
dict.GetOrAdd(k, Compute);                          // atomic (but Compute may be called twice)
dict.GetOrAdd(k, _ => new Lazy<T>(Compute)).Value;  // run Compute once per key
```

For read-mostly data, share an **immutable** collection and swap it:

```csharp
ImmutableDictionary<string, int> _snapshot = ImmutableDictionary<string, int>.Empty;
void Update(string k, int v) => ImmutableInterlocked.AddOrUpdate(ref _snapshot, k, v, (_, _) => v);
```

## Visibility and the memory model

CPUs and the JIT reorder reads and writes, and caches mean one thread may not see another's write promptly. A "stop" flag shows the problem:

```csharp
bool _stop;                                        // BUG: the loop may never see the change
void Worker() { while (!_stop) { /* work */ } }
void Stop() => _stop = true;
```

Fixes: `volatile bool _stop;`, `Volatile.Read/Write`, or better a `CancellationToken`. Locks and `Interlocked` operations include the required **memory barriers**; you don't need `volatile` for data protected by a lock. Prefer high-level tools: reasoning about the memory model by hand is error-prone.

`Lazy<T>` and `LazyInitializer` give thread-safe one-time initialization:

```csharp
private static readonly Lazy<Config> _config = new(() => Config.Load());
```

## Threads, the thread pool and PLINQ

```csharp
var thread = new Thread(() => Console.WriteLine("hello from another thread"))
{
    IsBackground = true,
    Name = "worker",
};
thread.Start();
thread.Join();
```

You rarely create threads directly. `Task.Run`, `Parallel.For`, `Parallel.ForEach` and PLINQ use the **thread pool**, which sizes itself to the machine. Long-blocking work on pool threads starves everyone else: use `TaskCreationOptions.LongRunning` or a dedicated thread.

```csharp
var results = data.AsParallel()
                  .WithDegreeOfParallelism(Environment.ProcessorCount)
                  .Where(IsInteresting)
                  .Select(Transform)
                  .ToList();
```

PLINQ has overhead; it helps for CPU-heavy work on lots of data and hurts for tiny items. Add `.AsOrdered()` only if you need order.

`[ThreadStatic]` and `ThreadLocal<T>` give each thread its own copy; `AsyncLocal<T>` flows with `async` execution.

## Finding concurrency bugs

- **Make races likely:** run tests with many iterations and high parallelism; use `Parallel.For` with a barrier to line threads up.
- **Thread sanitizers:** .NET lacks a built-in one, so reduce shared state and review by reading.
- **Stress under load** on a multi-core CI machine.
- **Deadlock?** Break in the debugger and look at *Parallel Stacks*; or `dotnet-stack` / `dotnet-dump analyze` (`clrstack -all`) on a hung process.
- **Heisenbugs:** logging can change timing; capture with ETW/EventPipe (`dotnet-trace`).

## Common mistakes

- **Locking on `this` or a public object.**
- **Check-then-act outside a lock.**
- **`async` method holding a `lock`** (won't compile) or blocking inside an async path.
- **Taking locks in inconsistent order.**
- **Using `ConcurrentDictionary` and then iterating while expecting a consistent snapshot.**
- **Using `volatile` as a substitute for a lock** on compound operations.
- **Spinning with `Thread.Sleep(1)` polling loops** instead of events or channels.
- **Sharing `Random`** instances across threads (old `new Random()` isn't thread-safe; use `Random.Shared`).
- **Unbounded `Parallel` loops** on I/O: they hog pool threads.

## Exercises

1. Reproduce the lost-update race with `counter++`, then fix it three ways: `Interlocked`, `lock` and thread-local sums. Time each.
2. Write a thread-safe bounded cache with `lock` that evicts the oldest item beyond N entries.
3. Create a deadlock with two locks and two threads, observe it with `dotnet-stack`, then fix it with lock ordering.
4. Implement the "run `Compute` once per key" cache with `ConcurrentDictionary<K, Lazy<V>>` and prove it with a call counter.
5. Write an async rate limiter with `SemaphoreSlim` that allows 3 concurrent operations.
6. Compare `AsParallel().Sum()` with a plain loop for 100 elements and for 100 million elements.
7. Demonstrate the missing-`volatile` stop-flag bug in a Release build (it usually needs optimizations to appear), then fix it with a `CancellationToken`.

## Recap

- Races come from unsynchronized shared mutable state. First choice: don't share; then use atomics, then locks.
- `lock` (on a private `Lock`/object) for short critical sections; `SemaphoreSlim` when you must `await`; consistent lock ordering avoids deadlock.
- `Concurrent*` collections make single operations atomic, not sequences. Immutable snapshots are often simpler.
- Locks and `Interlocked` handle memory visibility; hand-rolled flags need `volatile` or a token.

Next: [Channels & async streams →](07-channels-and-async-streams.md)
