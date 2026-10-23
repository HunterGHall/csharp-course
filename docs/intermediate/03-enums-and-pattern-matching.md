# 3. Enums & pattern matching

## What & why

Often a value is "one of a fixed set of things": a day of the week, an order status, a shape. C# models that with **enums** (named constants) and with **class hierarchies or records** matched by **patterns**. Together they let you write exhaustive, readable decision code and keep invalid states out of your program.

## Enums

```csharp
enum Status
{
    Pending,       // 0
    Active,        // 1
    Done = 10,
    Failed,        // 11
}

Status s = Status.Active;
Console.WriteLine(s);              // Active
Console.WriteLine((int)s);         // 1
Console.WriteLine(Enum.Parse<Status>("Done"));          // Done
Console.WriteLine(Enum.TryParse<Status>("nope", out _));// False

foreach (var value in Enum.GetValues<Status>())
    Console.WriteLine($"{value} = {(int)value}");
```

An enum is really an integer (an `int` by default) with names. That means **any integer can be cast to it**, even meaningless ones:

```csharp
Status weird = (Status)99;
Console.WriteLine(Enum.IsDefined(weird));    // False
```

so validate input before casting, and always give `switch` expressions a fallback for enums that come from outside.

### `[Flags]` enums

Powers of two let you combine options:

```csharp
[Flags]
enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    All = Read | Write | Execute,
}

var p = Permissions.Read | Permissions.Write;
Console.WriteLine(p);                              // Read, Write
Console.WriteLine(p.HasFlag(Permissions.Write));   // True
p &= ~Permissions.Write;                           // remove a flag
```

## Switch expressions over enums

```csharp
string Describe(Status s) => s switch
{
    Status.Pending => "waiting",
    Status.Active => "in progress",
    Status.Done or Status.Failed => "finished",
    _ => throw new ArgumentOutOfRangeException(nameof(s)),
};
```

## Modeling "one of several shapes" with records

Enums can't carry different data per case. For that, use a small hierarchy, often called a **discriminated union** or *sum type*:

```csharp
abstract record Shape;
record Circle(double Radius) : Shape;
record Rectangle(double Width, double Height) : Shape;
record Triangle(double Base, double Height) : Shape;

double Area(Shape shape) => shape switch
{
    Circle c => Math.PI * c.Radius * c.Radius,
    Rectangle r => r.Width * r.Height,
    Triangle t => 0.5 * t.Base * t.Height,
    null => throw new ArgumentNullException(nameof(shape)),
    _ => throw new NotSupportedException(shape.GetType().Name),
};
```

The type pattern `Circle c` tests and binds in one step. (Future C# versions plan first-class union types so the compiler can check you handled every case; today you add the `_` arm, or seal the hierarchy and write tests.)

## Property patterns

Match on the *contents* of an object:

```csharp
record Order(string Customer, decimal Total, bool IsExpress);

string Shipping(Order o) => o switch
{
    { IsExpress: true } => "overnight",
    { Total: >= 100 } => "free standard",
    { Customer: "Ada" or "Linus" } => "friends ship free",
    _ => "standard",
};

bool bigAndExpress = order is { IsExpress: true, Total: > 500 };
```

Properties can be nested: `{ Address.City: "Oslo" }`.

## Positional and tuple patterns

Records (and tuples) can be matched by position:

```csharp
record Point(int X, int Y);

string Where(Point p) => p switch
{
    (0, 0) => "origin",
    (_, 0) => "on the x-axis",
    (0, _) => "on the y-axis",
    (> 0, > 0) => "first quadrant",
    _ => "elsewhere",
};

string Fizz(int n) => (n % 3, n % 5) switch
{
    (0, 0) => "FizzBuzz",
    (0, _) => "Fizz",
    (_, 0) => "Buzz",
    _ => n.ToString(),
};
```

## List patterns

```csharp
string Describe(int[] xs) => xs switch
{
    [] => "empty",
    [var only] => $"just {only}",
    [var first, .., var last] => $"from {first} to {last}",
};

bool startsWithOne = xs is [1, ..];
bool hasMiddle = xs is [_, .. var middle, _];
```

`..` matches any number of elements (and can capture them as a slice).

## Combining patterns

| Pattern | Example |
| --- | --- |
| constant | `0`, `"x"`, `null`, `Status.Done` |
| type | `string s`, `Circle c` |
| relational | `>= 18`, `< 0` |
| logical | `and`, `or`, `not` |
| property | `{ Length: > 3 }` |
| positional | `(var x, var y)` |
| list | `[1, 2, ..]` |
| discard / `var` | `_`, `var x` |

```csharp
bool IsValidPort(int p) => p is > 0 and <= 65535;
bool IsShortName(string? s) => s is { Length: > 0 and < 10 };
```

(`{ Length: > 0 }` fails for `null`, so it also checks non-null.)

## `when` guards and `is`

```csharp
string Classify(object o) => o switch
{
    int n when n < 0 => "negative int",
    int n => "int",
    string { Length: 0 } => "empty string",
    string s => $"string of {s.Length}",
    IEnumerable<int> xs when xs.Any() => "non-empty ints",
    _ => "something else",
};
```

Arms are tried **top to bottom**; the compiler flags an arm that can never match because an earlier one covers it.

## Common mistakes

- **Casting unchecked integers to an enum** and trusting the result.
- **Forgetting the default arm** for externally supplied enum values.
- **Wrong arm order:** a broad pattern before a narrow one makes the narrow one unreachable.
- **`Flags` values that aren't powers of two**, which makes combinations ambiguous.
- **Using enums when each case needs different data.** Use a record hierarchy.
- **Comparing against `null` with `==` on types that overload it,** where `is null` always means "really null".

## Exercises

1. Define `enum Season` and a method returning the average temperature for each with a switch expression.
2. Create a `[Flags] enum FileMode2` and write helpers to add, remove and test flags.
3. Model arithmetic expressions as records (`Num`, `Add`, `Mul`, `Neg`) and write `Evaluate` and `Print` with switch expressions.
4. Write `Describe(int[] xs)` handling empty, single, pair, and longer arrays with list patterns.
5. Given a `Customer` record with an `Address` record, use nested property patterns to find customers living in `"Oslo"` with a premium flag.
6. Write a FizzBuzz using a tuple pattern; extend it to also handle multiples of 7.

## Recap

- Enums name a fixed set of integers; `[Flags]` combines them; validate values from outside.
- Record hierarchies model "one of several shapes" that carry data.
- Patterns (type, property, positional, list, relational, `and`/`or`/`not`, `when`) make decisions declarative.
- Order switch arms from most to least specific.

Next: [Interfaces & inheritance →](04-interfaces-and-inheritance.md)
