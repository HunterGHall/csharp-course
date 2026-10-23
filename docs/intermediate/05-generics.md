# 5. Generics

## What & why

You've used `List<int>` and `Dictionary<string, int>` since the Beginner level. The `<int>` is a **type parameter**: `List<T>` is written once and works for any `T`, with full type safety and no casting. This lesson shows how to write your own generic methods and types and how to constrain them.

## The problem generics solve

Without generics you'd either duplicate code per type (`IntStack`, `StringStack`) or store everything as `object` and cast:

```csharp
var items = new ArrayList();      // old non-generic collection
items.Add(1);
items.Add("two");                 // compiles
int n = (int)items[1];            // InvalidCastException at runtime
```

Generics move that failure to compile time and avoid boxing value types.

## Generic methods

```csharp
T Max<T>(T a, T b) where T : IComparable<T> =>
    a.CompareTo(b) >= 0 ? a : b;

Console.WriteLine(Max(3, 9));            // T inferred as int
Console.WriteLine(Max("pear", "apple")); // T inferred as string
Console.WriteLine(Max<double>(2, 3.5));  // T given explicitly
```

The compiler infers `T` from the arguments. The `where` clause is a **constraint**: it says what `T` must be able to do. Without it, `a.CompareTo(b)` wouldn't compile, because the compiler knows nothing about an arbitrary `T`.

```csharp
void Swap<T>(ref T a, ref T b) => (a, b) = (b, a);

IEnumerable<T> Repeat<T>(T item, int times)
{
    for (int i = 0; i < times; i++) yield return item;
}
```

## Generic types

```csharp
class Stack<T>
{
    private readonly List<T> _items = [];

    public int Count => _items.Count;
    public void Push(T item) => _items.Add(item);

    public T Pop()
    {
        if (_items.Count == 0) throw new InvalidOperationException("stack is empty");
        T top = _items[^1];
        _items.RemoveAt(_items.Count - 1);
        return top;
    }

    public bool TryPeek(out T? item)
    {
        if (_items.Count == 0) { item = default; return false; }
        item = _items[^1];
        return true;
    }
}

var s = new Stack<string>();
s.Push("a");
s.Push("b");
Console.WriteLine(s.Pop());   // b
```

`default` (or `default(T)`) is the zero value of `T`: `0`, `false`, or `null`.

You can have several parameters:

```csharp
record Pair<TFirst, TSecond>(TFirst First, TSecond Second);

var p = new Pair<string, int>("age", 36);
```

## Constraints

| Constraint | Meaning |
| --- | --- |
| `where T : class` | reference type |
| `where T : struct` | non-nullable value type |
| `where T : notnull` | non-nullable (value or reference) |
| `where T : new()` | has a public parameterless constructor |
| `where T : Base` | derives from `Base` |
| `where T : IFoo` | implements `IFoo` |
| `where T : U` | derives from another type parameter |
| `where T : unmanaged` | contains no references (for interop/Span) |
| `where T : allows ref struct` | may be a `ref struct` such as `Span<T>` |

Combine them: `where T : class, IDisposable, new()`.

```csharp
T CreateAndInit<T>() where T : IInitializable, new()
{
    var t = new T();
    t.Init();
    return t;
}
```

## Generic interfaces and inheritance

```csharp
interface IRepository<T> where T : class
{
    T? Find(int id);
    void Add(T item);
    IEnumerable<T> All();
}

class InMemoryRepository<T> : IRepository<T> where T : class
{
    private readonly Dictionary<int, T> _items = [];
    private int _next = 1;

    public T? Find(int id) => _items.GetValueOrDefault(id);
    public void Add(T item) => _items[_next++] = item;
    public IEnumerable<T> All() => _items.Values;
}
```

## Generic math

Because .NET 7 numeric types implement interfaces like `INumber<T>`, you can write one method for every numeric type:

```csharp
using System.Numerics;

T Sum<T>(IEnumerable<T> values) where T : INumber<T>
{
    T total = T.Zero;
    foreach (var v in values) total += v;
    return total;
}

Console.WriteLine(Sum([1, 2, 3]));          // 6
Console.WriteLine(Sum([1.5, 2.5]));         // 4
Console.WriteLine(Sum([1.5m, 2.5m]));       // 4.0
```

The Advanced level goes deeper (`static abstract` members).

## Covariance & contravariance (a first look)

`IEnumerable<out T>` is **covariant**: an `IEnumerable<string>` can be used where `IEnumerable<object>` is expected, because you can only read items out.

```csharp
IEnumerable<string> names = ["a", "b"];
IEnumerable<object> objs = names;       // allowed
```

`List<T>` is invariant, because you could also write into it: `List<string>` is not a `List<object>`. Delegates like `Action<in T>` are contravariant. See the Advanced level.

## Generics at run time

.NET generics are *reified*: `List<int>` and `List<string>` are distinct types at run time, and value-type instantiations are specialized (so `List<int>` stores ints directly, with no boxing). That is a big difference from Java's erasure. `typeof(T)` works inside generic code:

```csharp
string Describe<T>() => typeof(T).Name;
Console.WriteLine(Describe<List<int>>());   // List`1
```

## Common mistakes

- **Missing constraint:** "Operator '+' cannot be applied to operands of type 'T'". Add `INumber<T>` or similar.
- **`new T()` without `where T : new()`.**
- **Over-generalizing.** Don't make something generic until you have two real uses.
- **Assuming `List<Derived>` is a `List<Base>`.** It isn't (invariance).
- **Comparing `T` with `==` for unconstrained `T`.** Use `EqualityComparer<T>.Default.Equals(a, b)` or constrain to `IEquatable<T>`.
- **Forgetting that `default(T)` may be `null`.**

## Exercises

1. Write `T? FirstOrNull<T>(IEnumerable<T> xs) where T : struct`.
2. Implement a generic `Queue<T>` using two stacks.
3. Write `Dictionary<TKey, List<TValue>>`-based `MultiMap<TKey, TValue>` with `Add` and `Get`.
4. Create `IRepository<T>` with an in-memory implementation; use it for two different record types.
5. Write `Average<T>(IEnumerable<T>) where T : INumber<T>` returning `double`.
6. Write a generic `Cache<TKey, TValue>` with a `GetOrAdd(key, Func<TKey, TValue> factory)` method.

## Recap

- Generics let one definition work over many types, safely and without boxing.
- Constraints (`where`) tell the compiler what operations `T` supports.
- Generic types, methods, interfaces and records all take type parameters; the compiler usually infers them.
- `INumber<T>` and friends make numeric code generic; variance rules govern `IEnumerable<out T>`.

Next: [Delegates, lambdas & LINQ →](06-delegates-lambdas-linq.md)
