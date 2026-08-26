using System;
using System.Collections.Generic;
using System.Linq;

namespace FlairX_Mod_Manager.Services;

internal enum GameBananaUpdatedTimeRange
{
    Last30Days,
    Last90Days,
    Last180Days,
    Unlimited
}

internal enum GameBananaRecentSecondarySort
{
    LatestUpdated,
    MostDownloaded,
    MostLiked,
    MostCommented
}

internal sealed record GameBananaRecentPoolResult(
    IReadOnlyList<GameBananaService.ModRecord> Records,
    bool IsPartial);

internal static class GameBananaRecentSecondarySorter
{
    internal static long? GetCutoffUnixSeconds(
        GameBananaUpdatedTimeRange range,
        DateTimeOffset nowUtc) => range switch
    {
        GameBananaUpdatedTimeRange.Last30Days => nowUtc.AddDays(-30).ToUnixTimeSeconds(),
        GameBananaUpdatedTimeRange.Last90Days => nowUtc.AddDays(-90).ToUnixTimeSeconds(),
        GameBananaUpdatedTimeRange.Last180Days => nowUtc.AddDays(-180).ToUnixTimeSeconds(),
        _ => null
    };

    internal static GameBananaRecentSecondarySort Normalize(
        bool isCharacterSkins,
        GameBananaService.CategorySortOrder primarySort,
        string? search,
        GameBananaRecentSecondarySort requested)
    {
        return isCharacterSkins &&
               primarySort == GameBananaService.CategorySortOrder.LatestUpdated &&
               string.IsNullOrWhiteSpace(search)
            ? requested
            : GameBananaRecentSecondarySort.LatestUpdated;
    }

    internal static GameBananaRecentPoolResult? Build(
        IReadOnlyList<GameBananaService.ModRecord>? firstPage,
        IReadOnlyList<GameBananaService.ModRecord>? secondPage,
        GameBananaRecentSecondarySort sort)
    {
        if (firstPage is null)
        {
            return null;
        }

        var candidates = firstPage
            .Concat(secondPage ?? [])
            .DistinctBy(record => record.Id)
            .Take(100);

        var ordered = sort switch
        {
            GameBananaRecentSecondarySort.MostDownloaded => Order(candidates, record => record.GetDownloadCount()),
            GameBananaRecentSecondarySort.MostLiked => Order(candidates, record => record.GetLikeCount()),
            GameBananaRecentSecondarySort.MostCommented => Order(candidates, record => record.GetPostCount()),
            _ => candidates.ToList()
        };

        return new GameBananaRecentPoolResult(ordered, secondPage is null);
    }

    internal static bool CanLoadMore(GameBananaRecentSecondarySort sort, bool hasMorePages)
        => sort == GameBananaRecentSecondarySort.LatestUpdated && hasMorePages;

    private static IReadOnlyList<GameBananaService.ModRecord> Order(
        IEnumerable<GameBananaService.ModRecord> records,
        Func<GameBananaService.ModRecord, int> metric)
    {
        return records
            .OrderByDescending(metric)
            .ThenByDescending(record => record.DateUpdated)
            .ThenByDescending(record => record.Id)
            .ToList();
    }
}
