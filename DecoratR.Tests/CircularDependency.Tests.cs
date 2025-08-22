using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DecoratR.Tests;

[TestFixture]
public class CircularDependencyTests
{
    [Test]
    public void Apply_ThrowsException_WhenSameDecoratorTypeAppearsMultipleTimes()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.Decorate<IService>()
                    .With<LoggingDecorator>()     // First occurrence
                    .Then<RetryDecorator>()
                    .Then<LoggingDecorator>()     // Duplicate - should throw
                    .Then<BaseService>()
                    .Apply();
        });

        Assert.That(ex.Message, Does.Contain("Circular dependency detected"));
        Assert.That(ex.Message, Does.Contain("LoggingDecorator"));
        Assert.That(ex.Message, Does.Contain("appear multiple times"));
    }

    [Test]
    public void Apply_ThrowsException_WhenDecoratorHasCircularConstructorDependency()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.Decorate<IService>()
                    .With<CircularDependencyDecorator>()
                    .Then<BaseService>()
                    .Apply();
        });

        Assert.That(ex.Message, Does.Contain("Circular dependency detected"));
        Assert.That(ex.Message, Does.Contain("CircularDependencyDecorator"));
    }

    [Test]
    public void Apply_ThrowsException_WhenDecoratorHasGenericCircularDependency()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.Decorate<IService>()
                    .With<GenericCircularDependencyDecorator>()
                    .Then<BaseService>()
                    .Apply();
        });

        Assert.That(ex.Message, Does.Contain("Potential circular dependency detected"));
        Assert.That(ex.Message, Does.Contain("generic argument"));
    }

    [Test]
    public void Apply_DoesNotThrow_WhenDecoratorChainIsValid()
    {
        var services = new ServiceCollection();

        Assert.DoesNotThrow(() =>
        {
            services.Decorate<IService>()
                    .With<LoggingDecorator>()
                    .Then<RetryDecorator>()
                    .Then<BaseService>()
                    .Apply();
        });
    }

    [Test]
    public void Apply_AllowsFactoryDecorators_EvenWithDuplicateTypes()
    {
        var services = new ServiceCollection();

        // Factory decorators should be allowed even if they create similar functionality
        // because they might have different behavior based on runtime conditions
        Assert.DoesNotThrow(() =>
        {
            services.Decorate<IService>()
                    .With((sp, inner) => new LoggingDecorator(inner))
                    .Then((sp, inner) => new LoggingDecorator(inner)) // Different factory instance
                    .Then<BaseService>()
                    .Apply();
        });
    }
}

// Test decorators for circular dependency testing
public class CircularDependencyDecorator : IService
{
    private readonly IService _inner;
    private readonly IService _circularDep; // This creates a circular dependency!

    public CircularDependencyDecorator(IService inner, IService circularDep)
    {
        _inner = inner;
        _circularDep = circularDep;
    }

    public string Execute() => $"Circular({_inner.Execute()})";
}

public class GenericCircularDependencyDecorator : IService
{
    private readonly IService _inner;
    private readonly ILogger<IService> _logger; // Generic type containing IService

    public GenericCircularDependencyDecorator(IService inner, ILogger<IService> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public string Execute() => $"GenericCircular({_inner.Execute()})";
}

// Valid decorator for comparison
public class ValidDecoratorWithDependencies : IService
{
    private readonly IService _inner;
    private readonly ILogger<ValidDecoratorWithDependencies> _logger;

    public ValidDecoratorWithDependencies(IService inner, ILogger<ValidDecoratorWithDependencies> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public string Execute()
    {
        _logger.LogInformation("Executing ValidDecorator");
        return $"Valid({_inner.Execute()})";
    }
}
