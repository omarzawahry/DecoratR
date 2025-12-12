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
#if NET8_0_OR_GREATER
    private readonly object? _serviceKey;
#endif
    private readonly List<DecoratorDescriptor> _decorators = new();
    private ServiceLifetime _lifetime = ServiceLifetime.Transient;

    // Constructor for regular services
    public DecorationBuilder(IServiceCollection services)
    {
        _services = services;
    }

#if NET8_0_OR_GREATER
    // Constructor for keyed services
    public DecorationBuilder(IServiceCollection services, object? serviceKey)
    {
        _services = services;
        _serviceKey = serviceKey;
    }
#endif

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
#if NET8_0_OR_GREATER
                (_serviceKey != null ? $" with key '{_serviceKey}'" : "") + 
#endif
                ".");

        ValidateDecoratorChain();

        var baseImplementation = _decorators.Last();
        var wrapperDecorators = _decorators.Take(_decorators.Count - 1).ToList();

#if NET8_0_OR_GREATER
        if (_serviceKey != null)
        {
            // Handle keyed services
            RemoveExistingKeyedService();
            RegisterKeyedService(wrapperDecorators, baseImplementation);
        }
        else
#endif
        {
            // Handle regular services
            _services.RemoveAll(typeof(TService));
            RegisterRegularService(wrapperDecorators, baseImplementation);
        }
    }

#if NET8_0_OR_GREATER
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
#endif

    private void RegisterRegularService(List<DecoratorDescriptor> wrapperDecorators, DecoratorDescriptor baseImplementation)
    {
        _services.Add(new ServiceDescriptor(
            typeof(TService),
            serviceProvider => BuildDecoratorChain(serviceProvider, wrapperDecorators, baseImplementation),
            _lifetime
        ));
    }

    /// <summary>
    /// Validates that all type-based decorators have the required constructor pattern
    /// and checks for potential circular dependencies.
    /// </summary>
    private void ValidateDecoratorChain()
    {
        // Check for duplicate decorator types (direct circular dependency)
        var decoratorTypes = _decorators
            .Where(d => d.IsTypeDescriptor)
            .Select(d => d.DecoratorType!)
            .ToList();

        var duplicateTypes = decoratorTypes
            .GroupBy(t => t)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateTypes.Any())
        {
            throw new InvalidOperationException(
                $"Circular dependency detected: The following decorator types appear multiple times in the chain: {string.Join(", ", duplicateTypes.Select(t => t.Name))}. " +
                "Each decorator type should only appear once in a decoration chain.");
        }

        // Validate constructor patterns and check for potential DI circular dependencies
        foreach (var decorator in _decorators.Take(_decorators.Count - 1))
        {
            if (decorator.IsTypeDescriptor)
            {
                if (!HasValidDecoratorConstructor(decorator.DecoratorType!))
                {
                    throw new InvalidOperationException(
                        $"Decorator {decorator.DecoratorType!.Name} must have a constructor that accepts {typeof(TService).Name} as its first parameter.");
                }

                // Check for potential circular DI dependencies
                ValidateDecoratorDependencies(decorator.DecoratorType!);
            }
        }
    }

    /// <summary>
    /// Validates that a decorator doesn't have constructor dependencies that could create circular references.
    /// </summary>
    private void ValidateDecoratorDependencies(Type decoratorType)
    {
        var constructors = decoratorType.GetConstructors();
        var primaryConstructor = constructors
            .Where(ctor => ctor.GetParameters().Length > 0 && ctor.GetParameters()[0].ParameterType == typeof(TService))
            .OrderByDescending(ctor => ctor.GetParameters().Length)
            .FirstOrDefault();

        if (primaryConstructor == null) return;

        var parameters = primaryConstructor.GetParameters().Skip(1); // Skip the first parameter (the wrapped service)
        
        foreach (var parameter in parameters)
        {
            // Check if any parameter type is the same as the service type we're decorating
            if (parameter.ParameterType == typeof(TService))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected: Decorator {decoratorType.Name} has a constructor parameter of type {typeof(TService).Name} " +
                    "in addition to the required first parameter. This creates a circular dependency.");
            }

            // Check for generic types that might contain the service type
            if (parameter.ParameterType.IsGenericType)
            {
                var genericArguments = parameter.ParameterType.GetGenericArguments();
                if (genericArguments.Contains(typeof(TService)))
                {
                    throw new InvalidOperationException(
                        $"Potential circular dependency detected: Decorator {decoratorType.Name} has a constructor parameter " +
                        $"of type {parameter.ParameterType.Name} that contains {typeof(TService).Name} as a generic argument. " +
                        "This may create a circular dependency.");
                }
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