using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DecoratR.Example;

public class ExampleUsage
{
    public void ConfigureServices(IServiceCollection services, IHostEnvironment env)
    {
        // Example of conditional decoration based on environment
        services.Decorate<IService>()
                .With<LoggingDecorator>()
                .ThenIf<RetryDecorator>(env.IsDevelopment()) // Only add retry in development
                .Then<BaseService>()
                .Apply();

        // Example with multiple conditions
        bool isLoggingEnabled = true;
        bool isProduction = env.IsProduction();

        services.Decorate<IService>()
                .WithIf<LoggingDecorator>(isLoggingEnabled)
                .ThenIf<RetryDecorator>(!isProduction) // Retry only in non-production
                .Then<BaseService>()
                .Apply();
    }
}
