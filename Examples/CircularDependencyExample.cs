using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DecoratR.Examples;

// Let's say we have these services
public interface IOrderService
{
    Task<Order> ProcessOrderAsync(int orderId);
}

public interface IAuditService
{
    Task LogOrderProcessingAsync(int orderId, string action);
}

public interface INotificationService
{
    Task SendOrderNotificationAsync(int orderId);
}

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public decimal Amount { get; set; }
}

// Base implementation
public class OrderService : IOrderService
{
    public async Task<Order> ProcessOrderAsync(int orderId)
    {
        // Process the order
        await Task.Delay(100); // Simulate processing
        return new Order { Id = orderId, CustomerName = "John Doe", Amount = 100.50m };
    }
}

// This decorator has a HIDDEN circular dependency
public class AuditDecorator : IOrderService
{
    private readonly IOrderService _inner;
    private readonly IAuditService _auditService; // This is where the problem starts!

    public AuditDecorator(IOrderService inner, IAuditService auditService)
    {
        _inner = inner;
        _auditService = auditService;
    }

    public async Task<Order> ProcessOrderAsync(int orderId)
    {
        await _auditService.LogOrderProcessingAsync(orderId, "Starting");
        var result = await _inner.ProcessOrderAsync(orderId);
        await _auditService.LogOrderProcessingAsync(orderId, "Completed");
        return result;
    }
}

// Here's the problematic audit service that creates the circular dependency
public class ProblematicAuditService : IAuditService
{
    private readonly IOrderService _orderService; // CIRCULAR DEPENDENCY!
    private readonly ILogger<ProblematicAuditService> _logger;

    public ProblematicAuditService(IOrderService orderService, ILogger<ProblematicAuditService> logger)
    {
        _orderService = orderService; // This depends back on IOrderService!
        _logger = logger;
    }

    public async Task LogOrderProcessingAsync(int orderId, string action)
    {
        // Maybe the audit service wants to get order details for logging
        var order = await _orderService.ProcessOrderAsync(orderId); // This creates infinite loop!
        _logger.LogInformation($"Order {order.Id} for {order.CustomerName}: {action}");
    }
}

// A better audit service without circular dependency
public class GoodAuditService : IAuditService
{
    private readonly ILogger<GoodAuditService> _logger;

    public GoodAuditService(ILogger<GoodAuditService> logger)
    {
        _logger = logger; // Only depends on logging, not on IOrderService
    }

    public async Task LogOrderProcessingAsync(int orderId, string action)
    {
        _logger.LogInformation($"Order {orderId}: {action}");
        await Task.CompletedTask;
    }
}

public static class CircularDependencyExample
{
    public static void DemonstrateCircularDependencyProblem()
    {
        var services = new ServiceCollection();
        
        // Register the problematic audit service
        services.AddScoped<IAuditService, ProblematicAuditService>();
        services.AddLogging();

        // This will create a circular dependency when the container tries to resolve IOrderService:
        // 1. Container tries to create AuditDecorator
        // 2. AuditDecorator needs IAuditService
        // 3. ProblematicAuditService needs IOrderService  
        // 4. But IOrderService is the AuditDecorator we're trying to create!
        // 5. INFINITE LOOP / STACK OVERFLOW!
        
        services.Decorate<IOrderService>()
                .With<AuditDecorator>()           // This decorator depends on IAuditService
                .Then<OrderService>()             // Base implementation
                .Apply();

        var provider = services.BuildServiceProvider();
        
        // This will throw a stack overflow exception or circular dependency error
        try
        {
            var orderService = provider.GetRequiredService<IOrderService>();
            // BOOM! 💥 Circular dependency at runtime
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Circular dependency detected: {ex.Message}");
        }
    }

    public static void DemonstrateProperImplementation()
    {
        var services = new ServiceCollection();
        
        // Register the good audit service (no circular dependency)
        services.AddScoped<IAuditService, GoodAuditService>();
        services.AddLogging();

        // This works fine because GoodAuditService doesn't depend on IOrderService
        services.Decorate<IOrderService>()
                .With<AuditDecorator>()
                .Then<OrderService>()
                .Apply();

        var provider = services.BuildServiceProvider();
        var orderService = provider.GetRequiredService<IOrderService>(); // ✅ Works perfectly!
    }
}
