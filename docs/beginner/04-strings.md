# 4. Strings & characters

## What & why

Text is everywhere: names, files, messages, JSON. A C# `string` is a sequence of UTF-16 characters, and it is **immutable**: every "modification" creates a new string. Knowing that explains most of the advice in this lesson.

## Creating strings

```csharp
string a = "hello";
string empty = "";               // or string.Empty
string path = "C:\\Users\\ada";  // backslash escapes: \n \t \" \\

char c = 'A';                    // single quotes: one char
```

### Verbatim, raw and multi-line literals

```csharp
string verbatim = @"C:\Users\ada";           // backslashes are literal

string json = """
    {
      "name": "Ada",
      "age": 36
    }
    """;                                     // raw string literal
```

A **raw string literal** (`"""`) can contain quotes and backslashes untouched. The closing `"""` decides how much leading indentation is stripped from every line, so your code can stay indented. Combine it with interpolation using `$"""..."""`; with `$$"""`, interpolation uses `{{x}}` so single braces (like JSON) are literal.

## Interpolation and formatting

```csharp
string name = "Ada";
int age = 36;
Console.WriteLine($"{name} is {age} years old");
Console.WriteLine($"Next year: {age + 1}");
Console.WriteLine($"{name,10}|{age,-5}|");    // width and alignment
Console.WriteLine($"{3.14159:F2}");           // 3.14
```

You can also write `string.Format("{0} is {1}", name, age)`, but interpolation is clearer.

## Length, indexing and slicing

```csharp
string s = "Hello, World";
Console.WriteLine(s.Length);     // 12
Console.WriteLine(s[0]);         // 'H'
Console.WriteLine(s[^1]);        // 'd'  (^1 = last)
Console.WriteLine(s[7..]);       // "World"
Console.WriteLine(s[..5]);       // "Hello"
Console.WriteLine(s[7..10]);     // "Wor"  (end is exclusive)
// s[0] = 'J';                   // error: strings are read-only
```

`Substring(start, length)` does the same as a range; ranges are shorter.

## Common methods

```csharp
string s = "  The quick brown fox  ";

s.Trim();                    // "The quick brown fox"
s.ToUpper();  s.ToLower();   // case conversion (culture-aware)
s.Contains("quick");         // true
s.StartsWith("  The");       // true
s.IndexOf("quick");          // 6  (-1 if absent)
s.Replace("quick", "slow");
s.Trim().Split(' ');         // ["The", "quick", "brown", "fox"]
string.Join(", ", new[] { "a", "b", "c" });   // "a, b, c"
"7".PadLeft(3, '0');         // "007"
string.IsNullOrEmpty(s);     // false
string.IsNullOrWhiteSpace("   ");   // true
```

Because strings are immutable, **the result must be assigned**. `s.Trim();` on its own does nothing useful.

```csharp
s = s.Trim();
```

## Comparing strings

`==` compares **content**, not identity:

```csharp
string x = "abc";
string y = "ab" + "c";
Console.WriteLine(x == y);    // True
```

For case-insensitive or culture-sensitive comparisons, say what you mean:

```csharp
string.Equals("Straße", "STRASSE", StringComparison.OrdinalIgnoreCase);   // false
"file.TXT".EndsWith(".txt", StringComparison.OrdinalIgnoreCase);          // true
```

Use `Ordinal`/`OrdinalIgnoreCase` for identifiers, paths, protocol text and keys; use culture-aware comparisons only for text shown to people. `string.Compare` and `CompareTo` return negative, zero or positive for ordering.

## Building strings in loops: `StringBuilder`

Each `+=` allocates a new string. For a few concatenations that's fine; in a loop it's quadratic:

```csharp
using System.Text;

var sb = new StringBuilder();
for (int i = 1; i <= 5; i++)
{
    sb.Append(i).Append(' ');
}
sb.AppendLine("done");
Console.Write(sb.ToString());     // 1 2 3 4 5 done
```

## Characters

`char` has helpers for classification and conversion:

```csharp
char.IsDigit('7');       // true
char.IsLetter('x');      // true
char.IsUpper('a');       // false
char.ToUpper('a');       // 'A'
(int)'A';                // 65
(char)(65 + 2);          // 'C'
```

Iterate with `foreach`:

```csharp
int vowels = 0;
foreach (char ch in "education")
{
    if ("aeiou".Contains(ch)) vowels++;
}
Console.WriteLine(vowels);   // 5
```

## Unicode: chars aren't always characters

A `char` is one UTF-16 *code unit*. Many emoji and some scripts need two (a surrogate pair), so `.Length` can differ from what a person counts:

```csharp
string smile = "😀";
Console.WriteLine(smile.Length);                      // 2
Console.WriteLine(smile.EnumerateRunes().Count());    // 1
```

Use `EnumerateRunes()` (or `System.Globalization.StringInfo` for grapheme clusters like "👨‍👩‍👧") when you really mean "characters a user sees".

## Converting to and from text

```csharp
int n = int.Parse("42");
string s = 42.ToString();            // "42"
string hex = 255.ToString("X");      // "FF"
string joined = string.Concat("a", 1, true);   // "a1True"
```

## Common mistakes

- **Ignoring the return value:** `s.Replace("a", "b");` changes nothing; assign it.
- **`string` vs `char`:** `'ab'` is an error; `"a"` is not a `char`.
- **Quadratic concatenation** in big loops; use `StringBuilder` or `string.Join`.
- **Culture surprises:** `"I".ToLower()` is `"ı"` under the Turkish culture. For protocol text use `ToLowerInvariant()` or ordinal comparisons.
- **Off-by-one slicing:** `s[2..5]` has length 3; the end is exclusive.
- **Using `==` with `null`-able strings without checking:** `null == ""` is false. `string.IsNullOrEmpty` handles both.

## Exercises

1. Reverse a string without using `Array.Reverse` (hint: loop backwards, or `string.Concat(s.Reverse())`).
2. Count the words in a sentence, handling extra spaces.
3. Check whether a string is a palindrome, ignoring case and punctuation.
4. Build a Caesar cipher: shift each letter by *n*, wrapping `z` to `a`.
5. Read a full name as `Last, First` and print `First Last`.
6. Print a right-aligned table of three names and scores using alignment in interpolation.
7. Print `"😀".Length` and the rune count. Explain the difference.

## Recap

- Strings are immutable UTF-16 sequences; "modifying" returns a new string you must assign.
- Use interpolation, raw literals for embedded quotes/JSON, and ranges (`s[1..^1]`) for slicing.
- `==` compares content; use `StringComparison` when case or culture matters.
- Build big strings with `StringBuilder`; remember that a `char` isn't always a visible character.

Next: [Booleans & conditionals →](05-booleans-and-conditionals.md)
