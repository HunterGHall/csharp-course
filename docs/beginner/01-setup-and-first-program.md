# 1. Setup & your first program

## What & why

C# is a modern, type-safe, compiled language from Microsoft that runs on **Windows, macOS and Linux** through **.NET**. You'll meet it in web backends, desktop apps, games (Unity), cloud services and command-line tools. In this course we use the command line and the `dotnet` tool so everything works the same on every platform. No Visual Studio required (though it works fine too).

Two names to keep straight:

- **C#** is the language: the syntax and rules you type.
- **.NET** is the platform: the runtime that executes your program and the huge class library (`Console`, `File`, `List<T>`, ...) that comes with it.

## Install the SDK

Check whether you already have it:

```bash
dotnet --version
```

If not, install the **.NET SDK** (version 10 or newer; the SDK includes the runtime and the compiler):

- **Windows**: `winget install Microsoft.DotNet.SDK.10`
- **macOS**: `brew install --cask dotnet-sdk`, or the installer from [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
- **Linux**: use your package manager (for example `sudo apt install dotnet-sdk-10.0`) or the instructions on [learn.microsoft.com/dotnet/core/install/linux](https://learn.microsoft.com/dotnet/core/install/linux)

`dotnet --version` should print `10.x.x`. This course targets **.NET 10 and C# 14**. Most of the beginner material is identical in older versions; the lessons point out the few features that are new.

## Your first program

Create a project:

```bash
mkdir hello && cd hello
dotnet new console
```

That generates two files:

```
hello/
├── hello.csproj     # the project file: target framework, settings
└── Program.cs       # your code
```

Open `Program.cs`, replace its contents with:

```csharp
Console.WriteLine("Hello, world!");
```

Run it:

```bash
dotnet run
```

```
Hello, world!
```

That single line is a complete program. Modern C# supports **top-level statements**: the compiler wraps your statements in a hidden `Main` method for you. Everything in this course's beginner level uses that style.

## What just happened?

`dotnet run` did three things: it **restored** dependencies, **built** your code (compiled it to an assembly, a `.dll`, in `bin/`), and **ran** it. You can do the steps separately:

```bash
dotnet build      # compile only
dotnet run        # build if needed, then run
```

`Console` is a class from the .NET library, `WriteLine` is one of its methods, and the dot means "the `WriteLine` that belongs to `Console`". You didn't need an `import` for it because new projects turn on **implicit usings**, a hidden list of `using` directives for the most common namespaces (`System`, `System.IO`, `System.Linq`, `System.Collections.Generic`, ...).

## Reading input

```csharp
Console.Write("What's your name? ");
string? name = Console.ReadLine();
Console.WriteLine($"Hello, {name}!");
```

- `Write` prints without a newline; `WriteLine` adds one.
- `ReadLine()` returns the typed line, or `null` at the end of input. That's why the type is `string?` (the `?` means "may be null"). [Nullable types](10-nullable.md) get a full lesson.
- `$"..."` is **string interpolation**: anything inside `{}` is evaluated and inserted.

## Comments

```csharp
// A one-line comment.

/* A block comment
   over several lines. */

/// <summary>XML doc comments feed IntelliSense and generated docs.</summary>
```

## Scratch files

Starting with .NET 10 you can also run a single file without any project:

```bash
dotnet run scratch.cs
```

It's perfect for trying out the snippets in this course. When a snippet needs more than a file (tests, several files, packages added with `dotnet add package`), use a real project.

## A word on editors

[VS Code](https://code.visualstudio.com/) with the *C# Dev Kit* extension works on every platform; Visual Studio (Windows) and JetBrains Rider are full IDEs. Whichever you choose, make sure you can see the compiler's red squiggles and the errors in the terminal. You'll read a lot of them.

## Common mistakes

- **Missing semicolon.** Every statement ends with `;`. The error (`; expected`) usually points at the line *after* the mistake.
- **Wrong case.** `console.writeline` fails; C# is case-sensitive and names like `Console` and `WriteLine` use PascalCase.
- **Running from the wrong folder.** `dotnet run` looks for a `.csproj` in the current directory. If you see "Couldn't find a project to run", `cd` into the project folder.
- **Two entry points.** A project can only have one file with top-level statements.
- **Single quotes for strings.** `'hello'` is a `char` literal and only holds one character. Strings use double quotes.

## Exercises

1. Create a project named `greeter` that asks for your name and your age, then prints a sentence using both.
2. Print a small ASCII-art box using several `WriteLine` calls.
3. Deliberately remove a semicolon, then misspell `Console`. Read each compiler error and note the line number it reports.
4. Run `dotnet build` and look inside `bin/Debug/net10.0/`. Find your `.dll`.
5. Write a scratch file that prints the result of `2 + 3 * 4` and `(2 + 3) * 4`.

## Recap

- C# is the language, .NET is the platform, and the `dotnet` CLI builds and runs everything.
- `dotnet new console` makes a project; `dotnet run` builds and runs it.
- Top-level statements let a program be a few lines long; implicit usings save boilerplate.
- `Console.WriteLine` prints, `Console.ReadLine` reads, and `$"{...}"` interpolates.

Next: [Variables & types →](02-variables-and-types.md)
