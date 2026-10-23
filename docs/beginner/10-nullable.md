# 10. Nullable values

## What & why

`null` means "no value here". Tony Hoare called inventing it his "billion-dollar mistake", because calling a method on `null` crashes with `NullReferenceException`, the most common runtime error in C#. Modern C# fights back: with **nullable reference types** (on by default in new projects) the compiler tracks which variables can be `null` and warns you before you run.

## Two different "nullables"

1. **Nullable value types**: `int?`, `bool?`, `DateTime?`. Plain `int` can never be null; `int?` can.
2. **Nullable reference types**: `string?`, `Person?`. A reference like `string` is *supposed* to be non-null; `string?` may be null. This is a compile-time annotation: the runtime types are the same.

```csharp
int? maybeAge = null;
int? other = 42;

string name = "Ada";       // promised never to be null
string? nickname = null;   // may be null
```

New projects enable this with `<Nullable>enable</Nullable>` in the `.csproj`.

## What the compiler checks

```csharp
string? input = Console.ReadLine();

// Console.WriteLine(input.Length);    // warning CS8602: dereference of a possibly null reference

if (input != null)
{
    Console.WriteLine(input.Length);   // fine: the compiler knows it's non-null here
}
```

The compiler does *flow analysis*: after a null check, early `return`, or `is not null` test, it treats the variable as non-null.

```csharp
if (input is null) return;           // (in a method)
Console.WriteLine(input.Length);     // fine
```

Treat these warnings as errors for new code. Most real bugs hide behind them.

## Handling nulls

### `?.` null-conditional

Stops and gives `null` if the left side is null:

```csharp
int? length = input?.Length;           // null if input is null
string? upper = input?.ToUpper();
string? first = people?[0]?.Name;      // works with indexers too
```

(New in C# 14: it also works on the left side of an assignment, so `customer?.Order = CreateOrder();` assigns only if `customer` isn't null.)

### `??` null-coalescing

Provides a default:

```csharp
string display = nickname ?? "(none)";
int len = input?.Length ?? 0;
```

### `??=` null-coalescing assignment

Assigns only if the variable is currently null:

```csharp
List<string>? cache = null;
cache ??= [];            // creates the list once
cache.Add("x");
```

### Pattern checks

```csharp
if (nickname is { Length: > 0 } nick)
{
    Console.WriteLine($"Called {nick}");
}
```

## Nullable value types in detail

```csharp
int? n = 5;

Console.WriteLine(n.HasValue);      // True
Console.WriteLine(n.Value);         // 5 (throws InvalidOperationException if null)
Console.WriteLine(n ?? -1);         // 5
Console.WriteLine(n.GetValueOrDefault());   // 5, or 0 if null

int? sum = n + null;                // null: arithmetic with null yields null
bool? b = null;
if (b == true) { }                  // comparing is safe: null == true is false
```

Typical use: optional data and "not yet known" values.

```csharp
int? ParseOrNull(string text) => int.TryParse(text, out var v) ? v : null;
```

## The `!` (null-forgiving) operator

```csharp
string s = maybeNull!;     // "trust me, it's not null"
```

It silences the warning but does nothing at run time. If you're wrong, you get a `NullReferenceException`. Use it rarely, and only when you know something the compiler can't (for example, a value set by a framework).

## Designing without null

- **Don't return null for collections**: return an empty one.
- **Use `TryXxx` methods** (`int.TryParse`, `TryGetValue`) for lookups that may fail.
- **Guard public inputs**:

  ```csharp
  void Save(string path)
  {
      ArgumentNullException.ThrowIfNull(path);
      ArgumentException.ThrowIfNullOrWhiteSpace(path);
      // ...
  }
  ```
- **Use `required` members** (Intermediate level) so objects can't be created half-initialized.
- **Choose a better return type** than "null means failure": a `bool` + `out`, a result type, or an exception.

## Common mistakes

- **Ignoring warnings** CS8600-CS8604. Fix them; don't suppress them.
- **`!` as a habit.** It moves the crash from compile time to run time.
- **Confusing `""` and `null`.** An empty string is a real string; `string.IsNullOrEmpty` checks both.
- **`.Value` on a null `int?`** throws; use `?? default` or check `HasValue`.
- **Returning null from a method declared to return non-nullable** (the compiler warns).
- **Assuming `Console.ReadLine()` returns a string.** It returns `string?`.

## Exercises

1. Write `string Describe(string? name)` returning `"anonymous"` for null or whitespace, otherwise a greeting.
2. Parse a list of strings into `int?` values (null for bad ones), then compute the sum of the valid ones.
3. Given `Dictionary<string, string?>`, print the length of each value or `0` if missing/null, using `?.` and `??`.
4. Write a method that takes `Person? p` (with `Address? Address` and `string? City`) and returns the city or `"unknown"`, using a single expression.
5. Turn on `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and fix every nullable error in an earlier exercise.

## Recap

- `int?` is a value that might be missing; `string?` is a reference that might be null.
- The compiler tracks nullability and warns about unsafe dereferences.
- `?.`, `??`, `??=`, `is null` and patterns handle nulls concisely.
- Avoid `!`; prefer empty collections, `Try...` methods and guard clauses.

Next: [Files & exceptions →](11-files-and-exceptions.md)
