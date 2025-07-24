using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DecoratR.Examples;

public class GenericDecoratorExample
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register dependencies
        services.AddSingleton<ILogger<IUserService>, Logger<IUserService>>();
        services.AddSingleton<IMetrics, MetricsService>();

        // Example 1: Basic unconstrained generic decorators
        services.Decorate<IUserService>("primitive-types")
                .With<GenericDecorator<string>>()    // T = string
                .Then<GenericDecorator<int>>()       // T = int
                .Then<GenericDecorator<bool>>()      // T = bool
                .Then<UserService>()
                .Apply();

        // Example 2: Multi-generic decorator with string and int (as requested)
        services.Decorate<IUserService>("multi-generic")
                .With<MultiGenericDecorator<string, int>>()  // T1 = string, T2 = int
                .Then<UserService>()
                .Apply();

        // Example 3: Generic decorators with value types and structs
        services.Decorate<IUserService>("value-types")
                .With<GenericDecorator<DateTime>>()
                .Then<GenericDecorator<Guid>>()
                .Then<GenericDecorator<decimal>>()
                .Then<UserService>()
                .Apply();

        // Example 4: Complex generic types
        services.Decorate<IUserService>("complex-generics")
                .With<GenericDecorator<List<Dictionary<string, int>>>>()
                .Then<MultiGenericDecorator<Dictionary<string, object>, List<int>>>()
                .Then<UserService>()
                .Apply();

        // Example 5: Triple generic decorator
        services.Decorate<IUserService>("triple-generic")
                .With<TripleGenericDecorator<string, int, bool>>()
                .Then<UserService>()
                .Apply();

        // Example 6: Generic decorators with dependencies
        services.Decorate<IUserService>("with-dependencies")
                .With<GenericDecoratorWithDependencies<DateTime>>()
                .Then<UserService>()
                .Apply();

        // Example 7: Mixing generic and regular decorators
        services.Decorate<IUserService>("mixed")
                .With<LoggingDecorator>()                    // Regular decorator
                .Then<GenericDecorator<string>>()            // Generic decorator
                .Then<AuditDecorator>()                      // Regular decorator
                .Then<MultiGenericDecorator<int, bool>>()    // Multi-generic decorator
                .Then<UserService>()
                .Apply();

        // Example 8: Conditional generic decorators
        bool enableStringProcessing = true;
        bool enableIntProcessing = false;

        services.Decorate<IUserService>("conditional")
                .WithIf<GenericDecorator<string>>(enableStringProcessing)  // Will be added
                .ThenIf<GenericDecorator<int>>(enableIntProcessing)        // Will be skipped
                .Then<UserService>()
                .Apply();

        // Example 9: Custom factory with generic decorators
        services.Decorate<IUserService>("factory")
                .With((sp, inner) => new GenericFactoryDecorator<DateTime>(
                    inner,
                    sp.GetRequiredService<IMetrics>()))
                .Then<UserService>()
                .Apply();
    }

    public void Usage(IServiceProvider serviceProvider)
    {
        // Example 1: Primitive types
        var primitiveService = serviceProvider.GetRequiredKeyedService<IUserService>("primitive-types");
        var primitiveResult = primitiveService.GetUser("123");
        // Result: "Generic<String>(Generic<Int32>(Generic<Boolean>(User(123))))"

        // Example 2: Multi-generic with string and int
        var multiService = serviceProvider.GetRequiredKeyedService<IUserService>("multi-generic");
        var multiResult = multiService.GetUser("456");
        // Result: "Multi<String,Int32>(User(456))"

        // Example 3: Value types
        var valueTypeService = serviceProvider.GetRequiredKeyedService<IUserService>("value-types");
        var valueResult = valueTypeService.GetUser("789");
        // Result: "Generic<DateTime>(Generic<Guid>(Generic<Decimal>(User(789))))"

        // Example 4: Complex generics
        var complexService = serviceProvider.GetRequiredKeyedService<IUserService>("complex-generics");
        var complexResult = complexService.GetUser("abc");
        // Result: "Generic<List`1>(Multi<Dictionary`2,List`1>(User(abc)))"

        // Example 5: Triple generic
        var tripleService = serviceProvider.GetRequiredKeyedService<IUserService>("triple-generic");
        var tripleResult = tripleService.GetUser("def");
        // Result: "Triple<String,Int32,Boolean>(User(def))"

        // Example 6: With dependencies
        var depService = serviceProvider.GetRequiredKeyedService<IUserService>("with-dependencies");
        var depResult = depService.GetUser("ghi");
        // Result: "GenericWithDep<DateTime>(User(ghi))"

        // Example 7: Mixed decorators
        var mixedService = serviceProvider.GetRequiredKeyedService<IUserService>("mixed");
        var mixedResult = mixedService.GetUser("jkl");
        // Result: "Log(Generic<String>(Audit(Multi<Int32,Boolean>(User(jkl)))))"

        // Example 8: Conditional (only string processing enabled)
        var conditionalService = serviceProvider.GetRequiredKeyedService<IUserService>("conditional");
        var conditionalResult = conditionalService.GetUser("mno");
        // Result: "Generic<String>(User(mno))" (int processing was skipped)

        // Example 9: Factory
        var factoryService = serviceProvider.GetRequiredKeyedService<IUserService>("factory");
        var factoryResult = factoryService.GetUser("pqr");
        // Result: "GenericFactory<DateTime>(User(pqr))"
    }
}

