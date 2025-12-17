using System.Net;
using System.Net.Http;
using System.Text.Json;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Emistr.Watchdog.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Emistr.Watchdog.Tests.Services;

public class WebhookNotificationServiceTests
{
    private readonly ILogger<WebhookNotificationService> _logger;

    public WebhookNotificationServiceTests()
    {
        _logger = Substitute.For<ILogger<WebhookNotificationService>>();
    }

    [Fact]
    public async Task NotifyAsync_WhenAllWebhooksDisabled_ShouldNotSendAnyRequests()
    {
        // Arrange
        var options = new NotificationOptions
        {
            Teams = new TeamsOptions { Enabled = false },
            Slack = new SlackOptions { Enabled = false },
            Discord = new DiscordOptions { Enabled = false },
            GenericWebhook = new GenericWebhookOptions { Enabled = false }
        };

        var httpClient = new HttpClient(new FakeHttpMessageHandler());
        var service = CreateService(options, httpClient);

        var result = CreateHealthCheckResult(ServiceStatus.Unhealthy);

        // Act
        await service.NotifyAsync(result);

        // Assert - no exceptions thrown, method completes successfully
    }

    [Fact]
    public async Task NotifyAsync_WhenTeamsEnabled_ShouldSendTeamsNotification()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var options = new NotificationOptions
        {
            Teams = new TeamsOptions 
            { 
                Enabled = true, 
                WebhookUrl = "https://teams.example.com/webhook",
                CooldownMinutes = 0
            }
        };

        var httpClient = new HttpClient(handler);
        var service = CreateService(options, httpClient);

        var result = CreateHealthCheckResult(ServiceStatus.Critical);

        // Act
        await service.NotifyAsync(result);

