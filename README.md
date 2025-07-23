# DecoratR

A powerful and intuitive .NET library for implementing the Decorator pattern with Microsoft's Dependency Injection container. DecoratR provides a fluent API to chain decorators around your services, enabling cross-cutting concerns like logging, caching, retry logic, and more.

## Features

- **Fluent API**: Intuitive and readable decorator chain configuration
- **Regular Services**: Decorate standard services registered with dependency injection
- **Keyed Services**: Full support for .NET 8+ keyed services
- **Custom Factories**: Create decorators with complex dependencies using factory methods
- **Generic Decorators**: Support for generic decorator types with multiple type parameters
- **Conditional Decoration**: Apply decorators based on runtime conditions
- **Lifetime Management**: Control service lifetimes (Singleton, Scoped, Transient)

## Installation

```bash
dotnet add package DecoratR
```

## Quick Start

```csharp
using DecoratR;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Basic decoration
services.Decorate<IUserService>()
        .With<LoggingDecorator>()
        .Then<CacheDecorator>()
        .Then<UserService>()
        .Apply();

var provider = services.BuildServiceProvider();
var userService = provider.GetService<IUserService>();
// Result: LoggingDecorator -> CacheDecorator -> UserService
```

## Core Concepts

### Decorator Chain Order

Decorators are applied in the order they are defined. The **last** decorator added should be your base implementation:

```csharp
services.Decorate<IService>()
        .With<FirstDecorator>()    // Outermost decorator
        .Then<SecondDecorator>()   // Middle decorator
        .Then<BaseService>()       // Base implementation (innermost)
        .Apply();
```

### Constructor Requirements

Decorators (except the base implementation) must have a constructor that accepts the service type as the first parameter:

```csharp
public class LoggingDecorator : IService
{
    private readonly IService _inner;

    public LoggingDecorator(IService inner) => _inner = inner;

    public string Execute() => $"Log({_inner.Execute()})";
}
```

## Basic Usage

### Simple Decorator Chain

```csharp
// Service interface
public interface IOrderService
{
    Task<Order> GetOrderAsync(int orderId);
    Task<Order> CreateOrderAsync(Order order);
}

// Base implementation
public class OrderService : IOrderService
{
    public async Task<Order> GetOrderAsync(int orderId)
    {
        // Database logic here
        return new Order { Id = orderId };
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        // Creation logic here
        return order;
    }
}

// Decorators
public class LoggingDecorator : IOrderService
{
    private readonly IOrderService _inner;
    private readonly ILogger<LoggingDecorator> _logger;

    public LoggingDecorator(IOrderService inner, ILogger<LoggingDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<Order> GetOrderAsync(int orderId)
    {
        _logger.LogInformation("Getting order {OrderId}", orderId);
        var result = await _inner.GetOrderAsync(orderId);
        _logger.LogInformation("Retrieved order {OrderId}", result.Id);
        return result;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        _logger.LogInformation("Creating order");
        var result = await _inner.CreateOrderAsync(order);
        _logger.LogInformation("Created order {OrderId}", result.Id);
        return result;
    }
}

public class CacheDecorator : IOrderService
{
    private readonly IOrderService _inner;
    private readonly IMemoryCache _cache;

    public CacheDecorator(IOrderService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<Order> GetOrderAsync(int orderId)
    {
        var cacheKey = $"order_{orderId}";
        if (_cache.TryGetValue(cacheKey, out Order cachedOrder))
        {
            return cachedOrder;
        }

        var order = await _inner.GetOrderAsync(orderId);
        _cache.Set(cacheKey, order, TimeSpan.FromMinutes(5));
        return order;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        return await _inner.CreateOrderAsync(order);
    }
}

// Configuration
services.AddScoped<ILogger<LoggingDecorator>>();
services.AddMemoryCache();

services.Decorate<IOrderService>()
        .With<LoggingDecorator>()
        .Then<CacheDecorator>()
        .Then<OrderService>()
        .AsScoped()
        .Apply();
```

### Conditional Decoration

Apply decorators based on runtime conditions:

```csharp
services.Decorate<IOrderService>()
        .With<LoggingDecorator>()
        .ThenIf<RetryDecorator>(env.IsDevelopment())  // Only in development
        .ThenIf<CacheDecorator>(enableCaching)        // Based on configuration
        .Then<OrderService>()
        .Apply();
```

## Keyed Services

**Note**: Keyed services require .NET 8.0 or later. This feature is not available when targeting .NET 6.0.

DecoratR fully supports .NET 8+ keyed services, allowing you to create different decorator chains for the same service type:

```csharp
// Different configurations for different contexts
services.Decorate<IOrderService>("internal")
        .With<LoggingDecorator>()
        .Then<OrderService>()
        .Apply();

services.Decorate<IOrderService>("external")
        .With<LoggingDecorator>()
        .Then<RateLimitingDecorator>()
        .Then<SecurityDecorator>()
        .Then<OrderService>()
        .Apply();

services.Decorate<IOrderService>("cached")
        .With<CacheDecorator>()
        .Then<OrderService>()
        .AsSingleton()
        .Apply();

// Usage
var internalService = provider.GetRequiredKeyedService<IOrderService>("internal");
var externalService = provider.GetRequiredKeyedService<IOrderService>("external");
var cachedService = provider.GetRequiredKeyedService<IOrderService>("cached");
```

