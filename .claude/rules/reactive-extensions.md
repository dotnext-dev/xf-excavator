# Rule: reactive-extensions

System.Reactive (Rx.NET) simplification and correctness checks. These catch memory leaks, unnecessary complexity, and misuse patterns.

## Rules

### reactive-extensions/nested-subscribe 🟡 MEDIUM
**Nested `Subscribe` calls indicate missing Rx operators.** Common replacements:
- `Subscribe` + `if/else` inside → `Where()` + `Subscribe()`
- `Subscribe` + `try/catch` → `Catch()` or `Retry()`
- `Subscribe` + accumulator variable → `Scan()` or `Aggregate()`
- Nested `Subscribe` → `SelectMany()` / `Merge()` / `Switch()`

```csharp
// ❌ Nested subscribe
source.Subscribe(item =>
{
    GetDetailsAsync(item).Subscribe(detail => Process(detail));
});

// ✅ SelectMany
source
    .SelectMany(item => GetDetailsAsync(item))
    .Subscribe(detail => Process(detail))
    .DisposeWith(disposables);
```

### reactive-extensions/unnecessary-subject 🟡 MEDIUM
**Replace `Subject<T>` when not needed.** If a `Subject` is only used to bridge an event into Rx, use `Observable.FromEventPattern` or `Observable.Create` instead. `Subject` should be a last resort.

```csharp
// ❌ Subject as event bridge
private readonly Subject<string> _messages = new();
public IObservable<string> Messages => _messages;
void OnMessageReceived(string msg) => _messages.OnNext(msg);

// ✅ No Subject needed
public IObservable<string> Messages => Observable.FromEventPattern<string>(
    h => MessageReceived += h,
    h => MessageReceived -= h)
    .Select(e => e.EventArgs);
```

### reactive-extensions/hot-cold-confusion 🟠 HIGH
**Hot vs Cold confusion.** Flag when a cold observable is subscribed to multiple times causing duplicate side effects. Recommend `.Publish().RefCount()` or `.Replay(1).RefCount()` as appropriate.

```csharp
// ❌ Cold observable subscribed twice — HTTP call happens twice
var data = Observable.FromAsync(() => _httpClient.GetAsync(url));
data.Subscribe(r => UpdateUI(r));
data.Subscribe(r => LogResponse(r));  // second HTTP call!

// ✅ Share the observable
var data = Observable.FromAsync(() => _httpClient.GetAsync(url))
    .Publish()
    .RefCount();
data.Subscribe(r => UpdateUI(r));
data.Subscribe(r => LogResponse(r));  // same result, no duplicate call
```

### reactive-extensions/subscription-leak 🟠 HIGH
**Missing disposal of subscriptions.** Every `Subscribe()` call returns an `IDisposable`. Flag subscriptions that aren't added to a `CompositeDisposable` or otherwise disposed. This causes memory leaks.

```csharp
// ❌ Subscription leaks — never disposed
public class ViewModel
{
    public ViewModel(IObservable<Data> source)
    {
        source.Subscribe(d => Update(d));  // leaked!
    }
}

// ✅ Track and dispose
public class ViewModel : IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public ViewModel(IObservable<Data> source)
    {
        source
            .Subscribe(d => Update(d))
            .DisposeWith(_disposables);
    }

    public void Dispose() => _disposables.Dispose();
}
```

### reactive-extensions/hardcoded-scheduler 🟡 MEDIUM
**Scheduler usage:** Rx operations involving timing (`Delay`, `Throttle`, `Interval`, `Buffer` with time) should use explicit `IScheduler` parameters for testability. Flag hardcoded schedulers.

```csharp
// ❌ Untestable — uses real wall clock
source.Throttle(TimeSpan.FromMilliseconds(300))
    .Subscribe(x => Search(x));

// ✅ Injectable scheduler
source.Throttle(TimeSpan.FromMilliseconds(300), _scheduler)
    .Subscribe(x => Search(x))
    .DisposeWith(_disposables);
```

### reactive-extensions/overcomplex-chain 🟡 MEDIUM
**Over-complex chains.** If an Rx pipeline exceeds ~10 operators, suggest breaking it into named methods or intermediate observables with descriptive names.

```csharp
// ❌ Wall of operators
source
    .Where(x => x.IsValid)
    .Select(x => x.Value)
    .DistinctUntilChanged()
    .Throttle(TimeSpan.FromMilliseconds(300), scheduler)
    .SelectMany(v => FetchAsync(v))
    .Where(r => r.HasData)
    .Select(r => r.Data)
    .Catch<Data, HttpException>(ex => Observable.Return(Data.Empty))
    .Retry(3)
    .ObserveOn(scheduler)
    .Subscribe(d => UpdateUI(d));

// ✅ Named intermediates
var validInputs = source
    .Where(x => x.IsValid)
    .Select(x => x.Value)
    .DistinctUntilChanged();

var throttledFetches = validInputs
    .Throttle(TimeSpan.FromMilliseconds(300), scheduler)
    .SelectMany(v => FetchAsync(v).ToObservable());

throttledFetches
    .CatchAndRetry(maxRetries: 3)
    .ObserveOn(scheduler)
    .Subscribe(d => UpdateUI(d))
    .DisposeWith(_disposables);
```

### reactive-extensions/polling-replacement 🟡 MEDIUM
**Replace `Task`-based polling with Rx.** If code does `while(true) { await Task.Delay(); ... }`, suggest `Observable.Interval` with appropriate operators.

```csharp
// ❌ Manual polling loop
while (!ct.IsCancellationRequested)
{
    await PollAsync();
    await Task.Delay(TimeSpan.FromSeconds(30), ct);
}

// ✅ Rx-based polling
Observable.Interval(TimeSpan.FromSeconds(30))
    .SelectMany(_ => Observable.FromAsync(ct => PollAsync(ct)))
    .Subscribe()
    .DisposeWith(disposables);
```
