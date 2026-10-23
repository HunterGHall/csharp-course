# 9. Methods

## What & why

A **method** is a named, reusable block of code. Methods let you give a name to an idea, avoid repeating yourself, and test pieces in isolation. (C# calls them *methods* because they always live inside a type; in a top-level-statements file they live in a hidden `Program` class, so they feel like plain functions.)

## Defining and calling

```csharp
int Square(int n)
{
    return n * n;
}

Console.WriteLine(Square(7));    // 49

void Greet(string name)
{
    Console.WriteLine($"Hello, {name}!");
}

Greet("Ada");
```

Structure: **return type**, **name**, **parameters**, **body**. `void` means "returns nothing". A method that returns a value must return on every path.

With a single expression you can use the **expression-bodied** form:

```csharp
int Cube(int n) => n * n * n;
bool IsEven(int n) => n % 2 == 0;
```

In a top-level-statements file, declare methods anywhere; the compiler hoists them (they're local functions). In a normal class they would be members (Intermediate level).

## Optional parameters and named arguments

```csharp
string Repeat(string text, int times = 2, string separator = " ")
{
    return string.Join(separator, Enumerable.Repeat(text, times));
}

Console.WriteLine(Repeat("ha"));                       // ha ha
Console.WriteLine(Repeat("ha", 3));                    // ha ha ha
Console.WriteLine(Repeat("ha", separator: "-"));       // ha-ha
Console.WriteLine(Repeat(separator: "!", text: "x"));  // x!x
```

Optional parameters must come after required ones. Named arguments make calls self-documenting, which is especially valuable for `bool` flags: `Save(path, overwrite: true)` beats `Save(path, true)`.

## Returning several values

Use a **tuple**:

```csharp
(int Min, int Max) MinMax(IEnumerable<int> xs)
{
    return (xs.Min(), xs.Max());
}

var (lo, hi) = MinMax([4, 9, 2]);
Console.WriteLine($"{lo}..{hi}");       // 2..9

var r = MinMax([4, 9, 2]);
Console.WriteLine(r.Max);               // 9
```

## `out`, `ref` and `in`

By default arguments are passed **by value**: the method gets a copy. Three keywords change that:

```csharp
// out: the method must assign it; the caller reads it afterwards
bool TryDivide(int a, int b, out int result)
{
    if (b == 0)
    {
        result = 0;
        return false;
    }
    result = a / b;
    return true;
}

if (TryDivide(10, 2, out int q)) Console.WriteLine(q);   // 5

// ref: the caller's variable is read and modified
void Double(ref int x) => x *= 2;
int v = 21;
Double(ref v);
Console.WriteLine(v);                                     // 42

// in: pass by reference but read-only (avoids copying big structs)
double Length(in (double X, double Y) p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);
```

The `Try...` pattern (`int.TryParse`, `Dictionary.TryGetValue`) is the standard use of `out`.

## Variable arguments: `params`

```csharp
int Sum(params int[] numbers) => numbers.Sum();

Console.WriteLine(Sum());            // 0
Console.WriteLine(Sum(1, 2, 3));     // 6
Console.WriteLine(Sum([4, 5]));      // 9
```

## Overloading

Several methods may share a name if their parameter lists differ:

```csharp
double Area(double radius) => Math.PI * radius * radius;
double Area(double width, double height) => width * height;
```

The compiler chooses by the argument types and count. The return type alone doesn't distinguish overloads.

## Local functions

A method can declare helpers that only it can see, and that can use its variables:

```csharp
int CountWords(string text)
{
    return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(IsWord);

    static bool IsWord(string s) => s.Any(char.IsLetter);   // static: captures nothing
}
```

## Methods as values

Methods can be stored in variables and passed around using **delegate** types. The two you'll use most are `Func<...>` (returns a value; the last type argument is the return type) and `Action<...>` (returns nothing):

```csharp
Func<int, int> twice = x => x * 2;
Action<string> shout = s => Console.WriteLine(s.ToUpper());

Console.WriteLine(twice(21));     // 42
shout("hello");                   // HELLO

int[] numbers = [1, 2, 3];
Console.WriteLine(string.Join(",", numbers.Select(Square)));   // 1,4,9 (a method group)
```

Delegates and lambdas get a full lesson in the Intermediate level.

## Recursion

A method may call itself. Every recursive method needs a **base case** that stops it:

```csharp
long Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);

int Fib(int n) => n < 2 ? n : Fib(n - 1) + Fib(n - 2);   // correct but slow: try Fib(40)
```

Very deep recursion overflows the stack (a `StackOverflowException` that cannot be caught and ends the process). Prefer loops for deep or long-running work.

## Design tips

- A method should do one thing and be nameable with a verb phrase: `ParseLine`, `IsValid`.
- Prefer returning values over printing inside the method; it makes the method testable and reusable.
- Keep parameter lists short (three or four is plenty). If you need more, group them into a type.
- Name boolean methods and variables as questions: `IsEmpty`, `HasValue`, `CanRun`.

## Common mistakes

- **Not returning on every path:** "not all code paths return a value".
- **Expecting a method to modify a value-type argument** without `ref`.
- **Optional parameter defaults must be compile-time constants** (`= new List<int>()` isn't allowed; use `= null` and create inside).
- **Mixing up `out` and `ref`:** `out` doesn't need a value going in, but must be assigned before returning.
- **Unbounded recursion** with a missing or wrong base case.
- **Using exceptions for expected failures** when a `Try...` method would do.

## Exercises

1. Write `IsPrime(int n)` and use it to print the primes below 100.
2. Write `(int quotient, int remainder) DivMod(int a, int b)` and call it.
3. Implement `TryParsePercent(string s, out double value)` that accepts `"45%"`.
4. Write `Max(params int[] values)` that throws `ArgumentException` for an empty call.
5. Write `Fib` recursively, then with a loop, and compare the timing for `n = 40`.
6. Write `Compose(Func<int,int> f, Func<int,int> g)` returning a function that applies `f` then `g`.
7. Overload `Print` so it handles `int`, `string` and `IEnumerable<int>` differently.

## Recap

- Methods have a return type, a name, parameters and a body; `=>` handles one-liners.
- Optional parameters, named arguments, `params` and overloads make calls flexible.
- Tuples return several values; `out`/`ref`/`in` pass by reference; the `Try...` pattern uses `out`.
- Methods are values too: `Func<>` and `Action<>`.

Next: [Nullable values →](10-nullable.md)
