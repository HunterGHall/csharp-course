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
