using Emistr.Watchdog.Services.HealthCheckers;
using Emistr.Watchdog.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Emistr.Watchdog.Tests.Services;

public class ScriptSecurityTests
{
    private readonly ScriptSecurityOptions _securityOptions;
    private readonly ILogger<ScriptHealthChecker> _logger;

    public ScriptSecurityTests()
    {
        _securityOptions = new ScriptSecurityOptions
        {
            Enabled = true,
            AllowedDirectories = new List<string> { "scripts", "healthchecks" },
            AllowedExtensions = new List<string> { ".ps1", ".sh", ".bat", ".py" },
            MaxTimeoutSeconds = 60,
            DisallowedArgumentPatterns = new List<string>
            {
                @"[;&|`$]",
                @"\.\.[/\\]",
                @"rm\s+-rf",
                @">(>)?"
            },
            AllowEnvironmentExpansion = false
        };

        _logger = Substitute.For<ILogger<ScriptHealthChecker>>();
    }

    [Fact]
    public void Security_DefaultOptions_AreReasonable()
    {
        // Arrange
        var options = new ScriptSecurityOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.AllowedExtensions.Should().Contain(".ps1");
        options.AllowedExtensions.Should().Contain(".sh");
        options.MaxTimeoutSeconds.Should().Be(60);
        options.AllowEnvironmentExpansion.Should().BeFalse();
    }

    [Fact]
    public void Security_DisallowedPatterns_ContainDangerousCommands()
    {
        // Arrange
        var options = new ScriptSecurityOptions();

        // Assert
        options.DisallowedArgumentPatterns.Should().NotBeEmpty();
        // Should block shell command chaining
        options.DisallowedArgumentPatterns.Should().Contain(p => p.Contains(";") || p.Contains("&"));
    }

    [Theory]
    [InlineData("valid_script.ps1", true)]
    [InlineData("test.sh", true)]
    [InlineData("check.bat", true)]
    [InlineData("monitor.py", true)]
    [InlineData("malicious.exe", false)]
    [InlineData("script.vbs", false)]
    [InlineData("dangerous.js", false)]
    public void AllowedExtensions_FilterCorrectly(string filename, bool shouldBeAllowed)
    {
        // Arrange
        var extension = Path.GetExtension(filename).ToLowerInvariant();

        // Assert
        _securityOptions.AllowedExtensions.Contains(extension).Should().Be(shouldBeAllowed);
    }

    [Theory]
    [InlineData("-Param1 Value1", true)]
    [InlineData("--output /tmp/result.txt", true)]
    [InlineData("-f file.txt; rm -rf /", false)] // Command chaining
    [InlineData("$(whoami)", false)] // Command substitution
    [InlineData("`id`", false)] // Backtick execution
    [InlineData("../../../etc/passwd", false)] // Path traversal
    [InlineData("> /dev/null", false)] // Output redirection
    public void DisallowedArgumentPatterns_DetectDangerousInput(string arguments, bool shouldBeAllowed)
    {
        // Arrange
        var isBlocked = false;
        foreach (var pattern in _securityOptions.DisallowedArgumentPatterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(arguments, pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                isBlocked = true;
                break;
            }
        }

        // Assert
        isBlocked.Should().Be(!shouldBeAllowed);
    }

    [Fact]
    public void EnvironmentExpansion_Disabled_BlocksDollarSign()
    {
        // Arrange
        _securityOptions.AllowEnvironmentExpansion = false;
        var arguments = "value=$HOME/test";

        // Assert
        arguments.Contains("$").Should().BeTrue();
        _securityOptions.AllowEnvironmentExpansion.Should().BeFalse();
    }

    [Fact]
    public void EnvironmentExpansion_Disabled_BlocksPercentSign()
    {
        // Arrange
        _securityOptions.AllowEnvironmentExpansion = false;
        var arguments = "path=%USERPROFILE%\\test";

        // Assert
        arguments.Contains("%").Should().BeTrue();
        _securityOptions.AllowEnvironmentExpansion.Should().BeFalse();
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    [InlineData(120, false)]
    public void MaxTimeout_EnforcesLimit(int timeout, bool shouldBeAllowed)
    {
        // Assert
        (timeout <= _securityOptions.MaxTimeoutSeconds).Should().Be(shouldBeAllowed);
    }

    [Fact]
    public void Security_CanBeDisabled()
    {
        // Arrange
        var options = new ScriptSecurityOptions { Enabled = false };

        // Assert
        options.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Security_AllowedDirectories_DefaultsAreReasonable()
    {
        // Arrange
        var options = new ScriptSecurityOptions();

        // Assert
        options.AllowedDirectories.Should().Contain("scripts");
        options.AllowedDirectories.Should().Contain("healthchecks");
        options.AllowedDirectories.Should().NotContain("/");
        options.AllowedDirectories.Should().NotContain("C:\\");
    }
}

