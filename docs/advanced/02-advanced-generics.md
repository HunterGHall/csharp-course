# 2. Advanced generics

## What & why

Generics were introduced in Intermediate. This lesson covers the parts that matter when you design libraries: **variance** (when is `IEnumerable<Cat>` usable as `IEnumerable<Animal>`?), **generic math**, how the runtime specializes generics, and type-level techniques that move errors to compile time.

## Variance

Suppose `Cat : Animal`. Is a `List<Cat>` also a `List<Animal>`? If it were, this would compile:

```csharp
List<Animal> animals = catList;      // pretend this were allowed
animals.Add(new Dog());              // now catList contains a Dog!
```

So `List<T>` is **invariant**: no conversion either way. But interfaces and delegates that only *produce* or only *consume* `T` can safely allow one:

```csharp
// Covariant: T only comes OUT. IEnumerable<out T>
IEnumerable<Cat> cats = [new Cat(), new Cat()];
IEnumerable<Animal> animals = cats;                   // OK

// Contravariant: T only goes IN. IComparer<in T>, Action<in T>
Action<Animal> feed = a => Console.WriteLine($"feeding {a}");
Action<Cat> feedCat = feed;                           // OK: anything that feeds animals can feed cats
IComparer<Animal> byName = Comparer<Animal>.Create((x, y) => string.Compare(x.Name, y.Name));
IComparer<Cat> catComparer = byName;                  // OK
```

Declare variance on your own interfaces and delegates with `out` and `in`:

```csharp
interface IProducer<out T> { T Make(); }              // T only appears in output positions
interface IConsumer<in T> { void Take(T item); }      // T only appears in input positions
delegate TResult Transformer<in TIn, out TResult>(TIn input);
```

The compiler checks that you really only use `T` in the allowed positions. Variance applies only to **reference types** (`IEnumerable<int>` is not an `IEnumerable<object>`, since converting would require boxing each item).

**Array covariance is the old, unsafe version:** `Animal[] a = new Cat[1]; a[0] = new Dog();` compiles and throws `ArrayTypeMismatchException`. Avoid relying on it.

## Constraints revisited

```csharp
class Cache<TKey, TValue>
    where TKey : notnull, IEquatable<TKey>
    where TValue : class
{ }

T Create<T>() where T : new() => new T();

void Process<T>(T value) where T : struct, Enum { }   // any enum, via the Enum constraint

U Convert<T, U>(T value) where T : U => value;        // T derives from U

void Fill<T>(Span<T> span) where T : unmanaged { }
void Use<T>(T item) where T : allows ref struct { }   // T may be a Span<char> etc. (C# 13)
```

Notes:

- `notnull` blocks `string?` as `TKey` and gives warnings, not errors, since it is a nullable-analysis constraint.
- `unmanaged` means "contains no references": needed for pointers and `stackalloc`.
- Constraints on interface implementations must match those on the interface.

## Generic math in practice

Because numeric types implement interfaces such as `INumber<T>`, `IBinaryInteger<T>`, `IFloatingPoint<T>` and `IAdditionOperators<TSelf, TOther, TResult>`, you can write algorithms once:

```csharp
using System.Numerics;

T Sum<T>(ReadOnlySpan<T> values) where T : INumber<T>
{
    T total = T.Zero;
    foreach (var v in values) total += v;
    return total;
}

TResult Average<T, TResult>(IReadOnlyCollection<T> xs)
    where T : INumber<T>
    where TResult : INumber<TResult>
{
    T sum = T.Zero;
    foreach (var x in xs) sum += x;
    return TResult.CreateChecked(sum) / TResult.CreateChecked(xs.Count);
}

Console.WriteLine(Average<int, double>([1, 2, 4]));    // 2.3333333333333335
```

`T.CreateChecked`, `CreateSaturating` and `CreateTruncating` convert between numeric types with a chosen overflow behavior. For bit twiddling use `IBinaryInteger<T>`: `T.PopCount(x)`, `T.LeadingZeroCount(x)`.

For `Vector<T>`-style SIMD code, generic math lets one routine serve `float`, `double` and the integer types.

## How the runtime specializes generics

.NET generics are **reified** and compiled by the JIT:

- For **value-type** arguments, the JIT produces separate machine code for each instantiation (`List<int>` and `List<long>` are different compiled types). No boxing, and the JIT can inline everything.
- For **reference-type** arguments, a single *shared* instantiation is used for all (`List<string>` and `List<Customer>` share code), with a hidden dictionary for type-specific lookups.

Implications:

- A generic method over `T : struct` can be as fast as hand-written code.
- Code bloat is possible with many value-type instantiations, mostly a concern for AOT.
- `typeof(T)` and `default(T)` are cheap; the JIT folds them for value types.
- Calling an interface method on an unconstrained `T` that is a struct does **not** box if you constrain `T` to the interface (a *constrained call*):

