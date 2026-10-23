# 7. Arrays & lists

## What & why

Most programs work with *many* of something: scores, names, lines in a file. C# gives you two workhorses: the fixed-size **array** (`int[]`) and the growable **`List<T>`**. In practice you'll use `List<T>` most of the time and arrays when the size is fixed or an API demands one.

## Arrays

```csharp
int[] scores = [90, 72, 85];          // collection expression
int[] zeros = new int[5];             // five zeros: 0 0 0 0 0
string[] names = new string[3];       // three nulls
var primes = new[] { 2, 3, 5, 7 };    // type inferred: int[]

Console.WriteLine(scores[0]);         // 90 (zero-based)
Console.WriteLine(scores.Length);     // 3
scores[1] = 75;
```

An array's length is fixed at creation. Going out of range throws `IndexOutOfRangeException`; it never silently reads garbage.

### Indexes and ranges

```csharp
int[] a = [10, 20, 30, 40, 50];

a[^1]          // 50: last
a[^2]          // 40
a[1..3]        // [20, 30]: new array, end exclusive
a[..2]         // [10, 20]
a[3..]         // [40, 50]
```

### Useful `Array` helpers

```csharp
Array.Sort(a);
Array.Reverse(a);
int pos = Array.IndexOf(a, 30);
Array.Resize(ref a, 10);              // creates a new, bigger array
int[] copy = (int[])a.Clone();
```

### Multidimensional arrays

```csharp
int[,] grid = new int[3, 4];          // 3 rows, 4 columns
grid[1, 2] = 7;
Console.WriteLine(grid.GetLength(0)); // 3

int[][] jagged = [[1], [2, 3], [4, 5, 6]];   // arrays of arrays
```

## `List<T>`

```csharp
var names = new List<string> { "Ada", "Linus" };
names.Add("Grace");
names.AddRange(["Alan", "Margaret"]);
names.Insert(0, "Dennis");
names.Remove("Linus");          // removes the first match
names.RemoveAt(0);              // removes by index

Console.WriteLine(names.Count);         // note: Count, not Length
Console.WriteLine(names[1]);
Console.WriteLine(names.Contains("Ada"));
Console.WriteLine(names.IndexOf("Grace"));
```

Collection expressions work too: `List<int> xs = [1, 2, 3];`.

`List<T>` is backed by an array that doubles when it fills up, so appending is cheap on average. Removing or inserting near the front is slower, because everything shifts.

## Sorting and searching

```csharp
var nums = new List<int> { 5, 3, 9, 1 };
nums.Sort();                               // in place: 1 3 5 9
nums.Reverse();                            // 9 5 3 1

var words = new List<string> { "pear", "fig", "banana" };
words.Sort((a, b) => a.Length.CompareTo(b.Length));   // by length
words.Sort(StringComparer.OrdinalIgnoreCase);

int firstBig = nums.FindIndex(n => n > 4);
bool anyNeg = nums.Exists(n => n < 0);
```

## A first taste of LINQ

LINQ methods work on arrays, lists and any other sequence:

```csharp
int[] data = [4, 8, 15, 16, 23, 42];

var evens = data.Where(n => n % 2 == 0);          // 4 8 16 42
var doubled = data.Select(n => n * 2);            // 8 16 30 32 46 84
int total = data.Sum();                           // 108
double avg = data.Average();                      // 18
int biggest = data.Max();
var sorted = data.OrderByDescending(n => n);      // 42 23 16 15 8 4
bool any = data.Any(n => n > 40);                 // true
int first = data.First(n => n > 10);              // 15 (throws if none; FirstOrDefault returns 0)

foreach (var n in data.Where(n => n > 10).Select(n => n * 2))
{
    Console.Write($"{n} ");                       // 30 32 46 84
}
```

`n => n % 2 == 0` is a **lambda**: a tiny anonymous function. These queries are *lazy*: nothing runs until you enumerate the result (with `foreach`, `ToList()`, `Sum()`...). Call `.ToList()` or `.ToArray()` to materialize.

The Intermediate level covers lambdas and LINQ in depth.

## Arrays and lists are references

An array or list variable holds a *reference* to the data, so assigning it shares the data:

```csharp
int[] a = [1, 2, 3];
int[] b = a;          // same array!
b[0] = 99;
Console.WriteLine(a[0]);   // 99

int[] c = [.. a];     // spread: a real copy
c[0] = 0;
Console.WriteLine(a[0]);   // still 99
```

The same applies when you pass them to a method: the method can change your array.

## Spread and combining

```csharp
int[] first = [1, 2];
int[] second = [3, 4];
int[] all = [.. first, 0, .. second];     // 1 2 0 3 4
```

## Common mistakes

- **`Length` vs `Count`:** arrays and strings have `Length`; `List<T>` has `Count`.
- **Index out of range:** the last valid index is `Count - 1` (or `^1`).
- **Modifying a list while `foreach`-ing over it.**
- **Expecting `b = a` to copy.** It shares.
- **Calling `First()` on an empty sequence** throws; use `FirstOrDefault()` or check `Any()`.
- **Forgetting LINQ is lazy:** a query re-runs each time you enumerate it, and sees changes to the source made in between.
- **`Remove` in a loop by index** skips elements as indexes shift; loop backwards or use `RemoveAll(predicate)`.

## Exercises

1. Read numbers from the user until a blank line, store them in a `List<int>`, then print the sorted list, the maximum and the average.
2. Remove all duplicates from a list while keeping the original order (hint: `Distinct()`, or a `HashSet`).
3. Rotate an array left by *k* positions.
4. Merge two sorted arrays into one sorted array without calling `Sort`.
5. Use LINQ to print the squares of the odd numbers from 1 to 20.
6. Build a 5×5 multiplication grid as `int[,]` and print it.
7. Demonstrate the reference-sharing bug with `b = a`, then fix it with a copy.

## Recap

- Arrays are fixed-size; `List<T>` grows. Use `Length` for arrays and `Count` for lists.
- `^1` is the last element; `a[1..3]` slices.
- `Sort`, `Where`, `Select`, `Sum`, `OrderBy`... transform and summarize; LINQ queries are lazy.
- Arrays and lists are references; copy explicitly with `[.. x]`, `ToArray()` or `ToList()`.

Next: [Dictionaries & sets →](08-dictionaries-and-sets.md)
