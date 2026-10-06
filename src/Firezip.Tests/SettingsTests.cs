using Firezip.Core.Models;
using Firezip.Infrastructure.Settings;
using Xunit;

namespace Firezip.Tests;

public class SettingsTests
{
    [Fact]
    public async Task SettingsService_SavesAndLoads_RetainsValues()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "firezip_test_settings_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var service = new SettingsService(tempFile);
            service.ThemeMode = "Dark";
            service.DefaultArchiveFormat = ArchiveFormat.SevenZip;
            service.DefaultCompressionLevel = CompressionLevel.Ultra;
            service.ConfirmBeforeOverwriting = false;
            await service.SaveAsync();

            var reloadedService = new SettingsService(tempFile);
            await reloadedService.LoadAsync();

            Assert.Equal("Dark", reloadedService.ThemeMode);
            Assert.Equal(ArchiveFormat.SevenZip, reloadedService.DefaultArchiveFormat);
            Assert.Equal(CompressionLevel.Ultra, reloadedService.DefaultCompressionLevel);
            Assert.False(reloadedService.ConfirmBeforeOverwriting);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task SettingsService_SavesAndLoads_IndividualContextMenuCommands()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "firezip_test_context_menu_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var service = new SettingsService(tempFile)
            {
                EnableContextMenu = true,
                EnabledContextMenuCommands = ["OpenWith", "ExtractTo", "CompressTo7z"]
            };
            await service.SaveAsync();

            var reloadedService = new SettingsService(tempFile);
            await reloadedService.LoadAsync();

            Assert.Equal(new[] { "OpenWith", "ExtractTo", "CompressTo7z" }, reloadedService.EnabledContextMenuCommands);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SettingsService_SavesAndLoads_TaskWindowAndCascadingSettings()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "firezip_test_tasks_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var service = new SettingsService(tempFile)
            {
                AutoCloseTaskProgressWindow = false,
                NotifyOnTaskCompletion = false,
                UseCascadingContextMenu = true
            };
            await service.SaveAsync();

            var reloadedService = new SettingsService(tempFile);
            await reloadedService.LoadAsync();

            Assert.False(reloadedService.AutoCloseTaskProgressWindow);
            Assert.False(reloadedService.NotifyOnTaskCompletion);
            Assert.True(reloadedService.UseCascadingContextMenu);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
