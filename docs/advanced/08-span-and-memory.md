# 8. Span, Memory & low-allocation code

## What & why

Every `Substring`, array slice or `Split` allocates new objects, and every allocation is future work for the garbage collector. `Span<T>` and its relatives let you work on **a window into existing memory** (an array, a string, the stack, native memory) with **zero copies and zero allocations**, with the same bounds-checked safety as arrays. They are the foundation of high-performance .NET code, and the BCL itself uses them everywhere.

## `Span<T>` and `ReadOnlySpan<T>`

A span is a (pointer, length) pair:

```csharp
int[] numbers = [1, 2, 3, 4, 5, 6];

Span<int> all = numbers;                 // whole array
Span<int> middle = numbers.AsSpan(1, 3); // [2, 3, 4]: a view, not a copy
Span<int> tail = all[3..];               // slicing a span is free

middle[0] = 99;                          // writes through to the array
Console.WriteLine(numbers[1]);           // 99

ReadOnlySpan<char> text = "Hello, World".AsSpan();
ReadOnlySpan<char> word = text[7..];     // "World": no new string
Console.WriteLine(word.Length);          // 5
```

Common operations: `Fill`, `Clear`, `CopyTo`, `Reverse`, `Sort`, `IndexOf`, `Contains`, `SequenceEqual`, `StartsWith`, `Trim`, `Split` (via `MemoryExtensions.Split` in .NET 9: `text.Split(',')` returns a ref-struct enumerator).

`foreach` over a span doesn't allocate and is as fast as a `for` loop with no bounds-check overhead the JIT can prove.

### Parsing without allocating

```csharp
static int SumCsvLine(ReadOnlySpan<char> line)
{
    int total = 0;
    foreach (var range in line.Split(','))                   // .NET 9+
        total += int.Parse(line[range]);                     // int.Parse accepts ReadOnlySpan<char>
    return total;
}

Console.WriteLine(SumCsvLine("10,20,30"));   // 60
```

Compare with `line.Split(',').Select(int.Parse).Sum()`, which allocates an array of substrings plus the LINQ machinery.

Many framework methods have span overloads: `int.TryParse(ReadOnlySpan<char>, ...)`, `Guid.TryParse`, `Convert.ToHexString`, `Stream.Read(Span<byte>)`, `Encoding.UTF8.GetBytes(ReadOnlySpan<char>, Span<byte>)`, `string.Create`, `StringBuilder.Append(ReadOnlySpan<char>)`.

## `stackalloc`: stack memory in a span

For small, short-lived buffers, allocate on the stack (no GC involved):

```csharp
Span<byte> buffer = stackalloc byte[256];
int written = Encoding.UTF8.GetBytes("héllo", buffer);
ReadOnlySpan<byte> bytes = buffer[..written];
```

Rules of thumb: only for **small** sizes (a few hundred bytes up to ~1 KB; the stack is about 1 MB, and overflowing it kills the process), only for sizes you control, and use a fallback to the pool for larger:

```csharp
const int StackLimit = 256;
byte[]? rented = null;
Span<byte> scratch = length <= StackLimit
    ? stackalloc byte[StackLimit]
    : (rented = ArrayPool<byte>.Shared.Rent(length));
try
{
    // use scratch[..length]
}
finally
{
    if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
}
```

## Why spans are `ref struct`s

A span can point into the *stack*. If it could be stored on the heap, it might outlive the stack frame it points into, which would be a memory-safety hole. So `Span<T>` is a **`ref struct`**, and the compiler enforces:

- Can't be a field of a normal class or struct (only of another `ref struct`).
- Can't be boxed, captured by a lambda, or used as a generic argument (unless the parameter is `allows ref struct`, .NET 9).
- Can't be used across `await` or `yield` in classic async/iterator methods (C# 13 relaxes `yield`/`await` in some cases when no span lives across the boundary).
- Can't be returned if it refers to a local.

```csharp
Span<int> Bad()
{
    Span<int> local = stackalloc int[4];
    return local;                 // error CS8352: would escape the method
}
```

When you *do* need to keep a window across `await` or inside a class, use **`Memory<T>`**:

## `Memory<T>`

`Memory<T>` is the heap-friendly sibling: a regular struct that can be stored in fields and passed through `async` code. Get a `Span<T>` from it when you need to touch the data.

```csharp
async Task FillAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
{
    int read = await stream.ReadAsync(buffer, ct);       // takes Memory<byte>
    Process(buffer.Span[..read]);                        // use Span synchronously
}
```

`ReadOnlyMemory<char>` is the common choice for "a slice of a string that I'll keep".

## Pooling: `ArrayPool<T>` and `MemoryPool<T>`

Large arrays (over 85 KB) land on the large-object heap, which is expensive to churn. Rent and return instead:

```csharp
byte[] buffer = ArrayPool<byte>.Shared.Rent(minimumLength: 64 * 1024);
try
{
    int read = stream.Read(buffer, 0, buffer.Length);
    Process(buffer.AsSpan(0, read));
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);           // never use it after this!
}
```

Remember: the rented array may be **larger** than requested and may contain **old data**; only use the range you wrote. Pass `clearArray: true` to `Return` if it held secrets.

## `ref` locals, returns and `in`

