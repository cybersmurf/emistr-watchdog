using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Services.HealthCheckers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PingServiceOptions = Emistr.Watchdog.Configuration.PingOptions;

namespace Emistr.Watchdog.Tests.Services;

public class PingHealthCheckerTests
{
    private readonly ILogger<PingHealthChecker> _logger;

    public PingHealthCheckerTests()
    {
        _logger = Substitute.For<ILogger<PingHealthChecker>>();
    }

    [Fact]
    public void ServiceName_ShouldReturnConfiguredName()
    {
        // Arrange
        var options = new PingServiceOptions { Host = "127.0.0.1" };
        var checker = new PingHealthChecker("Gateway", options, _logger);

        // Assert
        checker.ServiceName.Should().Be("Gateway");
    }

    [Fact]
    public void DisplayName_WhenConfigured_ShouldReturnConfiguredValue()
    {
        // Arrange
        var options = new PingServiceOptions
        {
            Host = "192.168.1.1",
            DisplayName = "Main Gateway"
        };
        var checker = new PingHealthChecker("Gateway", options, _logger);

        // Assert
        checker.DisplayName.Should().Be("Main Gateway");
    }

    [Fact]
    public void DisplayName_WhenNotConfigured_ShouldReturnDefaultFormat()
    {
        // Arrange
        var options = new PingServiceOptions { Host = "192.168.1.1" };
        var checker = new PingHealthChecker("Gateway", options, _logger);

        // Assert
        checker.DisplayName.Should().Be("Ping 192.168.1.1");
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ShouldReturnTrue()
    {
        // Arrange
        var options = new PingServiceOptions { Host = "127.0.0.1", Enabled = true };
        var checker = new PingHealthChecker("Gateway", options, _logger);

        // Assert
        checker.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_WhenDisabled_ShouldReturnFalse()
    {
        // Arrange
        var options = new PingServiceOptions { Host = "127.0.0.1", Enabled = false };
        var checker = new PingHealthChecker("Gateway", options, _logger);

        // Assert
        checker.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenHostNotConfigured_ShouldReturnUnhealthy()
    {
        // Arrange
        var options = new PingServiceOptions { Host = "" };
        var checker = new PingHealthChecker("Gateway", options, _logger);

        // Act
        var result = await checker.CheckHealthAsync();

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Host is not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingLocalhost_ShouldReturnHealthy()
    {
        // Arrange
        var options = new PingServiceOptions
        {
            Host = "127.0.0.1",
            PingCount = 1,
            TimeoutSeconds = 5
        };
        var checker = new PingHealthChecker("Localhost", options, _logger);

        // Act
        var result = await checker.CheckHealthAsync();

        // Assert
        result.IsHealthy.Should().BeTrue();
        result.ResponseTimeMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPingUnreachableHost_ShouldReturnUnhealthy()
    {
        // Arrange - use a non-routable IP
        var options = new PingServiceOptions
        {
            Host = "192.0.2.1", // TEST-NET-1, should be unreachable
            PingCount = 1,
            TimeoutSeconds = 2
        };
        var checker = new PingHealthChecker("Unreachable", options, _logger);

        // Act
        var result = await checker.CheckHealthAsync();

        // Assert
        result.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void PingOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new PingServiceOptions();

        // Assert
        options.Host.Should().BeEmpty();
        options.PingCount.Should().Be(3);
        options.PacketLossThresholdPercent.Should().Be(20);
        options.HighLatencyThresholdMs.Should().Be(200);
        options.TimeoutSeconds.Should().Be(10);
        options.CriticalAfterFailures.Should().Be(3);
        options.Enabled.Should().BeTrue();
    }

    [Fact]
    public void CriticalThreshold_ShouldReturnConfiguredValue()
    {
        // Arrange
        var options = new PingServiceOptions
        {
            Host = "127.0.0.1",
            CriticalAfterFailures = 5
        };
        var checker = new PingHealthChecker("Gateway", options, _logger);

        // Assert
        checker.CriticalThreshold.Should().Be(5);
    }
}

