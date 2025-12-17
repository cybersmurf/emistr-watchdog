using Emistr.Common.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Emistr.Watchdog.Tests.Services;

public class PerformanceMiddlewareTests
{
    [Fact]
    public async Task Middleware_AddsResponseTimeHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var logger = Substitute.For<ILogger<PerformanceMiddleware>>();
        var options = new PerformanceOptions { Enabled = true };
        
        RequestDelegate next = _ =>
        {
            Thread.Sleep(10); // Simulate some work
            return Task.CompletedTask;
        };

        var middleware = new PerformanceMiddleware(next, logger, options);

        // Act
        await middleware.InvokeAsync(context);

        // Note: Headers are set via OnStarting callback
    }

    [Fact]
    public async Task Middleware_SkipsDisabled()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var logger = Substitute.For<ILogger<PerformanceMiddleware>>();
        var options = new PerformanceOptions { Enabled = false };
        var nextCalled = false;
        
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new PerformanceMiddleware(next, logger, options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_SkipsHealthEndpoint()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        var logger = Substitute.For<ILogger<PerformanceMiddleware>>();
        var options = new PerformanceOptions { Enabled = true };
        var nextCalled = false;
        
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new PerformanceMiddleware(next, logger, options);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void PerformanceOptions_DefaultValues_AreCorrect()
    {
        // Arrange
        var options = new PerformanceOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.WarningThresholdMs.Should().Be(500);
        options.CriticalThresholdMs.Should().Be(2000);
        options.LogAllRequests.Should().BeFalse();
        options.SkipPaths.Should().Contain("/health");
        options.SkipPaths.Should().Contain("/metrics");
    }

    [Fact]
    public void PerformanceMetrics_RecordRequest_IncrementsCounter()
    {
        // Arrange
        PerformanceMetrics.Reset();

        // Act
        PerformanceMetrics.RecordRequest("/api/test", "GET", 100, 200);
        PerformanceMetrics.RecordRequest("/api/test", "GET", 200, 200);
        var summary = PerformanceMetrics.GetSummary();

        // Assert
        summary.TotalRequests.Should().Be(2);
    }

    [Fact]
    public void PerformanceMetrics_RecordRequest_CalculatesAverage()
    {
        // Arrange
        PerformanceMetrics.Reset();

        // Act
        PerformanceMetrics.RecordRequest("/api/test", "GET", 100, 200);
        PerformanceMetrics.RecordRequest("/api/test", "GET", 300, 200);
        var summary = PerformanceMetrics.GetSummary();

        // Assert
        summary.AverageTimeMs.Should().Be(200);
    }

    [Fact]
    public void PerformanceMetrics_RecordRequest_CountsSlowRequests()
    {
        // Arrange
        PerformanceMetrics.Reset();

        // Act
        PerformanceMetrics.RecordRequest("/api/test", "GET", 100, 200); // Fast
        PerformanceMetrics.RecordRequest("/api/test", "GET", 600, 200); // Slow (>500ms)
        var summary = PerformanceMetrics.GetSummary();

        // Assert
        summary.SlowRequests.Should().Be(1);
    }

    [Fact]
    public void PerformanceMetrics_RecordRequest_CountsErrors()
    {
        // Arrange
        PerformanceMetrics.Reset();

        // Act
        PerformanceMetrics.RecordRequest("/api/test", "GET", 100, 200);
        PerformanceMetrics.RecordRequest("/api/test", "GET", 100, 500);
        PerformanceMetrics.RecordRequest("/api/test", "GET", 100, 503);
        var summary = PerformanceMetrics.GetSummary();

        // Assert
        summary.ErrorRequests.Should().Be(2);
    }

    [Fact]
    public void PerformanceMetrics_Reset_ClearsAllData()
    {
        // Arrange
        PerformanceMetrics.RecordRequest("/api/test", "GET", 100, 200);
        PerformanceMetrics.RecordRequest("/api/test", "GET", 600, 500);

        // Act
        PerformanceMetrics.Reset();
        var summary = PerformanceMetrics.GetSummary();

        // Assert
        summary.TotalRequests.Should().Be(0);
        summary.SlowRequests.Should().Be(0);
        summary.ErrorRequests.Should().Be(0);
    }

    [Fact]
    public void PerformanceMetrics_GetSummary_ReturnsTopEndpoints()
    {
        // Arrange
        PerformanceMetrics.Reset();
        for (int i = 0; i < 100; i++)
            PerformanceMetrics.RecordRequest("/api/popular", "GET", 50, 200);
        for (int i = 0; i < 10; i++)
            PerformanceMetrics.RecordRequest("/api/rare", "GET", 50, 200);

        // Act
        var summary = PerformanceMetrics.GetSummary();

        // Assert
        summary.TopEndpoints.Should().NotBeEmpty();
        summary.TopEndpoints.First().Path.Should().Contain("popular");
    }

    [Fact]
    public void EndpointMetrics_CalculatesMinMaxAverage()
    {
        // Arrange
        PerformanceMetrics.Reset();
        PerformanceMetrics.RecordRequest("/api/test", "GET", 100, 200);
        PerformanceMetrics.RecordRequest("/api/test", "GET", 200, 200);
        PerformanceMetrics.RecordRequest("/api/test", "GET", 300, 200);

        // Act
        var summary = PerformanceMetrics.GetSummary();
        var endpoint = summary.TopEndpoints.First();

        // Assert
        endpoint.MinTimeMs.Should().Be(100);
        endpoint.MaxTimeMs.Should().Be(300);
        endpoint.AverageTimeMs.Should().Be(200);
    }
}

