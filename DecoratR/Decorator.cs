using Microsoft.Extensions.DependencyInjection;

namespace DecoratR;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to support service decoration.
/// </summary>
public static class DecoratorServiceCollectionExtensions
{
    /// <summary>
    /// Begins decoration configuration for the specified service type.
    /// </summary>
    /// <typeparam name="TService">The service type to decorate.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>A decoration builder for configuring decorators.</returns>
    public static IDecorationBuilder<TService> Decorate<TService>(
        this IServiceCollection services) where TService : class =>
        new DecorationBuilder<TService>(services);

    /// <summary>
    /// Begins decoration configuration for the specified keyed service type.
    /// </summary>
    /// <typeparam name="TService">The service type to decorate.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="serviceKey">The key to identify the service registration.</param>
    /// <returns>A decoration builder for configuring decorators.</returns>
    public static IDecorationBuilder<TService> Decorate<TService>(
        this IServiceCollection services, object serviceKey) where TService : class =>
        new DecorationBuilder<TService>(services, serviceKey);
}