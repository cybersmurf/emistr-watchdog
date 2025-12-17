using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Emistr.Watchdog.Services.HealthCheckers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Emistr.Watchdog.Tests.Services;

public class HttpHealthCheckerTests
{
    private readonly ILogger<HttpHealthChecker> _logger;

    public HttpHealthCheckerTests()
    {
        _logger = Substitute.For<ILogger<HttpHealthChecker>>();
    }

    [Fact]
    public void ServiceName_ShouldReturnConfiguredName()
    {
        // Arrange
        var options = new HttpServiceOptions { Url = "http://localhost/health" };
        var httpClient = new HttpClient();

        // Act
        var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

        // Assert
        checker.ServiceName.Should().Be("TestService");
    }

    [Fact]
    public void DisplayName_WhenNotSet_ShouldReturnServiceName()
    {
        // Arrange
        var options = new HttpServiceOptions { Url = "http://localhost/health" };
        var httpClient = new HttpClient();

        // Act
        var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

        // Assert
        checker.DisplayName.Should().Be("TestService");
    }

    [Fact]
    public void DisplayName_WhenSet_ShouldReturnConfiguredDisplayName()
    {
        // Arrange
        var options = new HttpServiceOptions
        {
            Url = "http://localhost/health",
            DisplayName = "My Test Service"
        };
        var httpClient = new HttpClient();

        // Act
        var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

        // Assert
        checker.DisplayName.Should().Be("My Test Service");
    }

    [Fact]
    public void IsEnabled_ShouldReturnConfiguredValue()
    {
        // Arrange
        var options = new HttpServiceOptions { Enabled = false };
        var httpClient = new HttpClient();

        // Act
        var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

        // Assert
        checker.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDisabled_ShouldReturnUnknownStatus()
    {
        // Arrange
        var options = new HttpServiceOptions { Enabled = false };
        var httpClient = new HttpClient();
        var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

        // Act
        var result = await checker.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(ServiceStatus.Unknown);
        result.Details.Should().ContainKey("reason");
    }

        [Fact]
        public async Task CheckHealthAsync_WhenUrlNotConfigured_ShouldReturnUnhealthy()
        {
            // Arrange
            var options = new HttpServiceOptions { Url = "" };
            var httpClient = new HttpClient();
            var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

            // Act
            var result = await checker.CheckHealthAsync();

            // Assert
            result.IsHealthy.Should().BeFalse();
            result.ErrorMessage.Should().Contain("URL is not configured");
        }

        [Fact]
        public async Task CheckHealthAsync_WhenUrlIsWhitespace_ShouldReturnUnhealthy()
        {
            // Arrange
            var options = new HttpServiceOptions { Url = "   " };
            var httpClient = new HttpClient();
            var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

            // Act
            var result = await checker.CheckHealthAsync();

            // Assert
            result.IsHealthy.Should().BeFalse();
            result.ErrorMessage.Should().Contain("URL is not configured");
        }

        [Fact]
        public void CriticalThreshold_ShouldReturnConfiguredValue()
        {
            // Arrange
            var options = new HttpServiceOptions
            {
                Url = "http://localhost/health",
                CriticalAfterFailures = 5
            };
            var httpClient = new HttpClient();

            // Act
            var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

            // Assert
            checker.CriticalThreshold.Should().Be(5);
        }

        [Fact]
        public void CriticalThreshold_WhenNotConfigured_ShouldReturnDefaultValue()
        {
            // Arrange
            var options = new HttpServiceOptions { Url = "http://localhost/health" };
            var httpClient = new HttpClient();

            // Act
            var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

            // Assert
            checker.CriticalThreshold.Should().Be(3);
        }

        [Fact]
        public void RestartConfig_WhenConfigured_ShouldReturnValue()
        {
            // Arrange
            var restartConfig = new ServiceRestartConfig { WindowsServiceName = "Apache2.4" };
            var options = new HttpServiceOptions
            {
                Url = "http://localhost/health",
                RestartConfig = restartConfig
            };
            var httpClient = new HttpClient();

            // Act
            var checker = new HttpHealthChecker("Apache", options, httpClient, _logger);

            // Assert
            checker.RestartConfig.Should().NotBeNull();
            checker.RestartConfig!.WindowsServiceName.Should().Be("Apache2.4");
        }

        [Fact]
        public void RestartConfig_WhenNotConfigured_ShouldReturnNull()
        {
            // Arrange
            var options = new HttpServiceOptions { Url = "http://localhost/health" };
            var httpClient = new HttpClient();

            // Act
            var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

            // Assert
            checker.RestartConfig.Should().BeNull();
        }

        [Fact]
        public void Url_ShouldDefaultToEmpty()
        {
            // Arrange & Act
            var options = new HttpServiceOptions();

            // Assert
            options.Url.Should().BeEmpty();
        }

        [Fact]
        public void ExpectedStatusCodes_ShouldDefaultTo200()
        {
            // Arrange & Act
            var options = new HttpServiceOptions();

            // Assert
            options.ExpectedStatusCodes.Should().ContainSingle().Which.Should().Be(200);
        }

        [Fact]
        public void ExpectedStatusCodes_ShouldBeConfigurable()
        {
            // Arrange & Act
            var options = new HttpServiceOptions { ExpectedStatusCodes = [200, 201, 204] };

            // Assert
            options.ExpectedStatusCodes.Should().BeEquivalentTo(new[] { 200, 201, 204 });
        }

        [Fact]
        public void ExpectedContent_ShouldDefaultToNull()
        {
            // Arrange & Act
            var options = new HttpServiceOptions();

            // Assert
            options.ExpectedContent.Should().BeNull();
        }

        [Fact]
        public void IgnoreSslErrors_ShouldDefaultToFalse()
        {
            // Arrange & Act
            var options = new HttpServiceOptions();

            // Assert
            options.IgnoreSslErrors.Should().BeFalse();
        }

        [Fact]
        public void TimeoutSeconds_ShouldDefaultToTen()
        {
            // Arrange & Act
            var options = new HttpServiceOptions();

            // Assert
            options.TimeoutSeconds.Should().Be(10);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenCannotConnect_ShouldReturnUnhealthy()
        {
            // Arrange - use invalid URL that will fail
            var options = new HttpServiceOptions
            {
                Url = "http://invalid.host.local:9999/health",
                TimeoutSeconds = 2
            };
            var httpClient = new HttpClient();
            var checker = new HttpHealthChecker("TestService", options, httpClient, _logger);

            // Act
            var result = await checker.CheckHealthAsync();

            // Assert
            result.IsHealthy.Should().BeFalse();
            result.ConsecutiveFailures.Should().BeGreaterThan(0);
        }
    }
