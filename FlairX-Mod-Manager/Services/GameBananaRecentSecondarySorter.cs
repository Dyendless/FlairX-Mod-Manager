using System;
using System.Collections.Generic;
using System.Linq;

namespace FlairX_Mod_Manager.Services;

internal enum GameBananaRecentSecondarySort
{
    None,
    MostDownloaded,
    MostLiked,
    MostCommented
}

internal sealed record GameBananaRecentPoolResult(
    IReadOnlyList<GameBananaService.ModRecord> Records,
    bool IsPartial);

internal static class GameBananaRecentSecondarySorter
{
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
            : GameBananaRecentSecondarySort.None;
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
            GameBananaRecentSecondarySort.MostCommented => Order(candidates, record => record.PostCount),
            _ => candidates.ToList()
        };

        return new GameBananaRecentPoolResult(ordered, secondPage is null);
    }

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
