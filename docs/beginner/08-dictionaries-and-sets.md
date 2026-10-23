# 8. Dictionaries & sets

## What & why

A **dictionary** maps keys to values and finds a value from its key in (roughly) constant time: phone books, word counts, caches, configuration. A **set** is an unordered collection of unique items with fast "is it in there?" checks and set algebra (union, intersection...). Both are built on hashing.

## `Dictionary<TKey, TValue>`

```csharp
var ages = new Dictionary<string, int>
{
    ["Ada"] = 36,
    ["Linus"] = 54,
};

ages["Grace"] = 85;            // add
ages["Ada"] = 37;              // overwrite
Console.WriteLine(ages["Ada"]);          // 37
Console.WriteLine(ages.Count);           // 3

ages.Remove("Linus");
Console.WriteLine(ages.ContainsKey("Linus"));   // False
```

Reading a missing key with the indexer **throws** `KeyNotFoundException`. Use `TryGetValue` when absence is normal:

```csharp
if (ages.TryGetValue("Alan", out int age))
{
    Console.WriteLine($"Alan is {age}");
}
else
{
    Console.WriteLine("no Alan");
}

int unknown = ages.GetValueOrDefault("Nobody");      // 0
int withDefault = ages.GetValueOrDefault("Nobody", -1);
```

`Add(key, value)` throws if the key already exists; the indexer assignment doesn't. `TryAdd` returns `false` instead of throwing.

### Iterating

```csharp
foreach (var (name, age) in ages)
{
    Console.WriteLine($"{name}: {age}");
}

foreach (var name in ages.Keys) { }
foreach (var age in ages.Values) { }
```

Don't rely on the order. In practice `Dictionary` often enumerates in insertion order until you remove something, but that isn't guaranteed. Use `SortedDictionary<K,V>` for sorted keys, or sort at the point of use:

```csharp
foreach (var (name, age) in ages.OrderBy(p => p.Key))
{
    Console.WriteLine($"{name}: {age}");
}
```

## The counting idiom

```csharp
string text = "the cat and the hat and the bat";
var counts = new Dictionary<string, int>();

foreach (var word in text.Split(' '))
{
    counts[word] = counts.GetValueOrDefault(word) + 1;
}

foreach (var (word, n) in counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
{
    Console.WriteLine($"{word,-4} {n}");
}
// the  3
// and  2
// bat  1 ...
```

There is a shorter way with LINQ's `GroupBy` or `CountBy` (.NET 9+):

```csharp
var counts2 = text.Split(' ').CountBy(w => w).ToDictionary();
```

## Grouping

```csharp
string[] words = ["apple", "avocado", "banana", "blueberry", "cherry"];

var byLetter = words.GroupBy(w => w[0]);
foreach (var group in byLetter)
{
    Console.WriteLine($"{group.Key}: {string.Join(", ", group)}");
}
// a: apple, avocado
// b: banana, blueberry
// c: cherry
```

To get a dictionary of lists: `words.GroupBy(w => w[0]).ToDictionary(g => g.Key, g => g.ToList())`. `ToLookup` gives a read-only multi-map.

## Collection initializers and nesting

```csharp
var inventory = new Dictionary<string, List<string>>
{
    ["fruit"] = ["apple", "pear"],
    ["veg"] = ["kale"],
};
inventory["veg"].Add("leek");

// Adding to a list under a possibly-missing key:
if (!inventory.TryGetValue("grain", out var list))
{
    list = [];
    inventory["grain"] = list;
}
list.Add("rice");
```

## What can be a key?

A key's type needs sensible **equality and hashing**: equal keys must give the same hash code. Strings, numbers, enums, tuples and records all work. Custom classes use *reference* equality unless you override `Equals` and `GetHashCode` (Intermediate level). Never mutate a key object in a way that changes its hash while it is in a dictionary.

Pass a comparer to change how keys compare:

```csharp
var ci = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
ci["Ada"] = 1;
Console.WriteLine(ci["ADA"]);    // 1
```

## `HashSet<T>`

```csharp
var seen = new HashSet<int> { 1, 2, 3 };

Console.WriteLine(seen.Add(4));    // True: it was new
Console.WriteLine(seen.Add(2));    // False: already there
Console.WriteLine(seen.Contains(3));   // True
seen.Remove(1);
```

Typical use: remove duplicates and detect repeats.

```csharp
int[] nums = [3, 1, 3, 2, 1];
var unique = new HashSet<int>(nums);               // {3, 1, 2}

bool HasDuplicate(IEnumerable<int> xs)
{
    var seenSoFar = new HashSet<int>();
    foreach (var x in xs)
    {
        if (!seenSoFar.Add(x)) return true;
    }
    return false;
}
```

### Set algebra

```csharp
var a = new HashSet<int> { 1, 2, 3, 4 };
var b = new HashSet<int> { 3, 4, 5 };

var union = new HashSet<int>(a);      union.UnionWith(b);        // 1 2 3 4 5
var common = new HashSet<int>(a);     common.IntersectWith(b);   // 3 4
var onlyA = new HashSet<int>(a);      onlyA.ExceptWith(b);       // 1 2
var either = new HashSet<int>(a);     either.SymmetricExceptWith(b);   // 1 2 5

Console.WriteLine(a.IsSupersetOf([3, 4]));    // True
Console.WriteLine(a.Overlaps(b));             // True
```

These methods mutate the set. LINQ's `Union`, `Intersect`, `Except` return new sequences instead.

## Other collections worth knowing

| Type | Use |
| --- | --- |
| `SortedDictionary<K,V>`, `SortedSet<T>` | keep items ordered |
| `Queue<T>` | first in, first out |
| `Stack<T>` | last in, first out |
| `LinkedList<T>` | cheap inserts anywhere (rarely the right choice) |
| `ImmutableList<T>`, `FrozenDictionary<K,V>` | read-only, thread-safe snapshots |

## Common mistakes

- **Using the indexer on a missing key** instead of `TryGetValue` / `GetValueOrDefault`.
- **Relying on enumeration order.**
- **Mutating a collection while enumerating it** (including removing from a dictionary inside `foreach`).
- **Using a mutable object as a key** and then changing the part that affects `GetHashCode`.
- **Case-sensitive surprises:** `"Ada"` and `"ada"` are different keys unless you pass a comparer.
- **Using a `List<T>` and `Contains` in a hot loop** where a `HashSet<T>` would make each lookup constant time instead of linear.

## Exercises

1. Count how often each character appears in a string and print the three most common.
2. Group a list of words by length and print each group.
3. Given two lists of names, print who appears in both, who is only in the first, and who is only in the second.
4. Build a phone book: commands `add NAME NUMBER`, `find NAME`, `list`. Use `TryGetValue`.
5. Detect whether a list has duplicates in one pass using a `HashSet`.
6. Invert a dictionary (value to key). What happens if two keys share a value?
7. Make a case-insensitive word counter using a `StringComparer`.

## Recap

- Dictionaries map keys to values; `TryGetValue` and `GetValueOrDefault` handle missing keys safely.
- The counting idiom is `d[k] = d.GetValueOrDefault(k) + 1`; `GroupBy` handles grouping.
- Enumeration order isn't guaranteed; sort at the point of use.
- `HashSet<T>` gives uniqueness, fast membership and set algebra.

Next: [Methods →](09-methods.md)
