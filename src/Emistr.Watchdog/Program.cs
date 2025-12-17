using Emistr.Watchdog.Configuration;
using Emistr.Watchdog.Dashboard;
using Emistr.Watchdog.Services;
using Emistr.Watchdog.Services.HealthCheckers;
using Emistr.Common.Middleware;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Serilog;
using ServiceConfig = Emistr.Watchdog.Configuration.ServiceConfig;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Emistr Watchdog service");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Bind configuration
    builder.Services.Configure<WatchdogOptions>(
        builder.Configuration.GetSection(WatchdogOptions.SectionName));
    builder.Services.Configure<NotificationOptions>(
        builder.Configuration.GetSection(NotificationOptions.SectionName));
    builder.Services.Configure<DashboardOptions>(
        builder.Configuration.GetSection(DashboardOptions.SectionName));
    builder.Services.Configure<MaintenanceWindowOptions>(
        builder.Configuration.GetSection("MaintenanceWindows"));
    builder.Services.Configure<EscalationOptions>(
        builder.Configuration.GetSection("Escalation"));
    builder.Services.Configure<RecoveryActionOptions>(
        builder.Configuration.GetSection("Recovery"));

    // Configuration validators - validate at startup
    builder.Services.AddSingleton<IValidateOptions<WatchdogOptions>, WatchdogOptionsValidator>();
    builder.Services.AddSingleton<IValidateOptions<DashboardOptions>, DashboardOptionsValidator>();
    builder.Services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();

    // Correlation ID for distributed tracing
    builder.Services.AddCorrelationId();

    // Register HTTP client factory
    builder.Services.AddHttpClient();

    // Register uptime tracker for SLA reporting
    builder.Services.AddSingleton<IUptimeTracker, UptimeTracker>();

    // Register escalation tracker
    builder.Services.AddSingleton<IEscalationTracker, EscalationTracker>();

    // Register recovery service
    builder.Services.AddSingleton<IRecoveryService, RecoveryService>();

    // Register service restart capabilities (Windows only for actual service control)
    if (OperatingSystem.IsWindows())
    {
        builder.Services.AddSingleton<IServiceController, WindowsServiceController>();
    }
    else
    {
        // Register a no-op service controller for non-Windows platforms
        builder.Services.AddSingleton<IServiceController, NullServiceController>();
    }
    builder.Services.AddSingleton<ServiceRestartTracker>();

    // Register factories
    builder.Services.AddSingleton<MariaDbHealthCheckerFactory>();
    builder.Services.AddSingleton<HttpHealthCheckerFactory>();
    builder.Services.AddSingleton<TelnetHealthCheckerFactory>();
    builder.Services.AddSingleton<BackgroundServiceHealthCheckerFactory>();
    builder.Services.AddSingleton<PingHealthCheckerFactory>();
    builder.Services.AddSingleton<ScriptHealthCheckerFactory>();

    // Register health checkers
    RegisterHealthCheckers(builder.Services, builder.Configuration);

    // Register notification services
    builder.Services.AddSingleton<IEmailSenderFactory, EmailSenderFactory>();
    builder.Services.AddSingleton<INotificationService, MultiProviderEmailService>();
    builder.Services.AddSingleton<CriticalAlertService>();
    
    // Register webhook notification service with HTTP client and resilience
    builder.Services.AddHttpClient<WebhookNotificationService>()
        .AddWebhookResilienceHandler();
    builder.Services.AddSingleton<WebhookNotificationService>();

    // HTTP client for email services with resilience
    builder.Services.AddHttpClient("EmailService")
        .AddEmailResilienceHandler();

    // Register status tracker (shared state for dashboard)
    builder.Services.AddSingleton<StatusTracker>();

    // Register dashboard notifier
    builder.Services.AddSingleton<IDashboardNotifier, DashboardNotifier>();

    // Register main watchdog service
    builder.Services.AddHostedService<WatchdogService>();

    // Add SignalR
    builder.Services.AddSignalR();

    // Add CORS for development
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Configure as Windows Service if running on Windows
    if (OperatingSystem.IsWindows())
    {
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "Emistr.Watchdog";
        });
    }

    // Configure Kestrel
    var dashboardConfig = builder.Configuration
        .GetSection(DashboardOptions.SectionName)
        .Get<DashboardOptions>() ?? new DashboardOptions();

    if (dashboardConfig.Enabled)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            if (dashboardConfig.UseHttps)
            {
                options.ListenAnyIP(dashboardConfig.Port, listenOptions =>
                {
                    listenOptions.UseHttps();
                });
            }
            else
            {
                options.ListenAnyIP(dashboardConfig.Port);
            }
        });
    }

    var app = builder.Build();

    // Migrate V1 config to V2 if needed
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var env = app.Environment.EnvironmentName;
    var configPath = File.Exists($"appsettings.{env}.json") 
        ? $"appsettings.{env}.json" 
        : "appsettings.json";
    
    if (File.Exists(configPath))
    {
        await ConfigMigration.MigrateIfNeeded(configPath, logger);
    }

    // Configure middleware
    app.UseSerilogRequestLogging();
    app.UseCors();

    // Serve static files (dashboard)
    var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    if (Directory.Exists(wwwrootPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(wwwrootPath)
        });
    }
    else
    {
        // Development fallback
        var devWwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        if (Directory.Exists(devWwwroot))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(devWwwroot)
            });
        }
    }

    // Correlation ID middleware for request tracing (must be before endpoints)
    app.UseCorrelationId();

    // Performance monitoring (must be before endpoints)
    app.UsePerformanceMonitoring();

    // Map SignalR hub
    app.MapHub<DashboardHub>("/hub/dashboard");

    // Map API endpoints
    app.MapDashboardEndpoints();
    app.MapConfigurationEndpoints();
    app.MapConfigurationEndpointsV2();  // V2 endpoints at /api/v2/config

    // Serve index.html for root
    app.MapGet("/", async context =>
    {
        var indexPath = Path.Combine(wwwrootPath, "index.html");
        if (!File.Exists(indexPath))
        {
            indexPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
        }

        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(indexPath);
        }
        else
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Dashboard not found");
        }
    });

    Log.Information("Dashboard available at http://localhost:{Port}", dashboardConfig.Port);

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Watchdog service terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;

