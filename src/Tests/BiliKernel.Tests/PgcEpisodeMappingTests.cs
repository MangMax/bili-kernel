// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using Richasy.BiliKernel.Services.Media.Core;
using Richasy.BiliKernel.Services.Media.Core.Models;
using Xunit;

namespace Richasy.BiliKernel.Tests;

/// <summary>
/// 番剧分集映射：序号来源与社区数据的缺失语义.
/// </summary>
public sealed class PgcEpisodeMappingTests
{
    [Fact]
    public void MissingEpIndexFallsBackToTitleNumber()
    {
        var episode = CreateEpisode(title: "199", longTitle: "慕兰之战23");

        var info = episode.ToEpisodeInformation(CreateSeason());

        Assert.Equal(199, info.Index);
    }

    [Fact]
    public void ExplicitEpIndexWinsOverTitle()
    {
        var episode = CreateEpisode(title: "199", longTitle: "慕兰之战23");
        episode.Index = 7;

        var info = episode.ToEpisodeInformation(CreateSeason());

        Assert.Equal(7, info.Index);
    }

    [Fact]
    public void NonNumericTitleLeavesIndexEmpty()
    {
        var episode = CreateEpisode(title: "完整版OP", longTitle: "完整版OP");

        var info = episode.ToEpisodeInformation(CreateSeason());

        Assert.Null(info.Index);
    }

    [Fact]
    public void MissingStatLeavesCommunityInformationEmpty()
    {
        var episode = CreateEpisode(title: "199", longTitle: "慕兰之战23");

        var info = episode.ToEpisodeInformation(CreateSeason());

        Assert.Null(info.CommunityInformation);
    }

    [Fact]
    public void PresentStatCarriesCounts()
    {
        var episode = CreateEpisode(title: "199", longTitle: "慕兰之战23");
        episode.Stat = new PgcEpisodeStat { LikeCount = 42, CoinCount = 7, PlayCount = 1000 };

        var info = episode.ToEpisodeInformation(CreateSeason());

        Assert.NotNull(info.CommunityInformation);
        Assert.Equal(42, info.CommunityInformation!.LikeCount);
        Assert.Equal(7, info.CommunityInformation.CoinCount);
        Assert.Equal(1000, info.CommunityInformation.PlayCount);
    }

    private static PgcDetailResponse CreateSeason() => new() { SeasonId = 28747, Type = 4 };

    private static PgcEpisodeDetail CreateEpisode(string title, string longTitle) => new()
    {
        Id = 3854823,
        Aid = 117570468383201,
        PartId = 43389420598,
        Title = title,
        LongTitle = longTitle,
        Cover = "https://i0.hdslb.com/bfs/archive/example.jpg",
        Duration = 1266000,
        PublishTime = 1794020400,
        BadgeText = "会员",
    };
}
