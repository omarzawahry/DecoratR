using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Tests;

[TestFixture]
public class KeyedDecoratorTests
{
    [Test]
    public void KeyedDecorators_WithDifferentKeys_CreateSeparateChains()
    {
        var services = new ServiceCollection();

        // Configure "internal" keyed service
        services.Decorate<IService>("internal")
                .Then<LoggingDecorator>()
                .Then<BaseService>()
                .Apply();

        // Configure "external" keyed service
        services.Decorate<IService>("external")
                .Then<RetryDecorator>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();

        var internalService = provider.GetRequiredKeyedService<IService>("internal");
        var externalService = provider.GetRequiredKeyedService<IService>("external");

        var internalResult = internalService.Execute();
        var externalResult = externalService.Execute();

        Assert.That(internalResult, Is.EqualTo("Log(Base)"));
        Assert.That(externalResult, Is.EqualTo("Retry(Base)"));
    }

    [Test]
    public void KeyedDecorators_CanHaveMultipleDecoratorsPerChain()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>("complex")
                .Then<LoggingDecorator>()
                .Then<RetryDecorator>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredKeyedService<IService>("complex");

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Log(Retry(Base))"));
    }

    [Test]
    public void KeyedDecorators_SupportConditionalDecorators()
    {
        var services = new ServiceCollection();
        bool enableRetry = true;
        bool enableLogging = false;

        services.Decorate<IService>("conditional")
                .ThenIf<LoggingDecorator>(enableLogging)  // Skip
                .ThenIf<RetryDecorator>(enableRetry)      // Add
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredKeyedService<IService>("conditional");

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Retry(Base)"));
    }

    [Test]
    public void KeyedDecorators_SupportLifetimeConfiguration()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>("singleton")
                .Then<LoggingDecorator>()
                .Then<BaseService>()
                .AsSingleton()
                .Apply();

        var provider = services.BuildServiceProvider();

        var instance1 = provider.GetRequiredKeyedService<IService>("singleton");
        var instance2 = provider.GetRequiredKeyedService<IService>("singleton");

        Assert.That(instance1, Is.SameAs(instance2));
    }

    [Test]
    public void KeyedDecorators_DoNotInterfereWithRegularServices()
    {
        var services = new ServiceCollection();

        // Register regular service
        services.Decorate<IService>()
                .Then<RetryDecorator>()
                .Then<BaseService>()
                .Apply();

        // Register keyed service
        services.Decorate<IService>("keyed")
                .Then<LoggingDecorator>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();

        var regularService = provider.GetRequiredService<IService>();
        var keyedService = provider.GetRequiredKeyedService<IService>("keyed");

        var regularResult = regularService.Execute();
        var keyedResult = keyedService.Execute();

        Assert.That(regularResult, Is.EqualTo("Retry(Base)"));
        Assert.That(keyedResult, Is.EqualTo("Log(Base)"));
    }

    [Test]
    public void KeyedDecorators_ThrowsWhenNoImplementationProvided()
    {
        var services = new ServiceCollection();
        var builder = services.Decorate<IService>("empty");

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Apply());
        Assert.That(ex.Message, Contains.Substring("with key 'empty'"));
    }

    [Test]
    public void KeyedDecorators_CanBeOverridden()
    {
        var services = new ServiceCollection();

        // First registration
        services.Decorate<IService>("override")
                .Then<LoggingDecorator>()
                .Then<BaseService>()
                .Apply();

        // Override with new configuration
        services.Decorate<IService>("override")
                .Then<RetryDecorator>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredKeyedService<IService>("override");

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Retry(Base)"));
    }

    [Test]
    public void KeyedDecorators_SupportDifferentServiceLifetimes()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>("transient")
                .Then<LoggingDecorator>()
                .Then<BaseService>()
                .AsTransient()
                .Apply();

        services.Decorate<IService>("scoped")
                .Then<RetryDecorator>()
                .Then<BaseService>()
                .AsScoped()
                .Apply();

        var provider = services.BuildServiceProvider();

        // Test transient
        var transient1 = provider.GetRequiredKeyedService<IService>("transient");
        var transient2 = provider.GetRequiredKeyedService<IService>("transient");
        Assert.That(transient1, Is.Not.SameAs(transient2));

        // Test scoped
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var scoped1a = scope1.ServiceProvider.GetRequiredKeyedService<IService>("scoped");
        var scoped1b = scope1.ServiceProvider.GetRequiredKeyedService<IService>("scoped");
        var scoped2 = scope2.ServiceProvider.GetRequiredKeyedService<IService>("scoped");

        Assert.That(scoped1a, Is.SameAs(scoped1b)); // Same within scope
        Assert.That(scoped1a, Is.Not.SameAs(scoped2)); // Different across scopes
    }

    [Test]
    public void KeyedDecorators_WorkWithComplexKeys()
    {
        var services = new ServiceCollection();
        var complexKey = new { Type = "UserService", Environment = "Production" };

        services.Decorate<IService>(complexKey)
                .Then<LoggingDecorator>()
                .Then<BaseService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredKeyedService<IService>(complexKey);

        var result = service.Execute();

        Assert.That(result, Is.EqualTo("Log(Base)"));
    }
}
