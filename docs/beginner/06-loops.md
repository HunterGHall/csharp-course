# 6. Loops

## What & why

Loops repeat work: process every item in a list, retry until the input is valid, count from 1 to 100. In C# you'll mostly use **`foreach`** over collections and **`for`** when you need a counter. `while` and `do`/`while` handle the "until something happens" cases.

## `for`

```csharp
for (int i = 1; i <= 5; i++)
{
    Console.Write($"{i} ");
}
// 1 2 3 4 5
```

The three parts are: **initializer** (runs once), **condition** (checked before each pass), **iterator** (runs after each pass). Any of them may be empty; `for (;;)` is an infinite loop.

Counting backwards and by steps:

```csharp
for (int i = 5; i >= 1; i--) { }          // 5 4 3 2 1
for (int i = 0; i < 10; i += 3) { }       // 0 3 6 9
```

## `foreach`

```csharp
int[] scores = [90, 72, 85];
foreach (int s in scores)
{
    Console.WriteLine(s);
}

foreach (char ch in "hey")
{
    Console.WriteLine(ch);
}

var ages = new Dictionary<string, int> { ["Ada"] = 36, ["Linus"] = 54 };
foreach (var (name, age) in ages)
{
    Console.WriteLine($"{name} is {age}");
}
```

`foreach` works on anything that can be enumerated: arrays, lists, strings, dictionaries, files' lines, query results. The loop variable is read-only; you can't assign to `s`. To change elements in place, loop over indices:

```csharp
for (int i = 0; i < scores.Length; i++)
{
    scores[i] *= 10;
}
```

## Index *and* element

`foreach` has no counter. Either keep your own, or use LINQ's `Select` with an index:

```csharp
string[] fruits = ["apple", "banana", "cherry"];

int n = 1;
foreach (var fruit in fruits)
{
    Console.WriteLine($"{n++}. {fruit}");
}

foreach (var (fruit, index) in fruits.Select((f, i) => (f, i)))
{
    Console.WriteLine($"{index + 1}. {fruit}");
}
```

When you need the index, a plain `for` is often the clearest.

## `while`

Repeat while a condition holds, when you don't know the count in advance:

```csharp
long n = 27;
int steps = 0;
while (n != 1)
{
    n = n % 2 == 0 ? n / 2 : 3 * n + 1;
    steps++;
}
Console.WriteLine($"Collatz steps: {steps}");
```

## `do`/`while`

Like `while`, but the body runs **at least once**, which is perfect for input validation:

```csharp
int age;
do
{
    Console.Write("Enter age (1-120): ");
}
while (!int.TryParse(Console.ReadLine(), out age) || age is < 1 or > 120);

Console.WriteLine($"Age: {age}");
```

## `break`, `continue`, and leaving nested loops

```csharp
for (int i = 1; i <= 10; i++)
{
    if (i % 2 == 0) continue;     // skip the rest of this pass
    if (i > 7) break;             // leave the loop
    Console.Write($"{i} ");
}
// 1 3 5 7
```

`break` and `continue` affect the **innermost** loop. C# has no labeled `break`. To escape nested loops, move them into a method and `return`, or use a flag:

```csharp
(int, int)? FindPair(int target)
{
    for (int a = 1; a <= 20; a++)
    {
        for (int b = 1; b <= 20; b++)
        {
            if (a * b == target) return (a, b);    // leaves both loops
        }
    }
    return null;
}

Console.WriteLine(FindPair(91));    // (7, 13)
```

(`goto` can also jump out, but a method with `return` reads better.)

## Reading input until it runs out

```csharp
int total = 0;
string? line;
while ((line = Console.ReadLine()) is not null)    // null at end of input
{
    if (int.TryParse(line, out int value))
    {
        total += value;
    }
}
Console.WriteLine($"sum: {total}");
```

Try it with `printf '1\n2\n3\n' | dotnet run` or type numbers and finish with Ctrl+D (Ctrl+Z then Enter on Windows).

## Nested loops

```csharp
for (int row = 1; row <= 3; row++)
{
    for (int col = 1; col <= 3; col++)
    {
        Console.Write($"{row * col,4}");
    }
    Console.WriteLine();
}
```

## Functional alternatives (a preview)

A loop that builds a new collection is often clearer as a LINQ query:

```csharp
var squares = Enumerable.Range(1, 5).Select(x => x * x);       // 1 4 9 16 25
var evens = Enumerable.Range(1, 10).Where(x => x % 2 == 0);    // 2 4 6 8 10
int sum = Enumerable.Range(1, 100).Sum();                      // 5050
```

`Enumerable.Range(start, count)` takes a **count**, not an end value. You'll study LINQ properly in the Intermediate level.

## Common mistakes

- **Changing a collection while `foreach`-ing over it.** Adding or removing items throws `InvalidOperationException: Collection was modified`. Loop over a copy (`foreach (var x in list.ToList())`) or collect changes and apply them afterwards.
- **Off-by-one:** `i <= list.Count` instead of `i < list.Count` throws `ArgumentOutOfRangeException` on the last pass.
- **Infinite `while` loops** from forgetting to update the condition.
- **Trying to assign to the `foreach` variable.**
- **Declaring a variable inside the loop that you needed after it** (scope ends at the closing brace).
- **Capturing a loop variable in a lambda** that runs later. Each `foreach` iteration gets a fresh variable in modern C#, but a `for` counter is shared; copy it into a local first.

## Exercises

1. Print 1-100, but "Fizz" for multiples of 3, "Buzz" for multiples of 5 and "FizzBuzz" for both.
2. Read integers until end of input and print the count, sum, minimum, maximum and average. Skip lines that aren't numbers.
3. Number guessing game: pick `Random.Shared.Next(1, 101)`, give "higher"/"lower" hints, count the guesses, handle non-numeric input.
4. Print a right triangle of `*` of height N with nested loops.
5. Print all primes below 100.
6. Print a multiplication table (1-10) with aligned columns.
7. Write a method that finds the first pair `(a, b)` with `1 <= a, b <= 20` and `a * b == 91`; use `return` to leave both loops.

## Recap

- `for` for counting, `foreach` for collections, `while` for unknown counts, `do`/`while` runs at least once.
- `break` and `continue` act on the innermost loop; return from a method to escape several.
- Don't modify a collection while enumerating it.
- `while ((line = Console.ReadLine()) is not null)` reads until input ends.

Next: [Arrays & lists →](07-arrays-and-lists.md)
