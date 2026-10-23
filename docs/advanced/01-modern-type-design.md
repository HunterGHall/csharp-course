# 1. Modern type design

## What & why

The Intermediate level taught the building blocks. This lesson is about using them well: designing types so that **invalid states can't be represented**, that behavior lives where the data is, and that the compiler does as much checking as possible. It also introduces `static abstract` interface members, the feature that powers generic math.

## Make illegal states unrepresentable

Compare two designs for "a shipment may be pending, shipped (with a tracking number), or delivered (with a date)":

```csharp
// Bad: every combination is possible, so every consumer must guess.
class Shipment
{
    public string Status { get; set; } = "";
    public string? TrackingNumber { get; set; }   // only valid when shipped?
    public DateTime? DeliveredOn { get; set; }    // only valid when delivered?
}

// Better: each state carries exactly the data that makes sense for it.
abstract record ShipmentState;
sealed record Pending : ShipmentState;
sealed record Shipped(string TrackingNumber) : ShipmentState;
sealed record Delivered(string TrackingNumber, DateOnly On) : ShipmentState;

string Describe(ShipmentState s) => s switch
{
    Pending => "Packing",
    Shipped { TrackingNumber: var t } => $"On the way ({t})",
    Delivered d => $"Delivered {d.On}",
    _ => throw new ArgumentOutOfRangeException(nameof(s)),
};
```

The second design makes a "delivered shipment with no date" impossible to write.

## Value objects: wrap primitives

A `string` can be an email, a name, or a password; an `int` can be an id or a quantity. Wrap meaningful primitives so they can't be mixed up and so validation happens once:

```csharp
readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

readonly record struct OrderId(Guid Value);

void Ship(OrderId order, CustomerId customer) { }
// Ship(customerId, orderId);   // compile error: the types differ
```

```csharp
sealed record EmailAddress
{
    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static bool TryCreate(string? text, out EmailAddress? email)
    {
        if (text is { Length: > 3 } && text.Contains('@') && !text.StartsWith('@'))
        {
            email = new EmailAddress(text.Trim().ToLowerInvariant());
            return true;
        }
        email = null;
        return false;
    }
}
```

A private constructor plus a factory means every `EmailAddress` in the program has already been validated.

## Immutability and `with`

Prefer types that never change after construction. They are trivially thread-safe, safe as dictionary keys, and easy to reason about. When you need a changed version, use `with`:

```csharp
var order2 = order with { Quantity = order.Quantity + 1 };
```

For collections inside records, expose `IReadOnlyList<T>` or `ImmutableArray<T>` (from `System.Collections.Immutable`), but remember record equality compares the collection *reference*. For value equality of the contents, use `ImmutableArray` with a custom `Equals`, or compare with `SequenceEqual`.

## `required`, `init` and primary constructors, together

```csharp
public sealed class Options
{
    public required string ConnectionString { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
```

`required` + `init` gives you named-argument-style construction with compile-time completeness checks, and works well with configuration binding and serializers.

## Static abstract interface members

Interfaces can require **static** members, so generic code can call `T.Something` on a type parameter:

```csharp
interface IShape<TSelf> where TSelf : IShape<TSelf>
{
    static abstract string Name { get; }
    static abstract TSelf Parse(string text);
    double Area { get; }
}

readonly record struct Square(double Side) : IShape<Square>
{
    public static string Name => "square";
    public static Square Parse(string text) => new(double.Parse(text));
    public double Area => Side * Side;
}

string Describe<T>(string input) where T : IShape<T>
{
    T shape = T.Parse(input);                     // calls a static method on T
    return $"{T.Name} with area {shape.Area}";
}

Console.WriteLine(Describe<Square>("3"));         // square with area 9
```

Before this feature you needed instances, factories or reflection. The pattern `where TSelf : IFoo<TSelf>` (the *curiously recurring* pattern) lets an interface refer to the implementing type.

### Operators in interfaces

`static abstract` also covers operators, which is how `INumber<T>` works:

