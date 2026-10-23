# 2. Variables & types

## What & why

A **variable** is a named place to keep a value. C# is **statically typed**: every variable has a type that is fixed at compile time, so the compiler catches `"five" * 2` before your program ever runs.

## Declaring variables

```csharp
int age = 36;
string name = "Ada";
double height = 1.68;
bool isStudent = false;

age = 37;            // fine: variables can change
// age = "thirty";   // error: cannot convert string to int
```

## `var`: let the compiler infer the type

```csharp
var count = 10;          // int
var price = 9.99;        // double
var title = "C#";        // string
var items = new List<int>();
```

`var` is **not** dynamic typing. The type is still fixed at compile time; you just didn't write it. Use `var` when the type is obvious from the right-hand side, and write the type out when it isn't:

```csharp
var total = 0;               // clear
int parsed = Compute();      // clearer than var if Compute's return type isn't obvious
```

## Constants

```csharp
const int MaxPlayers = 4;
const string AppName = "demo";
// MaxPlayers = 5;   // error
```

A `const` is baked into the compiled code, so it must be a compile-time value (numbers, strings, `bool`...). For a value that is fixed once it's set but computed at run time, use `readonly` fields in a class (Intermediate level).

## The built-in types you'll use daily

| Type | What | Example |
| --- | --- | --- |
| `int` | 32-bit whole number (about ±2.1 billion) | `42` |
| `long` | 64-bit whole number | `5_000_000_000L` |
| `double` | 64-bit floating-point | `3.14` |
| `decimal` | exact base-10 number, for money | `19.99m` |
| `float` | 32-bit floating-point | `1.5f` |
| `bool` | `true` or `false` | `true` |
| `char` | one UTF-16 character | `'A'` |
| `string` | text (immutable) | `"hello"` |
| `byte` | 0 to 255 | `(byte)200` |

Literals may carry a suffix to choose the type: `L` (long), `f` (float), `m` (decimal), `u` (unsigned). Underscores make big numbers readable: `1_000_000`.

Others exist (`short`, `ushort`, `uint`, `ulong`, `sbyte`, `nint`), but `int`, `double`, `bool`, `string` plus `long`, `decimal` and `byte` cover nearly everything.

## Default values

Fields and array elements start at a default (`0`, `false`, `null`). **Local variables don't**: the compiler forces you to assign before reading.

```csharp
int n;
// Console.WriteLine(n);   // error CS0165: use of unassigned local variable 'n'
n = 5;
Console.WriteLine(n);
```

`default(int)` or just `default` gives the default explicitly.

## Converting between types

**Implicit** conversions happen automatically when nothing can be lost:

```csharp
int small = 100;
long big = small;      // fine
double d = small;      // fine
```

**Explicit casts** are required when data might be lost, so you have to say you mean it:

```csharp
double pi = 3.99;
int truncated = (int)pi;      // 3: the fraction is cut off, not rounded
long huge = 5_000_000_000;
int wrapped = (int)huge;      // silently wraps around (see Numbers & math)
```

**Parsing** text into numbers:

```csharp
int a = int.Parse("42");                 // throws FormatException if the text is bad
bool ok = int.TryParse("4x2", out int b);// ok == false, b == 0
if (int.TryParse(Console.ReadLine(), out var age))
{
    Console.WriteLine($"Next year you'll be {age + 1}");
}
```

Prefer `TryParse` for user input: bad input is *expected*, not exceptional.

`Convert.ToInt32`, `ToString()` and string interpolation go the other way, from values to text.

## Tuples

A **tuple** bundles a few values without declaring a type:

```csharp
var point = (X: 3, Y: 4);
Console.WriteLine(point.X + point.Y);     // 7

(string first, string last) = ("Ada", "Lovelace");   // deconstruction
(first, last) = (last, first);                        // swap, no temp needed
```

Tuples are handy for returning more than one value from a method (see [Methods](09-methods.md)).

## `nameof` and `typeof`

```csharp
Console.WriteLine(nameof(age));          // "age"   (rename-safe)
Console.WriteLine(age.GetType());        // System.Int32
Console.WriteLine(typeof(string).Name);  // String
```

## Naming conventions

- `camelCase` for locals and parameters: `userName`
- `PascalCase` for types, methods, properties, constants: `MaxPlayers`, `ReadAllText`
- `_camelCase` is common for private fields (Intermediate level)

The compiler doesn't care, but every C# codebase you read will follow these.

## Common mistakes

- **Integer where you wanted decimal:** `double half = 1 / 2;` gives `0`, because `1 / 2` is integer division. Write `1.0 / 2`.
- **Using `double` for money.** `0.1 + 0.2 != 0.3` in binary floating point. Use `decimal` for currency.
- **Forgetting the `m`/`f`/`L` suffix.** `decimal price = 19.99;` fails because `19.99` is a `double`.
- **Reading an unassigned local variable.**
- **`var x = null;`** The compiler can't infer a type from `null`.
- **Shadowing.** Declaring the same name twice in overlapping scopes is an error in C#, unlike some languages.

## Exercises

1. Declare one variable of each common type and print them with their `GetType()`.
2. Read two integers with `TryParse`, print their sum, and print a friendly error if either fails.
3. Show that `0.1 + 0.2 == 0.3` is `false` for `double` but `true` for `decimal`.
4. Swap two integers using tuple deconstruction.
5. Convert `3.99`, `-3.99` and `3.5` to `int` with a cast. What happens to the fraction?
6. Declare `const double TaxRate = 0.08;` and compute the total for a `decimal` price. Why does it not compile as written, and how do you fix it?

## Recap

- Every variable has a fixed type; `var` infers it, it doesn't make it dynamic.
- `const` for compile-time constants; locals must be assigned before use.
- Casts are explicit when data can be lost; `TryParse` is the safe way to read numbers from text.
- Tuples bundle values; PascalCase/camelCase conventions are universal.

Next: [Numbers & math →](03-numbers-and-math.md)
