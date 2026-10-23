# 7. Channels & async streams

## What & why

Many problems are **pipelines**: something produces items over time (lines from a socket, rows from a database, files found on disk), and something else consumes them, possibly slower or faster. Two .NET tools model this well:

- **`IAsyncEnumerable<T>`**: an *asynchronous sequence* you consume with `await foreach`.
- **`Channel<T>`**: a thread-safe, async-friendly **queue** connecting producers to consumers, with optional **back-pressure**.

## Async streams

A regular `IEnumerable<T>` blocks the caller while it computes the next item. `IAsyncEnumerable<T>` lets each step `await`:

```csharp
async IAsyncEnumerable<int> TicksAsync(int count, [EnumeratorCancellation] CancellationToken ct = default)
{
    for (int i = 1; i <= count; i++)
    {
        await Task.Delay(200, ct);
        yield return i;                          // yield inside an async method
    }
}

await foreach (var tick in TicksAsync(5))
{
    Console.WriteLine($"tick {tick}");
}
```

- The method is `async`, returns `IAsyncEnumerable<T>`, and uses `yield return`.
- Consume it with `await foreach`.
- `[EnumeratorCancellation]` lets a consumer pass a token with `.WithCancellation(token)` while your method still sees it as its `ct` parameter.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
try
{
    await foreach (var tick in TicksAsync(100).WithCancellation(cts.Token))
        Console.WriteLine(tick);
}
catch (OperationCanceledException) { Console.WriteLine("stopped"); }
```

### Real uses

```csharp
// Stream a big JSON array without loading it all
await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<Order>(stream))
    Process(item);

// Read lines asynchronously
await foreach (var line in File.ReadLinesAsync("big.log"))
    if (line.Contains("ERROR")) Console.WriteLine(line);

// ASP.NET Core minimal API can return IAsyncEnumerable<T> and stream the response.
```

### LINQ over async streams

`System.Linq.Async` (the `System.Linq.AsyncEnumerable` in .NET 10) provides `Where`, `Select`, `Take`, `ToListAsync`, `CountAsync` and friends for `IAsyncEnumerable<T>`:

```csharp
var firstEven = await TicksAsync(10).Where(n => n % 2 == 0).FirstAsync();
```

### Disposal

`await foreach` disposes the enumerator when the loop ends, even on `break` or exception, so `finally` blocks and `await using` inside your producer run:

```csharp
async IAsyncEnumerable<string> ReadAsync(string path)
{
    await using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    while (await reader.ReadLineAsync() is { } line)
        yield return line;
}   // the stream is closed when the consumer stops
```

## Channels

A channel decouples producers from consumers:

```csharp
using System.Threading.Channels;

var channel = Channel.CreateUnbounded<int>();

// Producer
_ = Task.Run(async () =>
{
    for (int i = 1; i <= 5; i++)
    {
        await channel.Writer.WriteAsync(i);
    }
    channel.Writer.Complete();                  // no more items
});

// Consumer
await foreach (var item in channel.Reader.ReadAllAsync())
{
    Console.WriteLine($"got {item}");
}
```

- `Writer.WriteAsync` / `TryWrite` add items; `Writer.Complete()` (or `Complete(exception)`) signals the end.
- `Reader.ReadAllAsync()` yields items until the channel is completed *and* drained. If it was completed with an exception, the exception is thrown to the reader.
- Many producers and many consumers can share one channel. Each item is delivered to exactly one reader.
- `Reader.Completion` is a task you can await to know when everything has been consumed.

### Bounded channels and back-pressure

An unbounded channel lets a fast producer pile up memory. A **bounded** channel makes the producer wait when it is full:

```csharp
var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity: 100)
{
    FullMode = BoundedChannelFullMode.Wait,    // WriteAsync waits for room
    SingleReader = false,
    SingleWriter = true,
});
```

`FullMode` can also drop items (`DropOldest`, `DropNewest`, `DropWrite`) for "latest value wins" situations such as telemetry. `SingleReader`/`SingleWriter` hints let the implementation use faster code paths if you promise.

## A pipeline

```csharp
record Job(int Id);
record Result(int Id, int Value);

