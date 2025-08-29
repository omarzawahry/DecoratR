using Microsoft.Extensions.DependencyInjection;

namespace DecoratR.Tests;

[TestFixture]
public class NullReferenceProtectionTests
{
    [Test]
    public void Apply_ThrowsException_WhenFactoryBaseImplementationUsesInnerService()
    {
        var services = new ServiceCollection();

        services.Decorate<IService>()
                .With<LoggingDecorator>()
                .Then((sp, inner) => new ProblematicFactoryService(inner)) // This factory tries to use inner service
                .Apply();
        
        var provider = services.BuildServiceProvider();
        
        var ex = Assert.Throws<InvalidOperationException>(() => 
            provider.GetRequiredService<IService>()); // This should trigger the exception

        Assert.That(ex.Message, Does.Contain("should not use the inner service parameter"));
        Assert.That(ex.Message, Does.Contain("Base implementations are the final service in the decorator chain"));
    }


    [Test]
    public void Apply_ThrowsException_WhenTypeBaseImplementationHasInnerServiceConstructor()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.Decorate<IService>()
                    .With<LoggingDecorator>()
                    .Then<ProblematicBaseService>() // This service has constructor that expects IService
                    .Apply();
        });

        Assert.That(ex.Message, Does.Contain("should not have a constructor parameter of type IService"));
        Assert.That(ex.Message, Does.Contain("Base implementations are the final service in the decorator chain"));
    }

    [Test]
    public void Apply_ThrowsException_WhenTypeBaseImplementationHasInnerServiceInSecondParameter()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.Decorate<IService>()
                    .With<LoggingDecorator>()
                    .Then<ProblematicBaseServiceSecondParam>() // This service has IService as second parameter
                    .Apply();
        });

        Assert.That(ex.Message, Does.Contain("should not have a constructor parameter of type IService"));
        Assert.That(ex.Message, Does.Contain("found at position 2"));
        Assert.That(ex.Message, Does.Contain("Base implementations are the final service in the decorator chain"));
    }

    [Test]
    public void Apply_SucceedsWithProperFactoryBaseImplementation()
    {
        var services = new ServiceCollection();

        Assert.DoesNotThrow(() =>
        {
            services.Decorate<IService>()
                    .With<LoggingDecorator>()
                    .Then((sp, inner) => new BaseService()) // Proper factory that ignores inner service
                    .Apply();
            
            var provider = services.BuildServiceProvider();
            var service = provider.GetRequiredService<IService>();
            
            var result = service.Execute();
            Assert.That(result, Is.EqualTo("Log(Base)"));
        });
    }
}

// Test services for null reference protection

public class ProblematicFactoryService(IService inner) : IService
{
    public string Execute() => $"Problematic({inner.Execute()})"; // NullReferenceException here
}

public class ArgumentValidatingFactoryService : IService
{
    public ArgumentValidatingFactoryService(IService inner)
    {
        ArgumentNullException.ThrowIfNull(inner, nameof(inner));
    }

    public string Execute() => "ValidatingFactory";
}

public class ProblematicBaseService(IService inner) : IService
{
    public string Execute() => $"ProblematicBase({inner.Execute()})";
}

public class ProblematicBaseServiceSecondParam(string someParam, IService inner) : IService
{
    public string Execute() => $"ProblematicBase({someParam}: {inner.Execute()})";
}
