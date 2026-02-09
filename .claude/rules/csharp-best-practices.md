# Rule: csharp-best-practices

General C# and .NET correctness, performance, and API usage checks.

## Rules

### csharp/throw-ex-loses-stacktrace 🔴 CRITICAL
**`catch (Exception ex) { throw ex; }` loses the stack trace.** Use `throw;` to rethrow preserving the original stack trace, or wrap in a new exception with `ex` as the inner exception.

```csharp
// ❌ Stack trace lost
catch (Exception ex) { throw ex; }

// ✅ Preserves stack trace
catch (Exception ex) { throw; }

// ✅ Wrap with context
catch (Exception ex) { throw new ProcessingException("Order failed", ex); }
```

### csharp/broad-catch 🟠 HIGH
**Catching `Exception` base class without re-throwing.** In non-top-level code, catching `Exception` swallows everything including `OutOfMemoryException`, `StackOverflowException`, etc. Catch specific exception types, or re-throw after logging.

### csharp/generic-exception-throw 🟡 MEDIUM
**Throwing `new Exception()` instead of specific exception types.** Use `InvalidOperationException`, `ArgumentException`, `ArgumentNullException`, or a custom exception type.

### csharp/idisposable-not-disposed 🟠 HIGH
**`IDisposable` not disposed.** Any `IDisposable` that isn't in a `using` statement, `using` declaration, or disposed in a `Dispose()` method. This causes resource leaks (connections, file handles, memory).

```csharp
// ❌ HttpClient not disposed (though HttpClient specifically should be long-lived)
var client = new HttpClient();
var result = await client.GetAsync(url);

// ✅ using declaration
using var stream = File.OpenRead(path);

// ✅ using statement
using (var connection = new SqlConnection(connStr))
{
    await connection.OpenAsync(ct);
}
```

Note: `HttpClient` is a special case — it should be long-lived and shared (via `IHttpClientFactory`), not disposed per-request.

### csharp/mutable-statics 🔴 CRITICAL
**Mutable static fields** written to from multiple threads without synchronization. This causes race conditions. Either make them `readonly`, use `Interlocked`, `ConcurrentDictionary`, or protect with a lock.

```csharp
// ❌ Race condition
private static Dictionary<string, object> _cache = new();
// Written from multiple threads without lock

// ✅ Thread-safe collection
private static readonly ConcurrentDictionary<string, object> _cache = new();
```

### csharp/datetime-now-in-logic 🟡 MEDIUM
**`DateTime.Now` vs `DateTime.UtcNow`.** In business logic, prefer `DateTime.UtcNow` to avoid timezone bugs. Better yet, inject an `IClock` / `TimeProvider` (.NET 8+) abstraction for testability.

### csharp/string-concat-in-loop 🟡 MEDIUM
**String concatenation in loops.** Use `StringBuilder` for loops that append strings. Each `+=` creates a new string allocation.

```csharp
// ❌ O(n²) allocations
var result = "";
foreach (var item in items)
    result += item.ToString() + ", ";

// ✅ StringBuilder
var sb = new StringBuilder();
foreach (var item in items)
    sb.Append(item).Append(", ");
```

### csharp/linq-in-hot-path 🟡 MEDIUM
**LINQ allocations in hot paths.** Flag `.ToList()`, `.ToArray()`, `.Select()` in tight loops or frequently-called methods. Suggest `Span<T>`, array pooling, or manual iteration when performance matters.

### csharp/multiple-enumeration 🟠 HIGH
**Multiple enumeration of `IEnumerable<T>`.** Causes duplicate computation, duplicate DB queries, or duplicate I/O. Materialize with `.ToList()` first if you need to enumerate more than once.

```csharp
// ❌ Enumerated twice — query runs twice
IEnumerable<Order> orders = GetOrders();
var count = orders.Count();        // first enumeration
var first = orders.FirstOrDefault(); // second enumeration

// ✅ Materialize once
var orders = GetOrders().ToList();
var count = orders.Count;
var first = orders.FirstOrDefault();
```

### csharp/collection-interface-usage 🔵 LOW
**Collection API design:**
- Accept `IEnumerable<T>`, `IReadOnlyList<T>`, or `IReadOnlyCollection<T>` in parameters (broad acceptance).
- Return concrete types or `IReadOnlyList<T>` (clear contract).
- Use `IReadOnlyDictionary` / `IReadOnlyCollection` to communicate immutability intent.

### csharp/record-for-dtos 🔵 LOW
**Prefer `record` types for DTOs and value objects** (.NET 5+). Records give you value equality, `ToString()`, and immutability by default.

```csharp
// ❌ Boilerplate class
public class OrderDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    // manual Equals, GetHashCode, ToString...
}

// ✅ Record
public record OrderDto(int Id, string Name);
```

### csharp/pattern-matching 🔵 LOW
**Prefer pattern matching** where it simplifies type checks and casts.

```csharp
// ❌ Verbose
if (obj is Foo) { var foo = (Foo)obj; foo.DoStuff(); }

// ✅ Pattern matching
if (obj is Foo foo) { foo.DoStuff(); }
```

### csharp/magic-numbers 🔵 LOW
**Magic numbers and strings.** Extract to named constants, enums, or configuration. Unnamed literals make code harder to understand and change.

```csharp
// ❌ What does 86400 mean?
await Task.Delay(86400000);

// ✅ Self-documenting
private static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(1);
await Task.Delay(RefreshInterval);
```
