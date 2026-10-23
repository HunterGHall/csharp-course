# 3. Numbers & math

## What & why

Numbers look simple until `7 / 2` gives `3` or an `int` quietly wraps to a negative value. This lesson covers the arithmetic operators, why integers and floating-point behave the way they do, and the `Math` helpers you'll reach for.

## Arithmetic

```csharp
int a = 17, b = 5;
Console.WriteLine(a + b);    // 22
Console.WriteLine(a - b);    // 12
Console.WriteLine(a * b);    // 85
Console.WriteLine(a / b);    // 3   <- integer division truncates toward zero
Console.WriteLine(a % b);    // 2   <- remainder
Console.WriteLine(-17 / 5);  // -3
Console.WriteLine(-17 % 5);  // -2  <- the remainder takes the sign of the left operand
```

If **both** operands are integers, `/` is integer division. Make either side a `double` for a fractional answer:

```csharp
Console.WriteLine(17 / 5.0);       // 3.4
Console.WriteLine((double)a / b);  // 3.4
```

Dividing an integer by zero throws `DivideByZeroException`. Dividing a `double` by zero gives `Infinity` (or `NaN` for `0.0 / 0.0`) and no exception.

## Compound assignment, `++` and `--`

```csharp
int n = 10;
n += 5;      // 15
n *= 2;      // 30
n++;         // 31   (n = n + 1)
n--;         // 30

int x = 5;
int y = x++;   // y = 5, x = 6   (postfix: use, then increment)
int z = ++x;   // x = 7, z = 7   (prefix: increment, then use)
```

Avoid using `x++` inside a bigger expression; it's a classic way to confuse readers.

## Integers overflow silently

An `int` holds values from `int.MinValue` (-2,147,483,648) to `int.MaxValue` (2,147,483,647). By default C# does **not** check:

```csharp
int big = int.MaxValue;
big++;
Console.WriteLine(big);       // -2147483648  (wrapped around!)
```

Ask for checking explicitly:

```csharp
try
{
    int boom = checked(int.MaxValue + 1);
}
catch (OverflowException)
{
    Console.WriteLine("overflow detected");
}
```

or turn it on for a whole project with `<CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>` in the `.csproj`. When values may exceed `int`, use `long`, or `System.Numerics.BigInteger` for arbitrarily large integers:

```csharp
using System.Numerics;

BigInteger f = 1;
for (int i = 2; i <= 30; i++) f *= i;
Console.WriteLine(f);    // 265252859812191058636308480000000
```

## Floating point is approximate

`double` stores numbers in binary, and some decimal fractions have no exact binary form:

```csharp
Console.WriteLine(0.1 + 0.2);            // 0.30000000000000004
Console.WriteLine(0.1 + 0.2 == 0.3);     // False
Console.WriteLine(Math.Abs(0.1 + 0.2 - 0.3) < 1e-9);   // True: compare with a tolerance
```

Use `decimal` when exactness in base 10 matters (money):

```csharp
decimal price = 19.99m;
decimal total = price * 3;
Console.WriteLine(total);        // 59.97
```

`decimal` is slower and has a smaller range, so use it for money and `double` for science, graphics and everything else.

Special `double` values: `double.PositiveInfinity`, `double.NaN` (never equal to anything, even itself; test with `double.IsNaN(x)`).

## The `Math` class

```csharp
Math.Abs(-3)            // 3
Math.Max(3, 9)          // 9
Math.Min(3, 9)          // 3
Math.Pow(2, 10)         // 1024
Math.Sqrt(144)          // 12
Math.Floor(3.7)         // 3
Math.Ceiling(3.2)       // 4
Math.Round(3.456, 2)    // 3.46
Math.Clamp(15, 0, 10)   // 10
Math.PI                 // 3.141592653589793
```

**Rounding gotcha:** `Math.Round` uses *banker's rounding* by default: halves go to the nearest **even** number.

```csharp
Math.Round(2.5)    // 2
Math.Round(3.5)    // 4
Math.Round(2.5, MidpointRounding.AwayFromZero)   // 3, the rounding from school
```

Integer helpers:

```csharp
int.DivRem(17, 5)             // (Quotient: 3, Remainder: 2)
int.IsEvenInteger(17)         // false (or just n % 2 == 0)
```

(These static methods on `int` come from *generic math*, which you'll meet properly in the Advanced level.)

## Formatting numbers

Format specifiers go after a colon inside interpolation:

```csharp
double ratio = 0.256;
decimal money = 1234.5m;
int n = 255;

Console.WriteLine($"{ratio:P1}");        // 25.6 %
Console.WriteLine($"{money:C}");         // $1,234.50 (depends on culture)
Console.WriteLine($"{money:N2}");        // 1,234.50
Console.WriteLine($"{3.14159:F2}");      // 3.14
Console.WriteLine($"{n:X}");             // FF   (hex)
Console.WriteLine($"{n:B8}");            // 11111111  (binary, padded to 8)
Console.WriteLine($"{42,6}|{42,-6}|");   //     42|42    |   (alignment)
```

Output follows the current *culture* (for example, `1,5` vs `1.5`). To get stable output for files and protocols, use `CultureInfo.InvariantCulture`.

## Random numbers

```csharp
int die = Random.Shared.Next(1, 7);          // 1..6 (upper bound is exclusive)
double u = Random.Shared.NextDouble();       // 0.0 <= u < 1.0
var rng = new Random(42);                    // seeded: same sequence every run
```

`Random` is not suitable for passwords or tokens. Use `System.Security.Cryptography.RandomNumberGenerator` for those.

## Bit operations

```csharp
Console.WriteLine(5 & 3);     // 1   AND
Console.WriteLine(5 | 3);     // 7   OR
Console.WriteLine(5 ^ 3);     // 6   XOR
Console.WriteLine(1 << 4);    // 16  shift left
Console.WriteLine(~0);        // -1  NOT
```

## Common mistakes

- **Expecting `7 / 2` to be `3.5`.** It's `3`. Convert one operand to `double`.
- **`double` for money.** Use `decimal`.
- **Comparing floating-point values with `==`.**
- **Ignoring overflow** in loops that accumulate or multiply (factorials, hashes, sums of big data).
- **`Random.Next(1, 6)` for a die.** The upper bound is exclusive, so you'd never roll a 6.
- **Creating `new Random()` in a loop.** Old runtimes seeded from the clock, giving repeated values; use `Random.Shared`.

## Exercises

1. Print the quotient and remainder of 1000 divided by 7, then reassemble 1000 from them.
2. Compute the average of three `int`s as a `double` (and show what happens if you forget the cast).
3. Write a loop that finds the first factorial that overflows `int`; repeat it with `checked`.
4. Round 0.5, 1.5, 2.5 and 3.5 with the default `Math.Round` and with `AwayFromZero`.
5. Convert a temperature from Celsius to Fahrenheit and print it with one decimal place.
6. Simulate 1000 rolls of two dice and print how often each total (2-12) occurred.

## Recap

- `/` on two integers truncates; `%` gives the remainder.
- `int` overflow wraps silently unless you use `checked`; `long` and `BigInteger` give more room.
- `double` is approximate; `decimal` is exact base-10 for money.
- `Math` covers the usual functions; mind banker's rounding.
- Format specifiers (`:F2`, `:P1`, `:N0`, `:X`) control output; `Random.Shared` gives random numbers.

Next: [Strings →](04-strings.md)
