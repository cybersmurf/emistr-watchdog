using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Emistr.Watchdog.Tests.Services;

public class EscalationTrackerTests
{
    private readonly EscalationTracker _tracker;
    private readonly EscalationOptions _options;

    public EscalationTrackerTests()
    {
        _options = new EscalationOptions
        {
            Enabled = true,
            ResetOnRecovery = true,
            Levels = new List<EscalationLevel>
            {
                new() { Level = 1, Name = "L1", DelayMinutes = 0 },
                new() { Level = 2, Name = "L2", DelayMinutes = 15 },
                new() { Level = 3, Name = "L3", DelayMinutes = 60 }
            }
        };

        var optionsMock = Substitute.For<IOptions<EscalationOptions>>();
        optionsMock.Value.Returns(_options);
        var logger = Substitute.For<ILogger<EscalationTracker>>();

        _tracker = new EscalationTracker(optionsMock, logger);
    }

    [Fact]
    public void RecordFailure_FirstFailure_SetsLevel1()
    {
        // Act
        var state = _tracker.RecordFailure("TestService");

        // Assert
        state.Should().NotBeNull();
        state.ServiceName.Should().Be("TestService");
        state.CurrentLevel.Should().Be(1);
        state.FailureCount.Should().Be(1);
    }

    [Fact]
    public void RecordFailure_MultipleFailures_IncreasesFailureCount()
    {
        // Arrange
        _tracker.RecordFailure("TestService");
        _tracker.RecordFailure("TestService");

        // Act
        var state = _tracker.RecordFailure("TestService");

        // Assert
        state.FailureCount.Should().Be(3);
    }

    [Fact]
    public void RecordRecovery_ResetsState()
    {
        // Arrange
        _tracker.RecordFailure("TestService");
        _tracker.RecordFailure("TestService");

        // Act
        _tracker.RecordRecovery("TestService");
        var state = _tracker.GetState("TestService");

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public void GetState_UnknownService_ReturnsNull()
    {
        // Act
        var state = _tracker.GetState("UnknownService");

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public void GetAllActiveEscalations_ReturnsAllActive()
    {
        // Arrange
        _tracker.RecordFailure("Service1");
        _tracker.RecordFailure("Service2");
        _tracker.RecordFailure("Service3");

        // Act
        var escalations = _tracker.GetAllActiveEscalations();

        // Assert
        escalations.Should().HaveCount(3);
        escalations.Should().ContainKey("Service1");
        escalations.Should().ContainKey("Service2");
        escalations.Should().ContainKey("Service3");
    }

    [Fact]
    public void GetAllActiveEscalations_AfterRecovery_ExcludesRecovered()
    {
        // Arrange
        _tracker.RecordFailure("Service1");
        _tracker.RecordFailure("Service2");
        _tracker.RecordRecovery("Service1");

        // Act
        var escalations = _tracker.GetAllActiveEscalations();

        // Assert
        escalations.Should().HaveCount(1);
        escalations.Should().ContainKey("Service2");
    }

    [Fact]
    public void RecordFailure_TracksFirstFailureTime()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var state = _tracker.RecordFailure("TestService");

        // Assert
        state.FirstFailureAt.Should().BeOnOrAfter(before);
        state.FirstFailureAt.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void RecordFailure_UpdatesLastFailureTime()
    {
        // Arrange
        _tracker.RecordFailure("TestService");
        var firstState = _tracker.GetState("TestService");
        
        Thread.Sleep(10); // Small delay

        // Act
        var state = _tracker.RecordFailure("TestService");

        // Assert
        state.LastFailureAt.Should().BeOnOrAfter(firstState!.LastFailureAt);
    }

    [Fact]
    public void RecordFailure_PreservesFirstFailureTime()
    {
        // Arrange
        var firstState = _tracker.RecordFailure("TestService");
        var originalFirstFailure = firstState.FirstFailureAt;

        // Act
        _tracker.RecordFailure("TestService");
        _tracker.RecordFailure("TestService");
        var state = _tracker.GetState("TestService");

        // Assert
        state!.FirstFailureAt.Should().Be(originalFirstFailure);
    }

    [Fact]
    public void CheckEscalation_NewFailure_ReturnsLevel1()
    {
        // Arrange
        _tracker.RecordFailure("TestService");

        // Act
        var level = _tracker.CheckEscalation("TestService");

        // Assert
        level.Should().NotBeNull();
        level!.Level.Should().Be(1);
    }

    [Fact]
    public void IsEscalated_Level1_ReturnsFalse()
    {
        // Arrange
        var state = _tracker.RecordFailure("TestService");

        // Assert
        state.IsEscalated.Should().BeFalse();
    }

    [Fact]
    public void DurationSinceFirstFailure_IsAccurate()
    {
        // Arrange
        _tracker.RecordFailure("TestService");
        Thread.Sleep(50);

        // Act
        var state = _tracker.GetState("TestService");

        // Assert
        state!.DurationSinceFirstFailure.TotalMilliseconds.Should().BeGreaterOrEqualTo(50);
    }
}