```csharp
ref int Find(int[] array, int target)
{
    for (int i = 0; i < array.Length; i++)
        if (array[i] == target) return ref array[i];     // returns a reference to the element
    throw new KeyNotFoundException();
}

int[] data = [1, 2, 3];
ref int slot = ref Find(data, 2);
slot = 20;                                               // modifies data[1]
```

`ref` locals avoid copying big structs and let you update in place. `ref readonly` and `in` give read-only references. `CollectionsMarshal.AsSpan(list)` exposes a `List<T>`'s buffer, and `CollectionsMarshal.GetValueRefOrAddDefault(dict, key, out exists)` allows a single-lookup update of a dictionary value:

```csharp
ref int count = ref CollectionsMarshal.GetValueRefOrAddDefault(counts, word, out _);
count++;                                                 // one hash lookup instead of two
```

(Don't add to the list or dictionary while holding such a reference; it may move.)

## Writing your own `ref struct`

```csharp
ref struct LineReader
{
    private ReadOnlySpan<char> _rest;
    public ReadOnlySpan<char> Current { get; private set; }

    public LineReader(ReadOnlySpan<char> text) { _rest = text; Current = default; }

    public bool MoveNext()
    {
        if (_rest.IsEmpty) return false;
        int newline = _rest.IndexOf('\n');
        if (newline < 0) { Current = _rest; _rest = default; }
        else { Current = _rest[..newline]; _rest = _rest[(newline + 1)..]; }
        return true;
    }

    public LineReader GetEnumerator() => this;           // makes it foreach-able
}

foreach (var line in new LineReader("one\ntwo\nthree"))
    Console.WriteLine(line.ToString());
```

No allocation per line: every `Current` is a slice of the original text.

## `string.Create` and interpolated string handlers

Build a string with exactly one allocation (the string itself):

```csharp
string hex = string.Create(8, 0xBEEF, (span, value) => value.TryFormat(span, out _, "X8"));
```

Interpolation (`$"..."`) already uses a handler that avoids boxing and intermediate strings in many cases, and `Console.WriteLine($"...")`, `StringBuilder.Append($"...")` and `Debug.Assert` have special overloads.

## `Utf8` strings

For protocols and parsing, working in UTF-8 bytes avoids the UTF-16 conversion. C# 11's `u8` literals produce a `ReadOnlySpan<byte>` with no allocation:

```csharp
ReadOnlySpan<byte> get = "GET "u8;
bool isGet = request.StartsWith(get);
```

`System.Text.Json`'s `Utf8JsonReader` and `Utf8JsonWriter` and `System.Buffers` types (`ReadOnlySequence<byte>`, `SequenceReader<T>`, `IBufferWriter<T>`) build on the same ideas for multi-segment buffers.

## Safety: when spans are *not* enough

Spans are safe: bounds are checked, lifetime is enforced. For unsafe operations (reinterpreting memory), use `MemoryMarshal`:

```csharp
Span<byte> raw = stackalloc byte[8];
Span<int> asInts = MemoryMarshal.Cast<byte, int>(raw);        // reinterpret, no copy
```

This is the doorway to the next-but-one lesson on unsafe code.

## Measure, don't guess

Span code is harder to read than LINQ. Use it where measurements justify it: parsers, serializers, protocol handlers, hot loops, libraries. The next lesson covers how to measure.

## Common mistakes

- **Storing a `Span<T>` in a field** (needs `Memory<T>`).
- **Using a rented array after `Return`.**
- **Assuming a rented array has the exact length requested.**
- **`stackalloc` with a runtime size from user input** (stack overflow risk).
- **Slicing off by one:** `span[a..b]` excludes `b`.
- **Holding a `ref` into a `List<T>`/dictionary** while modifying the collection.
- **Premature optimization:** replacing straightforward code with spans in code that isn't hot.
- **Forgetting `ToString()`** when you finally need a string from a `ReadOnlySpan<char>` (printing the span directly prints its type name in some contexts).

## Exercises

1. Write `CountWords(ReadOnlySpan<char>)` that allocates nothing, and verify allocations with `GC.GetAllocatedBytesForCurrentThread()` before and after.
2. Parse `"12:34:56"` into a `TimeSpan` using only spans (`IndexOf`, slicing, `int.Parse`).
3. Implement `ReverseInPlace(Span<char>)` and use it on a `stackalloc` buffer.
4. Read a file in 4 KB chunks using `ArrayPool<byte>` and count newlines with `span.Count((byte)'\n')`.
5. Write a `ref struct` tokenizer that yields words as `ReadOnlySpan<char>` slices.
6. Use `CollectionsMarshal.GetValueRefOrAddDefault` for the word-count idiom and benchmark it against `TryGetValue`/indexer.

## Recap

- `Span<T>`/`ReadOnlySpan<T>` are allocation-free, bounds-checked windows on arrays, strings, stack and native memory.
- They're `ref struct`s so they can't escape to the heap; use `Memory<T>` for async and fields.
- `stackalloc` for tiny buffers, `ArrayPool<T>` for big ones, `string.Create`/`u8` for low-allocation text.
- `ref` returns and `CollectionsMarshal` remove copies and double lookups. Use them where measurements show a benefit.

Next: [Performance & profiling →](09-performance.md)
