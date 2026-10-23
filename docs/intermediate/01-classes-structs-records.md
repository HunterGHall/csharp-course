# 1. Classes, structs & records

## What & why

So far you've used types other people wrote. Now you design your own. C# has three main ways to define a type that holds data and behavior, and the difference that matters most is **reference vs value semantics**: what happens when you copy a variable.

| | `class` | `struct` | `record` / `record struct` |
| --- | --- | --- | --- |
| Copy means | copy of the *reference* (shared object) | copy of the *data* | like class / like struct |
| Equality default | same object? | all fields equal | all properties equal |
| Lives | heap (managed by the GC) | inline (stack, or inside its container) | like class / like struct |
| Can be `null` | yes (`Person?`) | no (needs `Nullable<T>`) | as its base kind |

## Classes

```csharp
class Counter
{
    private int _value;                     // a field: private state

    public Counter(int start = 0)           // a constructor
    {
        _value = start;
    }

    public int Value => _value;             // read-only view

    public void Increment() => _value++;
}

var a = new Counter();
var b = a;                  // b and a point at the SAME object
a.Increment();
Console.WriteLine(b.Value); // 1
```

Key points:

- `new` allocates an object and runs a **constructor**.
- A class variable holds a *reference*. Assignment copies the reference, not the object.
- `null` is the default for reference variables; `a == b` compares references (unless the class overrides equality).
- Prefer `private` fields and expose what's needed through members (next lesson).

## Structs

```csharp
struct Point
{
    public double X;
    public double Y;

    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double DistanceTo(Point other) =>
        Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
}

var p = new Point(1, 2);
var q = p;                  // a full copy
q.X = 99;
Console.WriteLine(p.X);     // 1: p is unaffected
```

Structs are best for **small, short-lived, immutable data** (points, colors, money, ids). Guidelines (Microsoft's own): ≤ 16 bytes, logically a single value, immutable, not boxed often. Mutable structs are a famous source of bugs, because you modify a copy without noticing.

Make structs immutable with `readonly`:

```csharp
readonly struct Money(decimal amount, string currency)
{
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
}
```

(That uses a **primary constructor**; see below.)

## Records

A **record** is a class (or struct) designed for data, where the compiler generates equality, `ToString`, deconstruction and non-destructive copying for you:

```csharp
record Person(string Name, int Age);

var ada = new Person("Ada", 36);
var same = new Person("Ada", 36);

Console.WriteLine(ada == same);        // True: value-based equality
Console.WriteLine(ada);                // Person { Name = Ada, Age = 36 }

var older = ada with { Age = 37 };     // copy with a change
var (name, age) = ada;                 // deconstruct
```

The positional syntax `Person(string Name, int Age)` creates init-only properties `Name` and `Age`. Records are shallowly immutable by default; you can add members, interfaces and validation:

```csharp
record Temperature(double Celsius)
{
    public double Fahrenheit => Celsius * 9 / 5 + 32;
}
```

`record struct` (and `readonly record struct`) gives the same conveniences with value semantics:

```csharp
readonly record struct Point3(double X, double Y, double Z);
```

**Rule of thumb:** use a `record` for data you pass around and compare; use a `class` for things with identity and changing state (a connection, a service, a game entity); use a `readonly record struct` for tiny values.

## Primary constructors

C# 12 lets a class or struct take constructor parameters directly in its declaration:

```csharp
class Greeter(string greeting)
{
    public string Greet(string name) => $"{greeting}, {name}!";
}
```

The parameter `greeting` is in scope throughout the class body. In a non-record type it is *captured* (a hidden field), not a property. This is great for dependency injection:

```csharp
class OrderService(ILogger logger, IClock clock) { /* use logger and clock directly */ }
```

## Object initializers, `init` and `required`

```csharp
class Settings
{
    public required string Name { get; init; }       // must be set, then read-only
    public int Retries { get; init; } = 3;
    public string? Note { get; set; }                // changeable later
}

var s = new Settings { Name = "prod", Retries = 5 };
// var bad = new Settings();                         // error: 'Name' is required
// s.Name = "dev";                                   // error: init-only
```

`init` allows setting a property in the constructor or an initializer, but never afterwards. `required` makes forgetting one a compile error.

## Equality for classes

By default two class instances are equal only if they are the same object. To get value equality on a class, override `Equals` and `GetHashCode` together, or implement `IEquatable<T>`:

```csharp
class Sku : IEquatable<Sku>
{
    public string Code { get; }
    public Sku(string code) => Code = code;

    public bool Equals(Sku? other) => other is not null && Code == other.Code;
    public override bool Equals(object? obj) => Equals(obj as Sku);
    public override int GetHashCode() => Code.GetHashCode();
}
```

Or simply make it a record, which does all of that.

## Passing around: boxing and copying costs

Passing a struct copies it; passing a class copies a pointer. For big structs use `in` parameters. Treating a struct as `object` or an interface **boxes** it (allocates it on the heap), which can hurt performance in tight loops.

## Static members

```csharp
class IdGenerator
{
    private static int _next;
    public static int Next() => ++_next;     // belongs to the type, not an instance
}

int id = IdGenerator.Next();
```

A `static class` can only contain static members (utility holders like `Math` or `File`).

## Common mistakes

- **Mutating a struct copy:** `list[0].X = 5;` doesn't compile for a `List<Point>` precisely because it would modify a copy.
- **Expecting a class assignment to copy.** It shares.
- **Large mutable structs.** Make them classes or records.
- **Overriding `Equals` without `GetHashCode`.** Dictionaries and sets break.
- **Forgetting that `record` equality is shallow:** a record holding a `List<int>` compares the list *reference*, not its elements.
- **Using public fields instead of properties.** You can't later add validation without breaking callers.

## Exercises

1. Define `record Book(string Title, string Author, int Year)`; create two equal books, compare them, and print one.
2. Write a `readonly record struct Money(decimal Amount, string Currency)` with an `Add` method that rejects mismatched currencies.
3. Write a `class BankAccount` with a private balance, `Deposit`, `Withdraw` (rejecting overdrafts) and a read-only `Balance`.
4. Demonstrate reference vs value semantics with a class `Box` and a struct `BoxValue`, copying each and mutating the copy.
5. Make a class with a `required` property and show the compile error when you forget it.
6. Create a `Point` class with value-based equality by implementing `IEquatable<Point>`, and use it as a `Dictionary` key.

## Recap

- Classes are references with identity; structs are small values copied on assignment; records add value-based equality and `with`.
- Primary constructors, `init`, and `required` cut boilerplate and prevent half-built objects.
- Override `Equals`/`GetHashCode` together, or use a record.
- Prefer immutable data where you can.

Next: [Properties & members →](02-properties-and-members.md)
