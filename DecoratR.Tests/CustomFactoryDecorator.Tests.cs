using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DecoratR.Tests;

[TestFixture]
public class CustomFactoryDecoratorTests
{
    [Test]
    public void Then_WithCustomFactory_CreatesDecoratorUsingFactory()
    {
        var services = new ServiceCollection();
        var mockMetrics = Substitute.For<IMetrics>();
        services.AddSingleton(mockMetrics);

        services.Decorate<IService>()
                .Then((sp, inner) => new CustomDecorator(inner, sp.GetRequiredService<IMetrics>()))
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Custom(Base)"));
        mockMetrics.Received(1).RecordExecution();
    }

    [Test]
    public void With_WithCustomFactory_CreatesDecoratorUsingFactory()
    {
        var services = new ServiceCollection();
        var mockLogger = Substitute.For<ILogger<IService>>();
        services.AddSingleton(mockLogger);

        services.Decorate<IService>()
                .With((sp, inner) => new LoggingDecoratorWithDependency(inner, sp.GetRequiredService<ILogger<IService>>()))
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("LogWithDep(Base)"));
    }

    [Test]
    public void ThenIf_WithCustomFactory_ConditionallyCreatesDecorator()
    {
        var services = new ServiceCollection();
        var mockMetrics = Substitute.For<IMetrics>();
        services.AddSingleton(mockMetrics);

        bool enableCustomDecorator = true;

        services.Decorate<IService>()
                .ThenIf(enableCustomDecorator, (sp, inner) => new CustomDecorator(inner, sp.GetRequiredService<IMetrics>()))
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Custom(Base)"));
        mockMetrics.Received(1).RecordExecution();
    }

    [Test]
    public void ThenIf_WithCustomFactory_SkipsWhenConditionFalse()
    {
        var services = new ServiceCollection();
        var mockMetrics = Substitute.For<IMetrics>();
        services.AddSingleton(mockMetrics);

        bool enableCustomDecorator = false;

        services.Decorate<IService>()
                .ThenIf(enableCustomDecorator, (sp, inner) => new CustomDecorator(inner, sp.GetRequiredService<IMetrics>()))
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Base"));
        mockMetrics.DidNotReceive().RecordExecution();
    }

    [Test]
    public void WithIf_WithCustomFactory_ConditionallyCreatesDecorator()
    {
        var services = new ServiceCollection();
        var mockLogger = Substitute.For<ILogger<IService>>();
        services.AddSingleton(mockLogger);

        bool enableLogging = true;

        services.Decorate<IService>()
                .WithIf(enableLogging, (sp, inner) => new LoggingDecoratorWithDependency(inner, sp.GetRequiredService<ILogger<IService>>()))
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("LogWithDep(Base)"));
    }

    [Test]
    public void CustomFactory_CanBeMixedWithTypeBasedDecorators()
    {
        var services = new ServiceCollection();
        var mockMetrics = Substitute.For<IMetrics>();
        services.AddSingleton(mockMetrics);

        services.Decorate<IService>()
                .Then<LoggingDecorator>()
                .Then((sp, inner) => new CustomDecorator(inner, sp.GetRequiredService<IMetrics>()))
                .Then<RetryDecorator>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Log(Custom(Retry(Base)))"));
        mockMetrics.Received(1).RecordExecution();
    }

    [Test]
    public void CustomFactory_WorksWithKeyedServices()
    {
        var services = new ServiceCollection();
        var mockMetrics = Substitute.For<IMetrics>();
        services.AddSingleton(mockMetrics);

        services.Decorate<IService>("custom")
                .Then((sp, inner) => new CustomDecorator(inner, sp.GetRequiredService<IMetrics>()))
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredKeyedService<IService>("custom");

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Custom(Base)"));
        mockMetrics.Received(1).RecordExecution();
    }

    [Test]
    public void CustomFactory_SupportsComplexDependencyInjection()
    {
        var services = new ServiceCollection();
        var mockConfig = Substitute.For<IConfiguration>();
        var mockLogger = Substitute.For<ILogger<IService>>();
        var mockMetrics = Substitute.For<IMetrics>();

        services.AddSingleton(mockConfig);
        services.AddSingleton(mockLogger);
        services.AddSingleton(mockMetrics);

        services.Decorate<IService>()
                .Then((sp, inner) => new ComplexCustomDecorator(
                    inner,
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<ILogger<IService>>(),
                    sp.GetRequiredService<IMetrics>()))
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Complex(Base)"));
    }

    [Test]
    public void CustomFactory_BaseImplementationCanAlsoBeFactory()
    {
        var services = new ServiceCollection();
        var mockConfig = Substitute.For<IConfiguration>();
        services.AddSingleton(mockConfig);

        services.Decorate<IService>()
                .Then<LoggingDecorator>()
                .Then((sp, _) => new CustomBaseService(sp.GetRequiredService<IConfiguration>()))
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Log(CustomBase)"));
    }

    [Test]
    public void CustomFactory_WorksWithLifetimeManagement()
    {
        var services = new ServiceCollection();
        var mockMetrics = Substitute.For<IMetrics>();
        services.AddSingleton(mockMetrics);

        services.Decorate<IService>()
                .Then((sp, inner) => new CustomDecorator(inner, sp.GetRequiredService<IMetrics>()))
                .Then<BaseService>()
                .AsSingleton()
                .Apply();

        var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredService<IService>();
        var instance2 = provider.GetRequiredService<IService>();

        Assert.That(instance1, Is.SameAs(instance2));
    }
}

// Test interfaces and implementations for factory tests
public interface IMetrics
{
    void RecordExecution();
}

public interface IConfiguration
{
    string GetValue(string key);
}

public class CustomDecorator : IService
{
    private readonly IService _inner;
    private readonly IMetrics _metrics;

    public CustomDecorator(IService inner, IMetrics metrics)
    {
        _inner = inner;
        _metrics = metrics;
    }

    public string Execute()
    {
        _metrics.RecordExecution();
        return $"Custom({_inner.Execute()})";
    }
}

public class LoggingDecoratorWithDependency : IService
{
    private readonly IService _inner;
    private readonly ILogger<IService> _logger;

    public LoggingDecoratorWithDependency(IService inner, ILogger<IService> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public string Execute()
    {
        return $"LogWithDep({_inner.Execute()})";
    }
}

public class ComplexCustomDecorator : IService
{
    private readonly IService _inner;
    private readonly IConfiguration _config;
    private readonly ILogger<IService> _logger;
    private readonly IMetrics _metrics;

    public ComplexCustomDecorator(IService inner, IConfiguration config, ILogger<IService> logger, IMetrics metrics)
    {
        _inner = inner;
        _config = config;
        _logger = logger;
        _metrics = metrics;
    }

    public string Execute()
    {
        return $"Complex({_inner.Execute()})";
    }
}

public class CustomBaseService : IService
{
    private readonly IConfiguration _config;

    public CustomBaseService(IConfiguration config)
    {
        _config = config;
    }

    public string Execute() => "CustomBase";
}
