using Emistr.Watchdog.Services;
using FluentAssertions;

namespace Emistr.Watchdog.Tests.Services;

public class ServiceRestartTrackerTests
{
    [Fact]
    public void ShouldAttemptRestart_Initially_ShouldReturnTrue()
    {
        // Arrange
        var tracker = new ServiceRestartTracker();

        // Act
        var shouldRestart = tracker.ShouldAttemptRestart("TestService", maxAttempts: 3);

        // Assert
        shouldRestart.Should().BeTrue();
    }

    [Fact]
    public void RecordRestartAttempt_ShouldIncrementCount()
    {
        // Arrange
        var tracker = new ServiceRestartTracker();

        // Act
        tracker.RecordRestartAttempt("TestService", success: true);
        tracker.RecordRestartAttempt("TestService", success: false);

        // Assert
        var info = tracker.GetRestartInfo("TestService");
        info.Should().NotBeNull();
        info!.Count.Should().Be(2);
    }

    [Fact]
    public void ShouldAttemptRestart_WhenMaxAttemptsReached_ShouldReturnFalse()
    {
        // Arrange
        var tracker = new ServiceRestartTracker();
        tracker.RecordRestartAttempt("TestService", success: false);
        tracker.RecordRestartAttempt("TestService", success: false);
        tracker.RecordRestartAttempt("TestService", success: false);

        // Act
        var shouldRestart = tracker.ShouldAttemptRestart("TestService", maxAttempts: 3);

        // Assert
        shouldRestart.Should().BeFalse();
    }

    [Fact]
    public void GetRestartInfo_ForUnknownService_ShouldReturnNull()
    {
        // Arrange
        var tracker = new ServiceRestartTracker();

        // Act
        var info = tracker.GetRestartInfo("UnknownService");

        // Assert
        info.Should().BeNull();
    }

    [Fact]
    public void ShouldAttemptRestart_DifferentServices_ShouldTrackIndependently()
    {
        // Arrange
        var tracker = new ServiceRestartTracker();
        tracker.RecordRestartAttempt("Service1", success: false);
        tracker.RecordRestartAttempt("Service1", success: false);
        tracker.RecordRestartAttempt("Service1", success: false);

        // Act & Assert
        tracker.ShouldAttemptRestart("Service1", maxAttempts: 3).Should().BeFalse();
        tracker.ShouldAttemptRestart("Service2", maxAttempts: 3).Should().BeTrue();
    }

    [Fact]
    public void GetRestartInfo_AfterRestart_ShouldReturnInfo()
    {
        // Arrange
        var tracker = new ServiceRestartTracker();
        var beforeRestart = DateTime.UtcNow;

        // Act
        tracker.RecordRestartAttempt("TestService", success: true);
        var info = tracker.GetRestartInfo("TestService");

        // Assert
        info.Should().NotBeNull();
        info!.Count.Should().Be(1);
        info.LastSuccess.Should().BeTrue();
        info.LastAttemptTime.Should().BeOnOrAfter(beforeRestart);
    }

    [Fact]
    public void RecordRestartAttempt_ShouldTrackLastSuccess()
    {
        // Arrange
        var tracker = new ServiceRestartTracker();

        // Act
        tracker.RecordRestartAttempt("TestService", success: true);
        tracker.RecordRestartAttempt("TestService", success: false);

        // Assert
        var info = tracker.GetRestartInfo("TestService");
        info!.LastSuccess.Should().BeFalse();
    }

    [Fact]
    public void RecordRestartAttempt_ShouldUpdateLastAttemptTime()
    {
        // Arrange
        var tracker = new ServiceRestartTracker();
        tracker.RecordRestartAttempt("TestService", success: true);
        var firstTime = tracker.GetRestartInfo("TestService")!.LastAttemptTime;
        
        // Small delay
        Thread.Sleep(10);

        // Act
        tracker.RecordRestartAttempt("TestService", success: true);
        var secondTime = tracker.GetRestartInfo("TestService")!.LastAttemptTime;

        // Assert
        secondTime.Should().BeAfter(firstTime);
    }
}

