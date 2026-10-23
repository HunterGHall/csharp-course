# 8. Exceptions in depth

## What & why

The Beginner level showed `try`/`catch`. Real programs need a *strategy*: which failures are bugs, which are expected, who handles each one, and how much context travels with it. This lesson is about designing error handling, not just using the syntax.

## What an exception contains

```csharp
try
{
    Process("bad");
}
catch (Exception ex)
{
    Console.WriteLine(ex.GetType().Name);   // FormatException
    Console.WriteLine(ex.Message);          // human-readable reason
    Console.WriteLine(ex.StackTrace);       // where it was thrown, and the path up
    Console.WriteLine(ex.InnerException);   // the underlying cause, if wrapped
    Console.WriteLine(ex);                  // everything, formatted
}

void Process(string text) => int.Parse(text);
```

`ex.ToString()` prints the type, message, inner exceptions and stack, which is exactly what you want in a log.

## Three kinds of failure

| Kind | Example | What to do |
| --- | --- | --- |
| **Bug** (programmer error) | `null` passed where forbidden, index out of range | Let it throw. Fix the code. Don't catch it. |
| **Expected failure** | invalid user input, file not found, duplicate key | Check first or use a `Try...` method or a result value |
| **Environmental / transient** | network down, disk full, timeout | Catch at a boundary, retry or report |

Most mistakes come from treating one kind as another.

## Guard clauses

Fail fast at the top of a method, with the right exception type:

```csharp
void Transfer(Account from, Account to, decimal amount)
{
    ArgumentNullException.ThrowIfNull(from);
    ArgumentNullException.ThrowIfNull(to);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

    if (from.Balance < amount)
        throw new InvalidOperationException($"insufficient funds: {from.Balance} < {amount}");
    // ...
}
```

Choose the type deliberately:

| Exception | Meaning |
| --- | --- |
| `ArgumentNullException` | argument was null |
| `ArgumentOutOfRangeException` | value outside the allowed range |
| `ArgumentException` | argument invalid in some other way |
| `InvalidOperationException` | object is in the wrong state for this call |
| `NotSupportedException` / `NotImplementedException` | will never / not yet implemented |
| `ObjectDisposedException` | used after `Dispose` |
| `TimeoutException`, `OperationCanceledException` | time ran out / cancelled |

## Custom exceptions

Create one when callers need to catch it *specifically*:

```csharp
class InsufficientFundsException : Exception
{
    public decimal Balance { get; }
    public decimal Requested { get; }

    public InsufficientFundsException(decimal balance, decimal requested)
        : base($"balance {balance} is less than requested {requested}")
    {
        Balance = balance;
        Requested = requested;
    }

    public InsufficientFundsException(string message, Exception inner) : base(message, inner) { }
}
```

Derive from `Exception` (not `ApplicationException`). Name it with the `Exception` suffix and carry useful data as properties. Don't create a custom type if `InvalidOperationException` with a clear message would do.

## Wrapping and chaining

When you catch a low-level exception and throw a higher-level one, **keep the original** as the inner exception:

```csharp
User LoadUser(int id)
{
    try
    {
        return Database.Fetch(id);
    }
    catch (SqlException ex)
    {
        throw new DataAccessException($"could not load user {id}", ex);
    }
}
```

Without the inner exception you lose the real cause.

## Rethrowing correctly

```csharp
catch (Exception ex)
{
    logger.Error(ex);
    throw;            // preserves the original stack trace
}
// NOT: throw ex;     // resets the stack trace to this line
```

To rethrow an exception object from elsewhere without losing its stack (for example, one captured on another thread): `ExceptionDispatchInfo.Capture(ex).Throw();`.

## Filters

```csharp
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    return null;
}
catch (HttpRequestException ex) when (IsTransient(ex))
{
    // retry
}
```

A filter runs *before* the stack unwinds, so unmatched exceptions keep their full context (and debuggers can break at the original throw site).

## `finally`, `using` and cleanup

`finally` and `using` run on every exit path: normal, `return`, or exception. Don't `return` or `throw` from inside a `finally`, which can mask the original exception.

## Where to catch

- **Catch where you can do something useful:** retry, fall back, add context, translate to a user message.
- **At the outermost boundary** (the `Main` method, a web request pipeline, a thread's entry point) log unexpected exceptions and exit or return an error response.
- **Not in the middle** merely to log and rethrow at every layer (you'll log the same error ten times).

A top-level handler for console tools:

```csharp
try
{
    return App.Run(args);
}
catch (UserFacingException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
// anything else: let it crash with the full stack trace, exit code is non-zero
```

## Results instead of exceptions

When failure is normal and frequent, return a value that says so:

```csharp
// 1. The Try pattern
bool TryParseColor(string s, out Color color) { /* ... */ }

// 2. A small result type
readonly record struct Result<T>(T? Value, string? Error)
{
    public bool IsOk => Error is null;
    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(default, error);
}

Result<int> ParseAge(string s) =>
    int.TryParse(s, out var n) && n is >= 0 and <= 150
        ? Result<int>.Ok(n)
        : Result<int>.Fail($"'{s}' is not a valid age");
```

Exceptions are for the unexpected; throwing is also slow (microseconds to tens of microseconds each), which matters only in loops where failure is common.

## Exceptions and async/iterators

- In `async` methods, exceptions are stored in the returned `Task` and thrown when you `await` it.
- `Task.WhenAll` throws only the *first* exception when awaited; the others are in the task's `Exception.InnerExceptions` (an `AggregateException`).
- Exceptions in an iterator (`yield`) surface when enumeration reaches them, not when the method is called.
- Unobserved exceptions on a background thread (`Thread`, `ThreadPool.QueueUserWorkItem`) can terminate the process.

## Common mistakes

- **Catching `Exception` and doing nothing.**
- **`throw ex;`** instead of `throw;`.
- **Dropping the inner exception** when wrapping.
- **Catching `NullReferenceException`/`IndexOutOfRangeException`** to "handle" bugs.
- **Using exceptions for normal control flow,** like exiting loops or signalling "not found".
- **Messages with no data:** "Invalid input" vs "'abc' is not a valid port (1-65535)".
- **Custom exception hierarchies** nobody catches.

## Exercises

1. Write `ParseConfig(string path)` that throws `ConfigException` (with the inner `IOException` or `JsonException`) and show the chained output.
2. Replace exception-based validation in an older exercise with a `Result<T>` type; compare the call sites.
3. Write a retry helper `Retry<T>(Func<T> action, int attempts)` that retries only on `IOException` using a `when` filter.
4. Demonstrate how `throw ex;` differs from `throw;` by printing both stack traces.
5. Use `Task.WhenAll` on three failing tasks and print every exception, not just the first.
6. Add guard clauses with the `ThrowIf...` helpers to a class from an earlier lesson.

## Recap

- Treat bugs, expected failures and environmental failures differently.
- Guard clauses fail fast with precise exception types; custom exceptions carry data and the inner cause.
- `throw;` preserves the stack; `when` filters preserve context; catch where you can act.
- Use `Try...` methods or result types when failure is routine.

Next: [JSON & serialization →](09-json-and-serialization.md)
