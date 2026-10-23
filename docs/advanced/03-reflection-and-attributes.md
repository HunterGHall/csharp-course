# 3. Reflection & attributes

## What & why

Compiled .NET assemblies carry **metadata**: descriptions of every type, member and attribute. **Reflection** reads that metadata (and can act on it) at run time. It powers serializers, dependency-injection containers, test runners, ORMs and plugin systems. It is also slow, fragile and hostile to trimming, so the second half of this lesson is about knowing when to avoid it, and what to use instead.

## Getting a `Type`

```csharp
Type t1 = typeof(string);                 // known at compile time
Type t2 = "hello".GetType();              // from an object (the run-time type)
Type? t3 = Type.GetType("System.Text.StringBuilder");   // from a name (needs a full name)

Console.WriteLine(t1.FullName);           // System.String
Console.WriteLine(t1.IsClass);            // True
Console.WriteLine(typeof(int).IsValueType);   // True
Console.WriteLine(typeof(List<int>).GetGenericTypeDefinition());   // System.Collections.Generic.List`1[T]
```

## Inspecting members

```csharp
class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    private string _secret = "hidden";
    public void Greet(string other) => Console.WriteLine($"Hi {other}, I'm {Name}");
}

Type type = typeof(Person);

foreach (var p in type.GetProperties())
    Console.WriteLine($"{p.PropertyType.Name} {p.Name}");

foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
    Console.WriteLine(m.Name);

FieldInfo? secret = type.GetField("_secret", BindingFlags.NonPublic | BindingFlags.Instance);
```

`BindingFlags` selects public/non-public, instance/static, inherited or declared-only members. Forgetting the flags is the top reason for "it returns null".

## Creating and using objects dynamically

```csharp
object? person = Activator.CreateInstance(typeof(Person));

PropertyInfo name = typeof(Person).GetProperty("Name")!;
name.SetValue(person, "Ada");
Console.WriteLine(name.GetValue(person));         // Ada

MethodInfo greet = typeof(Person).GetMethod("Greet")!;
greet.Invoke(person, ["Linus"]);                  // Hi Linus, I'm Ada

object? secretValue = typeof(Person)
    .GetField("_secret", BindingFlags.NonPublic | BindingFlags.Instance)!
    .GetValue(person);                            // reaches private state: powerful, dangerous
```

Exceptions thrown by an invoked method are wrapped in `TargetInvocationException`; look at `InnerException`.

## Attributes

Attributes attach declarative metadata to code:

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
sealed class RangeCheckAttribute(int min, int max) : Attribute
{
    public int Min { get; } = min;
    public int Max { get; } = max;
}

[AttributeUsage(AttributeTargets.Property)]
sealed class RequiredTextAttribute : Attribute;

class Signup
{
    [RequiredText] public string Name { get; set; } = "";
    [RangeCheck(13, 120)] public int Age { get; set; }
}
```

Read them with reflection:

```csharp
static List<string> Validate(object obj)
{
    var errors = new List<string>();
    foreach (var prop in obj.GetType().GetProperties())
    {
        var value = prop.GetValue(obj);

        if (prop.GetCustomAttribute<RequiredTextAttribute>() is not null &&
            string.IsNullOrWhiteSpace(value as string))
            errors.Add($"{prop.Name} is required");

        if (prop.GetCustomAttribute<RangeCheckAttribute>() is { } range &&
            value is int n && (n < range.Min || n > range.Max))
            errors.Add($"{prop.Name} must be {range.Min}-{range.Max}");
    }
    return errors;
}

var bad = new Signup { Name = "", Age = 5 };
Validate(bad).ForEach(Console.WriteLine);
```

This is a mini version of what DataAnnotations, ASP.NET model binding and test frameworks do. Attributes have constraints: constructor arguments must be compile-time constants (numbers, strings, `typeof`, enums, arrays of those).

Common built-ins: `[Obsolete]`, `[Flags]`, `[Serializable]`, `[Conditional("DEBUG")]`, `[CallerMemberName]`, `[MethodImpl(MethodImplOptions.AggressiveInlining)]`, `[DoesNotReturn]`, `[NotNullWhen(true)]`, `[SetsRequiredMembers]`.

### Caller info attributes

```csharp
void Log(string message,
    [CallerMemberName] string member = "",
    [CallerFilePath] string file = "",
    [CallerLineNumber] int line = 0) =>
    Console.WriteLine($"{Path.GetFileName(file)}:{line} {member}: {message}");
```

The compiler fills these in at the call site, with no reflection cost.

## Loading assemblies and plugins

