using FlairX_Mod_Manager.Services;
using Xunit;

namespace FlairX_Mod_Manager.Tests;

public class GameBananaRecentSecondarySorterTests
{
    [Theory]
    [InlineData((int)GameBananaRecentSecondarySort.MostDownloaded, 30, 20, 10)]
    [InlineData((int)GameBananaRecentSecondarySort.MostLiked, 3, 2, 1)]
    [InlineData((int)GameBananaRecentSecondarySort.MostCommented, 300, 200, 100)]
    public void Build_RanksSelectedMetricDescending(
        int sortValue,
        int firstExpected,
        int secondExpected,
        int thirdExpected)
    {
        var sort = (GameBananaRecentSecondarySort)sortValue;
        var records = new[]
        {
            Record(1, downloads: 10, likes: 3, comments: 200),
            Record(2, downloads: 30, likes: 1, comments: 100),
            Record(3, downloads: 20, likes: 2, comments: 300)
        };

        var result = GameBananaRecentSecondarySorter.Build(records, [], sort)!;
        var values = result.Records.Select(record => sort switch
        {
            GameBananaRecentSecondarySort.MostDownloaded => record.DownloadCount,
            GameBananaRecentSecondarySort.MostLiked => record.LikeCount,
            _ => record.PostCount
        });

        Assert.Equal([firstExpected, secondExpected, thirdExpected], values);
    }

    [Fact]
    public void Build_UsesUpdatedDateThenIdForMetricTies()
    {
        var result = GameBananaRecentSecondarySorter.Build(
            [Record(1, downloads: 10, updated: 100), Record(3, downloads: 10, updated: 200)],
            [Record(2, downloads: 10, updated: 200)],
            GameBananaRecentSecondarySort.MostDownloaded)!;

        Assert.Equal([3, 2, 1], result.Records.Select(record => record.Id));
    }

    [Fact]
    public void Build_DeduplicatesAcrossPagesAndCapsAtOneHundred()
    {
        var first = Enumerable.Range(1, 50).Select(id => Record(id)).ToArray();
        var second = Enumerable.Range(50, 60).Select(id => Record(id)).ToArray();

        var result = GameBananaRecentSecondarySorter.Build(
            first,
            second,
            GameBananaRecentSecondarySort.None)!;

        Assert.Equal(100, result.Records.Count);
        Assert.Equal(100, result.Records.Select(record => record.Id).Distinct().Count());
        Assert.Equal(1, result.Records[0].Id);
    }

    [Fact]
    public void Build_PreservesSourceOrderWhenSecondarySortIsNone()
    {
        var result = GameBananaRecentSecondarySorter.Build(
            [Record(8), Record(3)],
            [Record(5)],
            GameBananaRecentSecondarySort.None)!;

        Assert.Equal([8, 3, 5], result.Records.Select(record => record.Id));
    }

    [Fact]
    public void Build_ReturnsPartialFirstPageWhenSecondPageFailed()
    {
        var result = GameBananaRecentSecondarySorter.Build(
            [Record(1)],
            null,
            GameBananaRecentSecondarySort.MostLiked)!;

        Assert.True(result.IsPartial);
        Assert.Single(result.Records);
    }

    [Fact]
    public void Build_EmptySecondPageIsComplete()
    {
        var result = GameBananaRecentSecondarySorter.Build(
            [Record(1)],
            [],
            GameBananaRecentSecondarySort.MostLiked)!;

        Assert.False(result.IsPartial);
    }

    [Fact]
    public void Build_NullFirstPageReturnsNull()
    {
        var result = GameBananaRecentSecondarySorter.Build(
            null,
            [Record(2)],
            GameBananaRecentSecondarySort.MostLiked);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(false, GameBananaService.CategorySortOrder.LatestUpdated, null)]
    [InlineData(true, GameBananaService.CategorySortOrder.MostLiked, null)]
    [InlineData(true, GameBananaService.CategorySortOrder.LatestUpdated, "remielle")]
    public void Normalize_DisablesSecondarySortOutsideEligibleMode(
        bool isCharacterSkins,
        GameBananaService.CategorySortOrder primarySort,
        string? search)
    {
        Assert.Equal(
            GameBananaRecentSecondarySort.None,
            GameBananaRecentSecondarySorter.Normalize(
                isCharacterSkins,
                primarySort,
                search,
                GameBananaRecentSecondarySort.MostLiked));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_PreservesRequestedSortForEligibleMode(string? search)
    {
        Assert.Equal(
            GameBananaRecentSecondarySort.MostCommented,
            GameBananaRecentSecondarySorter.Normalize(
                isCharacterSkins: true,
                GameBananaService.CategorySortOrder.LatestUpdated,
                search,
                GameBananaRecentSecondarySort.MostCommented));
    }

    private static GameBananaService.ModRecord Record(
        int id,
        int downloads = 0,
        int likes = 0,
        int comments = 0,
        long updated = 0)
    {
        return new GameBananaService.ModRecord
        {
            Id = id,
            DownloadCount = downloads,
            LikeCount = likes,
            PostCount = comments,
            DateUpdated = updated
        };
    }
}
