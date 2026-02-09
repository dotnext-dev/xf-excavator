# Rule: autofac

Autofac dependency injection container usage checks. These catch hidden dependencies, memory leaks from lifetime mismatches, and anti-patterns.

## Rules

### autofac/service-locator 🟠 HIGH
**Avoid service locator pattern.** Don't resolve from `ILifetimeScope` or `IComponentContext` inside business logic. Inject dependencies through constructors. Valid exceptions: composition roots, factory delegates, and middleware.

```csharp
// ❌ Service locator — hidden dependency
public class OrderService
{
    private readonly ILifetimeScope _scope;
    public OrderService(ILifetimeScope scope) => _scope = scope;
    public void Process()
    {
        var repo = _scope.Resolve<IOrderRepository>();  // hidden!
        repo.Save(order);
    }
}

// ✅ Constructor injection — explicit, testable
public class OrderService
{
    private readonly IOrderRepository _repo;
    public OrderService(IOrderRepository repo) => _repo = repo;
    public void Process() => _repo.Save(order);
}
```

### autofac/captive-dependency 🔴 CRITICAL
**Watch for captive dependencies.** A singleton must never hold a reference to a scoped or transient service — it causes the scoped service to live forever (memory leak) and may cause thread safety issues.

Flag when a `SingleInstance()` registration injects something registered as `InstancePerLifetimeScope()` or `InstancePerDependency()`.

```csharp
// ❌ Singleton holds scoped service — memory leak + thread safety
builder.RegisterType<CacheService>().SingleInstance();
builder.RegisterType<DbContext>().InstancePerLifetimeScope();

public class CacheService  // singleton
{
    private readonly DbContext _db;  // scoped — never disposed!
    public CacheService(DbContext db) => _db = db;
}

// ✅ Use Func<T> for deferred resolution
public class CacheService
{
    private readonly Func<DbContext> _dbFactory;
    public CacheService(Func<DbContext> dbFactory) => _dbFactory = dbFactory;
    public void DoWork()
    {
        using var db = _dbFactory();  // new each time, properly scoped
        db.Query(...);
    }
}
```

### autofac/deferred-resolution 🟡 MEDIUM
**Prefer `Func<T>` or `Lazy<T>` for deferred resolution** instead of injecting `ILifetimeScope` and resolving manually. Autofac supports these natively without additional registration.

- `Func<T>` — creates a new instance each call
- `Lazy<T>` — creates once on first access
- `Func<Owned<T>>` — creates with explicit disposal control

### autofac/owned-for-disposal 🟡 MEDIUM
**Use `Owned<T>` for deterministic disposal.** When a component needs to create and dispose of short-lived dependencies, use `Owned<T>` rather than manually managing lifetime scopes.

```csharp
// ❌ Manual lifetime scope management
public void DoWork()
{
    using var scope = _rootScope.BeginLifetimeScope();
    var worker = scope.Resolve<IWorker>();
    worker.Execute();
}

// ✅ Owned<T> handles it
public class MyService
{
    private readonly Func<Owned<IWorker>> _workerFactory;
    public MyService(Func<Owned<IWorker>> factory) => _workerFactory = factory;
    public void DoWork()
    {
        using var ownedWorker = _workerFactory();
        ownedWorker.Value.Execute();
    }  // IWorker disposed here
}
```

### autofac/direct-new-of-services 🟡 MEDIUM
**Flag direct `new` of services that should be injected.** If a class instantiates something with `new` that has dependencies of its own, it's bypassing the container and making the code untestable.

### autofac/missing-interface-registration 🟠 HIGH
**Check `.As<>()` registrations for missing interfaces.** A common bug is registering `RegisterType<Foo>()` without `.As<IFoo>()`, making `IFoo` unresolvable while `Foo` works fine — causes confusing runtime failures.

```csharp
// ❌ IFoo unresolvable
builder.RegisterType<Foo>();  // only resolves as Foo, not IFoo

// ✅ Register as interface
builder.RegisterType<Foo>().As<IFoo>();

// ✅ Or both
builder.RegisterType<Foo>().As<IFoo>().AsSelf();
```

### autofac/singleton-disposable 🟡 MEDIUM
**Disposal concerns:** Classes registered as `SingleInstance()` that implement `IDisposable` will not be disposed until the container is disposed (app shutdown). Flag if the class holds expensive unmanaged resources that should be released earlier.

### autofac/module-organization 🔵 LOW
**Prefer Autofac modules for registration grouping.** Large registration blocks in a single file are hard to navigate. Group related registrations into `Module` subclasses.

```csharp
// ✅ Clean module organization
public class DataAccessModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<DbContext>().InstancePerLifetimeScope();
        builder.RegisterType<OrderRepository>().As<IOrderRepository>();
    }
}
```
