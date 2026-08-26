using System.Xml.Linq;
using System.Text.Json;
using Xunit;

namespace FlairX_Mod_Manager.Tests;

public class GameBananaBrowserXamlTests
{
    [Fact]
    public void CharacterFilter_IsCreatedAfterXamlInitialization()
    {
        var xamlPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml");
        var document = XDocument.Load(xamlPath);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.DoesNotContain(
            document.Descendants(),
            element => (string?)element.Attribute(xaml + "Name") == "CharacterFilterComboBox");
        Assert.DoesNotContain(
            document.Descendants(),
            element => (string?)element.Attribute(xaml + "Name") == "SecondarySortComboBox");
        Assert.DoesNotContain(
            document.Descendants(),
            element => (string?)element.Attribute(xaml + "Name") == "TimeRangeComboBox");
        Assert.Contains(
            document.Descendants(),
            element => (string?)element.Attribute(xaml + "Name") == "FiltersToolbarGrid");

        var codeBehindPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml.cs");
        var codeBehind = File.ReadAllText(codeBehindPath);

        Assert.Contains("CreateCharacterFilterComboBox();", codeBehind);
        Assert.Contains("private void CreateCharacterFilterComboBox()", codeBehind);
        Assert.Contains("CreateRecentWindowComboBoxes();", codeBehind);
        Assert.Contains("private void CreateRecentWindowComboBoxes()", codeBehind);
        Assert.DoesNotContain("CategoryFilterComboBox.Parent", codeBehind);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh-CN")]
    [InlineData("zh-TW")]
    public void RecentWindowTranslations_AreComplete(string language)
    {
        var path = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Language",
            "GameBananaBrowser",
            $"{language}.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var keys = new[]
        {
            "TimeRange_Header",
            "TimeRange_Last30Days",
            "TimeRange_Last90Days",
            "TimeRange_Last180Days",
            "TimeRange_Unlimited",
            "WindowSort_Header",
            "WindowSort_LatestUpdated",
            "WindowSort_MostLiked",
            "WindowSort_MostDownloaded",
            "WindowSort_MostCommented",
            "RecentWindow_PartialWarning",
            "RecentWindow_CappedWarning"
        };

        foreach (var key in keys)
        {
            Assert.True(root.TryGetProperty(key, out var value), $"Missing {key} in {language}.json");
            Assert.False(string.IsNullOrWhiteSpace(value.GetString()));
        }
    }

    [Fact]
    public void NavigationState_PreservesEligibleSecondarySort()
    {
        var codeBehindPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml.cs");
        var codeBehind = File.ReadAllText(codeBehindPath);

        Assert.Contains("GameBananaRecentSecondarySort SecondarySort", codeBehind);
        Assert.Contains("SecondarySort: _currentSecondarySort", codeBehind);
        Assert.Contains("entry.SecondarySort", codeBehind);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. segments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(segments)}.");
    }
}
