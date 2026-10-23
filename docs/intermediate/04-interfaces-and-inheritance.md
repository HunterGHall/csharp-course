# 4. Interfaces & inheritance

## What & why

Real programs have parts that must be swapped without rewriting everything: a real database vs a fake one, file storage vs memory, one payment provider vs another. C# gives you two tools for that: **interfaces** (a contract with no implementation) and **inheritance** (a new class that extends an existing one). Today most designers reach for interfaces and composition first, and inheritance sparingly.

## Interfaces

```csharp
interface INotifier
{
    void Send(string to, string message);
}

class EmailNotifier : INotifier
{
    public void Send(string to, string message) =>
        Console.WriteLine($"email to {to}: {message}");
}

class ConsoleNotifier : INotifier
{
    public void Send(string to, string message) =>
        Console.WriteLine($"[{to}] {message}");
}

void Alert(INotifier notifier) => notifier.Send("ops", "disk is full");

Alert(new EmailNotifier());
Alert(new ConsoleNotifier());
```

`Alert` doesn't know or care which class it gets, only that it can `Send`. That's **polymorphism**, and the basis of testing with fakes: in a test you pass a `FakeNotifier` that records messages.

- An interface can contain methods, properties, indexers and events; none have bodies (by default).
- A class may implement **many** interfaces: `class Cache : IDisposable, IEnumerable<int>`.
- Name interfaces with an `I` prefix by convention.

### Default interface methods

Interfaces may provide a default body, so adding a member later doesn't break existing implementers:

```csharp
interface ILogger
{
    void Log(string message);
    void Warn(string message) => Log("WARN: " + message);     // default
}
```

Call it through the interface type: `((ILogger)obj).Warn("x")`. Use this for evolving libraries, not as a replacement for classes.

### Explicit implementation

When two interfaces share a member name, or you don't want a member on the class's public surface:

```csharp
class Dual : IReader, IWriter
{
    void IReader.Close() { /* ... */ }
    void IWriter.Close() { /* ... */ }
}
```

### Common built-in interfaces

| Interface | Meaning |
| --- | --- |
| `IEnumerable<T>` | can be looped over with `foreach` |
| `IComparable<T>` | has a natural ordering |
| `IEquatable<T>` | typed equality |
| `IDisposable` | needs cleanup |
| `IFormattable` | custom `ToString(format)` |

```csharp
record Version2(int Major, int Minor) : IComparable<Version2>
{
    public int CompareTo(Version2? other) =>
        other is null ? 1 : (Major, Minor).CompareTo((other.Major, other.Minor));
}
```

## Inheritance

A class can extend one base class and inherit its members:

```csharp
class Animal(string name)
{
    public string Name { get; } = name;
    public virtual string Speak() => "...";                 // may be overridden
    public override string ToString() => $"{GetType().Name} {Name}";
}

class Dog(string name) : Animal(name)
{
    public override string Speak() => "Woof";
}

class Cat(string name) : Animal(name)
{
    public override string Speak() => "Meow";
}

Animal[] zoo = [new Dog("Rex"), new Cat("Tom"), new Animal("Generic")];
foreach (var a in zoo)
    Console.WriteLine($"{a}: {a.Speak()}");
```

Keys:

- `virtual` marks a member as overridable; `override` replaces it. Calls go to the **runtime** type (dynamic dispatch).
- `base.Member()` calls the parent's version.
- `sealed` prevents further inheritance (`sealed class`) or overriding (`sealed override`). **Make classes `sealed` by default** unless designed for extension. It's faster and safer.
- Without `virtual`, a same-named member in a subclass only *hides* the base one (`new`); avoid that.

### Abstract classes

An `abstract` class can't be instantiated and may declare members that subclasses *must* implement:

```csharp
abstract class Shape
{
    public abstract double Area();
    public string Describe() => $"{GetType().Name} with area {Area():F2}";
}

sealed class Square(double side) : Shape
{
    public override double Area() => side * side;
}
```

Use an abstract class when subclasses share real code *and* state; use an interface when you only need a contract.

### Casting and type tests

```csharp
Animal a = new Dog("Rex");

if (a is Dog d) Console.WriteLine(d.Name);     // test + cast in one
Dog? maybe = a as Dog;                          // null if not a Dog
Dog sure = (Dog)a;                              // throws InvalidCastException if wrong
```

If you keep writing `if (x is Dog) ... else if (x is Cat) ...` you may be missing a virtual method.

## Composition over inheritance

Inheritance couples a subclass tightly to its parent's internals. A common alternative is to *hold* a collaborator and delegate to it:

```csharp
interface IEngine { void Start(); }

sealed class Car(IEngine engine)      // has an engine (not "is an" engine)
{
    public void Drive()
    {
        engine.Start();
        Console.WriteLine("driving");
    }
}
```

Ask "is-a, or has-a?". A `Dog` *is an* `Animal`; a `Car` *has an* `Engine`. When in doubt, compose.

## Dependency injection by hand

Depend on the interface, receive the implementation from outside:

```csharp
sealed class SignupService(INotifier notifier)
{
    public void Register(string email)
    {
        // ... save the user ...
        notifier.Send(email, "Welcome!");
    }
}

var service = new SignupService(new ConsoleNotifier());     // chosen in one place
service.Register("ada@example.com");
```

Frameworks like ASP.NET Core automate the wiring (`builder.Services.AddSingleton<INotifier, EmailNotifier>()`), but the idea is just constructor parameters.

## Liskov in one sentence

Code that works with a base type must keep working with any subtype. If your `Square : Rectangle` breaks code that sets width and height independently, inheritance was the wrong model.

## Polymorphism with records

Records can inherit from other records (not from classes) and implement interfaces:

```csharp
record Employee(string Name, decimal Salary);
record Manager(string Name, decimal Salary, int Reports) : Employee(Name, Salary);
```

## Common mistakes

- **Forgetting `virtual`/`override`**, so the base version runs.
- **Deep inheritance trees.** More than two levels is usually a smell.
- **Inheriting just to reuse code.** Use composition or a helper.
- **Calling virtual methods from a constructor,** when the derived constructor hasn't run yet.
- **Huge interfaces.** Prefer several small ones (`IReader`, `IWriter`).
- **Casting with `(T)x` when `is` or `as` would do.**

## Exercises

1. Define `IShape` with `Area()` and `Perimeter()`; implement `Circle`, `Rect` and `Triangle` (records or classes) and print the total area of a list.
2. Create an `abstract class Employee` with `abstract decimal Pay()` and two subclasses (hourly and salaried).
3. Make a `SignupService` test double: `FakeNotifier : INotifier` that stores messages, then assert on it.
4. Implement `IComparable<T>` for a `Version` type and sort a list of versions.
5. Replace an inheritance hierarchy of your own with composition and compare the code.
6. Implement `IEnumerable<int>` on a `Countdown` class using `yield return`.

## Recap

- Interfaces define contracts; classes implement any number of them; depend on interfaces, inject implementations.
- Inheritance gives one base class, `virtual`/`override` dispatch, and `abstract` members. Seal classes by default.
- Prefer composition ("has-a") over inheritance ("is-a") unless the relationship is truly substitutable.
- `is`/`as`/pattern matching test types safely.

Next: [Generics →](05-generics.md)
