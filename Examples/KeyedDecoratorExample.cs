using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DecoratR.Examples;

public class KeyedDecoratorExample
{
    public void ConfigureServices(IServiceCollection services, IHostEnvironment env)
    {
        // Example 1: Basic keyed decorators as requested
        services.Decorate<IUserService>("internal")
                .Then<LoggingDecorator>()
                .Then<UserService>()
                .Apply();

        services.Decorate<IUserService>("external")
                .Then<AuditDecorator>()
                .Then<UserService>()
                .Apply();

        // Example 2: Keyed decorators with conditions
        services.Decorate<IUserService>("development")
                .Then<LoggingDecorator>()
                .ThenIf<RetryDecorator>(env.IsDevelopment())
                .Then<UserService>()
                .Apply();

        // Example 3: Keyed decorators with different lifetimes
        services.Decorate<IUserService>("cached")
                .Then<CacheDecorator>()
                .Then<UserService>()
                .AsSingleton()
                .Apply();

        // Example 4: Complex keys (using anonymous objects)
        var productionKey = new { Environment = "Production", Service = "UserService" };
        services.Decorate<IUserService>(productionKey)
                .Then<SecurityDecorator>()
                .Then<AuditDecorator>()
                .Then<UserService>()
                .Apply();
    }

    public void Usage(IServiceProvider serviceProvider)
    {
        // Retrieve keyed services
        var internalUserService = serviceProvider.GetRequiredKeyedService<IUserService>("internal");
        var externalUserService = serviceProvider.GetRequiredKeyedService<IUserService>("external");
        
        // Use the services
        var internalResult = internalUserService.GetUser("123");
        var externalResult = externalUserService.GetUser("456");
        
        // Results will have different decorator chains applied:
        // internalResult: processed with LoggingDecorator
        // externalResult: processed with AuditDecorator
    }
}

// Sample interfaces and implementations for the example
public interface IUserService
{
    string GetUser(string id);
}

public class UserService : IUserService
{
    public string GetUser(string id) => $"User({id})";
}

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

public class RetryDecorator : IUserService
{
    private readonly IUserService _inner;
    
    public RetryDecorator(IUserService inner) => _inner = inner;
    
    public string GetUser(string id) => $"Retry({_inner.GetUser(id)})";
}

public class CacheDecorator : IUserService
{
    private readonly IUserService _inner;
    
    public CacheDecorator(IUserService inner) => _inner = inner;
    
    public string GetUser(string id) => $"Cache({_inner.GetUser(id)})";
}

public class SecurityDecorator : IUserService
{
    private readonly IUserService _inner;
    
    public SecurityDecorator(IUserService inner) => _inner = inner;
    
    public string GetUser(string id) => $"Security({_inner.GetUser(id)})";
}
