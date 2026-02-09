# Rule: thread-management

Thread exhaustion and thread mismanagement checks. These catch patterns that degrade performance under load or cause resource starvation.

## Rules

### thread-management/explicit-thread-creation 🔴 CRITICAL
**Never use `new Thread(...)` or `ThreadPool.QueueUserWorkItem` for recurring or high-frequency work.** This leads to thread exhaustion under load. Replace with:
- `Task.Run` for CPU-bound one-off work
- `Channel<T>` or `System.Threading.Tasks.Dataflow` for producer/consumer pipelines
- `PeriodicTimer` (or Rx `Observable.Interval`) for recurring work
- Event-loop/scheduler patterns instead of dedicated threads

```csharp
// ❌ Thread per task
new Thread(() => ProcessItem(item)) { IsBackground = true }.Start();

// ✅ One-off CPU-bound
await Task.Run(() => ProcessItem(item));

// ✅ Producer/consumer pipeline
var channel = Channel.CreateBounded<WorkItem>(100);
// Single consumer loop instead of thread-per-item
await foreach (var item in channel.Reader.ReadAllAsync(ct))
    await ProcessItemAsync(item, ct);
```

### thread-management/thread-sleep 🟠 HIGH
**Flag `Thread.Sleep()`.** Replace with `await Task.Delay()` in async contexts. `Thread.Sleep` blocks a thread pool thread.

```csharp
// ❌ Blocks thread pool thread
Thread.Sleep(5000);

// ✅ Async-friendly
await Task.Delay(5000, cancellationToken);
```

### thread-management/thread-per-connection 🔴 CRITICAL
**Detect thread-per-connection patterns.** If code spawns a thread for each incoming request, connection, or message — flag it. Use async I/O and semaphores for concurrency limiting instead.

```csharp
// ❌ Thread per connection
foreach (var conn in connections)
    new Thread(() => HandleConnection(conn)).Start();

// ✅ Async with concurrency control
var semaphore = new SemaphoreSlim(maxConcurrency);
var tasks = connections.Select(async conn => {
    await semaphore.WaitAsync(ct);
    try { await HandleConnectionAsync(conn, ct); }
    finally { semaphore.Release(); }
});
await Task.WhenAll(tasks);
```

### thread-management/timer-sprawl 🟡 MEDIUM
**Timer sprawl:** Multiple `System.Threading.Timer` or `System.Timers.Timer` instances should be consolidated. Prefer `PeriodicTimer` (.NET 6+) or Rx schedulers.

```csharp
// ❌ Timer sprawl — 5 separate timers for 5 tasks
_timer1 = new Timer(_ => PollDatabase(), null, 0, 30000);
_timer2 = new Timer(_ => CheckHealth(), null, 0, 60000);

// ✅ Consolidated with Rx
Observable.Interval(TimeSpan.FromSeconds(30)).Subscribe(_ => PollDatabase());
Observable.Interval(TimeSpan.FromSeconds(60)).Subscribe(_ => CheckHealth());

// ✅ Or PeriodicTimer (.NET 6+)
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
while (await timer.WaitForNextTickAsync(ct))
    await PollDatabaseAsync(ct);
```

### thread-management/signaling-primitives 🟡 MEDIUM
**Replace manual `ManualResetEvent`/`AutoResetEvent` signaling with `SemaphoreSlim`, `TaskCompletionSource`, or `Channel<T>`** where possible — they compose better with async code.

```csharp
// ❌ Blocking wait on signal
var signal = new ManualResetEventSlim();
signal.Wait();  // blocks thread

// ✅ Async-compatible
var tcs = new TaskCompletionSource<bool>();
await tcs.Task;  // no thread blocked
```

### thread-management/lock-with-await 🔴 CRITICAL
**Flag `lock` statements that contain `await` inside them.** You cannot `await` inside a `lock`. This is a compile error in some cases and a correctness bug in others (e.g., using `Monitor` directly).

```csharp
// ❌ Cannot await inside lock
lock (_sync) {
    await DoWorkAsync();  // Compile error or runtime bug
}

// ✅ Use SemaphoreSlim
private readonly SemaphoreSlim _sync = new(1, 1);
await _sync.WaitAsync(ct);
try { await DoWorkAsync(); }
finally { _sync.Release(); }
```

### thread-management/task-factory-longrunning 🟠 HIGH
**Flag `Task.Factory.StartNew` with `TaskCreationOptions.LongRunning`.** This creates a dedicated thread (not a thread pool thread). It's rarely what you want. If you need a long-running background loop, use a `BackgroundService` or `Channel<T>` consumer instead.

```csharp
// ❌ Dedicated thread, bypasses thread pool
Task.Factory.StartNew(LongLoop, TaskCreationOptions.LongRunning);

// ✅ BackgroundService (hosted service pattern)
public class PollingService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(ct))
            await DoPollAsync(ct);
    }
}
```
