# Rule: migration-xf-uwp

Xamarin.Forms → UWP migration checks and transformation knowledge. This is the primary rule set for the Migrator agent. It covers control mapping, namespace replacement, API substitution, and XF-specific patterns that need restructuring.

**Key insight:** XF running on UWP renders as real UWP controls. The spy sees UWP controls regardless of whether XF or native UWP code produced them. One UWP mapper works before AND after migration.

## Rules

### xf-uwp/xaml-namespace 🟠 HIGH
**Replace XF XAML root namespace with UWP XAML namespace.**

```xml
<!-- ❌ XF -->
<ContentPage xmlns="http://xamarin.com/schemas/2014/forms"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">

<!-- ✅ UWP -->
<Page xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
```

Also remove: `xmlns:ios`, `xmlns:android`, `xmlns:d`, any `OnPlatform`-related xmlns.

### xf-uwp/control-swap 🟠 HIGH
**Replace XF controls with UWP equivalents.** Consult CLAUDE.md Section 3 for project-specific mappings. Core mappings:

| XF Control | UWP Control | Critical Attribute Changes |
|---|---|---|
| `ContentPage` | `Page` | `Title` → set on NavigationView or Page |
| `Label` | `TextBlock` | `Text` same. `TextColor` → `Foreground`. `FontAttributes` → `FontWeight`+`FontStyle` |
| `Entry` | `TextBox` | `Placeholder` → `PlaceholderText`. Add `Mode=TwoWay` to Text bindings |
| `Editor` | `TextBox` | Add `AcceptsReturn="True"` `TextWrapping="Wrap"` |
| `Button` | `Button` | `Text` → `Content`. `Clicked` → `Click` |
| `Switch` | `ToggleSwitch` | `IsToggled` → `IsOn`. `Toggled` → `Toggled` |
| `ActivityIndicator` | `ProgressRing` | `IsRunning` → `IsActive` |
| `ProgressBar` | `ProgressBar` | `Progress` (0-1) → `Value` (0-100, set `Maximum="1"` to keep same scale) |
| `Picker` | `ComboBox` | `SelectedIndex` same. `ItemsSource` same |
| `DatePicker` | `DatePicker` | `Date` → `Date`. `MinimumDate` → `MinYear`+`MinDate` |
| `TimePicker` | `TimePicker` | `Time` → `Time` |
| `Image` | `Image` | Check `Source` URI format. XF `ImageSource.FromResource` → UWP `ms-appx:///` |
| `ListView` | `ListView` | `ItemsSource` same. `ItemTemplate` → `DataTemplate`. `HasUnevenRows` → remove (UWP auto-sizes) |
| `CollectionView` | `ListView`/`GridView` | `ItemsLayout` needs conversion. Linear→ListView, Grid→GridView+`ItemsWrapGrid` |
| `Frame` | `Border` | `CornerRadius` same. `HasShadow` → `ThemeShadow` or drop shadow |
| `BoxView` | `Rectangle` (Shapes) | `Color` → `Fill` |

### xf-uwp/layout-swap 🟡 MEDIUM
**Replace XF layout containers with UWP equivalents.**

| XF Layout | UWP Panel | Notes |
|---|---|---|
| `StackLayout` | `StackPanel` | `Orientation` same. `Spacing` same |
| `Grid` | `Grid` | `RowDefinitions`/`ColumnDefinitions` same syntax. `Grid.Row`/`Grid.Column` same |
| `AbsoluteLayout` | `Canvas` | Position via `Canvas.Left`/`Canvas.Top` |
| `RelativeLayout` | `RelativePanel` | Different constraint syntax |
| `FlexLayout` | `StackPanel` or custom | No direct equivalent; approximate with StackPanel or ItemsWrapGrid |
| `ScrollView` | `ScrollViewer` | `Orientation` → `VerticalScrollBarVisibility`/`HorizontalScrollBarVisibility` |
| `ContentView` | `ContentControl` | `Content` property same |

### xf-uwp/binding-mode 🟠 HIGH
**XF default binding mode differs from UWP.** XF Entry.Text defaults to `TwoWay`. UWP TextBox.Text defaults to `OneWay`. **Always make binding mode explicit after migration.**

```xml
<!-- ❌ XF — implicitly TwoWay for Entry -->
<Entry Text="{Binding Username}" />

<!-- ✅ UWP — must be explicit -->
<TextBox Text="{Binding Username, Mode=TwoWay}" />
```

Also: XF `{Binding}` and UWP `{Binding}` are the same syntax, but `{x:Bind}` (UWP-only) is preferred for performance. During migration, keep `{Binding}` for compatibility; switch to `{x:Bind}` as an optimization later.

### xf-uwp/shell-to-navigationview 🔴 CRITICAL
**XF Shell → UWP NavigationView is a structural redesign, not a control swap.** This requires architectural decisions.

Shell concepts and their UWP equivalents:
| Shell | UWP |
|---|---|
| `Shell` (root) | `NavigationView` + `Frame` |
| `FlyoutItem` | `NavigationViewItem` |
| `Tab` | `NavigationViewItem` (with `Pivot` or `TabView` for tab groups) |
| `ShellContent` | Page loaded in `Frame` |
| `Shell.GoToAsync("route")` | `Frame.Navigate(typeof(Page))` |
| `Shell.FlyoutBehavior` | `NavigationView.PaneDisplayMode` |
| Query parameters `?id=5` | Navigation parameter object |