### Complex Keys

You can use any object as a key, including anonymous objects:

```csharp
var productionKey = new { Environment = "Production", Version = "v2" };

services.Decorate<IOrderService>(productionKey)
        .With<AuditDecorator>()
        .Then<SecurityDecorator>()
        .Then<OrderService>()
        .Apply();

// Usage
var service = provider.GetRequiredKeyedService<IOrderService>(productionKey);
```

## Custom Factory Methods

For decorators that require complex initialization or dependencies not easily handled by the DI container:

```csharp
services.AddSingleton<IMetrics, MetricsService>();
services.AddSingleton<IConfiguration, ConfigurationService>();

services.Decorate<IOrderService>()
        .With((serviceProvider, inner) =>
            new MetricsDecorator(
                inner,
                serviceProvider.GetRequiredService<IMetrics>(),
                serviceProvider.GetRequiredService<IConfiguration>().GetValue<string>("MetricsPrefix")))
        .Then<OrderService>()
        .Apply();
```

### Advanced Factory Examples

```csharp
// Conditional factory with complex logic
services.Decorate<IOrderService>()
        .WithIf(enableAdvancedMetrics, (sp, inner) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var metrics = sp.GetRequiredService<IMetrics>();
            var logger = sp.GetRequiredService<ILogger<AdvancedMetricsDecorator>>();

            return new AdvancedMetricsDecorator(inner, metrics, config, logger);
        })
        .Then<OrderService>()
        .Apply();

// Factory for base implementation
services.Decorate<IOrderService>()
        .With<LoggingDecorator>()
        .Then((serviceProvider, _) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString("DefaultConnection");
            return new DatabaseOrderService(connectionString);
        })
        .Apply();
```

## Generic Decorators

DecoratR supports generic decorators with multiple type parameters:

```csharp
// Generic decorator with one type parameter
public class GenericCacheDecorator<T> : IService<T>
{
    private readonly IService<T> _inner;
    private readonly IMemoryCache _cache;

    public GenericCacheDecorator(IService<T> inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<T> GetAsync(string key)
    {
        var cacheKey = $"{typeof(T).Name}_{key}";
        if (_cache.TryGetValue(cacheKey, out T cachedValue))
        {
            return cachedValue;
        }

        var value = await _inner.GetAsync(key);
        _cache.Set(cacheKey, value, TimeSpan.FromMinutes(5));
        return value;
    }
}

// Multiple generic parameters
public class TransformDecorator<TInput, TOutput> : ITransformService<TInput, TOutput>
{
    private readonly ITransformService<TInput, TOutput> _inner;

    public TransformDecorator(ITransformService<TInput, TOutput> inner) => _inner = inner;

    public async Task<TOutput> TransformAsync(TInput input)
    {
        // Pre-processing logic
        var result = await _inner.TransformAsync(input);
        // Post-processing logic
        return result;
    }
}

// Configuration
services.Decorate<IService<string>>()
        .With<GenericCacheDecorator<string>>()
        .Then<StringService>()
        .Apply();

services.Decorate<ITransformService<User, UserDto>>()
        .With<TransformDecorator<User, UserDto>>()
        .Then<UserTransformService>()
        .Apply();
```

## Lifetime Management

Control the lifetime of your decorated services:

```csharp
// Singleton
services.Decorate<IOrderService>()
        .With<CacheDecorator>()
        .Then<OrderService>()
        .AsSingleton()
        .Apply();

// Scoped
services.Decorate<IOrderService>()
        .With<LoggingDecorator>()
        .Then<OrderService>()
        .AsScoped()
        .Apply();

// Transient (default)
services.Decorate<IOrderService>()
        .With<RetryDecorator>()
        .Then<OrderService>()
        .AsTransient()  // Optional, as it's the default
        .Apply();

// Custom lifetime
services.Decorate<IOrderService>()
        .With<MetricsDecorator>()
        .Then<OrderService>()
        .WithLifetime(ServiceLifetime.Scoped)
        .Apply();
```

## Real-World Examples

### E-commerce Order Processing

```csharp
public class OrderProcessingConfiguration
{
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        var enableMetrics = config.GetValue<bool>("Features:Metrics");
        var enableRetry = config.GetValue<bool>("Features:Retry");
        var enableAudit = config.GetValue<bool>("Features:Audit");

        services.Decorate<IOrderService>()
                .WithIf<MetricsDecorator>(enableMetrics)
                .Then<ValidationDecorator>()
                .ThenIf<RetryDecorator>(enableRetry)
                .Then<CacheDecorator>()
                .ThenIf<AuditDecorator>(enableAudit)
                .Then<DatabaseOrderService>()
                .AsScoped()
                .Apply();

        // Payment service with different requirements
        services.Decorate<IPaymentService>()
                .With<SecurityDecorator>()
                .Then<LoggingDecorator>()
                .Then<RateLimitingDecorator>()
                .Then<PaymentService>()
                .Apply();
    }
}
```

