using Emistr.Watchdog.Services;
using FluentAssertions;
using Xunit;

namespace Emistr.Watchdog.Tests.Services;

public class RuntimeConfigurationServiceTests
{
    [Fact]
    public void GetEffectiveEnabled_WhenNoOverride_ShouldReturnConfigValue()
    {
        // Arrange
        var service = RuntimeConfigurationService.Instance;
        service.ClearOverrides("test-service-enabled-1");
        
        // Act
        var result = service.GetEffectiveEnabled("test-service-enabled-1", true);
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void GetEffectiveEnabled_WhenOverrideSet_ShouldReturnOverrideValue()
    {
        // Arrange
        var service = RuntimeConfigurationService.Instance;
        service.SetEnabled("test-service-enabled-2", false);
        
        // Act
        var result = service.GetEffectiveEnabled("test-service-enabled-2", true);
        
        // Assert
        result.Should().BeFalse();
        
        // Cleanup
        service.ClearOverrides("test-service-enabled-2");
    }
    
    [Fact]
    public void GetEffectivePrioritized_WhenNoOverride_ShouldReturnConfigValue()
    {
        // Arrange
        var service = RuntimeConfigurationService.Instance;
        service.ClearOverrides("test-service-priority-1");
        
        // Act
        var result = service.GetEffectivePrioritized("test-service-priority-1", false);
        
        // Assert
        result.Should().BeFalse();
    }
    
    [Fact]
    public void GetEffectivePrioritized_WhenOverrideSet_ShouldReturnOverrideValue()
    {
        // Arrange
        var service = RuntimeConfigurationService.Instance;
        service.SetPrioritized("test-service-priority-2", true);
        
        // Act
        var result = service.GetEffectivePrioritized("test-service-priority-2", false);
        
        // Assert
        result.Should().BeTrue();
        
        // Cleanup
        service.ClearOverrides("test-service-priority-2");
    }
    
    [Fact]
    public void ClearOverrides_ShouldRemoveAllOverridesForService()
    {
        // Arrange
        var service = RuntimeConfigurationService.Instance;
        service.SetEnabled("test-clear-service", false);
        service.SetPrioritized("test-clear-service", true);
        
        // Act
        service.ClearOverrides("test-clear-service");
        
        // Assert
        service.GetEffectiveEnabled("test-clear-service", true).Should().BeTrue();
        service.GetEffectivePrioritized("test-clear-service", false).Should().BeFalse();
    }
    
    [Fact]
    public void GetAllOverrides_ShouldReturnAllSetOverrides()
    {
        // Arrange
        var service = RuntimeConfigurationService.Instance;
        service.ClearOverrides("test-all-1");
        service.ClearOverrides("test-all-2");
        service.SetEnabled("test-all-1", true);
        service.SetPrioritized("test-all-2", true);
        
        // Act
        var overrides = service.GetAllOverrides();
        
        // Assert
        overrides.Should().ContainKey("test-all-1");
        overrides.Should().ContainKey("test-all-2");
        
        // Cleanup
        service.ClearOverrides("test-all-1");
        service.ClearOverrides("test-all-2");
    }
    
    [Fact]
    public void Service_ShouldBeCaseInsensitive()
    {
        // Arrange
        var service = RuntimeConfigurationService.Instance;
        service.SetEnabled("CaSe-TeSt", true);
        
        // Act & Assert
        service.GetEffectiveEnabled("case-test", false).Should().BeTrue();
        service.GetEffectiveEnabled("CASE-TEST", false).Should().BeTrue();
        
        // Cleanup
        service.ClearOverrides("case-test");
    }
    
    [Fact]
    public void Instance_ShouldReturnSameSingleton()
    {
        // Act
        var instance1 = RuntimeConfigurationService.Instance;
        var instance2 = RuntimeConfigurationService.Instance;
        
        // Assert
        instance1.Should().BeSameAs(instance2);
    }
}