async Task RunPipelineAsync(IEnumerable<Job> jobs, int workers, CancellationToken ct)
{
    var input = Channel.CreateBounded<Job>(workers * 2);
    var output = Channel.CreateUnbounded<Result>();

    // Stage 1: feed
    var feeder = Task.Run(async () =>
    {
        try
        {
            foreach (var job in jobs)
                await input.Writer.WriteAsync(job, ct);
        }
        finally { input.Writer.Complete(); }
    }, ct);

    // Stage 2: N workers
    var pool = Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
    {
        await foreach (var job in input.Reader.ReadAllAsync(ct))
        {
            await Task.Delay(50, ct);                         // simulate work
            await output.Writer.WriteAsync(new Result(job.Id, job.Id * job.Id), ct);
        }
    }, ct)).ToArray();

    // When every worker finishes, close the output.
    var closer = Task.WhenAll(pool).ContinueWith(
        t => output.Writer.Complete(t.Exception), TaskScheduler.Default);

    // Stage 3: consume (on this task)
    await foreach (var result in output.Reader.ReadAllAsync(ct))
        Console.WriteLine($"{result.Id} -> {result.Value}");

    await Task.WhenAll(feeder, closer);
    await Task.WhenAll(pool);                                 // surface worker exceptions
}
```

Properties of this design:

- **Bounded input** gives back-pressure all the way to the feeder.
- Each worker is a long-lived loop, not a task per item: bounded parallelism by construction.
- Completion flows downstream: feeder completes `input`; when workers drain it they finish; then `output` completes.
- Results arrive in **completion order**, not input order. Carry an id (or sort afterwards) if the order matters.

## Channels vs. other options

| Need | Use |
| --- | --- |
| Return a sequence lazily, one consumer | `IAsyncEnumerable<T>` |
| Multiple producers/consumers, buffering, back-pressure | `Channel<T>` |
| Simple "N at a time" over a collection | `Parallel.ForEachAsync` |
| Fan-out to *all* subscribers | Rx (`System.Reactive`) or events |
| Complex graphs, batching, joins | `System.Threading.Tasks.Dataflow` |
| Cross-process queues | a message broker (RabbitMQ, Azure Service Bus, ...) |
| Old `BlockingCollection<T>` | blocks threads: prefer `Channel<T>` in async code |

You can also turn a channel into a stream and back: `channel.Reader.ReadAllAsync()` is just an `IAsyncEnumerable<T>`.

## Errors and cancellation

- If a producer fails, call `writer.Complete(ex)` so consumers see the failure instead of hanging.
- Always complete writers in `finally`; a forgotten `Complete()` leaves consumers waiting forever.
- Pass a `CancellationToken` to `WriteAsync` and `ReadAllAsync` so both sides can stop promptly.
- An exception in one worker of `Task.WhenAll(pool)` doesn't stop the others automatically; use a linked `CancellationTokenSource` and cancel it when any stage fails.

## Common mistakes

- **Forgetting `Writer.Complete()`**, so `await foreach` never ends.
- **Unbounded channels with a fast producer,** which grows memory without limit.
- **One task per item** instead of a fixed worker pool.
- **Assuming result order** matches input order.
- **Ignoring `[EnumeratorCancellation]`,** so `WithCancellation` doesn't reach your code.
- **Blocking (`Thread.Sleep`, `.Result`)** inside producers or consumers.
- **Multiple enumeration** of an `IAsyncEnumerable` expecting a cached result; each `await foreach` restarts the work.

## Exercises

1. Write `IAsyncEnumerable<string> FindFilesAsync(string root)` that yields paths while walking a directory and consume it with `await foreach`.
2. Convert a producer/consumer built on `BlockingCollection<T>` to `Channel<T>`.
3. Make a bounded channel of capacity 3 with a slow consumer. Log when the producer waits.
4. Build the pipeline above with three stages (parse, transform, write) and measure throughput with 1, 2 and 8 workers.
5. Add `DropOldest` to a channel that carries sensor readings and show that old values are discarded when the consumer lags.
6. Make a failing worker cancel the whole pipeline and surface its exception to the caller.

## Recap

- `IAsyncEnumerable<T>` + `await foreach` give lazy, asynchronous sequences with cancellation and cleanup.
- `Channel<T>` is an async queue; bounded channels provide back-pressure.
- Pipelines of stages with fixed worker pools give bounded, deterministic parallelism.
- Always complete writers (in `finally`), pass tokens, and don't assume ordering.

Next: [Span, Memory & low-allocation code →](08-span-and-memory.md)
