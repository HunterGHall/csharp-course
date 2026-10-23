# 12. Capstone: command-line task tracker

## Goal

Build a real command-line app that stores data on disk between runs. It pulls together almost everything in Beginner: variables, strings, lists, `switch`, methods, nullable values, exceptions and file I/O. It also gives you a first taste of a `record`, which the Intermediate level covers properly.

## What it does

```
$ dotnet run -- add Buy milk
Added #1: Buy milk
$ dotnet run -- add Learn C#
Added #2: Learn C#
$ dotnet run -- done 2
1. [ ] Buy milk
2. [x] Learn C#
1/2 done
$ dotnet run -- remove 9
no task #9 (you have 2)
$ echo $?
1
```

## Requirements

1. Commands: `add <title>`, `list`, `done <n>`, `undo <n>`, `remove <n>`, `clear` (delete finished tasks).
2. Tasks are numbered from 1 in the display and persist to a text file (`tasks.txt`, or the path in the `TASKS_FILE` environment variable) after every change.
3. Bad input is reported with a friendly message on **standard error** and exit code `1`, never a crash.
4. A missing storage file means "no tasks yet", not an error.
5. A corrupt storage file is reported with the line number.

## Setup

```bash
mkdir tasks && cd tasks
dotnet new console
```

Replace the generated `Program.cs` with the code below. For stricter checking, make your `.csproj` look like this (it's the one CI uses):

````xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

</Project>
````

## The design

- **`TodoItem`**: a tiny `record` bundling a title and a done flag. (A `record` is a custom type with named fields and built-in equality; you'll study them next level.)
- **`TrackerException`**: one exception type for everything that can go wrong, with a friendly message.
- **`Storage`** (`Load`, `Save`): a text format with one task per line, `0|title` or `1|title`. Simple, human-readable, easy to debug. The `|` is split with `Split('|', 2)` so titles may contain `|`.
- **`Commands`**: one `Run` method with a `switch` on the first argument. It returns whether anything changed, so we only save when needed.
- **Top-level statements** at the very top turn every `TrackerException` into a message and an exit code.

Notice the layering: storage knows nothing about commands, and commands know nothing about the file format.

## The full program

````csharp
// Command-line task tracker. Top-level statements come first; types follow.

try
{
    var path = Environment.GetEnvironmentVariable("TASKS_FILE") ?? "tasks.txt";
    var tasks = Storage.Load(path);
    if (Commands.Run(args, tasks))
    {
        Storage.Save(path, tasks);
    }
    return 0;
}
catch (TrackerException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

// ---- Model ----

record TodoItem(string Title, bool IsDone);

// ---- Errors ----

class TrackerException(string message) : Exception(message);

// ---- Storage: one task per line, "0|title" or "1|title" ----

static class Storage
{
    public static List<TodoItem> Load(string path)
    {
        var tasks = new List<TodoItem>();
        if (!File.Exists(path))
        {
            return tasks; // no file yet means no tasks yet
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TrackerException($"cannot read {path}: {ex.Message}");
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
            {
                continue;
            }

            // Split at the first '|' only, so titles may contain '|'.
            var parts = lines[i].Split('|', 2);
            if (parts.Length != 2 || (parts[0] != "0" && parts[0] != "1"))
            {
                throw new TrackerException($"{path}: line {i + 1} is corrupt: \"{lines[i]}\"");
            }
            tasks.Add(new TodoItem(parts[1], parts[0] == "1"));
        }
        return tasks;
    }

    public static void Save(string path, List<TodoItem> tasks)
    {
        var lines = tasks.Select(t => $"{(t.IsDone ? 1 : 0)}|{t.Title}");
        try
        {
            File.WriteAllLines(path, lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TrackerException($"cannot write {path}: {ex.Message}");
        }
    }
}

// ---- Commands ----

static class Commands
{
    const string Usage = "usage: tasks <add TITLE | list | done N | undo N | remove N | clear>";

    /// <summary>Runs one command. Returns true if the task list changed and must be saved.</summary>
    public static bool Run(string[] args, List<TodoItem> tasks)
    {
        if (args.Length == 0)
        {
            throw new TrackerException(Usage);
        }

        var rest = args[1..];
        switch (args[0])
        {
            case "add":
            {
                var title = string.Join(' ', rest).Trim();
                if (title.Length == 0)
                {
                    throw new TrackerException("add needs a title");
                }
                if (title.Contains('\n'))
                {
                    throw new TrackerException("a title cannot contain a line break");
                }
                tasks.Add(new TodoItem(title, false));
                Console.WriteLine($"Added #{tasks.Count}: {title}");
                return true;
            }
            case "list":
                Print(tasks);
                return false;
            case "done":
            case "undo":
            {
                var i = ParseIndex(rest, tasks);
                tasks[i] = tasks[i] with { IsDone = args[0] == "done" };
                Print(tasks);
                return true;
            }
            case "remove":
            {
                tasks.RemoveAt(ParseIndex(rest, tasks));
                Print(tasks);
                return true;
            }
            case "clear":
                tasks.RemoveAll(t => t.IsDone);
                Print(tasks);
                return true;
            default:
                throw new TrackerException($"unknown command '{args[0]}'\n{Usage}");
        }
    }

    /// <summary>Turns the user's 1-based "N" into a 0-based index, or throws a friendly error.</summary>
    static int ParseIndex(string[] rest, List<TodoItem> tasks)
    {
        if (rest.Length != 1 || !int.TryParse(rest[0], out var n))
        {
            throw new TrackerException("expected a task number");
        }
        if (n < 1 || n > tasks.Count)
        {
            throw new TrackerException($"no task #{n} (you have {tasks.Count})");
        }
        return n - 1;
    }

    static void Print(List<TodoItem> tasks)
    {
        if (tasks.Count == 0)
        {
            Console.WriteLine("No tasks yet.");
            return;
        }
        for (var i = 0; i < tasks.Count; i++)
        {
            Console.WriteLine($"{i + 1}. [{(tasks[i].IsDone ? 'x' : ' ')}] {tasks[i].Title}");
        }
        Console.WriteLine($"{tasks.Count(t => t.IsDone)}/{tasks.Count} done");
    }
}
````

## Try it

```bash
dotnet run -- add Buy milk
dotnet run -- add "Learn C#"
dotnet run -- done 1
dotnet run -- list
dotnet run -- remove 9; echo "exit code: $?"      # PowerShell: $LASTEXITCODE
cat tasks.txt
```

Open `tasks.txt` and read it. Edit a line to something invalid (say `x|oops`) and run `list` to see the corrupt-file message.

## Walkthrough of the tricky parts

- **`tasks[i] = tasks[i] with { IsDone = ... }`**: records are immutable, so we replace the item with a modified copy. The `with` expression builds that copy.
- **`Commands.Run` returns a `bool`** so `list` doesn't rewrite the file for no reason.
- **`ParseIndex`** converts the user's 1-based number into a 0-based index and validates it in one place, so every command gets the same error messages.
- **`catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)`** converts low-level failures into our own friendly exception. A bug like `NullReferenceException` is deliberately *not* caught: it should crash loudly.

## Self-check

- [ ] `dotnet build` shows no warnings.
- [ ] No input makes the program print a stack trace (try `done`, `done abc`, `done 0`, `add` with no title, an unknown command).
- [ ] Deleting `tasks.txt` gives "No tasks yet.", not an error.
- [ ] Task titles containing `|` survive a save and reload.
- [ ] You can explain why storage code doesn't call `Console.WriteLine`.

## Stretch goals

- Add `edit <n> <new title>`.
- Add priorities (`add -p 2 Title`) and sort `list` by priority.
- Add due dates with `DateOnly` and an `overdue` command.
- Store tasks as JSON instead (the next level has a lesson on `System.Text.Json`).
- Print the list in color: green for done, yellow for pending (`Console.ForegroundColor`).

## When you're done

You should be able to explain every line and add a new command unaided. Then go to [Intermediate](../intermediate/index.md).