```csharp
int CompareTwice<T>(T a, T b) where T : IComparable<T> =>
    a.CompareTo(b) + b.CompareTo(a);       // no boxing for T = int
```

while `IComparable x = a` would box.

## Type-level techniques

### Phantom types

A type parameter that never appears in the data, only in the type, to prevent mixing units or states:

```csharp
readonly record struct Quantity<TUnit>(double Value)
{
    public static Quantity<TUnit> operator +(Quantity<TUnit> a, Quantity<TUnit> b) =>
        new(a.Value + b.Value);
}

abstract class Meters;
abstract class Seconds;

var d = new Quantity<Meters>(5) + new Quantity<Meters>(3);
// var oops = new Quantity<Meters>(5) + new Quantity<Seconds>(1);   // compile error
```

### The builder with required steps

```csharp
interface IHasUrl { }
interface IMissingUrl { }

class RequestBuilder<TUrl>
{
    private string? _url;
    private RequestBuilder() { }
    public static RequestBuilder<IMissingUrl> Create() => new();

    public RequestBuilder<IHasUrl> Url(string url) => new() { _url = url };
}

static class RequestBuilderExtensions
{
    public static Request Build(this RequestBuilder<IHasUrl> b) => new Request();
}
class Request;
```

You can only call `Build()` after `Url(...)`. (Elaborate; use sparingly.)

### Generic attributes

Attributes can be generic since C# 11:

```csharp
class ValidatorAttribute<T> : Attribute where T : IValidator, new() { }

[Validator<EmailValidator>]
class SignupForm { }
```

## Open and closed generic types, and reflection

```csharp
Type open = typeof(List<>);               // open generic
Type closed = typeof(List<int>);          // closed
Type made = open.MakeGenericType(typeof(string));   // List<string> at run time

Console.WriteLine(closed.GetGenericTypeDefinition() == open);   // True
```

Dependency-injection containers register open generics (`services.AddScoped(typeof(IRepository<>), typeof(Repository<>))`). Creating generic instances by reflection is slow compared with compiled code, and it's a problem for trimming and AOT.

## Generic methods and delegates: inference limits

Type inference uses arguments, not return types:

```csharp
T Create<T>() => default!;
// var x = Create();             // error: cannot infer T
var y = Create<int>();           // OK

TOut Map<TIn, TOut>(TIn x, Func<TIn, TOut> f) => f(x);
var len = Map("hello", s => s.Length);   // TIn = string, TOut = int
```

Provide type arguments explicitly (or add a parameter that carries the type) when inference can't see them.

## Pitfalls

- **Static fields are per closed type:** `Counter<int>.Count` and `Counter<string>.Count` are separate variables.
- **Overloading with generics** can pick surprising overloads; prefer distinct names.
- **Too many type parameters** make APIs unreadable. Wrap related ones in a type.
- **`default(T)` is `null` for reference types,** so use `T?` carefully with unconstrained `T`.
- **Reflection-created generics break AOT/trimming** when the combination isn't statically visible.

## Common mistakes

- **Making an interface covariant when it has a method taking `T`.** The compiler rejects it (CS1961).
- **Expecting `List<Derived>` to convert to `List<Base>`.**
- **Using `object` where a constrained `T` avoids boxing.**
- **Writing numeric code for `double` only,** forcing callers to convert.
- **Constraining too tightly** (`class`) when `notnull` would allow structs.

## Exercises

1. Declare `IReader<out T>` and `IWriter<in T>` and demonstrate each conversion with `Cat`/`Animal`. Which operations does the compiler forbid?
2. Write `Median<T>(IEnumerable<T>) where T : INumber<T>`.
3. Implement `Matrix<T> where T : INumber<T>` with `*` and test it for `int` and `double`.
4. Create the `Quantity<TUnit>` phantom type with `Meters` and `Seconds`, and add `Speed = Meters / Seconds` as a separate type.
5. Use `MakeGenericType` to create `Dictionary<string, int>` at run time and add an item by reflection. Compare its speed to the compiled version.
6. Demonstrate with a micro-benchmark that `where T : IComparable<T>` avoids boxing but `IComparable` does not.

## Recap

- `out` makes an interface covariant, `in` contravariant; `List<T>` and arrays-as-writable stay invariant.
- Constraints include `struct`, `unmanaged`, `notnull`, `allows ref struct` and interface types with `static abstract` members.
- Generic math lets one algorithm serve every numeric type.
- Value-type instantiations are specialized (fast); reference-type instantiations share code.
- Phantom types and generic attributes push more checking to compile time.

Next: [Reflection & attributes →](03-reflection-and-attributes.md)
