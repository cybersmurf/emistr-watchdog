using Emistr.Watchdog.Configuration;
using FluentAssertions;

namespace Emistr.Watchdog.Tests.Configuration;

public class WatchdogOptionsTests
{
    [Fact]
    public void WatchdogOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new WatchdogOptions();

        // Assert
        options.CheckIntervalSeconds.Should().Be(30);
        options.Services.Should().NotBeNull();
    }

    [Fact]
    public void MariaDbOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new MariaDbOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.TimeoutSeconds.Should().Be(10);
        options.CriticalAfterFailures.Should().Be(3);
        options.ConnectionString.Should().BeEmpty();
    }

    [Fact]
    public void HttpServiceOptions_ShouldHaveDefaultStatusCode200()
    {
        // Act
        var options = new HttpServiceOptions();

        // Assert
        options.ExpectedStatusCodes.Should().ContainSingle().Which.Should().Be(200);
    }

    [Fact]
    public void TelnetServiceOptions_ShouldHaveDefaultHost()
    {
        // Act
        var options = new TelnetServiceOptions();

        // Assert
        options.Host.Should().Be("localhost");
        options.Port.Should().Be(0);
    }

    [Fact]
    public void EmailOptions_ShouldHaveDefaultCooldown()
    {
        // Act
        var options = new EmailOptions();

        // Assert
        options.CooldownMinutes.Should().Be(15);
        options.SmtpPort.Should().Be(587);
        options.UseSsl.Should().BeTrue();
    }

    [Fact]
    public void ServicesOptions_ShouldHaveEmptyCustomCollections()
    {
        // Act
        var options = new ServicesOptions();

        // Assert
        options.CustomHttpServices.Should().BeEmpty();
        options.CustomTelnetServices.Should().BeEmpty();
    }
}
