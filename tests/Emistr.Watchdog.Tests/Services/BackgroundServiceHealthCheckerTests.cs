using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Services.HealthCheckers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Emistr.Watchdog.Tests.Services;

public class BackgroundServiceHealthCheckerTests
{
    private readonly ILogger<BackgroundServiceHealthChecker> _logger;

    public BackgroundServiceHealthCheckerTests()
    {
        _logger = Substitute.For<ILogger<BackgroundServiceHealthChecker>>();
    }

    [Fact]
    public void ServiceName_ShouldReturnConfiguredName()
    {
        // Arrange
        var options = new BackgroundServiceOptions { ConnectionString = "Server=localhost" };

        // Act
        var checker = new BackgroundServiceHealthChecker("BGService", options, _logger);

        // Assert
        checker.ServiceName.Should().Be("BGService");
    }

    [Fact]
    public void DisplayName_WhenConfigured_ShouldReturnConfiguredValue()
    {
        // Arrange
        var options = new BackgroundServiceOptions
        {
            ConnectionString = "Server=localhost",
            DisplayName = "Background Job Service"
        };

        // Act
        var checker = new BackgroundServiceHealthChecker("BGService", options, _logger);

        // Assert
        checker.DisplayName.Should().Be("Background Job Service");
    }

    [Fact]
    public void DisplayName_WhenNotConfigured_ShouldReturnDefaultDisplayName()
    {
        // Arrange
        var options = new BackgroundServiceOptions
        {
            ConnectionString = "Server=localhost",
            DisplayName = null
        };

        // Act
        var checker = new BackgroundServiceHealthChecker("BGService", options, _logger);

        // Assert
        checker.DisplayName.Should().Be("Emistr Background Service");
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ShouldReturnTrue()
    {
        // Arrange
        var options = new BackgroundServiceOptions
        {
            ConnectionString = "Server=localhost",
            Enabled = true
        };

        // Act
        var checker = new BackgroundServiceHealthChecker("BGService", options, _logger);

        // Assert
        checker.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_WhenDisabled_ShouldReturnFalse()
    {
        // Arrange
        var options = new BackgroundServiceOptions
        {
            ConnectionString = "Server=localhost",
            Enabled = false
        };

        // Act
        var checker = new BackgroundServiceHealthChecker("BGService", options, _logger);

        // Assert
        checker.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CriticalThreshold_ShouldReturnConfiguredValue()
    {
        // Arrange
        var options = new BackgroundServiceOptions
        {
            ConnectionString = "Server=localhost",
            CriticalAfterFailures = 10
        };

        // Act
        var checker = new BackgroundServiceHealthChecker("BGService", options, _logger);

        // Assert
        checker.CriticalThreshold.Should().Be(10);
    }

    [Fact]
    public void CriticalThreshold_WhenNotConfigured_ShouldReturnDefaultValue()
    {
        // Arrange
        var options = new BackgroundServiceOptions { ConnectionString = "Server=localhost" };

        // Act
        var checker = new BackgroundServiceHealthChecker("BGService", options, _logger);

        // Assert
        checker.CriticalThreshold.Should().Be(3);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionStringNotConfigured_ShouldReturnUnhealthy()
    {
        // Arrange
        var options = new BackgroundServiceOptions { ConnectionString = "" };
        var checker = new BackgroundServiceHealthChecker("BGService", options, _logger);

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
        var options = new BackgroundServiceOptions { ConnectionString = "   " };
        var checker = new BackgroundServiceHealthChecker("BGService", options, _logger);

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
        var options = new BackgroundServiceOptions
        {
            ConnectionString = "Server=invalid.host.local;Port=3306;Database=test;Connect Timeout=1",
            TimeoutSeconds = 2
        };
        var checker = new BackgroundServiceHealthChecker("BGService", options, _logger);

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
        var options = new BackgroundServiceOptions();

        // Assert
        options.ConnectionString.Should().BeEmpty();
    }

    [Fact]
    public void DatabaseName_ShouldDefaultToSudUtf8Aaa()
    {
        // Arrange & Act
        var options = new BackgroundServiceOptions();

        // Assert
        options.DatabaseName.Should().Be("sud_utf8_aaa");
    }

    [Fact]
    public void TableName_ShouldDefaultToSystem()
    {
        // Arrange & Act
        var options = new BackgroundServiceOptions();

        // Assert
        options.TableName.Should().Be("system");
    }

    [Fact]
    public void ColumnName_ShouldDefaultToBgsLastRun()
    {
        // Arrange & Act
        var options = new BackgroundServiceOptions();

        // Assert
        options.ColumnName.Should().Be("bgs_last_run");
    }

    [Fact]
    public void SystemRowId_ShouldDefaultToOne()
    {
        // Arrange & Act
        var options = new BackgroundServiceOptions();

        // Assert
        options.SystemRowId.Should().Be(1);
    }

    [Fact]
    public void MaxAgeMinutes_ShouldDefaultToFive()
    {
        // Arrange & Act
        var options = new BackgroundServiceOptions();

        // Assert
        options.MaxAgeMinutes.Should().Be(5);
    }

    [Fact]
    public void MaxAgeMinutes_ShouldBeConfigurable()
    {
        // Arrange & Act
        var options = new BackgroundServiceOptions { MaxAgeMinutes = 15 };

        // Assert
        options.MaxAgeMinutes.Should().Be(15);
    }

    [Fact]
    public void TimeoutSeconds_ShouldDefaultToTen()
    {
        // Arrange & Act
        var options = new BackgroundServiceOptions();

        // Assert
        options.TimeoutSeconds.Should().Be(10);
    }
}
