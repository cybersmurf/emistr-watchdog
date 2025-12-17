using Emistr.Watchdog.Models;
using FluentAssertions;

namespace Emistr.Watchdog.Tests.Models;

public class HealthCheckResultTests
{
    [Fact]
    public void Healthy_ShouldCreateHealthyResult()
    {
        // Act
        var result = HealthCheckResult.Healthy("TestService", 100);

        // Assert
        result.ServiceName.Should().Be("TestService");
        result.IsHealthy.Should().BeTrue();
        result.Status.Should().Be(ServiceStatus.Healthy);
        result.ResponseTimeMs.Should().Be(100);
        result.ErrorMessage.Should().BeNull();
        result.IsCritical.Should().BeFalse();
    }

    [Fact]
    public void Unhealthy_BelowThreshold_ShouldNotBeCritical()
    {
        // Act
        var result = HealthCheckResult.Unhealthy(
            "TestService",
            "Connection failed",
            consecutiveFailures: 2,
            criticalThreshold: 3);

        // Assert
        result.ServiceName.Should().Be("TestService");
        result.IsHealthy.Should().BeFalse();
        result.Status.Should().Be(ServiceStatus.Unhealthy);
        result.IsCritical.Should().BeFalse();
        result.ConsecutiveFailures.Should().Be(2);
    }

    [Fact]
    public void Unhealthy_AtThreshold_ShouldBeCritical()
    {
        // Act
        var result = HealthCheckResult.Unhealthy(
            "TestService",
            "Connection failed",
            consecutiveFailures: 3,
            criticalThreshold: 3);

        // Assert
        result.Status.Should().Be(ServiceStatus.Critical);
        result.IsCritical.Should().BeTrue();
    }

    [Fact]
    public void Unhealthy_AboveThreshold_ShouldBeCritical()
    {
        // Act
        var result = HealthCheckResult.Unhealthy(
            "TestService",
            "Connection failed",
            consecutiveFailures: 5,
            criticalThreshold: 3);

        // Assert
        result.Status.Should().Be(ServiceStatus.Critical);
        result.IsCritical.Should().BeTrue();
    }

    [Fact]
    public void Degraded_ShouldBeHealthyButDegraded()
    {
        // Act
        var result = HealthCheckResult.Degraded("TestService", "Slow response", 1500);

        // Assert
        result.IsHealthy.Should().BeTrue();
        result.Status.Should().Be(ServiceStatus.Degraded);
        result.ResponseTimeMs.Should().Be(1500);
        result.ErrorMessage.Should().Be("Slow response");
    }

    [Fact]
    public void CheckedAt_ShouldBeSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var result = HealthCheckResult.Healthy("TestService");

        // Assert
        var after = DateTime.UtcNow;
        result.CheckedAt.Should().BeOnOrAfter(before);
        result.CheckedAt.Should().BeOnOrBefore(after);
    }
}
