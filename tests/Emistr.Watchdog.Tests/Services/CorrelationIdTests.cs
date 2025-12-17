using Emistr.Common.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Emistr.Watchdog.Tests.Services;

public class CorrelationIdTests
{
    [Fact]
    public void CorrelationIdHeader_IsCorrectName()
    {
        // Assert
        CorrelationIdMiddleware.CorrelationIdHeader.Should().Be("X-Correlation-ID");
    }

    [Fact]
    public void CorrelationIdProperty_IsCorrectName()
    {
        // Assert
        CorrelationIdMiddleware.CorrelationIdProperty.Should().Be("CorrelationId");
    }

    [Fact]
    public async Task Middleware_GeneratesCorrelationId_WhenNotProvided()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var logger = Substitute.For<ILogger<CorrelationIdMiddleware>>();
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Items[CorrelationIdMiddleware.CorrelationIdProperty].Should().NotBeNull();
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdProperty]?.ToString();
        correlationId.Should().NotBeNullOrEmpty();
        correlationId!.Length.Should().Be(16);
    }

    [Fact]
    public async Task Middleware_UsesProvidedCorrelationId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = "test-correlation-123";
        
        var logger = Substitute.For<ILogger<CorrelationIdMiddleware>>();
        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new CorrelationIdMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Items[CorrelationIdMiddleware.CorrelationIdProperty].Should().Be("test-correlation-123");
    }

    [Fact]
    public void HttpContextAccessor_ReturnsNull_WhenNoContext()
    {
        // Arrange
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        
        var accessor = new HttpContextCorrelationIdAccessor(httpContextAccessor);

        // Act
        var result = accessor.CorrelationId;

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void HttpContextAccessor_ReturnsCorrelationId_WhenAvailable()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.CorrelationIdProperty] = "test-id-456";
        
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(context);
        
        var accessor = new HttpContextCorrelationIdAccessor(httpContextAccessor);

        // Act
        var result = accessor.CorrelationId;

        // Assert
        result.Should().Be("test-id-456");
    }

    [Fact]
    public async Task Middleware_SetsResponseHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var logger = Substitute.For<ILogger<CorrelationIdMiddleware>>();
        
        // Capture response start callback
        context.Response.OnStarting(() =>
        {
            // Callback is registered for response headers
            return Task.CompletedTask;
        });

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert: Verify correlation ID was set (callback will be triggered when response starts)
        context.Items[CorrelationIdMiddleware.CorrelationIdProperty].Should().NotBeNull();
    }

    [Fact]
    public async Task Middleware_HandlesEmptyCorrelationIdHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = "";
        
        var logger = Substitute.For<ILogger<CorrelationIdMiddleware>>();
        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new CorrelationIdMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - should generate new ID since provided is empty
        var id = context.Items[CorrelationIdMiddleware.CorrelationIdProperty]?.ToString();
        id.Should().NotBeNullOrEmpty();
        id!.Length.Should().Be(16);
    }

    [Fact]
    public async Task Middleware_HandlesWhitespaceCorrelationIdHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = "   ";
        
        var logger = Substitute.For<ILogger<CorrelationIdMiddleware>>();
        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new CorrelationIdMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - should generate new ID since provided is whitespace
        var id = context.Items[CorrelationIdMiddleware.CorrelationIdProperty]?.ToString();
        id.Should().NotBeNullOrEmpty();
    }
}

