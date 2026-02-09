# Rule: async-await

Async/Await correctness checks. These catch deadlocks, exception-swallowing, and misuse patterns that cause production failures.

## Rules

### async-await/deadlock-result-wait 🔴 CRITICAL
**Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` in UI or ASP.NET contexts.**
These cause deadlocks. The only acceptable use is in `Main()` entry points or console apps where no `SynchronizationContext` exists.

```csharp
// ❌ Deadlocks in UI/ASP.NET
var data = GetDataAsync().Result;
GetDataAsync().Wait();
var data = GetDataAsync().GetAwaiter().GetResult();

// ✅ Await properly
var data = await GetDataAsync();
```

### async-await/async-void 🔴 CRITICAL
**Never use `async void`** except for event handlers. It swallows exceptions and makes error handling impossible. Always return `Task` or `Task<T>`.

```csharp
// ❌ Exception vanishes, caller can't await
async void LoadData() { await _repo.LoadAsync(); }

// ✅ Return Task
async Task LoadDataAsync() { await _repo.LoadAsync(); }

// ✅ Event handlers are the ONE exception
async void OnButtonClick(object sender, RoutedEventArgs e)
{
    try { await LoadDataAsync(); }
    catch (Exception ex) { _logger.LogError(ex, "Load failed"); }
}
```

### async-await/cancellation-propagation 🟠 HIGH
**Always pass `CancellationToken` through the entire call chain.** If a method accepts one, every awaited call inside it should forward it.

```csharp
// ❌ Token accepted but not forwarded
async Task<Data> FetchAsync(CancellationToken ct)
{
    var response = await _httpClient.GetAsync(url);  // ct not forwarded!
    return Parse(response);
}

// ✅ Forward everywhere
async Task<Data> FetchAsync(CancellationToken ct)
{
    var response = await _httpClient.GetAsync(url, ct);
    return Parse(response);
}
```

### async-await/continue-with 🟡 MEDIUM
**Prefer `await` over `ContinueWith`.** `ContinueWith` is error-prone — it doesn't capture `SynchronizationContext` by default, requires manual exception handling, and has `TaskScheduler` confusion.

```csharp
// ❌ Error prone
GetDataAsync().ContinueWith(t => {
    if (t.IsFaulted) HandleError(t.Exception);
    else ProcessResult(t.Result);
}, TaskScheduler.FromCurrentSynchronizationContext());

// ✅ Normal control flow
try {
    var result = await GetDataAsync();
    ProcessResult(result);
} catch (Exception ex) {
    HandleError(ex);
}
```

### async-await/async-over-sync 🟠 HIGH
**Don't wrap synchronous code in `Task.Run` and call it "async."** That's async-over-sync — it wastes a thread pool thread and lies to the caller about the nature of the work.

```csharp
// ❌ Pretending sync work is async
Task<int> CalculateAsync() => Task.Run(() => Calculate());

// ✅ Just keep it synchronous if it's synchronous
int Calculate() { ... }

// ✅ Or be honest about it in the API design
// Only use Task.Run at the CALL SITE when you need to offload from UI thread
await Task.Run(() => Calculate());
```

### async-await/configure-await 🟡 MEDIUM
**Use `ConfigureAwait(false)` in library code** that doesn't need to resume on the UI thread. In application-level code (ViewModels, UI handlers), omit it so you resume on the UI thread correctly.

### async-await/fire-and-forget 🟠 HIGH
**Watch for fire-and-forget `Task` calls.** If a `Task`-returning method is called without `await`, the exception is silently lost. Either `await` it, or use an explicit fire-and-forget helper that logs exceptions.

```csharp
// ❌ Exception silently lost
_ = SendTelemetryAsync();

// ✅ Helper that surfaces failures
FireAndForget(SendTelemetryAsync());

static async void FireAndForget(Task task, [CallerMemberName] string caller = "")
{
    try { await task; }
    catch (Exception ex) { Log.Error(ex, "Fire-and-forget failed in {Caller}", caller); }
}
```

### async-await/valuetask-misuse 🔴 CRITICAL
**`ValueTask` rules:** Never await a `ValueTask` more than once. Never use `.Result` on an incomplete `ValueTask`. If you need to do either, call `.AsTask()` first.
