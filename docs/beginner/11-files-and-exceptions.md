# 11. Files & exceptions

## What & why

Real programs read and write files, and real files go missing, get locked, or contain garbage. C# reports unexpected failures with **exceptions**: objects that interrupt the normal flow and travel up the call stack until something handles them. This lesson covers both file I/O and exception handling, because you can't do one well without the other.

## Exceptions: `try` / `catch` / `finally`

```csharp
try
{
    int n = int.Parse("forty-two");
    Console.WriteLine(n);
}
catch (FormatException ex)
{
    Console.WriteLine($"Not a number: {ex.Message}");
}
finally
{
    Console.WriteLine("always runs");
}
```

- The `try` block runs normally until something throws.
- The first `catch` whose type matches handles it. Put specific types before general ones.
- `finally` runs whether or not there was an exception, which makes it good for cleanup.

Multiple catches and **filters**:

```csharp
try
{
    DoWork();
}
catch (FileNotFoundException ex)
{
    Console.Error.WriteLine($"missing: {ex.FileName}");
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"I/O problem: {ex.Message}");
}
```

The `when` filter keeps the exception unhandled (and its stack intact) if the condition is false.

## Throwing

```csharp
int Withdraw(int balance, int amount)
{
    if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "must be positive");
    if (amount > balance) throw new InvalidOperationException("insufficient funds");
    return balance - amount;
}
```

Re-throw with a bare `throw;` (which keeps the original stack trace), **not** `throw ex;`:

```csharp
try { Save(); }
catch (IOException)
{
    Console.Error.WriteLine("save failed, rethrowing");
    throw;
}
```

Common built-in exceptions: `ArgumentException`, `ArgumentNullException`, `InvalidOperationException`, `FormatException`, `IOException`, `FileNotFoundException`, `KeyNotFoundException`, `NotSupportedException`, `NullReferenceException` (a bug: don't catch it).

## Exceptions vs. expected failures

Exceptions are for *exceptional* situations. If failure is an everyday outcome (user typed a bad number, file might not exist), check first or use a `Try` method:

```csharp
if (!File.Exists(path)) { /* handle */ }
if (int.TryParse(text, out var n)) { /* use n */ }
```

Still wrap the actual I/O in `try`/`catch`: the file can vanish between the check and the open.

## Reading and writing files

```csharp
File.WriteAllText("notes.txt", "first line\nsecond line\n");
string all = File.ReadAllText("notes.txt");

File.WriteAllLines("list.txt", ["apple", "banana", "cherry"]);
string[] lines = File.ReadAllLines("list.txt");

File.AppendAllText("notes.txt", "third line\n");

foreach (string line in File.ReadLines("big.log"))   // lazy: one line at a time
{
    if (line.Contains("ERROR")) Console.WriteLine(line);
}
```

- `ReadAllText`/`ReadAllLines` load everything into memory: fine for small files.
- `ReadLines` streams: use it for large ones.
- Text is read and written as UTF-8 by default.
- Relative paths are relative to the **current working directory**, not to the source file. `dotnet run` uses the directory you ran it from.

### Streams and `using`

For more control, open a stream. Anything that holds a file handle implements `IDisposable` and must be closed. `using` guarantees that, even if an exception occurs:

```csharp
using (var writer = new StreamWriter("out.txt"))
{
    writer.WriteLine("hello");
}                                    // writer is disposed (flushed and closed) here

using var reader = new StreamReader("out.txt");     // disposed at the end of the enclosing scope
Console.WriteLine(reader.ReadLine());
```

The `using` declaration (no braces) is the modern, shorter form.

## Paths and directories

Build paths with `Path`, never with string concatenation, so the separator is right on every OS:

```csharp
string dir = Path.Combine("data", "reports");
string file = Path.Combine(dir, "2026.csv");

Path.GetFileName(file);                  // "2026.csv"
Path.GetFileNameWithoutExtension(file);  // "2026"
Path.GetExtension(file);                 // ".csv"
Path.GetDirectoryName(file);             // "data/reports" (or data\reports)
Path.GetFullPath(file);                  // absolute path
Path.GetTempPath();

Directory.CreateDirectory(dir);          // creates parents; no error if it exists
bool there = Directory.Exists(dir);

foreach (string f in Directory.EnumerateFiles(".", "*.cs", SearchOption.AllDirectories))
{
    Console.WriteLine(f);
}

File.Copy("a.txt", "b.txt", overwrite: true);
File.Move("b.txt", "c.txt");
File.Delete("c.txt");
```

## Command-line arguments and environment

In a top-level program, the array `args` is available automatically:

```csharp
if (args.Length < 1)
{
    Console.Error.WriteLine("usage: wordcount <file>");
    return 1;          // exit code: non-zero means failure
}

string path = args[0];
string home = Environment.GetEnvironmentVariable("HOME") ?? "(unset)";
```

Run with arguments: `dotnet run -- notes.txt` (the `--` separates `dotnet`'s options from yours).

Write errors to `Console.Error` so they don't mix with normal output when someone redirects `>`. A top-level program may `return` an `int` as its exit code.

## Putting it together

```csharp
if (args.Length != 1)
{
    Console.Error.WriteLine("usage: wordcount <file>");
    return 2;
}

try
{
    int words = 0, lines = 0;
    foreach (string line in File.ReadLines(args[0]))
    {
        lines++;
        words += line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
    Console.WriteLine($"{lines} lines, {words} words");
    return 0;
}
catch (FileNotFoundException)
{
    Console.Error.WriteLine($"no such file: {args[0]}");
    return 1;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"cannot read {args[0]}: {ex.Message}");
    return 1;
}
```

## Common mistakes

- **Catching `Exception` everywhere** and swallowing it. Catch what you can actually handle; let the rest crash loudly or be logged at the top.
- **`throw ex;`** resets the stack trace. Use `throw;`.
- **Empty `catch {}` blocks** hide bugs.
- **Forgetting to dispose** streams: data may not be flushed and files stay locked. Use `using`.
- **Hard-coded path separators** (`"data\\file.txt"`). Use `Path.Combine`.
- **Assuming the working directory.** Print `Directory.GetCurrentDirectory()` when a relative path misbehaves.
- **Using exceptions for control flow** in hot loops: throwing is slow compared with a check or `TryParse`.

## Exercises

1. Write `wordcount <file>`: lines, words and characters, with friendly errors and exit codes.
2. Write `tail <file> <n>`: print the last *n* lines (hint: keep a `Queue<string>`).
3. Read a CSV of `name,score` rows, skip malformed rows with a warning on `Console.Error`, and print the average.
4. Write a program that appends a timestamped line to `log.txt` each time it runs.
5. List all files under a directory, grouped by extension, with a total size for each.
6. Write `TryReadAllText(string path, out string text)` that returns `false` instead of throwing.
7. Make a method that throws `ArgumentException` for bad input; catch it, add context, and rethrow it with `throw;`.

## Recap

- `try`/`catch`/`finally` handle failures; filter with `when`; rethrow with `throw;`.
- Use `Try...` methods and checks for expected failures; exceptions for the unexpected.
- `File.ReadAllText`, `ReadLines`, `WriteAllText`, `AppendAllText`, `Path.Combine` and `Directory` cover most needs.
- `using` disposes streams; `args`, `Console.Error` and exit codes make real command-line tools.

Next: [Capstone: task tracker →](12-capstone-task-tracker.md)
