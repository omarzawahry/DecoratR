using Microsoft.Extensions.Logging;

namespace DecoratR.Tests;

public interface IService
{
    string Execute();
}

public class BaseService : IService
{
    public string Execute() => "Base";
}

public class LoggingDecorator : IService
{
    private readonly IService _inner;
    public LoggingDecorator(IService inner) => _inner = inner;

    public string Execute() => $"Log({_inner.Execute()})";
}

public class RetryDecorator : IService
{
    private readonly IService _inner;
    public RetryDecorator(IService inner) => _inner = inner;

    public string Execute() => $"Retry({_inner.Execute()})";
}

public class DependencyLoggingDecorator(
    IService inner,
    ILogger<DependencyLoggingDecorator> logger) : IService
{
    public string Execute()
    {
        logger.LogInformation("Executing");
        return $"DependencyLog({inner.Execute()})";
    }
}

/// <summary>
/// Invalid decorator for testing validation - missing proper constructor
/// </summary>
public class InvalidDecorator : IService
{
    // This decorator intentionally has no constructor that accepts IService
    public InvalidDecorator() { }

    public string Execute() => "Invalid";
}
