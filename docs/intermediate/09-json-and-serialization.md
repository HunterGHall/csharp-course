# 9. JSON & serialization

## What & why

JSON is the lingua franca of config files, web APIs and data storage. In .NET, the built-in library **`System.Text.Json`** converts objects to JSON text (*serialization*) and back (*deserialization*). It is fast, strict about correctness, and requires no extra packages.

## Serializing

```csharp
using System.Text.Json;

record Person(string Name, int Age, string[] Languages);

var ada = new Person("Ada", 36, ["English", "French"]);

string json = JsonSerializer.Serialize(ada);
Console.WriteLine(json);
// {"Name":"Ada","Age":36,"Languages":["English","French"]}
```

By default only **public properties** are serialized (fields aren't, unless you opt in), in the order they're declared.

## Deserializing

```csharp
Person? back = JsonSerializer.Deserialize<Person>(json);
Console.WriteLine(back?.Name);     // Ada
```

The result is nullable because the JSON may literally be `null`. Invalid JSON throws `JsonException`.

## Options

Configure once and reuse the instance (creating a new `JsonSerializerOptions` per call is slow):

```csharp
var options = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,        // for reading
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
};

string pretty = JsonSerializer.Serialize(ada, options);
```

```json
{
  "name": "Ada",
  "age": 36,
  "languages": [
    "English",
    "French"
  ]
}
```

`JsonSerializerOptions.Web` is a ready-made preset with web defaults (camelCase, case-insensitive, numbers from strings).

## Attributes

Control individual members:

```csharp
using System.Text.Json.Serialization;

class Product
{
    [JsonPropertyName("sku")]
    public string Code { get; set; } = "";

    [JsonIgnore]
    public string Internal { get; set; } = "";

    [JsonInclude]
    public int Stock;                      // opt a public field in (fields are skipped by default)

    [JsonPropertyOrder(1)]
    public decimal Price { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Category Kind { get; set; }
}

enum Category { Tool, Toy }
```

Enums serialize as numbers unless you add `JsonStringEnumConverter`.

## Records, `required` and constructors

Records with a primary constructor work out of the box: the deserializer matches JSON names to constructor parameters (case-insensitively). `required` properties make a missing value a `JsonException`:

```csharp
class Settings
{
    public required string Host { get; init; }
    public int Port { get; init; } = 8080;
}

var s = JsonSerializer.Deserialize<Settings>("""{"Host":"example.org"}""");
Console.WriteLine(s!.Port);     // 8080 (default kept)
// Deserialize<Settings>("{}") throws: Host is required
```

## Working with files

```csharp
File.WriteAllText("settings.json", JsonSerializer.Serialize(settings, options));
var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText("settings.json"), options);

// Streaming, for larger files:
await using var stream = File.OpenRead("big.json");
var items = await JsonSerializer.DeserializeAsync<List<Person>>(stream);
```

For huge arrays, `JsonSerializer.DeserializeAsyncEnumerable<T>(stream)` yields items one at a time.

## Dynamic JSON: `JsonDocument` and `JsonNode`

When you don't have (or want) a matching type:

```csharp
using System.Text.Json.Nodes;

JsonNode? root = JsonNode.Parse("""{"user":{"name":"Ada","tags":["a","b"]},"count":3}""");
string? name = root?["user"]?["name"]?.GetValue<string>();    // "Ada"
int count = root!["count"]!.GetValue<int>();                  // 3

root["user"]!["name"] = "Grace";                              // mutable
root["added"] = 42;
Console.WriteLine(root.ToJsonString());
```

`JsonDocument` is a read-only, pooled, lower-allocation alternative:

```csharp
using var doc = JsonDocument.Parse(json);
foreach (var lang in doc.RootElement.GetProperty("Languages").EnumerateArray())
    Console.WriteLine(lang.GetString());

if (doc.RootElement.TryGetProperty("Missing", out var el)) { /* ... */ }
```

## Custom converters

Teach the serializer about a type that doesn't map naturally:

```csharp
class UnixTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).UtcDateTime;

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(new DateTimeOffset(value, TimeSpan.Zero).ToUnixTimeSeconds());
}

class Event
{
    [JsonConverter(typeof(UnixTimeConverter))]
    public DateTime At { get; set; }
}
```

## Polymorphism

To serialize a hierarchy, mark the base type with the allowed subtypes and a discriminator:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Circle), "circle")]
[JsonDerivedType(typeof(Square), "square")]
abstract record Shape;
record Circle(double Radius) : Shape;
record Square(double Side) : Shape;

string j = JsonSerializer.Serialize<Shape>(new Circle(2));
// {"kind":"circle","Radius":2}
```

## Source generation (AOT-friendly)

By default `System.Text.Json` uses reflection at run time. A **source generator** produces the serialization code at compile time: faster start-up, trimming and Native AOT support.

```csharp
[JsonSerializable(typeof(Person))]
[JsonSerializable(typeof(List<Person>))]
partial class AppJsonContext : JsonSerializerContext;

string json2 = JsonSerializer.Serialize(ada, AppJsonContext.Default.Person);
Person? p2 = JsonSerializer.Deserialize(json2, AppJsonContext.Default.Person);
```

## Dates, numbers and gotchas

- `DateTime` and `DateTimeOffset` use ISO 8601 (`2026-10-23T14:05:00Z`). Prefer `DateTimeOffset` or UTC.
- `decimal` round-trips exactly; `double` may print as `0.30000000000000004`.
- `long` values above 2^53 lose precision in JavaScript consumers. Send them as strings if that matters.
- Property name matching on read is case-sensitive by default.
- Unknown JSON properties are ignored by default (`UnmappedMemberHandling.Disallow` makes them an error).
- Cycles throw unless you set `ReferenceHandler.IgnoreCycles`.

## Other formats

For XML use `XmlSerializer`/`XDocument`; for CSV use a library such as CsvHelper; for MessagePack/Protobuf use their packages; for YAML use YamlDotNet. Avoid `BinaryFormatter`, which is insecure and has been removed.

## Common mistakes

- **Expecting fields to serialize.** Use properties (or `IncludeFields = true`).
- **Not handling `null`** from `Deserialize<T>`.
- **Creating `JsonSerializerOptions` repeatedly.** Cache it in a `static readonly` field.
- **Case mismatch** between JSON names and C# names on read.
- **Deserializing untrusted JSON** into a type with a huge object graph without limits; set `MaxDepth` and check sizes.
- **Enums as numbers** in public APIs, which breaks when the enum is reordered.

## Exercises

1. Serialize a list of records to a pretty-printed, camelCase JSON file and read it back.
2. Convert the task tracker capstone to store tasks as JSON.
3. Parse a JSON document into a `JsonNode`, change a nested value, and write it out.
4. Write a converter that stores a `TimeSpan` as `"hh:mm"`.
5. Model a `Shape` hierarchy with polymorphic JSON and round-trip a list of shapes.
6. Add a source-generated `JsonSerializerContext` and measure start-up time or allocations against reflection.

## Recap

- `JsonSerializer.Serialize` / `Deserialize<T>` convert objects to and from JSON; cache your `JsonSerializerOptions`.
- Attributes and options control names, ignored members, enums and ordering.
- `JsonNode` and `JsonDocument` handle JSON of unknown shape.
- Converters, polymorphism and source generators handle the hard cases.

Next: [Projects, solutions & NuGet →](10-projects-and-nuget.md)
