# Rule: migration-uwp-winui

UWP → WinUI 3 and .NET 5 → .NET 10 migration readiness checks. These flag APIs and patterns that will break or become obsolete during migration.

## Rules

### migration/uwp-namespace-swap 🟠 HIGH
**Flag UWP-specific XAML namespaces** that must change in WinUI 3:

| UWP (current) | WinUI 3 (target) |
|---|---|
| `Windows.UI.Xaml` | `Microsoft.UI.Xaml` |
| `Windows.UI.Xaml.Controls` | `Microsoft.UI.Xaml.Controls` |
| `Windows.UI.Xaml.Media` | `Microsoft.UI.Xaml.Media` |
| `Windows.UI.Xaml.Data` | `Microsoft.UI.Xaml.Data` |
| `Windows.UI.Xaml.Navigation` | `Microsoft.UI.Xaml.Navigation` |
| `Windows.UI.Composition` | `Microsoft.UI.Composition` |

### migration/dispatcher-api 🟠 HIGH
**CoreDispatcher → DispatcherQueue.** This is a breaking change with different semantics.

```csharp
// ❌ UWP — will not exist in WinUI
await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateUI());

// ✅ WinUI 3
DispatcherQueue.TryEnqueue(() => UpdateUI());

// ✅ Migration-safe: wrap behind interface
public interface IDispatcherService
{
    void Enqueue(Action action);
    Task EnqueueAsync(Func<Task> action);
}
```

### migration/corewindow-removal 🟠 HIGH
**CoreWindow → Window (WinUI).** `CoreWindow.GetForCurrentThread()` does not exist in WinUI 3. Access the `Window` via the App class or pass it through DI.

### migration/deprecated-net-apis 🟠 HIGH
**Flag APIs deprecated or removed between .NET 5 and .NET 10:**

| Deprecated/Removed | Replacement |
|---|---|
| `BinaryFormatter` | `System.Text.Json`, `MessagePack` |
| `Thread.Abort()` | `CancellationToken` |
| `WebClient` | `HttpClient` |
| `AppDomain.CreateDomain` | `AssemblyLoadContext` |
| `Remoting` | gRPC, named pipes |

### migration/platform-abstraction 🟡 MEDIUM
**Wrap platform-specific code behind interfaces** so UWP → WinUI swap is a single implementation change.

```csharp
// ❌ Platform code scattered through business logic
#if WINDOWS_UWP
    CoreWindow.GetForCurrentThread()...
#else
    // WinUI path
#endif

// ✅ Abstraction — swap implementation, not callers
public interface IPlatformServices
{
    Task<StorageFile> PickFileAsync();
    void ShowNotification(string message);
}

public class UwpPlatformServices : IPlatformServices { ... }   // register now
public class WinUIPlatformServices : IPlatformServices { ... } // register after migration
```

### migration/conditional-compilation 🟡 MEDIUM
**Avoid `#if` conditional compilation for platform differences.** Prefer runtime abstraction via interfaces and DI. `#if` blocks are hard to test, easy to get stale, and create invisible code paths.

### migration/nullable-reference-types 🟡 MEDIUM
**Enable nullable reference types.** .NET 10 projects should have `<Nullable>enable</Nullable>` in `.csproj`. Flag APIs that return `null` without proper nullability annotations.

### migration/target-framework 🟠 HIGH
**Flag outdated TFMs.** Check `.csproj` files for `netcoreapp3.1`, `net5.0`, or other deprecated target frameworks. Plan migration path to `net10.0`.

### migration/modern-csharp-features 🔵 LOW
**Recommend modern C# features** available in .NET 10 for cleaner code:
- `global using` directives
- File-scoped namespaces (`namespace Foo;`)
- Raw string literals
- Collection expressions (`[1, 2, 3]`)
- Primary constructors
- Pattern matching enhancements

### migration/winui-lifecycle 🟠 HIGH
**UWP lifecycle differences.** WinUI 3 apps don't use `OnLaunched` the same way. The activation model, suspend/resume, and background task registration all differ. Flag any code that relies on UWP-specific lifecycle events:
- `Application.Suspending` / `Resuming` work differently
- `ExtendedExecutionSession` has different behavior
- Background tasks use a different registration model
