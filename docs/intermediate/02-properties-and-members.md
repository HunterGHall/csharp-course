# 2. Properties & members

## What & why

A type is more than its data. This lesson covers the members you put inside a class or struct: **fields**, **properties**, **methods**, **indexers**, **events** (previewed), and the **access modifiers** that decide who can see them. Good members hide how things are stored while promising a stable interface.

## Fields

```csharp
class Account
{
    private decimal _balance;                   // mutable field
    private readonly string _id;                // can only be set in the constructor/initializer
    public const int MaxOwners = 4;             // compile-time constant
    public static readonly DateTime Epoch = new(2000, 1, 1);

    public Account(string id) => _id = id;
}
```

`readonly` fields are set once (in a constructor or at declaration), then locked. Prefer them wherever the value shouldn't change.

## Properties

A **property** looks like a field to callers but is really a pair of methods (`get` and `set`), so you can validate, compute, or change the implementation later.

```csharp
class Person
{
    public string Name { get; set; } = "";             // auto-property
    public int Age { get; private set; }               // public read, private write
    public DateOnly Born { get; init; }                // set only at creation

    public bool IsAdult => Age >= 18;                  // computed, read-only
}
```

### Properties with logic

```csharp
class Thermostat
{
    private double _target = 20;

    public double Target
    {
        get => _target;
        set
        {
            if (value is < 5 or > 30)
                throw new ArgumentOutOfRangeException(nameof(value), "5-30 only");
            _target = value;
        }
    }
}
```

### The `field` keyword (C# 14)

You no longer need a separate backing field just to add logic to one accessor. Inside an accessor, `field` refers to the compiler-generated backing field:

```csharp
class Person2
{
    public string Name
    {
        get;
        set => field = value?.Trim() ?? throw new ArgumentNullException(nameof(value));
    }
}
```

(On older compilers, declare `private string _name;` and use it instead.)

### `required` and `init`

See the previous lesson: `required` forces callers to supply a value, `init` allows it only at creation.

## Methods and expression-bodied members

```csharp
class Rectangle(double width, double height)
{
    public double Width { get; } = width;
    public double Height { get; } = height;

    public double Area => Width * Height;                 // property
    public double Perimeter() => 2 * (Width + Height);    // method

    public Rectangle Scale(double factor) => new(Width * factor, Height * factor);
}
```

## Access modifiers

| Modifier | Visible to |
| --- | --- |
| `public` | everyone |
| `private` | this type only (the default for members) |
| `protected` | this type and derived types |
| `internal` | this assembly (project); the default for top-level types |
| `protected internal` | derived types, or the same assembly |
| `private protected` | derived types **in** the same assembly |
| `file` | (types only) the same source file |

Make things as private as you can. Public members are promises you must keep.

## Constructors in depth

```csharp
class Config
{
    public string Host { get; }
    public int Port { get; }

    public Config() : this("localhost", 8080) { }     // chain to another constructor

    public Config(string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        Host = host;
        Port = port;
    }

    static Config() { /* runs once, before first use of the type */ }
}
```

Use factory methods when construction can fail or has several meaningful flavors:

```csharp
public static Config Parse(string text) { /* ... */ }
```

## Indexers

An indexer lets an object be used like an array or dictionary:

```csharp
class Playlist
{
    private readonly List<string> _songs = [];
    public int Count => _songs.Count;

    public string this[int index]
    {
        get => _songs[index];
        set => _songs[index] = value;
    }

    public void Add(string song) => _songs.Add(song);
}

var list = new Playlist();
list.Add("Intro");
Console.WriteLine(list[0]);
```

## Operators and conversions

```csharp
readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator *(Vec v, double k) => new(v.X * k, v.Y * k);
    public static implicit operator Vec((double x, double y) t) => new(t.x, t.y);
}

Vec v = new Vec(1, 2) + (3, 4);        // Vec { X = 4, Y = 6 }
```

Define operators only when the meaning is obvious (`+` for vectors, money). Otherwise write a named method.

## `ToString`

```csharp
class Card(string rank, string suit)
{
    public override string ToString() => $"{rank} of {suit}";
}

Console.WriteLine(new Card("Ace", "Spades"));    // Ace of Spades
```

## Extension methods

Add methods to a type you don't own by writing a `static` method with a `this` parameter:

```csharp
static class StringExtensions
{
    public static string Truncate(this string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

Console.WriteLine("A very long sentence".Truncate(6));   // A very…
```

C# 14 adds **extension blocks**, which can also declare extension properties and static members:

```csharp
static class StringExtensions2
{
    extension(string s)
    {
        public bool IsBlank => string.IsNullOrWhiteSpace(s);
        public string Truncate(int max) => s.Length <= max ? s : s[..max] + "…";
    }
}
```

LINQ is entirely made of extension methods on `IEnumerable<T>`.

## Partial classes

`partial` lets one type be split across files, usually so generated code and hand-written code can live side by side:

```csharp
// Person.cs
public partial class Person { public string Name { get; set; } = ""; }
// Person.Generated.cs
public partial class Person { public override string ToString() => Name; }
```

You'll meet this with source generators in the Advanced level.

## Events (preview)

Events let an object announce that something happened without knowing who listens:

```csharp
class Downloader
{
    public event EventHandler<int>? ProgressChanged;

    public void Run()
    {
        for (int pct = 0; pct <= 100; pct += 25)
            ProgressChanged?.Invoke(this, pct);
    }
}

var d = new Downloader();
d.ProgressChanged += (sender, pct) => Console.WriteLine($"{pct}%");
d.Run();
```

Events are built on delegates, the subject of lesson 6.

## Common mistakes

- **Public fields** instead of properties.
- **A getter with side effects or heavy work.** Callers assume properties are cheap.
- **Setters that leave an object invalid.** Prefer constructors and `init` plus validation.
- **Returning a mutable internal list** from a property. Callers can modify your state. Return `IReadOnlyList<T>` or a copy.
- **Overusing `static`** state, which makes code hard to test.
- **Operator overloads with surprising meaning.**

## Exercises

1. Build `class Temperature` with a `Celsius` property that rejects values below absolute zero (-273.15) and read-only `Fahrenheit` and `Kelvin` properties.
2. Write a `Matrix` class with a two-index indexer `this[int row, int col]`.
3. Write an extension method `IEnumerable<T>.Chunk`-like `Batches(int size)` (compare with the built-in `Chunk`).
4. Create a `Stopwatch`-like class that raises an event when a time limit passes.
5. Define `Money` with `+`, `-`, comparison operators and `ToString` that prints `12.50 USD`.
6. Refactor a class with public fields into one with properties and validation, keeping the same call sites compiling where possible.

## Recap

- Fields hold state; properties wrap it with validation and computed values; `init`, `required` and `readonly` express intent.
- Access modifiers limit your public surface. Default to `private`.
- Indexers, operators and `ToString` make custom types feel native; extension methods and blocks add members you don't own.
- Events announce changes without coupling.

Next: [Enums & pattern matching →](03-enums-and-pattern-matching.md)