// Sample interfaces and implementations
public interface IUserService
{
    string GetUser(string id);
}

public interface IMetrics
{
    void RecordOperation(string operation, string type);
}

public class UserService : IUserService
{
    public string GetUser(string id) => $"User({id})";
}

// Generic decorator implementations demonstrating unconstrained generics
public class GenericDecorator<T> : IUserService
{
    private readonly IUserService _inner;
    private readonly Type _genericType;

    public GenericDecorator(IUserService inner)
    {
        _inner = inner;
        _genericType = typeof(T);
    }

    public string GetUser(string id)
    {
        return $"Generic<{_genericType.Name}>({_inner.GetUser(id)})";
    }
}

// Multi-generic decorator where T1 and T2 can be ANY types (string, int, etc.)
public class MultiGenericDecorator<T1, T2> : IUserService
{
    private readonly IUserService _inner;
    private readonly Type _type1;
    private readonly Type _type2;

    public MultiGenericDecorator(IUserService inner)
    {
        _inner = inner;
        _type1 = typeof(T1);
        _type2 = typeof(T2);
    }

    public string GetUser(string id)
    {
        return $"Multi<{_type1.Name},{_type2.Name}>({_inner.GetUser(id)})";
    }
}

// Triple generic decorator with three unconstrained type parameters
public class TripleGenericDecorator<T1, T2, T3> : IUserService
{
    private readonly IUserService _inner;
    private readonly Type _type1;
    private readonly Type _type2;
    private readonly Type _type3;

    public TripleGenericDecorator(IUserService inner)
    {
        _inner = inner;
        _type1 = typeof(T1);
        _type2 = typeof(T2);
        _type3 = typeof(T3);
    }

    public string GetUser(string id)
    {
        return $"Triple<{_type1.Name},{_type2.Name},{_type3.Name}>({_inner.GetUser(id)})";
    }
}

// Generic decorator with dependency injection
public class GenericDecoratorWithDependencies<T> : IUserService
{
    private readonly IUserService _inner;
    private readonly ILogger<IUserService> _logger;
    private readonly Type _genericType;

    public GenericDecoratorWithDependencies(IUserService inner, ILogger<IUserService> logger)
    {
        _inner = inner;
        _logger = logger;
        _genericType = typeof(T);
    }

    public string GetUser(string id)
    {
        _logger.LogInformation("Processing request for user {UserId} with type {GenericType}", id, _genericType.Name);
        return $"GenericWithDep<{_genericType.Name}>({_inner.GetUser(id)})";
    }
}

// Generic factory decorator
public class GenericFactoryDecorator<T> : IUserService
{
    private readonly IUserService _inner;
    private readonly IMetrics _metrics;
    private readonly Type _genericType;

    public GenericFactoryDecorator(IUserService inner, IMetrics metrics)
    {
        _inner = inner;
        _metrics = metrics;
        _genericType = typeof(T);
    }

    public string GetUser(string id)
    {
        _metrics.RecordOperation("GetUser", _genericType.Name);
        return $"GenericFactory<{_genericType.Name}>({_inner.GetUser(id)})";
    }
}

// Regular decorators for mixing examples
public class LoggingDecorator : IUserService
{
    private readonly IUserService _inner;

    public LoggingDecorator(IUserService inner) => _inner = inner;

    public string GetUser(string id) => $"Log({_inner.GetUser(id)})";
}

public class AuditDecorator : IUserService
{
    private readonly IUserService _inner;

    public AuditDecorator(IUserService inner) => _inner = inner;

    public string GetUser(string id) => $"Audit({_inner.GetUser(id)})";
}

// Sample service implementations
public class MetricsService : IMetrics
{
    public void RecordOperation(string operation, string type)
    {
        Console.WriteLine($"Metrics: {operation} executed for type {type}");
    }
}

public class Logger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Console.WriteLine($"Log: {formatter(state, exception)}");
    }
}
