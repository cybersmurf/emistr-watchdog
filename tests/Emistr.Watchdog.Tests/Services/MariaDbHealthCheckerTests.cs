using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Services.HealthCheckers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Emistr.Watchdog.Tests.Services;

public class MariaDbHealthCheckerTests
{
    private readonly ILogger<MariaDbHealthChecker> _logger;

    public MariaDbHealthCheckerTests()
    {
        _logger = Substitute.For<ILogger<MariaDbHealthChecker>>();
    }

    [Fact]
    public void ServiceName_ShouldReturnConfiguredName()
    {
        // Arrange
        var options = new MariaDbOptions { ConnectionString = "Server=localhost" };

        // Act
        var checker = new MariaDbHealthChecker("TestDB", options, _logger);

        // Assert
        checker.ServiceName.Should().Be("TestDB");
    }

    [Fact]
    public void DisplayName_WhenConfigured_ShouldReturnConfiguredValue()
    {
        // Arrange
        var options = new MariaDbOptions
        {
            ConnectionString = "Server=localhost",
            DisplayName = "Production Database"
        };

        // Act
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Assert
        checker.DisplayName.Should().Be("Production Database");
    }

    [Fact]
    public void DisplayName_WhenNotConfigured_ShouldFallbackToServiceName()
    {
        // Arrange
        var options = new MariaDbOptions
        {
            ConnectionString = "Server=localhost",
            DisplayName = null
        };

        // Act
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Assert
        checker.DisplayName.Should().Be("MariaDB");
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ShouldReturnTrue()
    {
        // Arrange
        var options = new MariaDbOptions
        {
            ConnectionString = "Server=localhost",
            Enabled = true
        };

        // Act
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Assert
        checker.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_WhenDisabled_ShouldReturnFalse()
    {
        // Arrange
        var options = new MariaDbOptions
        {
            ConnectionString = "Server=localhost",
            Enabled = false
        };

        // Act
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Assert
        checker.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CriticalThreshold_ShouldReturnConfiguredValue()
    {
        // Arrange
        var options = new MariaDbOptions
        {
            ConnectionString = "Server=localhost",
            CriticalAfterFailures = 5
        };

        // Act
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Assert
        checker.CriticalThreshold.Should().Be(5);
    }

    [Fact]
    public void CriticalThreshold_WhenNotConfigured_ShouldReturnDefaultValue()
    {
        // Arrange
        var options = new MariaDbOptions { ConnectionString = "Server=localhost" };

        // Act
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Assert
        checker.CriticalThreshold.Should().Be(3);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionStringNotConfigured_ShouldReturnUnhealthy()
    {
        // Arrange
        var options = new MariaDbOptions { ConnectionString = "" };
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Act
        var result = await checker.CheckHealthAsync();

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection string is not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionStringIsWhitespace_ShouldReturnUnhealthy()
    {
        // Arrange
        var options = new MariaDbOptions { ConnectionString = "   " };
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Act
        var result = await checker.CheckHealthAsync();

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection string is not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCannotConnect_ShouldReturnUnhealthy()
    {
        // Arrange - invalid connection string that will fail
        var options = new MariaDbOptions
        {
            ConnectionString = "Server=invalid.host.local;Port=3306;Database=test;Connect Timeout=1",
            TimeoutSeconds = 2
        };
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Act
        var result = await checker.CheckHealthAsync();

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.ConsecutiveFailures.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ConnectionString_ShouldDefaultToEmpty()
    {
        // Arrange & Act
        var options = new MariaDbOptions();

        // Assert
        options.ConnectionString.Should().BeEmpty();
    }

    [Fact]
    public void HealthCheckQuery_ShouldDefaultToNull()
    {
        // Arrange & Act
        var options = new MariaDbOptions();

        // Assert
        options.HealthCheckQuery.Should().BeNull();
    }

    [Fact]
    public void TimeoutSeconds_ShouldDefaultToTen()
    {
        // Arrange & Act
        var options = new MariaDbOptions();

        // Assert
        options.TimeoutSeconds.Should().Be(10);
    }

    [Fact]
    public void RestartConfig_WhenConfigured_ShouldReturnValue()
    {
        // Arrange
        var restartConfig = new ServiceRestartConfig { WindowsServiceName = "MySQL" };
        var options = new MariaDbOptions
        {
            ConnectionString = "Server=localhost",
            RestartConfig = restartConfig
        };

        // Act
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Assert
        checker.RestartConfig.Should().NotBeNull();
        checker.RestartConfig!.WindowsServiceName.Should().Be("MySQL");
    }

    [Fact]
    public void RestartConfig_WhenNotConfigured_ShouldReturnNull()
    {
        // Arrange
        var options = new MariaDbOptions { ConnectionString = "Server=localhost" };

        // Act
        var checker = new MariaDbHealthChecker("MariaDB", options, _logger);

        // Assert
        checker.RestartConfig.Should().BeNull();
    }
}
