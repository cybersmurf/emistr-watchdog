using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Emistr.Watchdog.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Emistr.Watchdog.Tests.Services;

public class MultiProviderEmailServiceTests
{
    private readonly ILogger<MultiProviderEmailService> _logger;
    private readonly IEmailSenderFactory _senderFactory;
    private readonly IEmailSender _mockSender;

    public MultiProviderEmailServiceTests()
    {
        _logger = Substitute.For<ILogger<MultiProviderEmailService>>();
        _senderFactory = Substitute.For<IEmailSenderFactory>();
        _mockSender = Substitute.For<IEmailSender>();
        _senderFactory.Create(Arg.Any<EmailOptions>()).Returns(_mockSender);
    }

    [Fact]
    public async Task NotifyAsync_WhenDisabled_ShouldNotSendEmail()
    {
        // Arrange
        var options = CreateOptions(enabled: false);
        var service = CreateService(options);
        var result = CreateHealthCheckResult(ServiceStatus.Unhealthy);

        // Act
        await service.NotifyAsync(result);

        // Assert
        await _mockSender.DidNotReceive().SendAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_WhenEnabled_ShouldSendEmail()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = CreateService(options);
        var result = CreateHealthCheckResult(ServiceStatus.Unhealthy);

        // Act
        await service.NotifyAsync(result);

        // Assert
        await _mockSender.Received(1).SendAsync(
            Arg.Is<IEnumerable<string>>(r => r.Contains("admin@test.com")),
            Arg.Is<string>(s => s.Contains("TestService")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_WhenCriticalOnlyAndNotCritical_ShouldNotSend()
    {
        // Arrange
        var options = CreateOptions(enabled: true, criticalOnly: true);
        var service = CreateService(options);
        var result = CreateHealthCheckResult(ServiceStatus.Unhealthy, isCritical: false);

        // Act
        await service.NotifyAsync(result);

        // Assert
        await _mockSender.DidNotReceive().SendAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyAsync_WhenCriticalOnlyAndIsCritical_ShouldSend()
    {
        // Arrange
        var options = CreateOptions(enabled: true, criticalOnly: true);
        var service = CreateService(options);
        var result = CreateHealthCheckResult(ServiceStatus.Critical, isCritical: true);

        // Act
        await service.NotifyAsync(result);

        // Assert
        await _mockSender.Received(1).SendAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyRecoveryAsync_WhenEnabled_ShouldSendRecoveryEmail()
    {
        // Arrange
        var options = CreateOptions(enabled: true);
        var service = CreateService(options);

        // Act
        await service.NotifyRecoveryAsync("TestService");

        // Assert
        await _mockSender.Received(1).SendAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Is<string>(s => s.Contains("RECOVERED")),
            Arg.Is<string>(b => b.Contains("back online")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void EmailProvider_ShouldHaveAllExpectedValues()
    {
        // Assert
        Enum.GetValues<EmailProvider>().Should().HaveCount(5);
        Enum.IsDefined(EmailProvider.Smtp).Should().BeTrue();
        Enum.IsDefined(EmailProvider.Microsoft365).Should().BeTrue();
        Enum.IsDefined(EmailProvider.SendGrid).Should().BeTrue();
        Enum.IsDefined(EmailProvider.Mailchimp).Should().BeTrue();
        Enum.IsDefined(EmailProvider.Mailgun).Should().BeTrue();
    }

    [Fact]
    public void EmailOptions_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new EmailOptions();

        // Assert
        options.Enabled.Should().BeFalse();
        options.Provider.Should().Be(EmailProvider.Smtp);
        options.SmtpPort.Should().Be(587);
        options.UseSsl.Should().BeTrue();
        options.FromName.Should().Be("Emistr Watchdog");
        options.CooldownMinutes.Should().Be(15);
        options.CriticalOnly.Should().BeFalse();
    }

    private MultiProviderEmailService CreateService(NotificationOptions options)
    {
        return new MultiProviderEmailService(
            Options.Create(options),
            _senderFactory,
            _logger);
    }

    private static NotificationOptions CreateOptions(bool enabled, bool criticalOnly = false)
    {
        return new NotificationOptions
        {
            Email = new EmailOptions
            {
                Enabled = enabled,
                Provider = EmailProvider.Smtp,
                FromAddress = "watchdog@test.com",
                FromName = "Test Watchdog",
                Recipients = ["admin@test.com"],
                CooldownMinutes = 0,
                CriticalOnly = criticalOnly
            }
        };
    }

    private static HealthCheckResult CreateHealthCheckResult(ServiceStatus status, bool isCritical = false)
    {
        return new HealthCheckResult
        {
            ServiceName = "TestService",
            IsHealthy = status == ServiceStatus.Healthy,
            Status = status,
            IsCritical = isCritical,
            ErrorMessage = status != ServiceStatus.Healthy ? "Test error" : null,
            CheckedAt = DateTime.UtcNow
        };
    }
}

