# 6. Delegates, lambdas & LINQ

## What & why

Sometimes the thing you want to pass around isn't data but *behavior*: "sort these by length", "run this when the button is clicked", "keep only the even ones". C# does this with **delegates** (typed references to methods) and **lambdas** (concise inline functions). **LINQ** (Language Integrated Query) builds a whole vocabulary of data-processing operations on top.

## Delegates

A delegate type describes a method signature:

```csharp
delegate int BinaryOp(int a, int b);

int Add(int a, int b) => a + b;

BinaryOp op = Add;                // a method group converts to the delegate
Console.WriteLine(op(2, 3));      // 5
```

You rarely declare your own. The built-in generic ones cover nearly everything:

| Type | Signature |
| --- | --- |
| `Action` / `Action<T1, T2>` | takes arguments, returns nothing |
| `Func<TResult>` / `Func<T1, TResult>` | returns a value (last type argument) |
| `Predicate<T>` | `T` in, `bool` out |
| `Comparison<T>` | two `T`s in, `int` out |

```csharp
Func<int, int, int> add = (a, b) => a + b;
Action<string> log = msg => Console.WriteLine($"[log] {msg}");
Predicate<int> isEven = n => n % 2 == 0;
```

Delegates can be **combined**: `log += other;` calls both in order (this is how events work).

## Lambdas

```csharp
Func<int, int> square = x => x * x;                 // expression lambda
Func<int, int, int> max = (a, b) => a > b ? a : b;

Func<string, int> parse = s =>                      // statement lambda
{
    if (int.TryParse(s, out var n)) return n;
    return -1;
};

var greet = (string name = "world") => $"Hello, {name}";   // C# 12: default parameters
Console.WriteLine(greet());
```

Parameter types are normally inferred from the target type. C# 14 lets you add modifiers without spelling the types: `(text, out result) => int.TryParse(text, out result)`.

`static` lambdas promise not to capture anything: `static x => x * 2`.

### Closures: capturing variables

A lambda can use local variables from the enclosing scope. It captures the *variable*, not a copy of its value:

```csharp
int counter = 0;
Action tick = () => counter++;
tick(); tick();
Console.WriteLine(counter);      // 2
```

```csharp
var actions = new List<Action>();
for (int i = 0; i < 3; i++)
{
    int copy = i;                       // capture a per-iteration copy
    actions.Add(() => Console.Write(copy));
}
actions.ForEach(a => a());              // 012 (without 'copy' it would print 333)
```

`foreach` gives each iteration its own variable; a classic `for` loop shares one.

## Higher-order functions

A method that takes or returns a function:

```csharp
IEnumerable<T> Where2<T>(IEnumerable<T> source, Func<T, bool> predicate)
{
    foreach (var item in source)
        if (predicate(item)) yield return item;
}

Func<int, int> MakeAdder(int n) => x => x + n;
var plus5 = MakeAdder(5);
Console.WriteLine(plus5(10));      // 15
```

That `Where2` is, in essence, how LINQ's `Where` is built.

## LINQ: the core operators

LINQ methods are extension methods on `IEnumerable<T>` (from `System.Linq`, implicitly imported).

```csharp
record Person(string Name, int Age, string City);

List<Person> people =
[
    new("Ada", 36, "London"),
    new("Linus", 54, "Portland"),
    new("Grace", 85, "New York"),
    new("Alan", 41, "London"),
];

// Filter, project, order
var names = people
    .Where(p => p.Age > 40)
    .OrderBy(p => p.Name)
    .Select(p => p.Name);                 // Alan, Grace, Linus

// Aggregate
int total = people.Sum(p => p.Age);
double avg = people.Average(p => p.Age);
Person oldest = people.MaxBy(p => p.Age)!;
bool anyLondon = people.Any(p => p.City == "London");
bool allAdults = people.All(p => p.Age >= 18);
int count = people.Count(p => p.City == "London");

// Lookup
Person? ada = people.FirstOrDefault(p => p.Name == "Ada");
Person single = people.Single(p => p.Name == "Grace");     // throws if 0 or 2+

// Group
foreach (var g in people.GroupBy(p => p.City))
    Console.WriteLine($"{g.Key}: {string.Join(", ", g.Select(p => p.Name))}");

// Join to another sequence
var cities = new Dictionary<string, string> { ["London"] = "UK", ["Portland"] = "US" };
var withCountry = people.Where(p => cities.ContainsKey(p.City))
                        .Select(p => (p.Name, Country: cities[p.City]));

// Paging & set operations
var page = people.OrderBy(p => p.Name).Skip(2).Take(2);
var distinctCities = people.Select(p => p.City).Distinct();
var pairs = people.Zip(Enumerable.Range(1, 4), (p, i) => $"{i}. {p.Name}");
var flat = new[] { new[] { 1, 2 }, new[] { 3 } }.SelectMany(x => x);   // 1 2 3
var chunks = Enumerable.Range(1, 7).Chunk(3);                           // [1,2,3] [4,5,6] [7]
```

