// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Media.Core.Models;

/// <summary>
/// 二级分区房间列表响应，App 与 Web 两个接口共用.
/// </summary>
internal sealed class LiveSecondListResponse
{
    /// <summary>
    /// 直播间列表.
    /// </summary>
    [JsonPropertyName("list")]
    public IList<LiveSecondListRoom>? List { get; set; }

    /// <summary>
    /// 总数.
    /// </summary>
    [JsonPropertyName("count")]
    public int? Count { get; set; }

    /// <summary>
    /// 是否还有下一页.
    /// </summary>
    [JsonPropertyName("has_more")]
    public int? HasMore { get; set; }

    /// <summary>
    /// 子标签.
    /// </summary>
    [JsonPropertyName("new_tags")]
    public IList<LiveSecondListTag>? Tags { get; set; }
}

/// <summary>
/// 二级分区房间卡片，兼容 App 与 Web 两套字段名.
/// </summary>
internal sealed class LiveSecondListRoom
{
    /// <summary>
    /// 直播间Id（App）.
    /// </summary>
    [JsonPropertyName("roomid")]
    public long? RoomId { get; set; }

    /// <summary>
    /// 直播间Id（Web 及部分 App 卡片）.
    /// </summary>
    [JsonPropertyName("room_id")]
    public long? RoomIdSnakeCase { get; set; }

    /// <summary>
    /// 直播间Id（旧版卡片）.
    /// </summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>
    /// 用户Id.
    /// </summary>
    [JsonPropertyName("uid")]
    public long? UserId { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    [JsonPropertyName("uname")]
    public string? UserName { get; set; }

    /// <summary>
    /// 用户头像.
    /// </summary>
    [JsonPropertyName("face")]
    public string? UserAvatar { get; set; }

    /// <summary>
    /// 直播间标题.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// 系统封面.
    /// </summary>
    [JsonPropertyName("system_cover")]
    public string? SystemCover { get; set; }

    /// <summary>
    /// 主播封面.
    /// </summary>
    [JsonPropertyName("cover")]
    public string? Cover { get; set; }

    /// <summary>
    /// 观看人数信息.
    /// </summary>
    [JsonPropertyName("watched_show")]
    public LiveSecondListWatchedShow? WatchedShow { get; set; }

    /// <summary>
    /// 在线人数.
    /// </summary>
    [JsonPropertyName("online")]
    public int? Online { get; set; }

    /// <summary>
    /// 子分区Id（App）.
    /// </summary>
    [JsonPropertyName("area_v2_id")]
    public int? AreaV2Id { get; set; }

    /// <summary>
    /// 子分区Id（Web）.
    /// </summary>
    [JsonPropertyName("area_id")]
    public int? AreaId { get; set; }

    /// <summary>
    /// 子分区名（App）.
    /// </summary>
    [JsonPropertyName("area_v2_name")]
    public string? AreaV2Name { get; set; }

    /// <summary>
    /// 子分区名（Web）.
    /// </summary>
    [JsonPropertyName("area_name")]
    public string? AreaName { get; set; }

    /// <summary>
    /// 父分区Id（App）.
    /// </summary>
    [JsonPropertyName("area_v2_parent_id")]
    public int? AreaV2ParentId { get; set; }

    /// <summary>
    /// 父分区Id（Web）.
    /// </summary>
    [JsonPropertyName("parent_area_id")]
    public int? ParentAreaId { get; set; }

    /// <summary>
    /// 实际直播间Id.
    /// </summary>
    public long EffectiveRoomId => RoomId ?? RoomIdSnakeCase ?? Id ?? 0;

    /// <summary>
    /// 实际封面地址，优先系统封面.
    /// </summary>
    public string? EffectiveCover => !string.IsNullOrEmpty(SystemCover) ? SystemCover : Cover;

    /// <summary>
    /// 实际在线人数，优先 <c>watched_show</c>.
    /// </summary>
    public int EffectiveViewerCount => WatchedShow?.Num is int watched && watched > 0 ? watched : Online ?? 0;

    /// <summary>
    /// 实际子分区名.
    /// </summary>
    public string? EffectiveAreaName => !string.IsNullOrEmpty(AreaV2Name) ? AreaV2Name : AreaName;
}

/// <summary>
/// 观看人数信息.
/// </summary>
internal sealed class LiveSecondListWatchedShow
{
    /// <summary>
    /// 观看人数.
    /// </summary>
    [JsonPropertyName("num")]
    public int? Num { get; set; }

    /// <summary>
    /// 短文本.
    /// </summary>
    [JsonPropertyName("text_small")]
    public string? TextSmall { get; set; }

    /// <summary>
    /// 长文本.
    /// </summary>
    [JsonPropertyName("text_large")]
    public string? TextLarge { get; set; }
}

/// <summary>
/// 二级分区的排序标签.
/// </summary>
internal sealed class LiveSecondListTag
{
    /// <summary>
    /// 标签Id.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// 标签名.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// 排序方式.
    /// </summary>
    [JsonPropertyName("sort_type")]
    public string? SortType { get; set; }

    /// <summary>
    /// 类型.
    /// </summary>
    [JsonPropertyName("type")]
    public int? Type { get; set; }

    /// <summary>
    /// 排序.
    /// </summary>
    [JsonPropertyName("sort")]
    public string? Sort { get; set; }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is LiveSecondListTag tag && Id == tag.Id;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id);
}
