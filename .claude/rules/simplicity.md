# Rule: simplicity

Complexity and over-engineering checks. Applied as a lens on top of all other findings. Before recommending any fix, ask: **"Is there a simpler way?"**

## Rules

### simplicity/unnecessary-pattern 🟡 MEDIUM
**If a design pattern is used but the problem is simple, remove the pattern.** A Strategy pattern with one strategy, a Factory that builds one type, an Observer with one subscriber — these add indirection without value. Replace with direct code.

### simplicity/inheritance-over-composition 🟡 MEDIUM
**If inheritance is used where composition or a simple function would work, simplify.** Deep inheritance hierarchies (>2 levels) are almost always a sign of over-design. Prefer composition (inject behavior) or simple functions.

```csharp
// ❌ Inheritance for code reuse
abstract class BaseProcessor { protected void Log(...) { } }
abstract class ValidatingProcessor : BaseProcessor { protected void Validate(...) { } }
class OrderProcessor : ValidatingProcessor { ... }

// ✅ Composition — flat and testable
class OrderProcessor
{
    private readonly ILogger _logger;
    private readonly IValidator _validator;
    public OrderProcessor(ILogger logger, IValidator validator) { ... }
}
```

### simplicity/unnecessary-abstraction 🟡 MEDIUM
**If there are multiple layers of abstraction for a single implementation, flatten.** An `IFoo`, `FooBase`, and `Foo` where only `Foo` exists is two unnecessary layers. Wait until you actually need the abstraction.

Exception: interfaces used for DI/testing are fine even with a single implementation.

### simplicity/comments-explaining-what 🔵 LOW
**If a comment explains *what* the code does (not *why*), the code isn't clear enough.** Simplify the code instead of adding comments. Comments should explain *why* — business rules, non-obvious constraints, workarounds.

```csharp
// ❌ Comment explains what
// Loop through orders and check if valid
foreach (var order in orders)
    if (order.IsValid) process(order);

// ✅ Code is self-explanatory, comment explains why
// Must process in order — downstream system requires sequential IDs
foreach (var order in orders.Where(o => o.IsValid))
    Process(order);
```

### simplicity/too-many-parameters 🟡 MEDIUM
**If a method has more than 3-4 parameters, consider grouping into an options/config object.** Long parameter lists are error-prone (wrong order) and hard to extend.

```csharp
// ❌ Too many params
void Send(string to, string from, string subject, string body, bool isHtml, int priority, string[] cc)

// ✅ Options object
void Send(EmailMessage message)
record EmailMessage(string To, string From, string Subject, string Body,
    bool IsHtml = false, int Priority = 0, string[]? Cc = null);
```

### simplicity/god-class 🟠 HIGH
**Flag classes with >500 lines or >5 injected constructor dependencies.** These almost always have multiple responsibilities. Suggest splitting by responsibility.

### simplicity/deep-nesting 🟡 MEDIUM
**Deeply nested code (>3 levels of indentation).** Suggest guard clauses and early returns.

```csharp
// ❌ Deep nesting
void Process(Order order)
{
    if (order != null)
    {
        if (order.IsValid)
        {
            if (order.Items.Any())
            {
                // actual logic at level 4
            }
        }
    }
}

// ✅ Guard clauses — flat
void Process(Order order)
{
    if (order is null) return;
    if (!order.IsValid) return;
    if (!order.Items.Any()) return;

    // actual logic at level 1
}
```

### simplicity/large-methods 🟡 MEDIUM
**Methods over ~50 lines.** Suggest extraction into smaller, named methods. Each method should do one thing and be nameable in a way that describes what it does.

### simplicity/boolean-complexity 🔵 LOW
**Complex boolean expressions.** Extract into descriptive methods or local variables.

```csharp
// ❌ What does this check?
if (user.Age >= 18 && user.HasVerifiedEmail && !user.IsBanned && user.SubscriptionEnd > DateTime.UtcNow)

// ✅ Named intent
if (user.IsEligibleForPurchase())
```
