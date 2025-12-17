using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Emistr.Watchdog.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Emistr.Watchdog.Tests.Services;

public class CriticalAlertServiceTests
{
    private readonly ILogger<CriticalAlertService> _logger;

    public CriticalAlertServiceTests()
    {
        _logger = Substitute.For<ILogger<CriticalAlertService>>();
    }

    [Fact]
    public async Task RaiseAlertAsync_WhenDisabled_ShouldNotThrow()
    {
        // Arrange
        var options = CreateOptions(enableDesktop: false, enableSound: false);
        var service = new CriticalAlertService(Options.Create(options), _logger);
        var result = CreateCriticalResult("TestService");

        // Act & Assert
        await service.Invoking(s => s.RaiseAlertAsync(result))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task RaiseAlertAsync_WhenDesktopEnabled_ShouldNotThrow()
    {
        // Arrange
        var options = CreateOptions(enableDesktop: true, enableSound: false);
        var service = new CriticalAlertService(Options.Create(options), _logger);
        var result = CreateCriticalResult("TestService");

        // Act & Assert - just verify no exception (actual notification is platform-dependent)
        await service.Invoking(s => s.RaiseAlertAsync(result))
            .Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldNotThrow()
    {
        // Arrange
        var options = CreateOptions(enableDesktop: true, enableSound: false);
        
        // Act
        var service = new CriticalAlertService(Options.Create(options), _logger);
        
        // Assert
        service.Should().NotBeNull();
    }

    [Theory]
    [InlineData(ServiceStatus.Critical)]
    [InlineData(ServiceStatus.Unhealthy)]
    public async Task RaiseAlertAsync_ForDifferentStatuses_ShouldNotThrow(ServiceStatus status)
    {
        // Arrange
        var options = CreateOptions(enableDesktop: true, enableSound: false);
        var service = new CriticalAlertService(Options.Create(options), _logger);
        var result = new HealthCheckResult
        {
            ServiceName = "TestService",
            Status = status,
            IsHealthy = false,
            IsCritical = status == ServiceStatus.Critical,
            ErrorMessage = "Test error"
        };

        // Act & Assert
        await service.Invoking(s => s.RaiseAlertAsync(result))
            .Should().NotThrowAsync();
    }

    private static NotificationOptions CreateOptions(bool enableDesktop, bool enableSound)
    {
        return new NotificationOptions
        {
            CriticalEvents = new CriticalEventOptions
            {
                EnableDesktopNotification = enableDesktop,
                EnableSound = enableSound,
                LogToEventLog = false
            }
        };
    }

    private static HealthCheckResult CreateCriticalResult(string serviceName)
    {
        return new HealthCheckResult
        {
            ServiceName = serviceName,
            IsHealthy = false,
            Status = ServiceStatus.Critical,
            IsCritical = true,
            ErrorMessage = "Critical failure",
            ConsecutiveFailures = 5
        };
    }
}

