using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DecoratR.Tests;

[TestFixture]
public class GenericDecoratorTests
{
    [Test]
    public void GenericDecorator_WorksWithSpecificServiceType()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<GenericDecorator<string>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Generic<String>(Base)"));
    }

    [Test]
    public void GenericDecorator_CanBeChainedWithRegularDecorators()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<LoggingDecorator>()
                .Then<GenericDecorator<int>>()
                .Then<RetryDecorator>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Log(Generic<Int32>(Retry(Base)))"));
    }

    [Test]
    public void GenericDecorator_WorksWithKeyedServices()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>("generic")
                .Then<GenericDecorator<Double>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredKeyedService<IService>("generic");

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Generic<Double>(Base)"));
    }

    [Test]
    public void GenericDecorator_WorksWithConditionalDecorators()
    {
        var services = new ServiceCollection();
        bool enableGeneric = true;

        services.Decorate<IService>()
                .ThenIf<GenericDecorator<decimal>>(enableGeneric)
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Generic<Decimal>(Base)"));
    }

    [Test]
    public void GenericDecorator_SkippedWhenConditionFalse()
    {
        var services = new ServiceCollection();
        bool enableGeneric = false;

        services.Decorate<IService>()
                .ThenIf<GenericDecorator<float>>(enableGeneric)
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Base"));
    }

    [Test]
    public void GenericDecorator_WithDependencies_WorksCorrectly()
    {
        var services = new ServiceCollection();
        var mockLogger = Substitute.For<ILogger<GenericDecoratorWithDependencies<long>>>();
        services.AddSingleton(mockLogger);

        services.Decorate<IService>()
                .Then<GenericDecoratorWithDependencies<long>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("GenericWithDep<Int64>(Base)"));
    }

    [Test]
    public void GenericDecorator_WorksWithCustomFactory()
    {
        var services = new ServiceCollection();
        var mockConfig = Substitute.For<IConfiguration>();
        services.AddSingleton(mockConfig);

        services.Decorate<IService>()
                .Then((sp, inner) => new GenericFactoryDecorator<byte>(inner, sp.GetRequiredService<IConfiguration>()))
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("GenericFactory<Byte>(Base)"));
    }

    [Test]
    public void GenericDecorator_WorksWithLifetimeManagement()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<GenericDecorator<char>>()
                .Then<BaseService>()
                .AsSingleton()
                .Apply();

        var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredService<IService>();
        var instance2 = provider.GetRequiredService<IService>();

        Assert.That(instance1, Is.SameAs(instance2));
    }

    [Test]
    public void MultipleGenericDecorators_CanBeUsedTogether()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<GenericDecorator<short>>()
                .Then<AnotherGenericDecorator<uint>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Generic<Int16>(AnotherGeneric<UInt32>(Base))"));
    }

    [Test]
    public void UnconstrainedGenericDecorator_WorksWithAnyType()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<UnconstrainedGenericDecorator<DateTime>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Unconstrained<DateTime>(Base)"));
    }

    [Test]
    public void MultiGenericDecorator_WorksWithStringAndInt()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<MultiGenericDecorator<string, int>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Multi<String,Int32>(Base)"));
    }

    [Test]
    public void MultiGenericDecorator_WorksWithDifferentTypesCombinations()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>("complex-types")
                .Then<MultiGenericDecorator<List<string>, Dictionary<int, bool>>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredKeyedService<IService>("complex-types");

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Multi<List`1,Dictionary`2>(Base)"));
    }

    [Test]
    public void GenericDecorator_WorksWithValueTypes()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<UnconstrainedGenericDecorator<int>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Unconstrained<Int32>(Base)"));
    }

    [Test]
    public void GenericDecorator_WorksWithStructs()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<UnconstrainedGenericDecorator<Guid>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Unconstrained<Guid>(Base)"));
    }

    [Test]
    public void TripleGenericDecorator_WorksWithThreeTypes()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<TripleGenericDecorator<string, int, bool>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Triple<String,Int32,Boolean>(Base)"));
    }

    [Test]
    public void GenericDecorator_WorksWithPrimitiveTypes()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<GenericDecorator<string>>()  // T = string
                .Then<GenericDecorator<int>>()     // T = int  
                .Then<GenericDecorator<bool>>()    // T = bool
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Generic<String>(Generic<Int32>(Generic<Boolean>(Base)))"));
    }

    [Test]
    public void GenericDecorator_WorksWithComplexNonServiceTypes()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .Then<GenericDecorator<List<Dictionary<string, int>>>>()  // Complex generic type
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Generic<List`1>(Base)"));
    }

    [Test]
    public void MultiGenericDecorator_DemonstratesStringAndIntAsRequested()
    {
        var services = new ServiceCollection();

        // This proves T1=string and T2=int work as you requested
        services.Decorate<IService>()
                .Then<MultiGenericDecorator<string, int>>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Multi<String,Int32>(Base)"));
    }
}

// Generic decorator implementations for testing
public class GenericDecorator<T> : IService
{
    private readonly IService _inner;
    private readonly Type _genericType;

    public GenericDecorator(IService inner)
    {
        _inner = inner;
        _genericType = typeof(T);
    }

    public string Execute()
    {
        return $"Generic<{_genericType.Name}>({_inner.Execute()})";
    }
}

public class AnotherGenericDecorator<T> : IService
{
    private readonly IService _inner;
    private readonly Type _genericType;

    public AnotherGenericDecorator(IService inner)
    {
        _inner = inner;
        _genericType = typeof(T);
    }

    public string Execute()
    {
        return $"AnotherGeneric<{_genericType.Name}>({_inner.Execute()})";
    }
}

public class GenericDecoratorWithDependencies<T> : IService
{
    private readonly IService _inner;
    private readonly ILogger<GenericDecoratorWithDependencies<T>> _logger;
    private readonly Type _genericType;

    public GenericDecoratorWithDependencies(IService inner, ILogger<GenericDecoratorWithDependencies<T>> logger)
    {
        _inner = inner;
        _logger = logger;
        _genericType = typeof(T);
    }

    public string Execute()
    {
        _logger.LogInformation("Executing generic decorator for {ServiceType}", _genericType.Name);
        return $"GenericWithDep<{_genericType.Name}>({_inner.Execute()})";
    }
}

public class GenericFactoryDecorator<T> : IService
{
    private readonly IService _inner;
    private readonly IConfiguration _config;
    private readonly Type _genericType;

    public GenericFactoryDecorator(IService inner, IConfiguration config)
    {
        _inner = inner;
        _config = config;
        _genericType = typeof(T);
    }

    public string Execute()
    {
        return $"GenericFactory<{_genericType.Name}>({_inner.Execute()})";
    }
}

// Unconstrained generic decorators - T can be ANY type
public class UnconstrainedGenericDecorator<T> : IService
{
    private readonly IService _inner;
    private readonly Type _genericType;

    public UnconstrainedGenericDecorator(IService inner)
    {
        _inner = inner;
        _genericType = typeof(T);
    }

    public string Execute()
    {
        return $"Unconstrained<{_genericType.Name}>({_inner.Execute()})";
    }
}

// Multi-generic decorator where T1 and T2 can be ANY types (like string and int)
public class MultiGenericDecorator<T1, T2> : IService
{
    private readonly IService _inner;
    private readonly Type _type1;
    private readonly Type _type2;

    public MultiGenericDecorator(IService inner)
    {
        _inner = inner;
        _type1 = typeof(T1);
        _type2 = typeof(T2);
    }

    public string Execute()
    {
        return $"Multi<{_type1.Name},{_type2.Name}>({_inner.Execute()})";
    }
}

// Triple generic decorator with three unconstrained type parameters
public class TripleGenericDecorator<T1, T2, T3> : IService
{
    private readonly IService _inner;
    private readonly Type _type1;
    private readonly Type _type2;
    private readonly Type _type3;

    public TripleGenericDecorator(IService inner)
    {
        _inner = inner;
        _type1 = typeof(T1);
        _type2 = typeof(T2);
        _type3 = typeof(T3);
    }

    public string Execute()
    {
        return $"Triple<{_type1.Name},{_type2.Name},{_type3.Name}>({_inner.Execute()})";
    }
}
