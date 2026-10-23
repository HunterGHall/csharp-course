# 5. Booleans & conditionals

## What & why

Programs make decisions. A `bool` holds `true` or `false`, and conditionals pick which code runs based on one. C# has the classic `if`/`switch` statements, plus modern **switch expressions** and **patterns** that make decision code much shorter.

## Booleans and comparisons

```csharp
bool isAdult = 20 >= 18;     // true

5 == 5      // equal
5 != 4      // not equal
3 < 4   3 <= 3   4 > 3   4 >= 5
```

There is no "truthiness" in C#: an `if` condition must be a real `bool`. `if (count)` or `if (name)` won't compile. Write `if (count != 0)` or `if (!string.IsNullOrEmpty(name))`.

## Logical operators

```csharp
bool a = true, b = false;
a && b      // false  AND
a || b      // true   OR
!a          // false  NOT
a ^ b       // true   XOR
```

`&&` and `||` **short-circuit**: the right side is skipped when the left side already decides the answer. That makes guards safe:

```csharp
if (name != null && name.Length > 0) { ... }
```

(`&` and `|` on bools always evaluate both sides; you rarely want them.)

## `if` / `else if` / `else`

```csharp
int score = 83;

if (score >= 90)
{
    Console.WriteLine("A");
}
else if (score >= 80)
{
    Console.WriteLine("B");
}
else
{
    Console.WriteLine("Below B");
}
```

Always use braces, even for one-line bodies. Skipping them is how bugs like Apple's "goto fail" happen.

## The conditional (ternary) operator

```csharp
string label = score >= 60 ? "pass" : "fail";
```

Use it for simple either/or values. Nested ternaries get unreadable fast.

## `switch` statements

```csharp
Console.Write("Command: ");
string? cmd = Console.ReadLine();

switch (cmd)
{
    case "start":
        Console.WriteLine("Starting");
        break;
    case "stop":
    case "quit":                       // several labels, one body
        Console.WriteLine("Stopping");
        break;
    default:
        Console.WriteLine("Unknown command");
        break;
}
```

Unlike C and C++, C# does **not** fall through: every section must end with `break`, `return`, `throw` or `continue`, or the compiler complains.

## `switch` expressions

When each branch just produces a value, a switch **expression** is much tidier:

```csharp
string DayType(DayOfWeek day) => day switch
{
    DayOfWeek.Saturday or DayOfWeek.Sunday => "weekend",
    _ => "weekday",
};
```

The `_` is the discard (catch-all). If the compiler can see you missed a case, it warns; at run time an unmatched value throws `SwitchExpressionException`.

## Patterns

Patterns test the *shape* of a value and can bind variables. They work in `switch`, `is`, and expressions.

```csharp
string Grade(int score) => score switch
{
    >= 90 => "A",
    >= 80 => "B",
    >= 70 => "C",
    >= 0  => "D",
    _     => "invalid",
};
```

More forms:

```csharp
object o = 42;

if (o is int n && n > 10) Console.WriteLine($"big int {n}");   // type + test + bind
if (o is not string) Console.WriteLine("not text");
if (o is null) Console.WriteLine("null");

char c = 'k';
bool isLetter = c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

int age = 30;
string bracket = age switch
{
    < 13 => "child",
    >= 13 and < 20 => "teen",
    _ => "adult",
};
```

Combine several inputs with a tuple pattern:

```csharp
string Play(string a, string b) => (a, b) switch
{
    ("rock", "scissors") or ("scissors", "paper") or ("paper", "rock") => "first wins",
    _ when a == b => "draw",
    _ => "second wins",
};
```

`when` adds an extra condition to a case. You'll use patterns constantly for types, records and enums in the Intermediate level.

## Ranges and `Enumerable.Range`

You can test membership with relational patterns (`score is >= 0 and <= 100`). Keep `Range` (`1..5`) for slicing, not for "is this number in range" checks.

## Common mistakes

- **`=` instead of `==`.** `if (x = 5)` doesn't compile for ints (assignment isn't a bool), but `if (flag = true)` does, and assigns.
- **Missing `break`** in a switch section; the compiler error is "Control cannot fall through".
- **Comparing floating-point values with `==`** (see Numbers & math).
- **Dangling `else`** without braces, where the `else` attaches to the wrong `if`.
- **Order of `if`/`else if` branches:** `>= 70` before `>= 90` makes everything a "C".
- **Forgetting the `_` arm** in a switch expression over `int` or `string`.

## Exercises

1. Read an integer and print whether it's negative, zero, or positive.
2. Classify a year as a leap year (divisible by 4, except centuries, except multiples of 400).
3. Rewrite the grade `if` chain as a `switch` expression.
4. Build a rock-paper-scissors judge with a tuple pattern.
5. Write `FizzBuzz` for a single number using a switch expression on `(n % 3, n % 5)`.
6. Read a character and say whether it's a vowel, a consonant, a digit, or something else, using patterns.
7. Write a tiny calculator: read `a`, an operator and `b`; use a switch expression to compute the result; reject division by zero.

## Recap

- Conditions are real `bool`s; `&&`/`||` short-circuit.
- `if`/`else`, the ternary operator and `switch` pick paths; `switch` never falls through.
- Switch **expressions** and **patterns** (`>= 90`, `is not null`, `and`/`or`, tuples, `when`) replace long `if` chains.

Next: [Loops →](06-loops.md)