### Query syntax

LINQ also has a SQL-like syntax that compiles to the same calls:

```csharp
var q = from p in people
        where p.Age > 40
        orderby p.Name
        select p.Name;
```

Most C# code uses the method form. Query syntax shines for `join` and multiple `from` clauses.

## Deferred execution

A query isn't run when you write it; it runs when you **enumerate** it:

```csharp
var numbers = new List<int> { 1, 2, 3 };
var query = numbers.Where(n => n > 1);     // nothing happens yet
numbers.Add(4);
Console.WriteLine(string.Join(",", query));   // 2,3,4: includes the later addition!
```

Consequences:

- Enumerating twice runs the query twice (and may hit the database or disk twice).
- Capture the result with `ToList()`, `ToArray()`, `ToDictionary()` or `ToHashSet()` when you want a snapshot or to run it once.
- Operators like `Count()`, `Sum()`, `First()`, `ToList()` run immediately.
- Be careful returning a lazy query that depends on something that's about to be disposed.

## Custom iterators with `yield`

```csharp
IEnumerable<int> Evens(int limit)
{
    for (int i = 0; i <= limit; i += 2)
        yield return i;
}

foreach (var e in Evens(10)) Console.Write($"{e} ");   // 0 2 4 6 8 10
```

The compiler turns this method into a state machine. It produces values on demand, so infinite sequences are possible (`while (true) yield return next++;`) as long as the consumer stops (`Take(5)`).

## Performance notes

- LINQ allocates enumerators and delegates; in hot paths, a plain `for` loop may be several times faster. Measure first (Advanced level).
- Avoid multiple enumeration of expensive queries.
- `Count() > 0` is worse than `Any()`.
- `OrderBy` is a stable sort; `ThenBy` sorts by a second key.
- Newer operators: `CountBy`, `AggregateBy`, `Index`, `Order`, `OrderDescending`.

## Common mistakes

- **Expecting a query to be a snapshot.**
- **Modifying the source collection during enumeration.**
- **`Single()` vs `First()`:** `Single` throws on more than one match.
- **`First()` on an empty sequence** throws; use `FirstOrDefault()` and handle `null`.
- **Capturing the loop variable in a `for` loop.**
- **Side effects in `Select`/`Where`.** Queries should be pure; use `foreach` to do things.
- **Forgetting that `Select` on `IEnumerable` is lazy:** `list.Select(x => Console.WriteLine(x))` prints nothing.

## Exercises

1. Given a list of words, print them grouped by first letter, sorted, each group sorted by length then alphabetically.
2. Implement your own `Map`, `Filter` and `Reduce` as extension methods on `IEnumerable<T>`.
3. Write `Memoize<TIn, TOut>(Func<TIn, TOut>)` returning a caching function (use a `Dictionary`).
4. Compute word frequencies of a text file and print the top 10 using LINQ.
5. Write an infinite `Primes()` iterator and print the first 20 with `Take`.
6. Rewrite a nested `foreach` that builds a list as a LINQ query, then benchmark both with `Stopwatch`.
7. Show deferred execution: build a query, modify the source, and explain the output.

## Recap

- Delegates are typed method references; `Func`, `Action` and `Predicate` cover most needs; lambdas are inline functions that can capture variables.
- LINQ's `Where`/`Select`/`OrderBy`/`GroupBy`/`Any`/`Sum`... compose into readable pipelines.
- Queries are lazy: they run when enumerated. Materialize with `ToList()` when you need a snapshot.
- `yield return` writes your own lazy sequences.

Next: [Memory, GC & disposal →](07-memory-gc-and-disposal.md)
