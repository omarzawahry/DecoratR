using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DecoratR;

/// <summary>
/// Internal implementation of <see cref="IDecorationBuilder{TService}"/> that builds decorator chains.
/// </summary>
/// <typeparam name="TService">The service type to decorate.</typeparam>
internal sealed class DecorationBuilder<TService>(IServiceCollection services)
    : IDecorationBuilder<TService> where TService : class
{
    private readonly List<Type> _decorators = new();
    private ServiceLifetime _lifetime = ServiceLifetime.Transient;

    public IDecorationBuilder<TService> With<TDecorator>()
        where TDecorator : class, TService => Then<TDecorator>();

    public IDecorationBuilder<TService> Then<TDecorator>()
        where TDecorator : class, TService
    {
        _decorators.Add(typeof(TDecorator));
        return this;
    }

    public IDecorationBuilder<TService> ThenIf<TDecorator>(bool condition)
        where TDecorator : class, TService
    {
        if (condition)
        {
            _decorators.Add(typeof(TDecorator));
        }
        return this;
    }

    public IDecorationBuilder<TService> WithIf<TDecorator>(bool condition)
        where TDecorator : class, TService => ThenIf<TDecorator>(condition);

    public IDecorationBuilder<TService> WithLifetime(ServiceLifetime lifetime)
    {
        _lifetime = lifetime;
        return this;
    }

    public IDecorationBuilder<TService> AsSingleton() => WithLifetime(ServiceLifetime.Singleton);

    public IDecorationBuilder<TService> AsScoped() => WithLifetime(ServiceLifetime.Scoped);

    public IDecorationBuilder<TService> AsTransient() => WithLifetime(ServiceLifetime.Transient);

    public void Apply()
    {
        if (_decorators.Count == 0)
            throw new InvalidOperationException(
                $"At least one decorator (the base implementation) must be provided for service type {typeof(TService).Name}.");

        ValidateDecoratorChain();

        var baseImplementation = _decorators.Last();
        var wrapperDecorators = _decorators.Take(_decorators.Count - 1).ToList();

        services.RemoveAll(typeof(TService));

        services.Add(new ServiceDescriptor(
            typeof(TService),
            serviceProvider => BuildDecoratorChain(serviceProvider, 
                                                         wrapperDecorators,
                                                         baseImplementation),
            _lifetime
        ));
    }

    /// <summary>
    /// Validates that all decorators have the required constructor pattern.
    /// </summary>
    private void ValidateDecoratorChain()
    {
        foreach (var decorator in _decorators.Take(_decorators.Count - 1))
        {
            if (!HasValidDecoratorConstructor(decorator))
            {
                throw new InvalidOperationException(
                    $"Decorator {decorator.Name} must have a constructor that accepts {typeof(TService).Name} as its first parameter.");
            }
        }
    }

    /// <summary>
    /// Checks if a decorator type has a valid constructor for dependency injection.
    /// </summary>
    private static bool HasValidDecoratorConstructor(Type decoratorType)
    {
        var constructors = decoratorType.GetConstructors();
        return constructors.Any(ctor =>
        {
            var parameters = ctor.GetParameters();
            return parameters.Length > 0 && parameters[0].ParameterType == typeof(TService);
        });
    }

    private object BuildDecoratorChain(
        IServiceProvider provider, List<Type> decorators, Type baseImplementation)
    {
        try
        {
            object current = ActivatorUtilities.GetServiceOrCreateInstance(provider, baseImplementation);

            foreach (var decorator in Enumerable.Reverse(decorators))
            {
                current = ActivatorUtilities.CreateInstance(provider, decorator, current);
            }

            return current;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to build decorator chain for service type {typeof(TService).Name}. " +
                $"Ensure all decorators have appropriate constructors and dependencies are registered.", ex);
        }
    }
}