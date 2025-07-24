using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DecoratR.Examples;

public class CustomFactoryExample
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register dependencies that will be injected via factory
        services.AddSingleton<IMetrics, MetricsService>();
        services.AddSingleton<IConfiguration, ConfigurationService>();
        services.AddScoped<ILogger<IUserService>, Logger<IUserService>>();

        // Example 1: Basic custom factory as requested
        services.Decorate<IUserService>()
                .With((sp, inner) => new CustomDecorator(inner, sp.GetRequiredService<IMetrics>()))
                .Then<UserService>()
                .Apply();

        // Example 2: Complex custom factory with multiple dependencies
        services.Decorate<IUserService>("complex")
                .With((sp, inner) => new ComplexDecorator(
                    inner,
                    sp.GetRequiredService<IMetrics>(),
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<ILogger<IUserService>>()))
                .Then<UserService>()
                .Apply();

        // Example 3: Mixing custom factories with type-based decorators
        services.Decorate<IUserService>("mixed")
                .With<LoggingDecorator>()
                .Then((sp, inner) => new MetricsDecorator(inner, sp.GetRequiredService<IMetrics>()))
                .Then<RetryDecorator>()
                .Then<UserService>()
                .Apply();

        // Example 4: Conditional custom factories
        bool enableAdvancedFeatures = true;
        services.Decorate<IUserService>("conditional")
                .WithIf(enableAdvancedFeatures, (sp, inner) =>
                    new AdvancedDecorator(inner, sp.GetRequiredService<IConfiguration>()))
                .Then<UserService>()
                .Apply();

        // Example 5: Custom factory for base implementation
        services.Decorate<IUserService>("custom-base")
                .With<AuditDecorator>()
                .Then((sp, _) => new CustomUserService(sp.GetRequiredService<IConfiguration>()))
                .Apply();
    }

    public void Usage(IServiceProvider serviceProvider)
    {
        // Use regular service with custom decorator
        var userService = serviceProvider.GetRequiredService<IUserService>();
        var result = userService.GetUser("123");
        // Result: "Custom(User(123))" with metrics recorded

        // Use keyed service with complex custom decorator
        var complexService = serviceProvider.GetRequiredKeyedService<IUserService>("complex");
        var complexResult = complexService.GetUser("456");
        // Result: "Complex(User(456))" with metrics, config, and logging
    }
}

// Sample interfaces and implementations
public interface IUserService
{
    string GetUser(string id);
}

public interface IMetrics
{
    void RecordExecution(string operation);
}

public interface IConfiguration
{
    T GetValue<T>(string key);
}

public class UserService : IUserService
{
    public string GetUser(string id) => $"User({id})";
}

// Custom decorators created via factory methods
public class CustomDecorator : IUserService
{
    private readonly IUserService _inner;
    private readonly IMetrics _metrics;

    public CustomDecorator(IUserService inner, IMetrics metrics)
    {
        _inner = inner;
        _metrics = metrics;
    }

    public string GetUser(string id)
    {
        _metrics.RecordExecution("GetUser");
        return $"Custom({_inner.GetUser(id)})";
    }
}

public class ComplexDecorator : IUserService
{
    private readonly IUserService _inner;
    private readonly IMetrics _metrics;
    private readonly IConfiguration _config;
    private readonly ILogger<IUserService> _logger;

    public ComplexDecorator(IUserService inner, IMetrics metrics, IConfiguration config, ILogger<IUserService> logger)
    {
        _inner = inner;
        _metrics = metrics;
        _config = config;
        _logger = logger;
    }

    public string GetUser(string id)
    {
        _logger.LogInformation("Getting user {UserId}", id);
        _metrics.RecordExecution("GetUser");
        var timeout = _config.GetValue<int>("UserService:Timeout");
        return $"Complex({_inner.GetUser(id)})";
    }
}

public class MetricsDecorator : IUserService
{
    private readonly IUserService _inner;
    private readonly IMetrics _metrics;

    public MetricsDecorator(IUserService inner, IMetrics metrics)
    {
        _inner = inner;
        _metrics = metrics;
    }

    public string GetUser(string id)
    {
        _metrics.RecordExecution("GetUser");
        return $"Metrics({_inner.GetUser(id)})";
    }
}

public class AdvancedDecorator : IUserService
{
    private readonly IUserService _inner;
    private readonly IConfiguration _config;

    public AdvancedDecorator(IUserService inner, IConfiguration config)
    {
        _inner = inner;
        _config = config;
    }

    public string GetUser(string id)
    {
        var feature = _config.GetValue<string>("Features:Advanced");
        return $"Advanced({_inner.GetUser(id)})";
    }
}

public class CustomUserService : IUserService
{
    private readonly IConfiguration _config;

    public CustomUserService(IConfiguration config)
    {
        _config = config;
    }

    public string GetUser(string id) => $"CustomUser({id})";
}

// Sample service implementations
public class MetricsService : IMetrics
{
    public void RecordExecution(string operation)
    {
        Console.WriteLine($"Metrics: {operation} executed");
    }
}

public class ConfigurationService : IConfiguration
{
    public T GetValue<T>(string key) => default(T)!;
}
