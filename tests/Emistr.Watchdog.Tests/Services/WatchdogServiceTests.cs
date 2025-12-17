using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Dashboard;
using Emistr.Watchdog.Models;
using FluentAssertions;

namespace Emistr.Watchdog.Tests.Services;

public class WatchdogServiceTests
{
    [Fact]
    public void WatchdogOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new WatchdogOptions();

        // Assert
        options.CheckIntervalSeconds.Should().Be(30);
        options.Services.Should().NotBeNull();
    }

    [Fact]
    public void ServicesOptions_ShouldHaveAllServiceTypes()
    {
        // Arrange & Act
        var services = new ServicesOptions();

        // Assert
        services.MariaDB.Should().NotBeNull();
        services.LicenseManager.Should().NotBeNull();
        services.Apache.Should().NotBeNull();
        services.PracantD.Should().NotBeNull();
        services.BackgroundService.Should().NotBeNull();
        services.Redis.Should().NotBeNull();
        services.RabbitMQ.Should().NotBeNull();
        services.Elasticsearch.Should().NotBeNull();
        services.CustomMariaDbServices.Should().NotBeNull();
        services.CustomHttpServices.Should().NotBeNull();
        services.CustomTelnetServices.Should().NotBeNull();
        services.CustomBackgroundServices.Should().NotBeNull();
        services.PingServices.Should().NotBeNull();
    }

    [Fact]
    public void ServiceOptionsBase_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new HttpServiceOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.TimeoutSeconds.Should().Be(10);
        options.CriticalAfterFailures.Should().Be(3);
    }

    [Fact]
    public void HealthCheckResult_Healthy_ShouldCreateCorrectResult()
    {
        // Act
        var result = HealthCheckResult.Healthy("TestService");

        // Assert
        result.ServiceName.Should().Be("TestService");
        result.IsHealthy.Should().BeTrue();
        result.Status.Should().Be(ServiceStatus.Healthy);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void HealthCheckResult_Unhealthy_ShouldCreateCorrectResult()
    {
        // Act
        var result = HealthCheckResult.Unhealthy("TestService", "Test error");

        // Assert
        result.ServiceName.Should().Be("TestService");
        result.IsHealthy.Should().BeFalse();
        result.Status.Should().Be(ServiceStatus.Unhealthy);
        result.ErrorMessage.Should().Be("Test error");
    }

    [Fact]
    public void PingOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new PingOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.PingCount.Should().Be(3);
        options.PacketLossThresholdPercent.Should().Be(20);
        options.HighLatencyThresholdMs.Should().Be(200);
    }

    [Fact]
    public void RedisOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new RedisOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.ConnectionString.Should().Be("localhost:6379");
    }

    [Fact]
    public void RabbitMqOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new RabbitMqOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.Host.Should().Be("localhost");
        options.Port.Should().Be(5672);
        options.Username.Should().Be("guest");
        options.Password.Should().Be("guest");
    }

    [Fact]
    public void ElasticsearchOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new ElasticsearchOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.Url.Should().Be("http://localhost:9200");
    }

    [Fact]
    public void ServiceStatusDto_ShouldSortPrioritizedFirst()
    {
        // Arrange
        var services = new List<Dashboard.ServiceStatusDto>
        {
            new()
            {
                ServiceName = "service-c",
                DisplayName = "Service C",
                Status = "Healthy",
                IsHealthy = true,
                IsCritical = false,
                IsPrioritized = false
            },
            new()
            {
                ServiceName = "service-a",
                DisplayName = "Service A",
                Status = "Healthy",
                IsHealthy = true,
                IsCritical = false,
                IsPrioritized = true
            },
            new()
            {
                ServiceName = "service-b",
                DisplayName = "Service B",
                Status = "Healthy",
                IsHealthy = true,
                IsCritical = false,
                IsPrioritized = false
            }
        };

        // Act - simulate the sorting logic used in DashboardEndpoints and WatchdogService
        var sorted = services
            .OrderByDescending(s => s.IsPrioritized)
            .ThenBy(s => s.DisplayName)
            .ToList();

        // Assert
        sorted[0].ServiceName.Should().Be("service-a", "prioritized service should be first");
        sorted[0].IsPrioritized.Should().BeTrue();
        sorted[1].ServiceName.Should().Be("service-b", "non-prioritized services sorted alphabetically");
        sorted[2].ServiceName.Should().Be("service-c");
    }

    [Fact]
    public void ServiceStatusDto_MultiplePrioritized_ShouldSortAlphabeticallyWithinGroup()
    {
        // Arrange
        var services = new List<Dashboard.ServiceStatusDto>
        {
            new()
            {
                ServiceName = "z-regular",
                DisplayName = "Z Regular",
                Status = "Healthy",
                IsHealthy = true,
                IsCritical = false,
                IsPrioritized = false
            },
            new()
            {
                ServiceName = "b-priority",
                DisplayName = "B Priority",
                Status = "Healthy",
                IsHealthy = true,
                IsCritical = false,
                IsPrioritized = true
            },
            new()
            {
                ServiceName = "a-priority",
                DisplayName = "A Priority",
                Status = "Healthy",
                IsHealthy = true,
                IsCritical = false,
                IsPrioritized = true
            },
            new()
            {
                ServiceName = "a-regular",
                DisplayName = "A Regular",
                Status = "Healthy",
                IsHealthy = true,
                IsCritical = false,
                IsPrioritized = false
            }
        };

        // Act
        var sorted = services
            .OrderByDescending(s => s.IsPrioritized)
            .ThenBy(s => s.DisplayName)
            .ToList();

        // Assert - prioritized first (alphabetically), then regular (alphabetically)
        sorted[0].DisplayName.Should().Be("A Priority");
        sorted[1].DisplayName.Should().Be("B Priority");
        sorted[2].DisplayName.Should().Be("A Regular");
        sorted[3].DisplayName.Should().Be("Z Regular");
    }
}
