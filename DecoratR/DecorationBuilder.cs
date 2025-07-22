using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DecoratR;

/// <summary>
/// Internal implementation of <see cref="IDecorationBuilder{TService}"/> that builds decorator chains.
/// </summary>
/// <typeparam name="TService">The service type to decorate.</typeparam>
internal sealed class DecorationBuilder<TService> : IDecorationBuilder<TService> 
    where TService : class
{
    private readonly IServiceCollection _services;
    private readonly object? _serviceKey;
    private readonly List<DecoratorDescriptor> _decorators = new();
    private ServiceLifetime _lifetime = ServiceLifetime.Transient;

    // Single constructor with optional serviceKey parameter
    public DecorationBuilder(IServiceCollection services, object? serviceKey = null)
    {
        _services = services;
        _serviceKey = serviceKey;
    }

    public IDecorationBuilder<TService> With<TDecorator>()
        where TDecorator : class, TService => Then<TDecorator>();

    public IDecorationBuilder<TService> Then<TDecorator>()
        where TDecorator : class, TService
    {
        _decorators.Add(new DecoratorDescriptor(typeof(TDecorator)));
        return this;
    }
    
    public IDecorationBuilder<TService> With(Func<IServiceProvider, TService, TService> factory)
        => Then(factory);

    public IDecorationBuilder<TService> Then(Func<IServiceProvider, TService, TService> factory)
    {
        _decorators.Add(new DecoratorDescriptor(factory));
        return this;
    }

    public IDecorationBuilder<TService> WithIf<TDecorator>(bool condition)
        where TDecorator : class, TService => ThenIf<TDecorator>(condition);
    
    public IDecorationBuilder<TService> ThenIf<TDecorator>(bool condition)
        where TDecorator : class, TService
    {
        if (condition)
        {
            _decorators.Add(new DecoratorDescriptor(typeof(TDecorator)));
        }
        return this;
    }
    
    public IDecorationBuilder<TService> WithIf(bool condition, Func<IServiceProvider, TService, TService> factory)
        => ThenIf(condition, factory);
    
    public IDecorationBuilder<TService> ThenIf(bool condition, Func<IServiceProvider, TService, TService> factory)
    {
        if (condition)
        {
            _decorators.Add(new DecoratorDescriptor(factory));
        }
        return this;
    }

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
                $"At least one decorator (the base implementation) must be provided for service type {typeof(TService).Name}" +
                (_serviceKey != null ? $" with key '{_serviceKey}'" : "") + ".");

        ValidateDecoratorChain();

        var baseImplementation = _decorators.Last();
        var wrapperDecorators = _decorators.Take(_decorators.Count - 1).ToList();

        if (_serviceKey != null)
        {
            // Handle keyed services
            RemoveExistingKeyedService();
            RegisterKeyedService(wrapperDecorators, baseImplementation);
        }
        else
        {
            // Handle regular services
            _services.RemoveAll(typeof(TService));
            RegisterRegularService(wrapperDecorators, baseImplementation);
        }
    }

    private void RemoveExistingKeyedService()
    {
        var existingDescriptors = _services
            .Where(descriptor => 
                descriptor.ServiceType == typeof(TService) && 
                descriptor.ServiceKey?.Equals(_serviceKey) == true)
            .ToList();
        
        foreach (var descriptor in existingDescriptors)
        {
            _services.Remove(descriptor);
        }
    }

    private void RegisterKeyedService(List<DecoratorDescriptor> wrapperDecorators, DecoratorDescriptor baseImplementation)
    {
        _services.Add(new ServiceDescriptor(
            typeof(TService),
            _serviceKey,
            (serviceProvider, _) => BuildDecoratorChain(serviceProvider, wrapperDecorators, baseImplementation),
            _lifetime
        ));
    }

    private void RegisterRegularService(List<DecoratorDescriptor> wrapperDecorators, DecoratorDescriptor baseImplementation)
    {
        _services.Add(new ServiceDescriptor(
            typeof(TService),
            serviceProvider => BuildDecoratorChain(serviceProvider, wrapperDecorators, baseImplementation),
            _lifetime
        ));
    }

    /// <summary>
    /// Validates that all type-based decorators have the required constructor pattern.
    /// </summary>
    private void ValidateDecoratorChain()
    {
        foreach (var decorator in _decorators.Take(_decorators.Count - 1))
        {
            if (decorator.IsTypeDescriptor && !HasValidDecoratorConstructor(decorator.DecoratorType!))
            {
                throw new InvalidOperationException(
                    $"Decorator {decorator.DecoratorType!.Name} must have a constructor that accepts {typeof(TService).Name} as its first parameter.");
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
        IServiceProvider provider, List<DecoratorDescriptor> decorators, DecoratorDescriptor baseImplementation)
    {
        try
        {
            object current = baseImplementation.IsTypeDescriptor
                ? ActivatorUtilities.GetServiceOrCreateInstance(provider, baseImplementation.DecoratorType!)
                : baseImplementation.Factory!(provider, default!); // Base implementation factory won't use inner service

            foreach (var decorator in Enumerable.Reverse(decorators))
            {
                current = decorator.IsTypeDescriptor
                    ? ActivatorUtilities.CreateInstance(provider, decorator.DecoratorType!, current)
                    : decorator.Factory!(provider, (TService)current);
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

    /// <summary>
    /// Represents a decorator that can be either a type or a factory function.
    /// </summary>
    private sealed class DecoratorDescriptor
    {
        public Type? DecoratorType { get; }
        public Func<IServiceProvider, TService, TService>? Factory { get; }
        public bool IsTypeDescriptor => DecoratorType != null;

        public DecoratorDescriptor(Type decoratorType)
        {
            DecoratorType = decoratorType ?? throw new ArgumentNullException(nameof(decoratorType));
        }

        public DecoratorDescriptor(Func<IServiceProvider, TService, TService> factory)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
    }
}