static void RegisterHealthCheckers(IServiceCollection services, IConfiguration configuration)
{
    // Try V2 config first (Services as array)
    var servicesArray = configuration.GetSection("Watchdog:Services").Get<List<ServiceConfig>>();
    
    if (servicesArray != null && servicesArray.Count > 0)
    {
        // V2 config - use HealthCheckerFactoryV2
        Log.Information("Using V2 configuration ({Count} services)", servicesArray.Count);
        
        services.AddSingleton<List<ServiceConfig>>(servicesArray);
        
        services.AddSingleton<IEnumerable<IHealthChecker>>(sp =>
        {
            var configs = sp.GetRequiredService<List<ServiceConfig>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var serviceController = sp.GetService<IServiceController>();
            var restartTracker = sp.GetService<ServiceRestartTracker>();
            
            return HealthCheckerFactoryV2.CreateFromConfig(
                configs, httpClientFactory, loggerFactory, serviceController, restartTracker);
        });
        
        // Register each as IHealthChecker for DI
        foreach (var config in servicesArray.Where(s => s.Enabled))
        {
            var configCopy = config; // Capture
            services.AddSingleton<IHealthChecker>(sp =>
            {
                var checkers = sp.GetRequiredService<IEnumerable<IHealthChecker>>();
                return checkers.First(c => c.ServiceName == configCopy.Name);
            });
        }
    }
    else
    {
        // Fallback to V1 config
        RegisterHealthCheckersV1(services, configuration);
    }
}

