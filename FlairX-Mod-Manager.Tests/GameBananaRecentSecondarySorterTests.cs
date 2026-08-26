using FlairX_Mod_Manager.Pages;
using FlairX_Mod_Manager.Services;
using System.Text.Json;
using Xunit;

namespace FlairX_Mod_Manager.Tests;

public class GameBananaRecentSecondarySorterTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData((int)GameBananaUpdatedTimeRange.Last30Days, 30)]
    [InlineData((int)GameBananaUpdatedTimeRange.Last90Days, 90)]
    [InlineData((int)GameBananaUpdatedTimeRange.Last180Days, 180)]
    public void GetCutoffUnixSeconds_UsesSelectedRange(
        int rangeValue,
        int expectedDays)
    {
        var range = (GameBananaUpdatedTimeRange)rangeValue;

        Assert.Equal(
            Now.AddDays(-expectedDays).ToUnixTimeSeconds(),
            GameBananaRecentSecondarySorter.GetCutoffUnixSeconds(range, Now));
    }

    [Fact]
    public void GetCutoffUnixSeconds_UnlimitedHasNoCutoff()
    {
        Assert.Null(GameBananaRecentSecondarySorter.GetCutoffUnixSeconds(
            GameBananaUpdatedTimeRange.Unlimited,
            Now));
    }

    [Fact]
    public void AddPage_KeepsCutoffBoundaryAndStopsAfterOlderRecord()
    {
        var cutoff = Now.AddDays(-90).ToUnixTimeSeconds();
        var collector = new GameBananaRecentWindowCollector(
            GameBananaUpdatedTimeRange.Last90Days,
            Now);

        var first = collector.AddPage(
            [Record(1, updated: cutoff + 2), Record(2, updated: cutoff)],
            sourceComplete: false);
        var second = collector.AddPage(
            [Record(3, updated: cutoff - 1)],
            sourceComplete: false);

        Assert.True(first.ShouldContinue);
        Assert.False(second.ShouldContinue);
        Assert.Equal(GameBananaRecentStopReason.CutoffReached, second.StopReason);
        Assert.Equal([1, 2], collector.Records.Select(record => record.Id));
    }

    [Fact]
    public void AddPage_DeduplicatesAcrossPagesAndContinuesPastOneHundred()
    {
        var collector = new GameBananaRecentWindowCollector(
            GameBananaUpdatedTimeRange.Unlimited,
            Now);

        Assert.True(collector.AddPage(
            Enumerable.Range(1, 100).Select(id => Record(id, updated: id)).ToArray(),
            sourceComplete: false).ShouldContinue);
        Assert.True(collector.AddPage(
            Enumerable.Range(100, 51).Select(id => Record(id, updated: id)).ToArray(),
            sourceComplete: false).ShouldContinue);

        Assert.Equal(150, collector.Records.Count);
        Assert.Equal(150, collector.Records.Select(record => record.Id).Distinct().Count());
    }

    [Fact]
    public void AddPage_StopsOnEmptyPage()
    {
        var collector = new GameBananaRecentWindowCollector(
            GameBananaUpdatedTimeRange.Unlimited,
            Now);

        var result = collector.AddPage([], sourceComplete: false);

        Assert.False(result.ShouldContinue);
        Assert.Equal(GameBananaRecentStopReason.EmptyPage, result.StopReason);
    }

    [Fact]
    public void AddPage_StopsWhenSourceIsComplete()
    {
        var collector = new GameBananaRecentWindowCollector(
            GameBananaUpdatedTimeRange.Unlimited,
            Now);

        var result = collector.AddPage([Record(1)], sourceComplete: true);

        Assert.False(result.ShouldContinue);
        Assert.Equal(GameBananaRecentStopReason.SourceComplete, result.StopReason);
    }

    [Fact]
    public void AddPage_StopsAfterTenPages()
    {
        var collector = new GameBananaRecentWindowCollector(
            GameBananaUpdatedTimeRange.Unlimited,
            Now);

        GameBananaRecentPageDecision result = default;
        for (var page = 1; page <= GameBananaRecentWindowCollector.MaxPages; page++)
        {
            result = collector.AddPage([Record(page)], sourceComplete: false);
        }

        Assert.False(result.ShouldContinue);
        Assert.Equal(GameBananaRecentStopReason.PageLimit, result.StopReason);
        Assert.Equal(10, collector.PagesProcessed);
    }

    [Fact]
    public void AddPage_ProcessesAtMostFiveHundredRawRecords()
    {
        var collector = new GameBananaRecentWindowCollector(
            GameBananaUpdatedTimeRange.Unlimited,
            Now);

        var result = collector.AddPage(
            Enumerable.Range(1, 600).Select(id => Record(id)).ToArray(),
            sourceComplete: false);

        Assert.False(result.ShouldContinue);
        Assert.Equal(GameBananaRecentStopReason.RecordLimit, result.StopReason);
        Assert.Equal(500, collector.RawRecordsProcessed);
        Assert.Equal(500, collector.Records.Count);
    }

    [Fact]
    public void AddPage_DoesNotUsePublishedTimestampWhenUpdatedTimestampIsMissing()
    {
        var collector = new GameBananaRecentWindowCollector(
            GameBananaUpdatedTimeRange.Last90Days,
            Now);
        var record = Record(1, updated: 0);
        record.DateAdded = Now.ToUnixTimeSeconds();

        var result = collector.AddPage([record], sourceComplete: false);

        Assert.False(result.ShouldContinue);
        Assert.Equal(GameBananaRecentStopReason.CutoffReached, result.StopReason);
        Assert.Empty(collector.Records);
    }

    [Fact]
    public void ModRecord_MapsGameBananaUpdatedTimestampSeparatelyFromPublishedTimestamp()
    {
        var record = JsonSerializer.Deserialize<GameBananaService.ModRecord>(
            """{"_idRow":7,"_tsDateAdded":111,"_tsDateUpdated":222}""")!;

        Assert.Equal(111, record.DateAdded);
        Assert.Equal(222, record.DateUpdated);
    }

    [Theory]
    [InlineData((int)GameBananaRecentSecondarySort.LatestUpdated, 3)]
    [InlineData((int)GameBananaRecentSecondarySort.MostLiked, 2)]
    [InlineData((int)GameBananaRecentSecondarySort.MostDownloaded, 1)]
    [InlineData((int)GameBananaRecentSecondarySort.MostCommented, 2)]
    public void FilterAndOrder_RanksSelectedField(int sortValue, int expectedFirstId)
    {
        var records = new[]
        {
            Record(1, downloads: 30, likes: 1, comments: 1, updated: 100),
            Record(2, downloads: 20, likes: 30, comments: 30, updated: 200),
            Record(3, downloads: 10, likes: 20, comments: 20, updated: 300)
        };

        var result = GameBananaRecentSecondarySorter.FilterAndOrder(
            records,
            _ => true,
            (GameBananaRecentSecondarySort)sortValue);

        Assert.Equal(expectedFirstId, result[0].Id);
    }

    [Fact]
    public void FilterAndOrder_AppliesContentFilterBeforeReturningOrder()
    {
        var result = GameBananaRecentSecondarySorter.FilterAndOrder(
            [Record(1, downloads: 100), Record(2, downloads: 10)],
            record => record.Id != 1,
            GameBananaRecentSecondarySort.MostDownloaded);

        Assert.Equal([2], result.Select(record => record.Id));
    }

    [Fact]
    public void FilterAndOrder_UsesUpdatedDateThenIdForMetricTies()
    {
        var result = GameBananaRecentSecondarySorter.FilterAndOrder(
            [
                Record(1, downloads: 10, updated: 100),
                Record(2, downloads: 10, updated: 200),
                Record(3, downloads: 10, updated: 200)
            ],
            _ => true,
            GameBananaRecentSecondarySort.MostDownloaded);

        Assert.Equal([3, 2, 1], result.Select(record => record.Id));
    }

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
            GameBananaRecentSecondarySort.LatestUpdated)!;

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
            GameBananaRecentSecondarySort.LatestUpdated)!;

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
            GameBananaRecentSecondarySort.LatestUpdated,
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

    [Fact]
    public void GetPostCount_ReadsMetadataFallback()
    {
        var record = new GameBananaService.ModRecord
        {
            Metadata = new Dictionary<string, object>
            {
                ["_nPostCount"] = JsonDocument.Parse("17").RootElement.Clone()
            }
        };

        Assert.Equal(17, record.GetPostCount());
    }

    [Fact]
    public void CreateModViewModel_MapsDownloadAndCommentCounts()
    {
        var record = Record(1, downloads: 42, comments: 7);

        var viewModel = GameBananaBrowserUserControl.CreateModViewModel(
            record,
            installedText: "Installed",
            isInstalled: false);

        Assert.Equal(42, viewModel.DownloadCount);
        Assert.Equal(7, viewModel.CommentCount);
    }

    [Theory]
    [InlineData((int)GameBananaRecentSecondarySort.LatestUpdated, true)]
    [InlineData((int)GameBananaRecentSecondarySort.MostDownloaded, false)]
    [InlineData((int)GameBananaRecentSecondarySort.MostLiked, false)]
    [InlineData((int)GameBananaRecentSecondarySort.MostCommented, false)]
    public void CanLoadMore_AllowsOnlyNone(int sortValue, bool expected)
    {
        var sort = (GameBananaRecentSecondarySort)sortValue;

        Assert.Equal(expected, GameBananaRecentSecondarySorter.CanLoadMore(sort, hasMorePages: true));
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
