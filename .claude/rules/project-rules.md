# Rule: project-rules

Project-specific rules unique to this codebase. Edit this file to add rules that apply only to your project's conventions, architecture, and known gotchas.

---

## How to Write a Project Rule

Each rule follows this template:

```
### project/rule-id SEVERITY_EMOJI SEVERITY_NAME
**One-line description of what to check.**

Detailed explanation of why this matters in YOUR project.

\`\`\`csharp
// ❌ The bad pattern
// ✅ The correct pattern
\`\`\`
```

Use rule IDs prefixed with `project/` so they're easily distinguishable from built-in rules.

---

## Example Project Rules

Below are common examples of project-specific rules. **Delete, modify, or add your own.** These are templates to show you the format — they won't match your codebase out of the box.

---

### project/viewmodel-must-call-base-init 🟠 HIGH
**All ViewModels inheriting `BaseViewModel` must call `base.InitializeAsync()` in their override.**

Our `BaseViewModel.InitializeAsync()` sets up global subscriptions (connectivity, authentication state, theme changes). Skipping it causes silent failures in those systems.

```csharp
// ❌ Forgot base call — connectivity monitoring won't work
public class OrderViewModel : BaseViewModel
{
    public override async Task InitializeAsync()
    {
        await LoadOrders();
    }
}

// ✅ Always call base
public class OrderViewModel : BaseViewModel
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await LoadOrders();
    }
}
```

---

### project/repository-requires-cancellation 🟠 HIGH
**All repository methods must accept `CancellationToken` as the last parameter.**

Our repositories hit SQL Server and external APIs. Without cancellation support, abandoned requests hold connections open and degrade the connection pool under load.

```csharp
// ❌ No cancellation support
Task<Order> GetOrderAsync(int id);

// ✅ Always accept CancellationToken
Task<Order> GetOrderAsync(int id, CancellationToken ct = default);
```

---

### project/no-direct-httpclient 🟡 MEDIUM
**Never instantiate `HttpClient` directly. Always use `IHttpClientFactory` via our `ApiClientBase`.**

Direct `HttpClient` causes socket exhaustion and bypasses our retry/circuit-breaker policies configured in `HttpClientModule`.

```csharp
// ❌ Socket exhaustion, no retry policy
var client = new HttpClient();
var response = await client.GetAsync(url);

// ✅ Use the factory via our base class
public class OrderApiClient : ApiClientBase
{
    public OrderApiClient(IHttpClientFactory factory) : base(factory, "OrderApi") { }
    public Task<Order> GetOrderAsync(int id, CancellationToken ct)
        => GetAsync<Order>($"orders/{id}", ct);
}
```

---

### project/navigation-via-service-only 🟡 MEDIUM
**Never navigate using Frame.Navigate() directly. Always use `INavigationService`.**

Direct Frame navigation bypasses our analytics tracking, deep link handling, and ViewModel lifecycle management.

```csharp
// ❌ Bypasses lifecycle and analytics
Frame.Navigate(typeof(OrderDetailPage), orderId);

// ✅ Uses our navigation service
await _navigationService.NavigateAsync<OrderDetailViewModel>(new OrderDetailParams(orderId));
```

---

### project/logging-structured-only 🟡 MEDIUM
**Use structured logging with message templates. Never use string interpolation in log calls.**

String interpolation defeats structured logging — Serilog/Seq can't index or search by parameter values.

```csharp
// ❌ String interpolation — not queryable in Seq
_logger.LogInformation($"Order {orderId} placed by {userId}");

// ✅ Message template — structured, queryable
_logger.LogInformation("Order {OrderId} placed by {UserId}", orderId, userId);
```

---

### project/settings-via-options-pattern 🔵 LOW
**Access configuration values via `IOptions<T>` / `IOptionsSnapshot<T>`, not by reading `IConfiguration` directly.**

Direct `IConfiguration` access is stringly-typed and not validated at startup.

```csharp
// ❌ Stringly-typed, no validation
var timeout = _config.GetValue<int>("Api:Timeout");

// ✅ Options pattern — typed, validated
public class ApiOptions { public int Timeout { get; set; } = 30; }
// In module: builder.Register(c => c.Resolve<IOptions<ApiOptions>>().Value);
```

---

### project/no-xaml-codebehind-logic 🟡 MEDIUM
**XAML code-behind files (`.xaml.cs`) should contain only UI wiring — no business logic.**

Business logic in code-behind is untestable and won't survive the UWP → WinUI migration cleanly. Keep logic in ViewModels.

```csharp
// ❌ Business logic in code-behind
private async void OnSubmitClick(object sender, RoutedEventArgs e)
{
    var order = new Order { ... };
    await _orderRepo.SaveAsync(order);        // business logic!
    await _emailService.SendConfirmation();    // business logic!
}

// ✅ Delegate to ViewModel
private async void OnSubmitClick(object sender, RoutedEventArgs e)
{
    await ViewModel.SubmitOrderAsync();
}
```

---

### project/event-naming-convention 🔵 LOW
**Events must follow our naming convention: `On{Subject}{Action}` for handlers, `{Subject}{Action}` for event names.**

Consistent naming helps when searching the codebase and reading Rx subscription chains.

```csharp
// ❌ Inconsistent naming
public event EventHandler DataChanged;
private void HandleData(object sender, EventArgs e) { }

// ✅ Our convention
public event EventHandler OrderUpdated;
private void OnOrderUpdated(object sender, EventArgs e) { }
```

---

### project/feature-flag-cleanup 🟡 MEDIUM
**Feature flags that have been fully rolled out (>30 days) should be cleaned up.** Remove the flag check and the dead code path. Stale flags accumulate and obscure the real code flow.

---

## Adding Your Own Rules

1. Copy one of the examples above.
2. Change the rule ID to `project/your-rule-name`.
3. Set the appropriate severity.
4. Explain **why** this matters for your specific project.
5. Include before/after code examples.

Rules without a clear "why" tend to get ignored — invest a sentence in explaining the real consequence.
