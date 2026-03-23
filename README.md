![NuGet](https://img.shields.io/nuget/v/DecoratR)
[![NuGet](https://img.shields.io/nuget/dt/decoratr.svg)](https://www.nuget.org/packages/decoratr)
![Build Status](https://github.com/omarzawahry/DecoratR/actions/workflows/prod-ci.yml/badge.svg?branch=main)

# DecoratR

Intuitive .NET library for implementing the Decorator pattern with Microsoft's Dependency Injection container. DecoratR provides a fluent API to chain decorators around your services, enabling cross-cutting concerns like logging, caching, retry logic, and more.

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
  - [Decorator Chain Order](#decorator-chain-order)
  - [Constructor Requirements](#constructor-requirements)
- [Basic Usage](#basic-usage)
  - [Simple Decorator Chain](#simple-decorator-chain)
  - [Conditional Decoration](#conditional-decoration)
- [Keyed Services](#keyed-services)
  - [Complex Keys](#complex-keys)
- [Custom Factory Methods](#custom-factory-methods)
  - [Advanced Factory Examples](#advanced-factory-examples)
- [Lifetime Management](#lifetime-management)
- [Best Practices](#best-practices)
  - [1. Decorator Ordering](#1-decorator-ordering)
  - [2. Error Handling](#2-error-handling)
  - [3. Performance Considerations](#3-performance-considerations)
  - [4. Testing](#4-testing)
- [API Reference](#api-reference)
  - [Extension Methods](#extension-methods)
  - [IDecorationBuilder Methods](#idecorationbuilder-methods)
- [Requirements](#requirements)
- [Contributing](#contributing)
- [License](#license)

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
public class LoggingDecorator(IService inner) : IService
{
    public string Execute() => $"Log({inner.Execute()})";
}
```

## Basic Usage

### Simple Decorator Chain

```csharp
using System.Collections.Concurrent;

// Service interface
public interface IOrderService
{
    Task<Order> GetOrderAsync(int orderId);
}

// Logging decorator
public class LoggingDecorator(IOrderService inner) : IOrderService
{
    public async Task<Order> GetOrderAsync(int orderId)
    {
        Console.WriteLine($"Getting order {orderId}");
        return await inner.GetOrderAsync(orderId);
    }
}

// Cache decorator
public class CacheDecorator(IOrderService inner) : IOrderService
{
    private static readonly ConcurrentDictionary<int, Order> _cache = new();

    public async Task<Order> GetOrderAsync(int orderId)
    {
        if (_cache.TryGetValue(orderId, out var cachedOrder))
            return cachedOrder;

        var order = await inner.GetOrderAsync(orderId);
        _cache.TryAdd(orderId, order);
        return order;
    }
}

// Configuration
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

You can also conditionally skip an entire decoration chain using `ApplyIf`:

```csharp
// Only register the service if the feature is enabled
services.Decorate<IOrderService>()
        .With<LoggingDecorator>()
        .Then<CacheDecorator>()
        .Then<OrderService>()
        .ApplyIf(featureFlags.OrderServiceEnabled);
```

## Keyed Services

**Note**: Keyed services require .NET 8.0 or later. This feature is not available when targeting .NET 6.0 or 7.0.

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
public class ErrorHandlingDecorator(IService inner, ILogger<ErrorHandlingDecorator> logger) : IService
{
    public async Task<Result> ExecuteAsync(Request request)
    {
        try
        {
            return await inner.ExecuteAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing request {RequestId}", request.Id);
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

Decorator chains can be tested in isolation by pre-defining the chain outside the test (e.g. in a shared registration helper), then importing and applying it against a lightweight test service collection:

```csharp
public interface IOrderService
{
    string Process();
}

public class OrderService : IOrderService
{
    public string Process() => "OrderService";
}

public class CachingDecorator(IOrderService inner) : IOrderService
{
    public string Process() => $"Caching({inner.Process()})";
}

public class LoggingDecorator(IOrderService inner) : IOrderService
{
    public string Process() => $"Logging({inner.Process()})";
}

// Shared registration helper — defined once, reused across tests and production setup
public static class OrderServiceRegistration
{
    public static IDecorationBuilder<IOrderService> GetChain(IServiceCollection services) =>
        services.Decorate<IOrderService>()
                .With<LoggingDecorator>()
                .Then<CachingDecorator>()
                .Then<OrderService>();
}

// Test
[Test]
public void DecoratorChain_CanBeTestedInIsolation()
{
    // Arrange - import the pre-defined chain and apply it to a test service collection
    var services = new ServiceCollection();
    OrderServiceRegistration.GetChain(services).Apply();

    var provider = services.BuildServiceProvider();

    // Act
    var result = provider.GetRequiredService<IOrderService>().Process();

    // Assert - verify the full chain executes in the correct order
    Assert.That(result, Is.EqualTo("Logging(Caching(OrderService))"));
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
- `ApplyIf(bool condition)` - Conditionally apply the entire decoration chain

## Requirements

- .NET 6.0 or later
- Microsoft.Extensions.DependencyInjection 6.0.0 or later

**Note**: Keyed services are only available in .NET 8.0 or later. If you're using .NET 6.0 or 7.0, you can only use the regular (non-keyed) decoration features.

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests on the GitHub repository.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
