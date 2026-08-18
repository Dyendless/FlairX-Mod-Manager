using System.Xml.Linq;
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
        Assert.Contains("CreateSecondarySortComboBox();", codeBehind);
        Assert.Contains("private void CreateSecondarySortComboBox()", codeBehind);
        Assert.DoesNotContain("CategoryFilterComboBox.Parent", codeBehind);
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
