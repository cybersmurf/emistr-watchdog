using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;
using Emistr.Watchdog.Services.HealthCheckers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Emistr.Watchdog.Tests.Services;

public class ElasticsearchHealthCheckerTests
{
    private readonly ILogger<ElasticsearchHealthChecker> _logger;

    public ElasticsearchHealthCheckerTests()
    {
        _logger = Substitute.For<ILogger<ElasticsearchHealthChecker>>();
    }

    [Fact]
    public void ServiceName_ShouldReturnConfiguredName()
    {
        var options = new ElasticsearchOptions { Url = "http://localhost:9200" };
        var checker = new ElasticsearchHealthChecker("TestElasticsearch", options, _logger);
        checker.ServiceName.Should().Be("TestElasticsearch");
    }

    [Fact]
    public void DefaultUrl_ShouldBeLocalhost9200()
    {
        var options = new ElasticsearchOptions();
        options.Url.Should().Be("http://localhost:9200");
    }

    [Fact]
    public void DefaultMinimumHealthStatus_ShouldBeYellow()
    {
        var options = new ElasticsearchOptions();
        options.MinimumHealthStatus.Should().Be("yellow");
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnUnhealthy_WhenUrlEmpty()
    {
        var options = new ElasticsearchOptions { Enabled = true, Url = "" };
        var checker = new ElasticsearchHealthChecker("Elasticsearch", options, _logger);
        var result = await checker.CheckHealthAsync();
        result.IsHealthy.Should().BeFalse();
        result.ErrorMessage.Should().Contain("URL");
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnUnknown_WhenDisabled()
    {
        var options = new ElasticsearchOptions { Enabled = false, Url = "http://localhost:9200" };
        var checker = new ElasticsearchHealthChecker("Elasticsearch", options, _logger);
        var result = await checker.CheckHealthAsync();
        result.Status.Should().Be(ServiceStatus.Unknown);
    }
}

public class ElasticsearchHealthCheckerFactoryTests
{
    [Fact]
    public void Create_ShouldCreateCheckerWithCorrectName()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<ElasticsearchHealthChecker>().Returns(Substitute.For<ILogger<ElasticsearchHealthChecker>>());
        var factory = new ElasticsearchHealthCheckerFactory(loggerFactory);
        var checker = factory.Create("TestES", new ElasticsearchOptions());
        checker.ServiceName.Should().Be("TestES");
    }
}
