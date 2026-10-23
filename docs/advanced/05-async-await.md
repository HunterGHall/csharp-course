# 5. Async/await & the Task model

## What & why

Programs spend most of their time **waiting**: for disks, networks, databases, users. A thread that waits is a thread that does nothing and still costs memory. **Asynchronous programming** lets the thread go do other work while an operation is in flight, then resume when it completes. In C# that is `async`/`await` over `Task`. Done right, a handful of threads can serve thousands of concurrent requests; done wrong, you get deadlocks and swallowed exceptions.

## The basics

```csharp
async Task<string> DownloadAsync(HttpClient http, string url)
{
    string text = await http.GetStringAsync(url);     // the thread is released while waiting
    return text.ToUpperInvariant();
}

using var http = new HttpClient();
string result = await DownloadAsync(http, "https://example.com");   // top-level await is allowed
```

- An `async` method returns `Task` (no value), `Task<T>`, `ValueTask`/`ValueTask<T>`, or `void` (event handlers only).
- `await` unwraps a task's result, **or rethrows its exception**, without blocking a thread.
- By convention async methods end in `Async`.
- A `Task` is a *promise* of a result, not a thread. Many tasks involve no thread at all while they wait (I/O completion).

## What the compiler does

An `async` method is rewritten into a **state machine**. At each `await`:

1. If the awaited task is already complete, execution continues synchronously.
2. Otherwise the method saves its state, returns an incomplete `Task` to the caller, and arranges for the rest of the method (the *continuation*) to run when the awaited task completes.

Understanding that one idea explains most of the rest: the code after `await` may run on a **different thread** than the code before it, unless a synchronization context says otherwise.

## Synchronization contexts and `ConfigureAwait`

UI frameworks (WPF, WinForms, MAUI) have a *synchronization context* that forces continuations back onto the UI thread, which is convenient but costs performance and creates deadlock risk. ASP.NET Core and console apps have no such context, so continuations run on the thread pool.

In **library** code, say you don't need the original context:

```csharp
var data = await stream.ReadAsync(buffer).ConfigureAwait(false);
```

In application code (UI event handlers) you usually *want* to come back to the UI thread, so leave it off. The CA2007 analyzer can enforce this in libraries.

## Never block on async code

```csharp
var text = DownloadAsync(http, url).Result;      // blocks the thread: can DEADLOCK
DownloadAsync(http, url).Wait();                 // same
var t = DownloadAsync(http, url).GetAwaiter().GetResult();   // same blocking, nicer exceptions
```

Classic deadlock: the UI thread blocks waiting for the task; the task's continuation needs the UI thread to finish. Fix: make the caller async all the way up (**async all the way**). The only legitimate blocking spot is the top of the program, and even `Main` can be `async Task Main` or use top-level `await`.

## Running things concurrently

Start several operations, *then* await them:

```csharp
// Sequential: total time = sum
var a = await http.GetStringAsync(urlA);
var b = await http.GetStringAsync(urlB);

// Concurrent: total time = the slowest
Task<string> ta = http.GetStringAsync(urlA);
Task<string> tb = http.GetStringAsync(urlB);
string[] both = await Task.WhenAll(ta, tb);

// First one wins
Task<string> winner = await Task.WhenAny(ta, tb);
```

`Task.WhenAll` over a collection:

```csharp
string[] urls = [ /* ... */ ];
string[] pages = await Task.WhenAll(urls.Select(u => http.GetStringAsync(u)));
```

Awaiting `WhenAll` throws only the **first** exception; inspect the task's `Exception.InnerExceptions` for all of them:

```csharp
var all = Task.WhenAll(tasks);
try { await all; }
catch
{
    foreach (var ex in all.Exception!.InnerExceptions) Console.WriteLine(ex.Message);
}
```

### Limiting concurrency

Firing 10,000 requests at once is rude. Use `Parallel.ForEachAsync` (which also gives you a cancellation token) or a `SemaphoreSlim`:

```csharp
await Parallel.ForEachAsync(urls, new ParallelOptions { MaxDegreeOfParallelism = 8 },
    async (url, ct) =>
    {
        var body = await http.GetStringAsync(url, ct);
        Console.WriteLine($"{url}: {body.Length}");
    });
```

## Cancellation

Cooperative cancellation is the standard: the caller creates a `CancellationTokenSource`, passes its `Token` along, and the callee checks it or passes it on.

```csharp
async Task WorkAsync(CancellationToken ct)
{
    for (int i = 0; i < 100; i++)
    {
        ct.ThrowIfCancellationRequested();
        await Task.Delay(100, ct);          // Delay throws OperationCanceledException when cancelled
    }
}

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));   // auto-cancel after 2 s
try
{
    await WorkAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("cancelled");
}
```

Rules:

