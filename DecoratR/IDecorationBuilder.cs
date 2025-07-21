using Microsoft.Extensions.DependencyInjection;

namespace DecoratR;

/// <summary>
/// Provides a fluent interface for configuring service decorators.
/// </summary>
/// <typeparam name="TService">The service type to decorate.</typeparam>
public interface IDecorationBuilder<TService>
    where TService : class
{
    /// <summary>
    /// Adds a decorator to the decoration chain. This is an alias for <see cref="Then{TDecorator}"/>.
    /// </summary>
    /// <typeparam name="TDecorator">The decorator type that implements <typeparamref name="TService"/>.</typeparam>
    /// <returns>The decoration builder for method chaining.</returns>
    IDecorationBuilder<TService> With<TDecorator>()
        where TDecorator : class, TService;

    /// <summary>
    /// Adds a decorator to the decoration chain. The last decorator added should be the base implementation.
    /// </summary>
    /// <typeparam name="TDecorator">The decorator type that implements <typeparamref name="TService"/>.</typeparam>
    /// <returns>The decoration builder for method chaining.</returns>
    IDecorationBuilder<TService> Then<TDecorator>()
        where TDecorator : class, TService;

    /// <summary>
    /// Conditionally adds a decorator to the decoration chain based on the provided condition.
    /// </summary>
    /// <typeparam name="TDecorator">The decorator type that implements <typeparamref name="TService"/>.</typeparam>
    /// <param name="condition">The condition that determines whether to add the decorator.</param>
    /// <returns>The decoration builder for method chaining.</returns>
    IDecorationBuilder<TService> ThenIf<TDecorator>(bool condition)
        where TDecorator : class, TService;

    /// <summary>
    /// Conditionally adds a decorator to the decoration chain based on the provided condition. This is an alias for <see cref="ThenIf{TDecorator}"/>.
    /// </summary>
    /// <typeparam name="TDecorator">The decorator type that implements <typeparamref name="TService"/>.</typeparam>
    /// <param name="condition">The condition that determines whether to add the decorator.</param>
    /// <returns>The decoration builder for method chaining.</returns>
    IDecorationBuilder<TService> WithIf<TDecorator>(bool condition)
        where TDecorator : class, TService;

    /// <summary>
    /// Sets the service lifetime for the decorated service.
    /// </summary>
    /// <param name="lifetime">The service lifetime to use.</param>
    /// <returns>The decoration builder for method chaining.</returns>
    IDecorationBuilder<TService> WithLifetime(ServiceLifetime lifetime);

    /// <summary>
    /// Sets the service lifetime to Singleton.
    /// </summary>
    /// <returns>The decoration builder for method chaining.</returns>
    IDecorationBuilder<TService> AsSingleton();

    /// <summary>
    /// Sets the service lifetime to Scoped.
    /// </summary>
    /// <returns>The decoration builder for method chaining.</returns>
    IDecorationBuilder<TService> AsScoped();

    /// <summary>
    /// Sets the service lifetime to Transient (default).
    /// </summary>
    /// <returns>The decoration builder for method chaining.</returns>
    IDecorationBuilder<TService> AsTransient();

    /// <summary>
    /// Applies the decorator configuration to the service collection.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no decorators have been configured.</exception>
    void Apply();
}