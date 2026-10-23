# 11. Testing

## What & why

Tests are code that checks your other code, automatically and repeatedly. They let you change things without fear, document the intended behavior, and pin down bugs forever. This lesson uses **xUnit**, the most common .NET test framework (NUnit and MSTest work the same way in spirit).

## Setting up

```bash
dotnet new xunit -o tests/Shop.Core.Tests
dotnet add tests/Shop.Core.Tests reference src/Shop.Core
dotnet test
```

A test project is a normal class library that references a test framework and the project under test. `dotnet test` builds it, discovers the tests, runs them, and returns a non-zero exit code if any fail (which is what CI looks at).

## A first test

```csharp
// src/Shop.Core/Cart.cs
namespace Shop.Core;

public class Cart
{
    private readonly List<decimal> _prices = [];
    public void Add(decimal price)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(price);
        _prices.Add(price);
    }
    public decimal Total => _prices.Sum();
}
```

```csharp
// tests/Shop.Core.Tests/CartTests.cs
using Shop.Core;
using Xunit;

public class CartTests
{
    [Fact]
    public void Total_is_the_sum_of_added_prices()
    {
        var cart = new Cart();
        cart.Add(10m);
        cart.Add(5.50m);

        Assert.Equal(15.50m, cart.Total);
    }

    [Fact]
    public void Add_rejects_negative_prices()
    {
        var cart = new Cart();

        Assert.Throws<ArgumentOutOfRangeException>(() => cart.Add(-1m));
    }
}
```

The shape is always **Arrange, Act, Assert**: set up the objects, do the thing, check the outcome. A test is a normal method marked `[Fact]`; it passes if it finishes without throwing.

## Naming

Name tests after the *behavior*, not the method: `Total_is_the_sum_of_added_prices`, `Add_rejects_negative_prices`. A failing test name should tell you what broke without opening the file.

## Common assertions

```csharp
Assert.Equal(expected, actual);           // expected first!
Assert.NotEqual(a, b);
Assert.True(condition);   Assert.False(condition);
Assert.Null(x);           Assert.NotNull(x);
Assert.Empty(list);       Assert.Single(list);
Assert.Contains("ell", "hello");
Assert.Contains(3, numbers);
Assert.All(numbers, n => Assert.True(n > 0));
Assert.Equal(1.0, 0.3 * 3.33, precision: 1);       // floating point with tolerance
Assert.Throws<InvalidOperationException>(() => Do());       // exact exception type
Assert.ThrowsAny<IOException>(() => Do());                  // or a subclass
await Assert.ThrowsAsync<TimeoutException>(() => DoAsync());
var item = Assert.IsType<Circle>(shape);                    // checks the type and returns it
```

Compare collections with `Assert.Equal(new[] {1, 2, 3}, actual)`; it checks elements in order.

## Parameterized tests: `[Theory]`

The same check, many inputs:

```csharp
public class SlugTests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  Trim me  ", "trim-me")]
    [InlineData("Already-slug", "already-slug")]
    [InlineData("", "")]
    public void Slugify_converts_titles(string input, string expected)
    {
        Assert.Equal(expected, Slugger.Slugify(input));
    }
}
```

For larger data sets use `[MemberData]` (a static member that yields rows) or `[ClassData]`:

```csharp
public static TheoryData<int, bool> PrimeCases => new()
{
    { 1, false }, { 2, true }, { 15, false }, { 17, true },
};

[Theory]
[MemberData(nameof(PrimeCases))]
public void IsPrime_matches_known_values(int n, bool expected) =>
    Assert.Equal(expected, Primes.IsPrime(n));
```

## Setup and cleanup

xUnit creates a **new instance of the test class for every test**, so use the constructor for setup and `IDisposable` for cleanup:

```csharp
public class FileStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("test-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Save_then_Load_round_trips()
    {
        var store = new FileStore(_dir);
        store.Save("k", "v");
        Assert.Equal("v", store.Load("k"));
    }
}
```

