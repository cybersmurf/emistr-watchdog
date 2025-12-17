using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Services.HealthCheckers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Emistr.Watchdog.Tests.Services;

public class TelnetHealthCheckerTests
{
    private readonly ILogger<TelnetHealthChecker> _logger;

    public TelnetHealthCheckerTests()
    {
        _logger = Substitute.For<ILogger<TelnetHealthChecker>>();
    }

    [Fact]
    public void ServiceName_ShouldReturnConfiguredName()
    {
        // Arrange
        var options = new TelnetServiceOptions { Host = "localhost", Port = 9999 };

        // Act
        var checker = new TelnetHealthChecker("PracantD", options, _logger);

        // Assert
        checker.ServiceName.Should().Be("PracantD");
    }

    [Fact]
    public void CriticalThreshold_ShouldReturnConfiguredValue()
    {
        // Arrange
        var options = new TelnetServiceOptions
        {
            Host = "localhost",
            Port = 9999,
            CriticalAfterFailures = 5
        };

        // Act
        var checker = new TelnetHealthChecker("PracantD", options, _logger);

        // Assert
        checker.CriticalThreshold.Should().Be(5);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenHostNotConfigured_ShouldReturnUnhealthy()
    {
        // Arrange
        var options = new TelnetServiceOptions { Host = "", Port = 9999 };
        var checker = new TelnetHealthChecker("PracantD", options, _logger);

        // Act
        var result = await checker.CheckHealthAsync();

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Host is not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenPortNotConfigured_ShouldReturnUnhealthy()
    {
        // Arrange
        var options = new TelnetServiceOptions { Host = "localhost", Port = 0 };
        var checker = new TelnetHealthChecker("PracantD", options, _logger);

        // Act
        var result = await checker.CheckHealthAsync();

        // Assert
        result.IsHealthy.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Port is not configured");
    }

        [Fact]
        public async Task CheckHealthAsync_WhenCannotConnect_ShouldReturnUnhealthy()
        {
            // Arrange - use an unlikely port that should fail
            var options = new TelnetServiceOptions
            {
                Host = "127.0.0.1",
                Port = 59999,
                TimeoutSeconds = 2
            };
            var checker = new TelnetHealthChecker("PracantD", options, _logger);

            // Act
            var result = await checker.CheckHealthAsync();

            // Assert
            result.IsHealthy.Should().BeFalse();
            result.ConsecutiveFailures.Should().BeGreaterThan(0);
        }

        [Fact]
        public void DisplayName_WhenConfigured_ShouldReturnConfiguredValue()
        {
            // Arrange
            var options = new TelnetServiceOptions
            {
                Host = "localhost",
                Port = 9999,
                DisplayName = "PracantD Service"
            };

            // Act
            var checker = new TelnetHealthChecker("PracantD", options, _logger);

            // Assert
            checker.DisplayName.Should().Be("PracantD Service");
        }

        [Fact]
        public void DisplayName_WhenNotConfigured_ShouldFallbackToServiceName()
        {
            // Arrange
            var options = new TelnetServiceOptions
            {
                Host = "localhost",
                Port = 9999,
                DisplayName = null
            };

            // Act
            var checker = new TelnetHealthChecker("PracantD", options, _logger);

            // Assert
            checker.DisplayName.Should().Be("PracantD");
        }

        [Fact]
        public void IsEnabled_WhenEnabled_ShouldReturnTrue()
        {
            // Arrange
            var options = new TelnetServiceOptions
            {
                Host = "localhost",
                Port = 9999,
                Enabled = true
            };

            // Act
            var checker = new TelnetHealthChecker("PracantD", options, _logger);

            // Assert
            checker.IsEnabled.Should().BeTrue();
        }

        [Fact]
        public void IsEnabled_WhenDisabled_ShouldReturnFalse()
        {
            // Arrange
            var options = new TelnetServiceOptions
            {
                Host = "localhost",
                Port = 9999,
                Enabled = false
            };

            // Act
            var checker = new TelnetHealthChecker("PracantD", options, _logger);

            // Assert
            checker.IsEnabled.Should().BeFalse();
        }

        [Fact]
        public async Task CheckHealthAsync_WhenHostIsWhitespace_ShouldReturnUnhealthy()
        {
            // Arrange
            var options = new TelnetServiceOptions { Host = "   ", Port = 9999 };
            var checker = new TelnetHealthChecker("PracantD", options, _logger);

            // Act
            var result = await checker.CheckHealthAsync();

            // Assert
            result.IsHealthy.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Host is not configured");
        }

        [Fact]
        public async Task CheckHealthAsync_WhenPortIsNegative_ShouldReturnUnhealthy()
        {
            // Arrange
            var options = new TelnetServiceOptions { Host = "localhost", Port = -1 };
            var checker = new TelnetHealthChecker("PracantD", options, _logger);

            // Act
            var result = await checker.CheckHealthAsync();

            // Assert
            result.IsHealthy.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Port is not configured");
        }

        [Fact]
        public async Task CheckHealthAsync_WhenConnectionTimeout_ShouldReturnUnhealthyWithTimeoutMessage()
        {
            // Arrange - use a non-routable IP to force timeout
            var options = new TelnetServiceOptions
            {
                Host = "10.255.255.1",
                Port = 9999,
                TimeoutSeconds = 1
            };
            var checker = new TelnetHealthChecker("PracantD", options, _logger);

            // Act
            var result = await checker.CheckHealthAsync();

            // Assert
            result.IsHealthy.Should().BeFalse();
            result.ErrorMessage.Should().Contain("10.255.255.1:9999");
        }

            [Fact]
            public void CriticalThreshold_WhenNotConfigured_ShouldReturnDefaultValue()
            {
                // Arrange
                var options = new TelnetServiceOptions
                {
                    Host = "localhost",
                    Port = 9999
                };

                // Act
                var checker = new TelnetHealthChecker("PracantD", options, _logger);

                // Assert
                checker.CriticalThreshold.Should().Be(3); // Default value
            }

            [Fact]
            public void ReadTimeoutMs_ShouldHaveDefaultValue()
            {
                // Arrange & Act
                var options = new TelnetServiceOptions();

                // Assert
                options.ReadTimeoutMs.Should().Be(2000);
            }

            [Fact]
            public void ReadTimeoutMs_ShouldBeConfigurable()
            {
                // Arrange & Act
                var options = new TelnetServiceOptions { ReadTimeoutMs = 5000 };

                // Assert
                options.ReadTimeoutMs.Should().Be(5000);
            }

            [Fact]
            public void AppendNewlineToRawCommand_ShouldDefaultToTrue()
            {
                // Arrange & Act
                var options = new TelnetServiceOptions();

                // Assert
                options.AppendNewlineToRawCommand.Should().BeTrue();
            }

            [Fact]
            public void AppendNewlineToRawCommand_ShouldBeConfigurable()
            {
                // Arrange & Act
                var options = new TelnetServiceOptions { AppendNewlineToRawCommand = false };

                // Assert
                options.AppendNewlineToRawCommand.Should().BeFalse();
            }

            [Fact]
            public void ConnectionOnly_ShouldDefaultToFalse()
            {
                // Arrange & Act
                var options = new TelnetServiceOptions();

                // Assert
                options.ConnectionOnly.Should().BeFalse();
            }

            [Fact]
            public void Host_ShouldDefaultToLocalhost()
            {
                // Arrange & Act
                var options = new TelnetServiceOptions();

                // Assert
                options.Host.Should().Be("localhost");
            }

            [Fact]
            public void Port_ShouldDefaultToZero()
            {
                // Arrange & Act
                var options = new TelnetServiceOptions();

                // Assert
                options.Port.Should().Be(0);
            }

            [Fact]
            public void TimeoutSeconds_ShouldDefaultToTen()
            {
                // Arrange & Act
                var options = new TelnetServiceOptions();

                // Assert
                options.TimeoutSeconds.Should().Be(10);
            }
        }
