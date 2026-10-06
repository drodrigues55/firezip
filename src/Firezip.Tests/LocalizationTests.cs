using System.Globalization;
using Firezip.Core.Interfaces;
using Firezip.Infrastructure.Services;
using Firezip.Infrastructure.Settings;
using Xunit;

namespace Firezip.Tests;

public class LocalizationTests
{
    [Fact]
    public void LocalizationService_InitialCulture_DefaultsCorrectly()
    {
        var loc = new LocalizationService("en-US");
        Assert.Equal("en-US", loc.CurrentCulture);
        Assert.Equal("en-US", loc.EffectiveCulture);

        var locPt = new LocalizationService("pt-BR");
        Assert.Equal("pt-BR", locPt.CurrentCulture);
        Assert.Equal("pt-BR", locPt.EffectiveCulture);
    }

    [Fact]
    public void LocalizationService_CultureSwitch_TriggersCultureChangedEvent()
    {
        var loc = new LocalizationService("en-US");
        var eventFired = false;
        loc.CultureChanged += (s, e) => eventFired = true;

        loc.CurrentCulture = "pt-BR";

        Assert.True(eventFired);
        Assert.Equal("pt-BR", loc.CurrentCulture);
        Assert.Equal("pt-BR", loc.EffectiveCulture);
    }

    [Fact]
    public void LocalizationService_EnglishStrings_ReturnCorrectValues()
    {
        var loc = new LocalizationService("en-US");

        Assert.Equal("Firezip", loc["AppName"]);
        Assert.Equal("Open", loc["Action_Open"]);
        Assert.Equal("Extract", loc["Action_Extract"]);
        Assert.Equal("Settings", loc["Action_Settings"]);
        Assert.Equal("Ready", loc["Status_Ready"]);
        Assert.Equal("Password Required", loc["Password_Title"]);
    }

    [Fact]
    public void LocalizationService_PortugueseStrings_ReturnCorrectValues()
    {
        var loc = new LocalizationService("pt-BR");

        Assert.Equal("Firezip", loc["AppName"]);
        Assert.Equal("Abrir", loc["Action_Open"]);
        Assert.Equal("Extrair", loc["Action_Extract"]);
        Assert.Equal("Configurações", loc["Action_Settings"]);
        Assert.Equal("Pronto", loc["Status_Ready"]);
        Assert.Equal("Senha Necessária", loc["Password_Title"]);
    }

    [Fact]
    public void LocalizationService_MissingKey_FallsBackGracefully()
    {
        var loc = new LocalizationService("en-US");

        var result = loc.GetString("NonExistent_Test_Key_12345");
        Assert.Equal("NonExistent_Test_Key_12345", result);

        var emptyResult = loc.GetString(string.Empty);
        Assert.Equal(string.Empty, emptyResult);
    }

    [Fact]
    public void LocalizationService_Formatting_AppliesArgumentsCorrectly()
    {
        var locEn = new LocalizationService("en-US");
        Assert.Equal("15 items", locEn.GetString("Status_ItemsCount", 15));

        var locPt = new LocalizationService("pt-BR");
        Assert.Equal("15 itens", locPt.GetString("Status_ItemsCount", 15));
    }

    [Fact]
    public async Task SettingsService_LanguageProperty_PersistsAndLoads()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"firezip_settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var settings = new SettingsService(tempFile);
            await settings.LoadAsync();
            Assert.Equal("System", settings.Language);

            settings.Language = "pt-BR";
            await settings.SaveAsync();

            var loadedSettings = new SettingsService(tempFile);
            await loadedSettings.LoadAsync();
            Assert.Equal("pt-BR", loadedSettings.Language);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AssemblyVersion_MatchesGeneralAvailability_Version_1_0_X()
    {
        var version = typeof(LocalizationService).Assembly.GetName().Version;
        Assert.NotNull(version);
        Assert.Equal(1, version.Major);
        Assert.Equal(0, version.Minor);
        Assert.True(version.Build >= 0);
    }
}
