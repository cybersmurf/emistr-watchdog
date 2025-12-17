using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Emistr.Watchdog.Tests.Services;

public class UptimeTrackerTests
{
    private readonly UptimeTracker _tracker;

    public UptimeTrackerTests()
    {
        _tracker = new UptimeTracker();
    }

    [Fact]
    public void RecordCheck_SingleHealthy_Records()
    {
        // Act
        _tracker.RecordCheck("TestService", true, DateTime.UtcNow);
        var stats = _tracker.GetStats("TestService", TimeSpan.FromDays(1));

        // Assert
        stats.Should().NotBeNull();
        stats.TotalChecks.Should().Be(1);
        stats.SuccessfulChecks.Should().Be(1);
        stats.FailedChecks.Should().Be(0);
    }

    [Fact]
    public void RecordCheck_SingleUnhealthy_Records()
    {
        // Act
        _tracker.RecordCheck("TestService", false, DateTime.UtcNow);
        var stats = _tracker.GetStats("TestService", TimeSpan.FromDays(1));

        // Assert
        stats.TotalChecks.Should().Be(1);
        stats.SuccessfulChecks.Should().Be(0);
        stats.FailedChecks.Should().Be(1);
    }

    [Fact]
    public void GetStats_UnknownService_ReturnsDefaults()
    {
        // Act
        var stats = _tracker.GetStats("UnknownService", TimeSpan.FromDays(1));

        // Assert
        stats.Should().NotBeNull();
        stats.UptimePercent.Should().Be(100);
        stats.TotalChecks.Should().Be(0);
    }

    [Fact]
    public void GetStats_MixedResults_CalculatesCorrectUptime()
    {
        // Arrange - 8 healthy, 2 unhealthy = 80%
        for (int i = 0; i < 8; i++)
            _tracker.RecordCheck("TestService", true, DateTime.UtcNow.AddMinutes(-i));
        for (int i = 0; i < 2; i++)
            _tracker.RecordCheck("TestService", false, DateTime.UtcNow.AddMinutes(-10 - i));

        // Act
        var stats = _tracker.GetStats("TestService", TimeSpan.FromDays(1));

        // Assert
        stats.TotalChecks.Should().Be(10);
        stats.SuccessfulChecks.Should().Be(8);
        stats.FailedChecks.Should().Be(2);
        stats.UptimePercent.Should().Be(80);
    }

    [Fact]
    public void GetStats_AllHealthy_Returns100Percent()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
            _tracker.RecordCheck("TestService", true, DateTime.UtcNow.AddMinutes(-i));

        // Act
        var stats = _tracker.GetStats("TestService", TimeSpan.FromDays(1));

        // Assert
        stats.UptimePercent.Should().Be(100);
    }

    [Fact]
    public void GetStats_AllUnhealthy_Returns0Percent()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
            _tracker.RecordCheck("TestService", false, DateTime.UtcNow.AddMinutes(-i));

        // Act
        var stats = _tracker.GetStats("TestService", TimeSpan.FromDays(1));

        // Assert
        stats.UptimePercent.Should().Be(0);
    }

    [Fact]
    public void GetAllStats_MultipleServices_ReturnsAll()
    {
        // Arrange
        _tracker.RecordCheck("Service1", true, DateTime.UtcNow);
        _tracker.RecordCheck("Service2", true, DateTime.UtcNow);
        _tracker.RecordCheck("Service3", false, DateTime.UtcNow);

        // Act
        var allStats = _tracker.GetAllStats(TimeSpan.FromDays(1));

        // Assert
        allStats.Should().HaveCount(3);
        allStats.Should().ContainKey("Service1");
        allStats.Should().ContainKey("Service2");
        allStats.Should().ContainKey("Service3");
    }

    [Fact]
    public void GetSlaSummary_ReturnsCorrectSummary()
    {
        // Arrange
        _tracker.RecordCheck("Service1", true, DateTime.UtcNow);
        _tracker.RecordCheck("Service2", true, DateTime.UtcNow);
        _tracker.RecordCheck("Service2", false, DateTime.UtcNow.AddMinutes(-1));

        // Act
        var summary = _tracker.GetSlaSummary();

        // Assert
        summary.Should().NotBeNull();
        summary.TotalServices.Should().Be(2);
    }

    [Fact]
    public void GetStats_OutsidePeriod_NotIncluded()
    {
        // Arrange - check from 2 days ago
        _tracker.RecordCheck("TestService", false, DateTime.UtcNow.AddDays(-2));
        _tracker.RecordCheck("TestService", true, DateTime.UtcNow);

        // Act - only look at last day
        var stats = _tracker.GetStats("TestService", TimeSpan.FromDays(1));

        // Assert
        stats.TotalChecks.Should().Be(1);
        stats.SuccessfulChecks.Should().Be(1);
    }

    [Fact]
    public void GetStats_LastCheck_IsRecorded()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _tracker.RecordCheck("TestService", true, now);

        // Act
        var stats = _tracker.GetStats("TestService", TimeSpan.FromDays(1));

        // Assert
        stats.LastCheck.Should().NotBeNull();
        stats.LastCheck!.Value.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetStats_LastFailure_IsRecorded()
    {
        // Arrange
        var failureTime = DateTime.UtcNow.AddMinutes(-5);
        _tracker.RecordCheck("TestService", false, failureTime);
        _tracker.RecordCheck("TestService", true, DateTime.UtcNow);

        // Act
        var stats = _tracker.GetStats("TestService", TimeSpan.FromDays(1));

        // Assert
        stats.LastFailure.Should().NotBeNull();
        stats.LastFailure!.Value.Should().BeCloseTo(failureTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetSlaSummary_ServicesBelow99_CountedCorrectly()
    {
        // Arrange - Service1 at 50%, Service2 at 100%
        for (int i = 0; i < 50; i++)
        {
            _tracker.RecordCheck("Service1", true, DateTime.UtcNow.AddMinutes(-i));
            _tracker.RecordCheck("Service1", false, DateTime.UtcNow.AddMinutes(-100 - i));
        }
        for (int i = 0; i < 100; i++)
        {
            _tracker.RecordCheck("Service2", true, DateTime.UtcNow.AddMinutes(-i));
        }

        // Act
        var summary = _tracker.GetSlaSummary();

        // Assert
        summary.ServicesBelow99Percent.Should().BeGreaterOrEqualTo(1);
    }
}

