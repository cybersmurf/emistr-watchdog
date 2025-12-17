using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Emistr.Watchdog.Services.HealthCheckers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Emistr.Watchdog.Tests.Services;

public class RedisHealthCheckerTests
{
    private readonly ILogger<RedisHealthChecker> _logger;

    public RedisHealthCheckerTests()
    {
        _logger = Substitute.For<ILogger<RedisHealthChecker>>();
    }

    [Fact]
    public void ServiceName_ShouldReturnConfiguredName()
    {
        var options = new RedisOptions { ConnectionString = "localhost:6379" };
        var checker = new RedisHealthChecker("TestRedis", options, _logger);
        checker.ServiceName.Should().Be("TestRedis");
    }

    [Fact]
    public void DisplayName_ShouldReturnConfiguredDisplayName()
    {
        var options = new RedisOptions { DisplayName = "Production Redis" };
        var checker = new RedisHealthChecker("Redis", options, _logger);
        checker.DisplayName.Should().Be("Production Redis");
    }

    [Fact]
    public void IsEnabled_ShouldReturnConfiguredValue()
    {
        var options = new RedisOptions { Enabled = false };
        var checker = new RedisHealthChecker("Redis", options, _logger);
        checker.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnUnhealthy_WhenConnectionStringEmpty()
    {
        var options = new RedisOptions { Enabled = true, ConnectionString = "" };
        var checker = new RedisHealthChecker("Redis", options, _logger);
        var result = await checker.CheckHealthAsync();
        result.IsHealthy.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection string");
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnUnknown_WhenDisabled()
    {
        var options = new RedisOptions { Enabled = false, ConnectionString = "localhost:6379" };
        var checker = new RedisHealthChecker("Redis", options, _logger);
        var result = await checker.CheckHealthAsync();
        result.Status.Should().Be(ServiceStatus.Unknown);
    }
}

public class RedisHealthCheckerFactoryTests
{
    [Fact]
    public void Create_ShouldCreateCheckerWithCorrectName()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<RedisHealthChecker>().Returns(Substitute.For<ILogger<RedisHealthChecker>>());
        var factory = new RedisHealthCheckerFactory(loggerFactory);
        var checker = factory.Create("TestRedis", new RedisOptions());
        checker.ServiceName.Should().Be("TestRedis");
    }
}