### Multi-tenant Application

```csharp
public class MultiTenantConfiguration
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Different configurations per tenant
        services.Decorate<IDataService>("tenant-basic")
                .With<LoggingDecorator>()
                .Then<BasicDataService>()
                .Apply();

        services.Decorate<IDataService>("tenant-premium")
                .With<LoggingDecorator>()
                .Then<CacheDecorator>()
                .Then<MetricsDecorator>()
                .Then<PremiumDataService>()
                .Apply();

        services.Decorate<IDataService>("tenant-enterprise")
                .With<SecurityDecorator>()
                .Then<AuditDecorator>()
                .Then<LoggingDecorator>()
                .Then<CacheDecorator>()
                .Then<MetricsDecorator>()
                .Then<EnterpriseDataService>()
                .Apply();
    }
}
```

## Best Practices

### 1. Decorator Ordering

Consider the logical flow of your decorators:

```csharp
// Good: Logical order from outside to inside
services.Decorate<IService>()
        .With<SecurityDecorator>()      // Authentication/Authorization first
        .Then<RateLimitingDecorator>()  // Rate limiting after security
        .Then<LoggingDecorator>()       // Logging after rate limiting
        .Then<MetricsDecorator>()       // Metrics collection
        .Then<CacheDecorator>()         // Cache closest to data source
        .Then<BaseService>()            // Base implementation
        .Apply();
```

### 2. Error Handling

Implement proper error handling in decorators:

```csharp
public class ErrorHandlingDecorator : IService
{
    private readonly IService _inner;
    private readonly ILogger<ErrorHandlingDecorator> _logger;

    public ErrorHandlingDecorator(IService inner, ILogger<ErrorHandlingDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(Request request)
    {
        try
        {
            return await _inner.ExecuteAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing request {RequestId}", request.Id);
            throw;
        }
    }
}
```

### 3. Performance Considerations

Be mindful of decorator overhead:

```csharp
// Use conditional decoration for expensive operations
var enableDetailedLogging = config.GetValue<bool>("Logging:Detailed");

services.Decorate<IService>()
        .WithIf<DetailedLoggingDecorator>(enableDetailedLogging)
        .Then<BaseService>()
        .Apply();
```

### 4. Testing

Decorators are easy to test in isolation:

```csharp
[Test]
public void LoggingDecorator_ShouldLogExecution()
{
    // Arrange
    var mockInner = new Mock<IService>();
    var mockLogger = new Mock<ILogger<LoggingDecorator>>();
    var decorator = new LoggingDecorator(mockInner.Object, mockLogger.Object);

    // Act
    decorator.Execute();

    // Assert
    mockLogger.Verify(x => x.LogInformation(It.IsAny<string>()), Times.Once);
    mockInner.Verify(x => x.Execute(), Times.Once);
}
```

## API Reference

### Extension Methods

- `Decorate<TService>()` - Begin decoration for a service type
- `Decorate<TService>(object serviceKey)` - Begin decoration for a keyed service

### IDecorationBuilder Methods

- `Then<TDecorator>()` - Add a decorator to the chain
- `With<TDecorator>()` - Alias for `Then<TDecorator>()`
- `Then(Func<IServiceProvider, TService, TService> factory)` - Add decorator via factory
- `ThenIf<TDecorator>(bool condition)` - Conditionally add decorator
- `WithIf<TDecorator>(bool condition)` - Alias for `ThenIf<TDecorator>(bool condition)`
- `ThenIf(bool condition, Func<IServiceProvider, TService, TService> factory)` - Conditionally add decorator via factory
- `WithLifetime(ServiceLifetime lifetime)` - Set service lifetime
- `AsSingleton()` - Set lifetime to Singleton
- `AsScoped()` - Set lifetime to Scoped
- `AsTransient()` - Set lifetime to Transient
- `Apply()` - Apply the decoration configuration

## Common Scenarios

### Cross-Cutting Concerns

DecoratR excels at implementing cross-cutting concerns:

- **Logging**: Track method calls and performance
- **Caching**: Store frequently accessed data
- **Retry Logic**: Handle transient failures
- **Rate Limiting**: Control API usage
- **Security**: Authentication and authorization
- **Metrics**: Performance monitoring
- **Validation**: Input/output validation
- **Circuit Breaker**: Fault tolerance

### Integration Patterns

- **Repository Pattern**: Add caching and logging to data access
- **Command/Query Pattern**: Add validation and auditing
- **API Clients**: Add retry, rate limiting, and circuit breaker logic
- **Message Handlers**: Add error handling and dead letter queue logic

## Requirements

- .NET 6.0 or later
- Microsoft.Extensions.DependencyInjection 6.0.0 or later

**Note**: Keyed services are only available in .NET 8.0 or later. If you're using .NET 6.0, you can only use the regular (non-keyed) decoration features.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests on the GitHub repository.

## License

This project is licensed under the MIT License. See the LICENSE file for details.