- Every public async method that can take a while should accept a `CancellationToken` (last parameter, often `= default`).
- Pass the token to every async call you make.
- `OperationCanceledException` means "stopped on purpose"; don't treat it as a failure.
- Combine tokens with `CancellationTokenSource.CreateLinkedTokenSource(a, b)`.
- Timeouts: `await task.WaitAsync(TimeSpan.FromSeconds(5))` (throws `TimeoutException`), or a token with a timeout.

## `Task.Run`: CPU-bound work

`async` is for waiting. For heavy computation you want a different thread, and `Task.Run` moves a delegate to the thread pool:

```csharp
int primes = await Task.Run(() => CountPrimesBelow(10_000_000));
```

Don't wrap I/O in `Task.Run` ("fake async"); use the real async API. In ASP.NET Core, don't `Task.Run` per request, since that just moves work between the same pool threads.

## `ValueTask`

`Task<T>` allocates on every call. When a method often completes synchronously (cache hit, buffered read), `ValueTask<T>` avoids the allocation:

```csharp
ValueTask<int> ReadByteAsync() =>
    _buffered ? new ValueTask<int>(NextBufferedByte()) : new ValueTask<int>(ReadFromNetworkAsync());
```

Constraints: await it **once**, don't await it twice or concurrently, and don't call `.Result` unless it's completed. If in doubt, use `Task<T>`; reach for `ValueTask` for measured hot paths.

## Exceptions

- An exception inside an `async Task` method is captured in the task and thrown when awaited.
- **`async void`** has nowhere to store it: the exception is thrown on the synchronization context and typically crashes the process. Use `async void` only for event handlers.
- A task you never await has unobserved exceptions: it may be silently ignored. Always await, or attach a handler (**fire-and-forget** should be an explicit, logged decision).

```csharp
_ = Task.Run(async () =>
{
    try { await BackgroundWorkAsync(); }
    catch (Exception ex) { logger.LogError(ex, "background work failed"); }
});
```

## Async lifecycle helpers

```csharp
await using var conn = new SqlConnection(cs);      // IAsyncDisposable
await Task.Delay(TimeSpan.FromMilliseconds(250));  // an async sleep: never Thread.Sleep in async code
await Task.Yield();                                // reschedule the continuation
Task done = Task.CompletedTask;
Task<int> five = Task.FromResult(5);
var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
while (await timer.WaitForNextTickAsync(ct)) { /* once per second */ }
```

`TaskCompletionSource<T>` bridges callback-style APIs into tasks:

```csharp
Task<string> ReadLineAsync(LegacyDevice device)
{
    var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    device.LineReceived += line => tcs.TrySetResult(line);
    device.Failed += ex => tcs.TrySetException(ex);
    return tcs.Task;
}
```

`RunContinuationsAsynchronously` stops your continuation from running inline on the thread that called `TrySetResult`, avoiding surprising re-entrancy.

## Common mistakes

- **Blocking with `.Result`/`.Wait()`**, causing deadlocks and thread starvation.
- **`async void`** outside event handlers.
- **Forgetting to `await`** (the compiler warns CS4014): the work runs but errors vanish.
- **Awaiting in a loop** when the operations are independent (use `WhenAll`).
- **Starting unbounded numbers of tasks.**
- **Ignoring `CancellationToken`.**
- **Capturing the wrong context** and updating UI from a thread-pool thread.
- **Using `Thread.Sleep`** instead of `Task.Delay`.
- **Sharing mutable state between concurrent continuations** without protection (next lesson).
- **`Task.Run` around async I/O.**

## Exercises

1. Download five URLs sequentially and then with `Task.WhenAll`. Print both timings.
2. Add a 3-second timeout and a manual Ctrl+C cancel to your downloader using linked tokens.
3. Write `RetryAsync<T>(Func<CancellationToken, Task<T>>, int attempts, TimeSpan delay)` with exponential back-off that respects cancellation.
4. Write an `async Task<T> WithTimeout<T>(Task<T>, TimeSpan)` helper; compare it with `WaitAsync`.
5. Reproduce the UI-thread deadlock in a small WinForms/WPF app (or explain it step by step) and fix it.
6. Wrap an event-based API with `TaskCompletionSource` and make sure it can be cancelled.
7. Use `Parallel.ForEachAsync` to process a folder of files with at most 4 in flight.

## Recap

- `async`/`await` frees threads during waits; the compiler builds a state machine; continuations may resume elsewhere.
- Never block on tasks; go async all the way. Use `ConfigureAwait(false)` in libraries.
- Start tasks first, await later: `WhenAll`, `WhenAny`, `Parallel.ForEachAsync` with a concurrency limit.
- Pass `CancellationToken` everywhere; `OperationCanceledException` means "cancelled", not "failed".
- Avoid `async void`; observe every task's exception.

Next: [Threads & synchronization →](06-threads-and-synchronization.md)
