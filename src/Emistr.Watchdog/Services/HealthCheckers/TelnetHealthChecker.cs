using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Models;

namespace Emistr.Watchdog.Services.HealthCheckers;

/// <summary>
/// Health checker for Telnet-based services (PracantD and similar).
/// </summary>
public sealed class TelnetHealthChecker : HealthCheckerBase
{
    private readonly TelnetServiceOptions _options;
    private readonly string _serviceName;

    public TelnetHealthChecker(
        string serviceName,
        TelnetServiceOptions options,
        ILogger<TelnetHealthChecker> logger,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
        : base(logger, serviceController, restartTracker)
    {
        _serviceName = serviceName;
        _options = options;
    }

    public override string ServiceName => _serviceName;
    public override string DisplayName => _options.DisplayName ?? _serviceName;
    protected override bool ConfigEnabled => _options.Enabled;
    public override int CriticalThreshold => _options.CriticalAfterFailures;
    protected override bool ConfigPrioritized => _options.Prioritized;

    protected override async Task<HealthCheckResult> PerformCheckAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            return HealthCheckResult.Unhealthy(ServiceName, "Host is not configured");
        }

        if (_options.Port <= 0)
        {
            return HealthCheckResult.Unhealthy(ServiceName, "Port is not configured");
        }

        var sw = Stopwatch.StartNew();

        using var client = new TcpClient();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            // Connect to the service
            await client.ConnectAsync(_options.Host, _options.Port, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred (not external cancellation)
            return HealthCheckResult.Unhealthy(
                ServiceName,
                $"Connection timeout after {_options.TimeoutSeconds}s to {_options.Host}:{_options.Port}");
        }
        catch (SocketException ex)
        {
            return HealthCheckResult.Unhealthy(
                ServiceName,
                $"Socket error connecting to {_options.Host}:{_options.Port}: {ex.Message}",
                ex);
        }

        if (!client.Connected)
        {
            return HealthCheckResult.Unhealthy(
                ServiceName,
                $"Failed to connect to {_options.Host}:{_options.Port}");
        }

        await using var stream = client.GetStream();
        string? response = null;

        // If ConnectionOnly mode, just verify we connected successfully
        if (_options.ConnectionOnly)
        {
            sw.Stop();
            Logger.LogDebug(
                "{ServiceName} telnet connection check completed in {ElapsedMs}ms",
                ServiceName,
                sw.ElapsedMilliseconds);

            return HealthCheckResult.Healthy(ServiceName, sw.ElapsedMilliseconds);
        }

        // Send raw command if configured (for binary protocols like PracantD)
        if (!string.IsNullOrWhiteSpace(_options.RawCommand))
        {
            var commandBytes = ParseHexCommand(_options.RawCommand);
            await stream.WriteAsync(commandBytes, cts.Token);
            await stream.FlushAsync(cts.Token);

            // Read response
            response = await ReadResponseAsync(stream, cts.Token);
        }
        // Send text command if configured
        else if (!string.IsNullOrWhiteSpace(_options.Command))
        {
            var commandBytes = Encoding.ASCII.GetBytes(_options.Command + "\r\n");
            await stream.WriteAsync(commandBytes, cts.Token);
            await stream.FlushAsync(cts.Token);

            // Read response
            response = await ReadResponseAsync(stream, cts.Token);
        }
        else if (!string.IsNullOrWhiteSpace(_options.ExpectedResponse))
        {
            // Just read initial response/banner
            response = await ReadResponseAsync(stream, cts.Token);
        }

        sw.Stop();

        // Validate expected response if configured
        if (!string.IsNullOrWhiteSpace(_options.ExpectedResponse))
        {
            if (response is null || !response.Contains(_options.ExpectedResponse, StringComparison.OrdinalIgnoreCase))
            {
                return HealthCheckResult.Unhealthy(
                    ServiceName,
                    $"Response does not contain expected content: '{_options.ExpectedResponse}'. Actual: '{response ?? "(empty)"}'");
            }
        }

        Logger.LogDebug(
            "{ServiceName} telnet health check completed in {ElapsedMs}ms",
            ServiceName,
            sw.ElapsedMilliseconds);

        return HealthCheckResult.Healthy(ServiceName, sw.ElapsedMilliseconds) with
        {
            Details = response != null
                ? new Dictionary<string, object> { ["response"] = response.Trim() }
                : []
        };
    }

        private async Task<string> ReadResponseAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024];
            var responseBuilder = new StringBuilder();

            // Set read timeout from configuration
            stream.ReadTimeout = _options.ReadTimeoutMs;

            try
            {
                // Read available data (may be partial)
                while (stream.DataAvailable || responseBuilder.Length == 0)
                {
                    var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0)
                        break;

                    responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                    // Small delay to allow more data to arrive
                    if (!stream.DataAvailable)
                    {
                        await Task.Delay(100, cancellationToken);
                        if (!stream.DataAvailable)
                            break;
                    }
                }
            }
            catch (IOException ex)
            {
                Logger.LogDebug(ex, "Read timeout or error while reading response from {ServiceName}", ServiceName);
            }

            return responseBuilder.ToString();
        }

        /// <summary>
        /// Parses a hex string command into bytes.
        /// Format: "01" for single byte 0x01, "010A" for two bytes, etc.
        /// Optionally adds \n (newline) at the end based on configuration.
        /// </summary>
        private byte[] ParseHexCommand(string hexCommand)
        {
            // Remove any spaces or dashes
            hexCommand = hexCommand.Replace(" ", "").Replace("-", "");

            var bytes = new List<byte>();

            for (var i = 0; i < hexCommand.Length; i += 2)
            {
                if (i + 1 < hexCommand.Length)
                {
                    bytes.Add(Convert.ToByte(hexCommand.Substring(i, 2), 16));
                }
                else
                {
                    // Single char at end - treat as single hex digit
                    bytes.Add(Convert.ToByte(hexCommand.Substring(i, 1), 16));
                }
            }

            // Add newline at the end if configured (default: true for PracantD compatibility)
            if (_options.AppendNewlineToRawCommand)
            {
                bytes.Add((byte)'\n');
            }

            return bytes.ToArray();
        }
    }

/// <summary>
/// Factory for creating Telnet health checkers.
/// </summary>
public sealed class TelnetHealthCheckerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceController? _serviceController;
    private readonly ServiceRestartTracker? _restartTracker;

    public TelnetHealthCheckerFactory(
        ILoggerFactory loggerFactory,
        IServiceController? serviceController = null,
        ServiceRestartTracker? restartTracker = null)
    {
        _loggerFactory = loggerFactory;
        _serviceController = serviceController;
        _restartTracker = restartTracker;
    }

    public TelnetHealthChecker Create(string serviceName, TelnetServiceOptions options)
    {
        var logger = _loggerFactory.CreateLogger<TelnetHealthChecker>();
        return new TelnetHealthChecker(serviceName, options, logger, _serviceController, _restartTracker);
    }
}