Because each test gets its own temp directory, tests never interfere with each other. To share expensive setup, use a **class fixture** (`IClassFixture<T>`).

## Test doubles: isolating code from the outside world

Depend on interfaces (lesson 4) so tests can substitute fakes for clocks, files, networks and databases:

```csharp
public interface IClock { DateTimeOffset Now { get; } }

public class Greeter(IClock clock)
{
    public string Greet() => clock.Now.Hour < 12 ? "Good morning" : "Good afternoon";
}

class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now { get; } = now;
}

public class GreeterTests
{
    [Fact]
    public void Greets_good_morning_before_noon()
    {
        var greeter = new Greeter(new FixedClock(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)));
        Assert.Equal("Good morning", greeter.Greet());
    }
}
```

.NET 8+ also ships `TimeProvider` with a `FakeTimeProvider` in `Microsoft.Extensions.TimeProvider.Testing`. Mocking libraries (Moq, NSubstitute) generate such fakes automatically, but hand-written fakes are often clearer.

## What makes a good test

- **Fast** (milliseconds) so you run them constantly.
- **Independent:** no test relies on another, or on run order.
- **Deterministic:** no real clock, random numbers (or seed them), network or shared files.
- **Readable:** a small story, one reason to fail.
- **Tests behavior, not implementation:** it shouldn't break when you refactor.

Unit tests cover pure logic; **integration tests** cover real files/databases/HTTP (slower, fewer); end-to-end tests exercise the whole app (slowest, fewest).

## Test-driven development, briefly

1. Write a failing test for the next small behavior (red).
2. Write the simplest code that passes (green).
3. Clean up with the tests as a safety net (refactor).

Even if you don't follow it strictly, *writing the test first for a bug* proves you've reproduced it.

## Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage tells you which lines never ran, not whether the tests are good. Use it to find gaps, not as a target.

## Async tests and timeouts

```csharp
[Fact]
public async Task Download_returns_text()
{
    var client = new FakeHttpClient("hello");
    Assert.Equal("hello", await Downloader.GetTextAsync(client));
}
```

Return `Task`, not `void`, from async tests. Use a `CancellationTokenSource` with a timeout to stop hung tests.

## Other frameworks

- **NUnit** uses `[Test]`, `[TestCase]`, `[SetUp]`, `Assert.That(...)`.
- **MSTest** uses `[TestMethod]`, `[DataRow]`, `[TestInitialize]`.
- **TUnit** and **xUnit v3** are newer designs.

The concepts transfer directly.

## Common mistakes

- **Swapped arguments:** `Assert.Equal(actual, expected)` produces confusing failure messages.
- **Tests that depend on today's date, locale or the machine.**
- **Testing private methods.** Test through the public API.
- **One giant test** asserting ten unrelated things.
- **Catching exceptions in the test** instead of `Assert.Throws`.
- **Sleeping in tests** (`Thread.Sleep`) to wait for async work. Await the task or use a fake clock.
- **Mocking everything,** so tests only check that the mocks were called.

## Exercises

1. Write tests for `Slugify` from the link checker capstone's rules (spaces to dashes, punctuation removed).
2. Write a `Calculator` class with `Divide` and test the zero-division case with `Assert.Throws`.
3. Test a `Clock`-dependent greeter by injecting a fake `TimeProvider`.
4. Use `[Theory]` with `[MemberData]` to test a `FizzBuzz` method for 15 inputs.
5. Write a `FileStore` that saves strings to a folder, and test it with a temp directory.
6. Introduce a bug in code you tested; confirm a test fails; fix the bug.
7. Run `dotnet test` with coverage and find an untested branch.

## Recap

- `[Fact]` for single cases, `[Theory]` + `[InlineData]` for many; Arrange-Act-Assert.
- A new test-class instance per test: constructor for setup, `IDisposable` for cleanup.
- Depend on interfaces so tests can use fakes for clocks, files and networks.
- Good tests are fast, independent, deterministic and about behavior.

Next: [Capstone: link checker →](12-capstone-link-checker.md)