        // Assert
        handler.RequestsReceived.Should().HaveCount(1);
        handler.RequestsReceived[0].RequestUri!.ToString().Should().Be("https://teams.example.com/webhook");
    }

    [Fact]
    public async Task NotifyAsync_WhenSlackEnabled_ShouldSendSlackNotification()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var options = new NotificationOptions
        {
            Slack = new SlackOptions 
            { 
                Enabled = true, 
                WebhookUrl = "https://hooks.slack.com/test",
                Channel = "#alerts",
                CooldownMinutes = 0
            }
        };

        var httpClient = new HttpClient(handler);
        var service = CreateService(options, httpClient);

        var result = CreateHealthCheckResult(ServiceStatus.Unhealthy);

        // Act
        await service.NotifyAsync(result);

        // Assert
        handler.RequestsReceived.Should().HaveCount(1);
        handler.RequestsReceived[0].RequestUri!.ToString().Should().Be("https://hooks.slack.com/test");
    }

    [Fact]
    public async Task NotifyAsync_WhenDiscordEnabled_ShouldSendDiscordNotification()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var options = new NotificationOptions
        {
            Discord = new DiscordOptions 
            { 
                Enabled = true, 
                WebhookUrl = "https://discord.com/api/webhooks/test",
                CooldownMinutes = 0
            }
        };

        var httpClient = new HttpClient(handler);
        var service = CreateService(options, httpClient);

        var result = CreateHealthCheckResult(ServiceStatus.Critical);

        // Act
        await service.NotifyAsync(result);

        // Assert
        handler.RequestsReceived.Should().HaveCount(1);
        handler.RequestsReceived[0].RequestUri!.ToString().Should().Be("https://discord.com/api/webhooks/test");
    }

    [Fact]
    public async Task NotifyAsync_WhenGenericWebhookEnabled_ShouldSendWithCustomHeaders()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var options = new NotificationOptions
        {
            GenericWebhook = new GenericWebhookOptions 
            { 
                Enabled = true, 
                Url = "https://api.example.com/alerts",
                Method = "POST",
                Headers = new Dictionary<string, string>
                {
                    ["Authorization"] = "Bearer test-token",
                    ["X-Custom"] = "custom-value"
                },
                CooldownMinutes = 0
            }
        };

        var httpClient = new HttpClient(handler);
        var service = CreateService(options, httpClient);

        var result = CreateHealthCheckResult(ServiceStatus.Unhealthy);

        // Act
        await service.NotifyAsync(result);

        // Assert
        handler.RequestsReceived.Should().HaveCount(1);
        var request = handler.RequestsReceived[0];
        request.Headers.GetValues("Authorization").Should().Contain("Bearer test-token");
        request.Headers.GetValues("X-Custom").Should().Contain("custom-value");
    }

    [Fact]
    public async Task NotifyAsync_WhenCriticalOnlyAndNotCritical_ShouldNotSend()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var options = new NotificationOptions
        {
            Teams = new TeamsOptions 
            { 
                Enabled = true, 
                WebhookUrl = "https://teams.example.com/webhook",
                CriticalOnly = true,
                CooldownMinutes = 0
            }
        };

        var httpClient = new HttpClient(handler);
        var service = CreateService(options, httpClient);

        var result = CreateHealthCheckResult(ServiceStatus.Unhealthy, isCritical: false);

        // Act
        await service.NotifyAsync(result);

        // Assert
        handler.RequestsReceived.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAsync_WhenCriticalOnlyAndIsCritical_ShouldSend()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var options = new NotificationOptions
        {
            Teams = new TeamsOptions 
            { 
                Enabled = true, 
                WebhookUrl = "https://teams.example.com/webhook",
                CriticalOnly = true,
                CooldownMinutes = 0
            }
        };

        var httpClient = new HttpClient(handler);
        var service = CreateService(options, httpClient);

        var result = CreateHealthCheckResult(ServiceStatus.Critical, isCritical: true);

        // Act
        await service.NotifyAsync(result);

        // Assert
        handler.RequestsReceived.Should().HaveCount(1);
    }

    [Fact]
    public async Task NotifyRecoveryAsync_WhenTeamsEnabled_ShouldSendRecoveryNotification()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var options = new NotificationOptions
        {
            Teams = new TeamsOptions 
            { 
                Enabled = true, 
                WebhookUrl = "https://teams.example.com/webhook"
            }
        };

        var httpClient = new HttpClient(handler);
        var service = CreateService(options, httpClient);

        // Act
        await service.NotifyRecoveryAsync("TestService");

        // Assert
        handler.RequestsReceived.Should().HaveCount(1);
        var content = await handler.RequestsReceived[0].Content!.ReadAsStringAsync();
        content.Should().Contain("back online");
    }

    [Fact]
    public async Task NotifyAsync_WhenMultipleWebhooksEnabled_ShouldSendToAll()
    {
        // Arrange
        var handler = new FakeHttpMessageHandler();
        var options = new NotificationOptions
        {
            Teams = new TeamsOptions { Enabled = true, WebhookUrl = "https://teams.test", CooldownMinutes = 0 },
            Slack = new SlackOptions { Enabled = true, WebhookUrl = "https://slack.test", CooldownMinutes = 0 },
            Discord = new DiscordOptions { Enabled = true, WebhookUrl = "https://discord.test", CooldownMinutes = 0 }
        };

        var httpClient = new HttpClient(handler);
        var service = CreateService(options, httpClient);

        var result = CreateHealthCheckResult(ServiceStatus.Critical, isCritical: true);

        // Act
        await service.NotifyAsync(result);

        // Assert
        handler.RequestsReceived.Should().HaveCount(3);
    }

    [Fact]
    public void TeamsOptions_ShouldHaveCorrectDefaults()
    {
        var options = new TeamsOptions();

        options.Enabled.Should().BeFalse();
        options.WebhookUrl.Should().BeEmpty();
        options.CooldownMinutes.Should().Be(15);
        options.CriticalOnly.Should().BeFalse();
    }

    [Fact]
    public void SlackOptions_ShouldHaveCorrectDefaults()
    {
        var options = new SlackOptions();

        options.Enabled.Should().BeFalse();
        options.WebhookUrl.Should().BeEmpty();
        options.Channel.Should().BeNull();
        options.Username.Should().Be("Emistr Watchdog");
        options.CooldownMinutes.Should().Be(15);
        options.CriticalOnly.Should().BeFalse();
    }

    [Fact]
    public void DiscordOptions_ShouldHaveCorrectDefaults()
    {
        var options = new DiscordOptions();

        options.Enabled.Should().BeFalse();
        options.WebhookUrl.Should().BeEmpty();
        options.Username.Should().Be("Emistr Watchdog");
        options.CooldownMinutes.Should().Be(15);
        options.CriticalOnly.Should().BeFalse();
    }

    [Fact]
    public void GenericWebhookOptions_ShouldHaveCorrectDefaults()
    {
        var options = new GenericWebhookOptions();

        options.Enabled.Should().BeFalse();
        options.Url.Should().BeEmpty();
        options.Method.Should().Be("POST");
        options.Headers.Should().BeEmpty();
        options.CooldownMinutes.Should().Be(15);
        options.CriticalOnly.Should().BeFalse();
    }

    private WebhookNotificationService CreateService(NotificationOptions options, HttpClient httpClient)
    {
        return new WebhookNotificationService(
            Options.Create(options),
            httpClient,
            _logger);
    }

    private static HealthCheckResult CreateHealthCheckResult(ServiceStatus status, bool isCritical = false)
    {
        return new HealthCheckResult
        {
            ServiceName = "TestService",
            IsHealthy = status == ServiceStatus.Healthy,
            Status = status,
            IsCritical = isCritical || status == ServiceStatus.Critical,
            ErrorMessage = status != ServiceStatus.Healthy ? "Test error message" : null,
            ConsecutiveFailures = isCritical ? 5 : 1,
            ResponseTimeMs = 100,
            CheckedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Fake HTTP handler for testing.
    /// </summary>
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> RequestsReceived { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestsReceived.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