```csharp
Assembly asm = Assembly.LoadFrom("Plugins/MyPlugin.dll");
var pluginTypes = asm.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
foreach (var type in pluginTypes)
{
    var plugin = (IPlugin)Activator.CreateInstance(type)!;
    plugin.Run();
}
```

For real plugin systems use `AssemblyLoadContext` so plugins can be isolated and unloaded, and so their dependencies don't collide with yours.

## The cost of reflection

Reflection is **slow** relative to normal calls: `PropertyInfo.GetValue` can be 10-100× slower than a direct property access, and calls allocate arrays for arguments. It also defeats the compiler: renaming a property silently breaks string-based lookups, and the **trimmer** and **Native AOT** can remove members that only reflection touches.

Mitigations:

1. **Cache** `Type`, `PropertyInfo` and friends; never look them up per item.
2. **Compile delegates once** and reuse them:

```csharp
static Func<object, object?> MakeGetter(PropertyInfo prop)
{
    var instance = Expression.Parameter(typeof(object), "o");
    var access = Expression.Property(Expression.Convert(instance, prop.DeclaringType!), prop);
    var boxed = Expression.Convert(access, typeof(object));
    return Expression.Lambda<Func<object, object?>>(boxed, instance).Compile();
}

var getName = MakeGetter(typeof(Person).GetProperty("Name")!);   // compile once
Console.WriteLine(getName(person!));                               // ~direct-call speed
```

3. Use `CreateDelegate` for methods: `var f = (Func<Person, string>)Delegate.CreateDelegate(typeof(Func<Person, string>), nameGetter);`
4. **Avoid reflection** with source generators (next lesson), which write the plain C# you would have written by hand.

## Trimming and AOT annotations

If you publish trimmed or AOT apps, tell the tooling which members reflection needs:

```csharp
void Inspect(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
{
    foreach (var p in type.GetProperties()) { /* the trimmer keeps the public properties */ }
}
```

`[RequiresUnreferencedCode("uses reflection")]` marks methods that can't be trimmed safely. The build then warns callers (IL2026, IL3050). Libraries that care ship `IsTrimmable` and `IsAotCompatible` and avoid open-ended reflection.

## Expression trees

An `Expression<Func<T, bool>>` is code represented **as data** that you can inspect and translate (this is how Entity Framework turns `where p.Age > 30` into SQL):

```csharp
Expression<Func<Person, bool>> filter = p => p.Age > 30 && p.Name.StartsWith("A");

var body = (BinaryExpression)filter.Body;
Console.WriteLine(body.NodeType);                 // AndAlso
Console.WriteLine(filter);                        // p => ((p.Age > 30) AndAlso p.Name.StartsWith("A"))

Func<Person, bool> compiled = filter.Compile();   // turn data back into code
```

`nameof(...)` and `CallerArgumentExpression` give you many of the same benefits for simple cases, without trees:

```csharp
void Require(bool condition, [CallerArgumentExpression(nameof(condition))] string? expr = null)
{
    if (!condition) throw new InvalidOperationException($"failed: {expr}");
}
Require(1 + 1 == 3);      // failed: 1 + 1 == 3
```

## Common mistakes

- **Reflecting in a hot loop** without caching.
- **Missing `BindingFlags`,** so private or static members aren't found.
- **String-based member names** instead of `nameof`.
- **Forgetting `TargetInvocationException`** wrapping.
- **Using reflection to bypass `private`** in production code: it breaks encapsulation and, over time, upgrades.
- **Ignoring trimming/AOT warnings,** then crashing in the published build.
- **Attributes with logic.** Attributes should only carry data; put the behavior elsewhere.

## Exercises

1. Write `ToDictionary(object obj)` that returns property names and values; then add `[Ignore]` support via a custom attribute.
2. Extend `Validate` with `[MaxLength(n)]` and `[EmailAddress]` attributes and test it.
3. Build a tiny plugin loader: find all types implementing `IPlugin` in a folder of DLLs and run them.
4. Benchmark `PropertyInfo.GetValue`, a compiled expression getter, and a direct property read for one million calls.
5. Use `CallerArgumentExpression` to write an `Assert(condition)` that prints the failing expression.
6. Publish a small app with `PublishTrimmed=true` that uses reflection and fix the warnings with annotations.

## Recap

- Reflection reads metadata (`Type`, `PropertyInfo`, attributes) and can create objects and call members by name at run time.
- Custom attributes attach data; code (usually reflection-based) acts on it.
- Reflection is slow and trimmer-hostile. Cache lookups, compile delegates, annotate for trimming, or generate code instead.
- Expression trees represent code as data, which is how LINQ providers work.

Next: [Source generators & analyzers →](04-source-generators.md)
