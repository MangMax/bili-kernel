// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Media.Core;

internal sealed class RecommendVideoResponse
{
    [JsonPropertyName("items")]
    public IList<RecommendCard> Items { get; set; }
}

internal sealed class RecommendCard
{
    [JsonPropertyName("goto")]
    public string? Goto { get; set; }
    [JsonPropertyName("track_id")]
    public string? TrackId { get; set; }

    [JsonPropertyName("avatar")]
    public RecommendAvatar? Avatar { get; set; }

    [JsonPropertyName("mask")]
    public RecommendMask? Mask { get; set; }

    [JsonPropertyName("is_atten")]
    public bool? IsAttended { get; set; }

    [JsonPropertyName("bottom_rcmd_reason")]
    public string? BottomReason { get; set; }

    [JsonPropertyName("desc")]
    public string? Description { get; set; }

    [JsonPropertyName("multiply_desc")]
    public RecommendDescription? MultipleDescription { get; set; }

    [JsonPropertyName("pubdate")]
    public long? PublishTime { get; set; }

    [JsonPropertyName("card_type")]
    public string? CardType { get; set; }

    [JsonPropertyName("card_goto")]
    public string? CardGoto { get; set; }

    [JsonPropertyName("args")]
    public VideoArgs Args { get; set; }

    [JsonPropertyName("idx")]
    public long Idx { get; set; }

    [JsonPropertyName("param")]
    public string Parameter { get; set; }

    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("player_args")]
    public PlayerArgs PlayerArgs { get; set; }

    [JsonPropertyName("three_point_v2")]
    public IList<OverflowFlyoutItem>? OverflowFlyout { get; set; }

    [JsonPropertyName("cover_left_text_1")]
    public string? PlayCountText { get; set; }

    [JsonPropertyName("cover_left_text_2")]
    public string? DanmakuCountText { get; set; }

    [JsonPropertyName("cover_right_text")]
    public string? DurationText { get; set; }

    [JsonPropertyName("rcmd_reason")]
    public string? RecommendReason { get; set; }
}

internal sealed class VideoArgs
{
    [JsonPropertyName("up_id")]
    public long? UpId { get; set; }

    [JsonPropertyName("up_name")]
    public string? UpName { get; set; }

    [JsonPropertyName("rid")]
    public int? Rid { get; set; }

    [JsonPropertyName("rname")]
    public string? Rname { get; set; }

    [JsonPropertyName("tid")]
    public long? TagId { get; set; }

    [JsonPropertyName("tname")]
    public string? TagName { get; set; }

    [JsonPropertyName("aid")]
    public long? Aid { get; set; }
}

internal sealed class PlayerArgs
{
    [JsonPropertyName("aid")]
    public long? Aid { get; set; }

    [JsonPropertyName("cid")]
    public long? Cid { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("duration")]
    public int? Duration { get; set; }
}

internal sealed class OverflowFlyoutItem
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }

    [JsonPropertyName("reasons")]
    public IList<OverflowReason>? Reasons { get; set; }

    [JsonPropertyName("id")]
    public int? Id { get; set; }
}

internal sealed class OverflowReason
{
    [JsonPropertyName("mid")]
    public long? Mid { get; set; }
    [JsonPropertyName("rid")]
    public long? Rid { get; set; }
    [JsonPropertyName("tag_id")]
    public long? TagId { get; set; }

    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("toast")]
    public string? Toast { get; set; }
}

internal sealed class RecommendAvatar
{
    [JsonPropertyName("cover")]
    public string? Cover { get; set; }
}

internal sealed class RecommendMask
{
    [JsonPropertyName("avatar")]
    public RecommendAvatar? Avatar { get; set; }
}

internal sealed class RecommendDescription
{
    [JsonPropertyName("extra")]
    public string? Extra { get; set; }
}
