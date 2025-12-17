using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Emistr.Watchdog.Services.HealthCheckers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Emistr.Watchdog.Tests.Services;

public class RabbitMqHealthCheckerTests
{
    private readonly ILogger<RabbitMqHealthChecker> _logger;

    public RabbitMqHealthCheckerTests()
    {
        _logger = Substitute.For<ILogger<RabbitMqHealthChecker>>();
    }

    [Fact]
    public void ServiceName_ShouldReturnConfiguredName()
    {
        var options = new RabbitMqOptions { Host = "localhost" };
        var checker = new RabbitMqHealthChecker("TestRabbitMQ", options, _logger);
        checker.ServiceName.Should().Be("TestRabbitMQ");
    }

    [Fact]
    public void DefaultPort_ShouldBe5672()
    {
        var options = new RabbitMqOptions();
        options.Port.Should().Be(5672);
    }

    [Fact]
    public void DefaultCredentials_ShouldBeGuest()
    {
        var options = new RabbitMqOptions();
        options.Username.Should().Be("guest");
        options.Password.Should().Be("guest");
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnUnknown_WhenDisabled()
    {
        var options = new RabbitMqOptions { Enabled = false };
        var checker = new RabbitMqHealthChecker("RabbitMQ", options, _logger);
        var result = await checker.CheckHealthAsync();
        result.Status.Should().Be(ServiceStatus.Unknown);
    }
}

public class RabbitMqHealthCheckerFactoryTests
{
    [Fact]
    public void Create_ShouldCreateCheckerWithCorrectName()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<RabbitMqHealthChecker>().Returns(Substitute.For<ILogger<RabbitMqHealthChecker>>());
        var factory = new RabbitMqHealthCheckerFactory(loggerFactory);
        var checker = factory.Create("TestRabbitMQ", new RabbitMqOptions());
        checker.ServiceName.Should().Be("TestRabbitMQ");
    }
}