**This is always HIGH effort.** Flag for user decision before transforming.

### xf-uwp/device-api 🟠 HIGH
**Replace `Xamarin.Forms.Device.*` calls.** These don't exist in UWP.

| XF Device API | UWP Replacement |
|---|---|
| `Device.BeginInvokeOnMainThread(action)` | `Dispatcher.RunAsync(CoreDispatcherPriority.Normal, action)` |
| `Device.RuntimePlatform == Device.UWP` | Remove check — always UWP after migration |
| `Device.Idiom` | `AnalyticsInfo.VersionInfo.DeviceFamily` or remove |
| `Device.StartTimer(interval, callback)` | `DispatcherTimer` or `Observable.Interval` |
| `Device.OpenUri(uri)` | `Launcher.LaunchUriAsync(uri)` |
| `Device.GetNamedSize(...)` | Use UWP theme resources |

### xf-uwp/messaging-center 🟡 MEDIUM
**Replace `MessagingCenter` with event aggregator, Rx Subject, or direct events.**

```csharp
// ❌ XF
MessagingCenter.Send<App>(this, "LoggedIn");
MessagingCenter.Subscribe<App>(this, "LoggedIn", (sender) => { ... });

// ✅ Option A: Rx Subject (if using Rx already)
_loggedInSubject.OnNext(Unit.Default);
_loggedInSubject.Subscribe(_ => { ... });

// ✅ Option B: Simple event
public event EventHandler LoggedIn;
LoggedIn?.Invoke(this, EventArgs.Empty);

// ✅ Option C: IEventAggregator (if DI container supports it)
```

### xf-uwp/dependency-service 🟡 MEDIUM
**Replace `DependencyService.Get<T>()` with constructor injection via Autofac.**

```csharp
// ❌ XF — service locator
var gps = DependencyService.Get<IGpsService>();

// ✅ Constructor injection
public class MapViewModel
{
    private readonly IGpsService _gps;
    public MapViewModel(IGpsService gps) => _gps = gps;
}
```

Register the implementation in Autofac instead of `[assembly: Dependency]`.

### xf-uwp/effects-and-behaviors 🟡 MEDIUM
**XF Effects → UWP attached behaviors or custom controls.**
**XF Behaviors → UWP Behaviors (Microsoft.Xaml.Behaviors.Uwp).**

Effects have no direct equivalent. Convert to either:
- Attached properties (for simple visual changes)
- Custom controls (for complex rendering changes)
- Behaviors (if interaction-focused)

### xf-uwp/converters 🔵 LOW
**XF value converters work identically in UWP.** Just update the namespace. The `IValueConverter` interface is the same shape (`Convert`/`ConvertBack`). Only change: XF passes `Xamarin.Forms.BindableObject` context, UWP passes `DependencyObject`.

### xf-uwp/image-source 🟡 MEDIUM
**XF image sources need URI format changes for UWP.**

| XF Source | UWP Source |
|---|---|
| `ImageSource.FromResource("ns.img.png")` | `ms-appx:///Assets/img.png` |
| `ImageSource.FromFile("img.png")` | `ms-appx:///Assets/img.png` |
| `ImageSource.FromUri(new Uri(...))` | `new BitmapImage(new Uri(...))` |
| `<Image Source="img.png"/>` | `<Image Source="ms-appx:///Assets/img.png"/>` |

Copy image assets from XF shared project to UWP `Assets/` folder.

### xf-uwp/onplatform-removal 🟡 MEDIUM
**Remove all `OnPlatform` and `OnIdiom` markup.** After migration to UWP, there's only one platform.

```xml
<!-- ❌ XF -->
<Label FontSize="{OnPlatform Android=14, iOS=15, UWP=16}" />

<!-- ✅ UWP — just use the UWP value -->
<TextBlock FontSize="16" />
```

Also remove `Device.RuntimePlatform` checks in code-behind.

### xf-uwp/page-lifecycle 🟠 HIGH
**XF page lifecycle differs from UWP.**

| XF Event | UWP Equivalent |
|---|---|
| `OnAppearing()` | `OnNavigatedTo()` or `Loaded` event |
| `OnDisappearing()` | `OnNavigatingFrom()` or `Unloaded` event |
| `Page.Appearing` event | `Page.Loaded` + `NavigationHelper` |

### xf-uwp/automation-id-preservation 🔴 CRITICAL
**Preserve all AutomationIds.** The spy, all snapshot comparisons, and all flow runner scripts depend on `AutomationProperties.AutomationId`. Never rename, remove, or change these during migration.

```xml
<!-- XF -->
<Entry AutomationId="UsernameField" ... />

<!-- UWP — keep identical -->
<TextBox AutomationProperties.AutomationId="UsernameField" ... />
```

Note the syntax difference: XF uses `AutomationId` attribute directly. UWP uses `AutomationProperties.AutomationId` attached property.

### xf-uwp/code-behind-events 🟡 MEDIUM
**XF code-behind event wiring may need updating.** Check for:
- `Clicked` → `Click` (Button)
- `TextChanged` → `TextChanged` (same name, different event args type)
- `Toggled` → `Toggled` (same name, different event args)
- `ItemSelected` → `SelectionChanged` (ListView)
- `Refreshing` → no direct equivalent (use `RefreshContainer`)

Event handler signatures change from `(object sender, EventArgs e)` with XF-specific `EventArgs` to UWP-specific `RoutedEventArgs` or typed args.