static void RegisterHealthCheckersV1(IServiceCollection services, IConfiguration configuration)
{
    Log.Information("Using V1 configuration (legacy format)");
    
    var watchdogConfig = configuration.GetSection(WatchdogOptions.SectionName).Get<WatchdogOptions>()
                         ?? new WatchdogOptions();

    // MariaDB checker (default instance)
    if (watchdogConfig.Services.MariaDB.Enabled)
    {
        services.AddSingleton<IHealthChecker>(sp =>
        {
            var factory = sp.GetRequiredService<MariaDbHealthCheckerFactory>();
            var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
            return factory.Create("MariaDB", options.Value.Services.MariaDB);
        });
    }

    // Custom MariaDB services
    foreach (var (name, config) in watchdogConfig.Services.CustomMariaDbServices)
    {
        if (config.Enabled)
        {
            var serviceName = name; // Capture for closure
            services.AddSingleton<IHealthChecker>(sp =>
            {
                var factory = sp.GetRequiredService<MariaDbHealthCheckerFactory>();
                var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
                return factory.Create(serviceName, options.Value.Services.CustomMariaDbServices[serviceName]);
            });
        }
    }

    // License Manager (HTTP)
    if (watchdogConfig.Services.LicenseManager.Enabled)
    {
        services.AddSingleton<IHealthChecker>(sp =>
        {
            var factory = sp.GetRequiredService<HttpHealthCheckerFactory>();
            var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
            return factory.Create("LicenseManager", options.Value.Services.LicenseManager);
        });
    }

    // Apache (HTTP)
    if (watchdogConfig.Services.Apache.Enabled)
    {
        services.AddSingleton<IHealthChecker>(sp =>
        {
            var factory = sp.GetRequiredService<HttpHealthCheckerFactory>();
            var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
            return factory.Create("Apache", options.Value.Services.Apache);
        });
    }

    // PracantD (Telnet)
    if (watchdogConfig.Services.PracantD.Enabled)
    {
        services.AddSingleton<IHealthChecker>(sp =>
        {
            var factory = sp.GetRequiredService<TelnetHealthCheckerFactory>();
            var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
            return factory.Create("PracantD", options.Value.Services.PracantD);
        });
    }

    // Custom HTTP services
    foreach (var (name, config) in watchdogConfig.Services.CustomHttpServices)
    {
        if (config.Enabled)
        {
            var serviceName = name; // Capture for closure
            services.AddSingleton<IHealthChecker>(sp =>
            {
                var factory = sp.GetRequiredService<HttpHealthCheckerFactory>();
                var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
                return factory.Create(serviceName, options.Value.Services.CustomHttpServices[serviceName]);
            });
        }
    }

        // Custom Telnet services
        foreach (var (name, config) in watchdogConfig.Services.CustomTelnetServices)
        {
            if (config.Enabled)
            {
                var serviceName = name; // Capture for closure
                services.AddSingleton<IHealthChecker>(sp =>
                {
                    var factory = sp.GetRequiredService<TelnetHealthCheckerFactory>();
                    var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
                    return factory.Create(serviceName, options.Value.Services.CustomTelnetServices[serviceName]);
                });
            }
        }

        // Background Service (bgs_last_run check)
        if (watchdogConfig.Services.BackgroundService.Enabled)
        {
            services.AddSingleton<IHealthChecker>(sp =>
            {
                var factory = sp.GetRequiredService<BackgroundServiceHealthCheckerFactory>();
                var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
                return factory.Create("BackgroundService", options.Value.Services.BackgroundService);
            });
        }

        // Custom Background services
        foreach (var (name, config) in watchdogConfig.Services.CustomBackgroundServices)
        {
            if (config.Enabled)
            {
                var serviceName = name; // Capture for closure
                services.AddSingleton<IHealthChecker>(sp =>
                {
                    var factory = sp.GetRequiredService<BackgroundServiceHealthCheckerFactory>();
                    var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
                    return factory.Create(serviceName, options.Value.Services.CustomBackgroundServices[serviceName]);
                });
            }
        }

        // Ping services (ICMP)
        foreach (var (name, config) in watchdogConfig.Services.PingServices)
        {
            if (config.Enabled)
            {
                var serviceName = name; // Capture for closure
                services.AddSingleton<IHealthChecker>(sp =>
                {
                    var factory = sp.GetRequiredService<PingHealthCheckerFactory>();
                    var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
                    return factory.Create(serviceName, options.Value.Services.PingServices[serviceName]);
                });
            }
        }

        // Script services (PowerShell, Bash, Python, etc.)
        foreach (var (name, config) in watchdogConfig.Services.ScriptServices)
        {
            if (config.Enabled)
            {
                var serviceName = name; // Capture for closure
                services.AddSingleton<IHealthChecker>(sp =>
                {
                    var factory = sp.GetRequiredService<ScriptHealthCheckerFactory>();
                    var options = sp.GetRequiredService<IOptions<WatchdogOptions>>();
                    return factory.Create(serviceName, options.Value.Services.ScriptServices[serviceName]);
                });
            }
        }
    }