```csharp
interface IAddable<T> where T : IAddable<T>
{
    static abstract T operator +(T a, T b);
    static abstract T Zero { get; }
}

T SumAll<T>(IEnumerable<T> xs) where T : IAddable<T>
{
    var total = T.Zero;
    foreach (var x in xs) total += x;
    return total;
}
```

### The standard parsing interfaces

The BCL uses the same trick for parsing and formatting:

```csharp
T ParseOrDefault<T>(string s, T fallback) where T : IParsable<T> =>
    T.TryParse(s, null, out var v) ? v : fallback;

Console.WriteLine(ParseOrDefault("42", 0));          // 42
Console.WriteLine(ParseOrDefault("x", 1.5));         // 1.5
Console.WriteLine(ParseOrDefault("2026-10-23", DateOnly.MinValue));
```

Implement `IParsable<T>` on your own value objects and they plug into the same generic code (and into ASP.NET Core model binding).

## Interfaces and records for roles

Prefer small role interfaces:

```csharp
interface IReadStore<T> { T? Find(int id); IEnumerable<T> All(); }
interface IWriteStore<T> { void Add(T item); }
class Store<T> : IReadStore<T>, IWriteStore<T> { /* ... */ }
```

Hand out `IReadStore<T>` to code that only reads. Callers now cannot write by accident, and tests need fewer fakes.

## Equality contracts

If you define value-like equality on a class or struct:

1. Implement `IEquatable<T>`, plus override `Equals(object?)` and `GetHashCode()` consistently.
2. Make the type immutable (or at least the members involved in equality).
3. Override `==`/`!=` only if you also do the above. Records do all of it for you.

Use `HashCode.Combine(a, b, c)` to build hash codes:

```csharp
public override int GetHashCode() => HashCode.Combine(Code, Region);
```

## API design guidelines

- Accept the most general parameter type (`IEnumerable<T>`, `ReadOnlySpan<T>`), return the most useful specific one (`IReadOnlyList<T>`, `List<T>` for private use).
- Don't return `null` collections; return empty ones.
- Avoid boolean parameters. Use enums or separate methods: `Save(overwrite: true)` is OK; `Save(true, false, true)` is not.
- Make methods that can fail say so: `TryParse`, `TryGetValue`, or a result type.
- Prefer composition and small sealed classes. Design for inheritance explicitly or forbid it.
- Keep public surface small; every `public` is a promise.

## Common mistakes

- **Primitive obsession:** `string`/`int` everywhere, with validation scattered.
- **Public setters on everything,** so invariants can be broken from anywhere.
- **Using `record` for entities with identity and lifecycle** (equality by value is wrong for those).
- **Infinite `where T : IFoo<T>` constraints** when a simple interface would do.
- **Exposing `List<T>` as a property,** which lets callers mutate your state.
- **Inheritance for code reuse,** instead of composition.

## Exercises

1. Model a payment as `Pending`, `Authorized(AuthCode)`, `Captured(AuthCode, DateTimeOffset)`, `Refunded(decimal)` and write `Describe`.
2. Create `Percentage`, a `readonly record struct` that can't be outside 0-100, with `IParsable<Percentage>`.
3. Write `T Parse<T>(string)` constrained to `IParsable<T>` and use it for `int`, `Guid` and your own type.
4. Define `IMonoid<T>` (`Zero` and `+`) with `static abstract` members; implement it for `int` and `string` wrappers and write a generic `Fold`.
5. Refactor a class with five public setters into an immutable record with a factory method that validates.
6. Split a `Repository` into read and write interfaces and change one consumer to use only the read half.

## Recap

- Encode rules in types: records per state, value objects instead of primitives, private constructors plus factories.
- Prefer immutability and `with`.
- `static abstract` interface members let generic code call static members and operators on `T`; `IParsable<T>` and `INumber<T>` are built on them.
- Keep surfaces small; accept general, return specific.

Next: [Advanced generics →](02-advanced-generics.md)
