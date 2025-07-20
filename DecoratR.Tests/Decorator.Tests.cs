using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DecoratR.Tests;

[TestFixture]
public class DecoratorTests
{
    [Test]
    public void BuildDecoratorChain_OrdersCorrectly()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .With<LoggingDecorator>()
                .Then<RetryDecorator>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IService>();

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Log(Retry(Base))"));
    }

    [Test]
    public void Service_IsSingleton()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .With<LoggingDecorator>()
                .Then<BaseService>()
                .WithLifetime(ServiceLifetime.Singleton)
                .Apply();

        var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredService<IService>();
        var instance2 = provider.GetRequiredService<IService>();

        Assert.That(instance1, Is.SameAs(instance2));
    }

    [Test]
    public void Throws_WhenNoImplementationProvided()
    {
        var services = new ServiceCollection();
        var builder = services.Decorate<IService>();

        Assert.Throws<InvalidOperationException>(() =>
            builder.WithLifetime(ServiceLifetime.Transient).Apply());
    }

    [Test]
    public void Supports_TransientLifetime()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .With<LoggingDecorator>()
                .Then<BaseService>()
                .WithLifetime(ServiceLifetime.Transient)
                .Apply();

        var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredService<IService>();
        var instance2 = provider.GetRequiredService<IService>();

        Assert.That(instance1, Is.Not.SameAs(instance2));
    }
    
    [Test]
    public void Supports_TransientLifetime_ByDefault()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
            .With<LoggingDecorator>()
            .Then<BaseService>()
            .Apply();

        var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredService<IService>();
        var instance2 = provider.GetRequiredService<IService>();

        Assert.That(instance1, Is.Not.SameAs(instance2));
    }

    [Test]
    public void Decorators_Can_Have_Dependencies()
    {
        var services = new ServiceCollection();
        var logger = Substitute.For<ILogger<DependencyLoggingDecorator>>();
        services.AddSingleton(logger);

        services.Decorate<IService>()
                .With<DependencyLoggingDecorator>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var scopedService = scope.ServiceProvider.GetRequiredService<IService>();

        Assert.That(scopedService, Is.InstanceOf<DependencyLoggingDecorator>());
    }

    [Test]
    public void AsSingleton_SetsSingletonLifetime()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .With<LoggingDecorator>()
                .Then<BaseService>()
                .AsSingleton()
                .Apply();

        var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredService<IService>();
        var instance2 = provider.GetRequiredService<IService>();

        Assert.That(instance1, Is.SameAs(instance2));
    }

    [Test]
    public void AsScoped_SetsScopedLifetime()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .With<LoggingDecorator>()
                .Then<BaseService>()
                .AsScoped()
                .Apply();

        var provider = services.BuildServiceProvider();

        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var instance1a = scope1.ServiceProvider.GetRequiredService<IService>();
        var instance1b = scope1.ServiceProvider.GetRequiredService<IService>();
        var instance2 = scope2.ServiceProvider.GetRequiredService<IService>();

        Assert.That(instance1a, Is.SameAs(instance1b)); // Same within scope
        Assert.That(instance1a, Is.Not.SameAs(instance2)); // Different across scopes
    }

    [Test]
    public void AsTransient_SetsTransientLifetime()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .With<LoggingDecorator>()
                .Then<BaseService>()
                .AsTransient()
                .Apply();

        var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredService<IService>();
        var instance2 = provider.GetRequiredService<IService>();

        Assert.That(instance1, Is.Not.SameAs(instance2));
    }

    [Test]
    public void ValidateDecoratorChain_ThrowsWhenDecoratorHasInvalidConstructor()
    {
        var services = new ServiceCollection();

        var builder = services.Decorate<IService>()
                              .With<InvalidDecorator>()
                              .Then<BaseService>();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Apply());
        Assert.That(ex.Message, Contains.Substring("InvalidDecorator"));
        Assert.That(ex.Message, Contains.Substring("constructor"));
    }
}