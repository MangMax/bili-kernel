// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Richasy.BiliKernel.Services.Media.Core.Models;

/// <summary>
/// 直播间历史弹幕响应（进房补屏）.
/// </summary>
internal sealed class LiveHistoryDanmakuResponse
{
    /// <summary>
    /// 普通观众弹幕.
    /// </summary>
    [JsonPropertyName("room")]
    public List<LiveHistoryDanmakuItem> Room { get; set; }

    /// <summary>
    /// 房主与管理员弹幕.
    /// </summary>
    [JsonPropertyName("admin")]
    public List<LiveHistoryDanmakuItem> Admin { get; set; }
}

/// <summary>
/// 一条历史弹幕.
/// </summary>
internal sealed class LiveHistoryDanmakuItem
{
    /// <summary>
    /// 文本.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; }

    /// <summary>
    /// 发送者用户 Id.
    /// </summary>
    [JsonPropertyName("uid")]
    public long UserId { get; set; }

    /// <summary>
    /// 发送者昵称.
    /// </summary>
    [JsonPropertyName("nickname")]
    public string NickName { get; set; }

    /// <summary>
    /// 发送者昵称的备用字段；部分响应只给 uname，不给 nickname.
    /// </summary>
    [JsonPropertyName("uname")]
    public string? UserName { get; set; }

    /// <summary>
    /// 是否为管理员.
    /// </summary>
    [JsonPropertyName("isadmin")]
    public int IsAdmin { get; set; }

    /// <summary>
    /// 舰长等级：0 无，1 总督，2 提督，3 舰长.
    /// </summary>
    [JsonPropertyName("guard_level")]
    public int GuardLevel { get; set; }

    /// <summary>
    /// 用户等级.
    /// </summary>
    [JsonPropertyName("user_level")]
    [JsonConverter(typeof(FlexibleInt32Converter))]
    public int UserLevel { get; set; }

    /// <summary>
    /// 新版历史弹幕的用户对象；字段比旧版平铺结构更完整.
    /// </summary>
    [JsonPropertyName("user")]
    public LiveHistoryUser? User { get; set; }

    /// <summary>
    /// 单个表情（旧字段）.
    /// </summary>
    [JsonPropertyName("emoticon")]
    public LiveHistoryEmoticon? Emoticon { get; set; }

    /// <summary>
    /// 行内表情索引，键形如 [微笑].
    /// </summary>
    [JsonPropertyName("emots")]
    public Dictionary<string, LiveHistoryEmoticon>? Emots { get; set; }

    /// <summary>
    /// 发送时间文本（服务端为本地时间，不做时区换算）.
    /// </summary>
    [JsonPropertyName("timeline")]
    public string Timeline { get; set; }

    /// <summary>
    /// 文本颜色（十进制 RGB）.
    /// </summary>
    [JsonPropertyName("color")]
    public int Color { get; set; }
}

internal sealed class LiveHistoryUser
{
    [JsonPropertyName("uid")]
    public long Uid { get; set; }

    [JsonPropertyName("base")]
    public LiveHistoryUserBase? Base { get; set; }

    [JsonPropertyName("medal")]
    public LiveHistoryMedal? Medal { get; set; }

    [JsonPropertyName("guard")]
    public LiveHistoryGuard? Guard { get; set; }

    [JsonPropertyName("title")]
    public LiveHistoryTitle? Title { get; set; }
}

internal sealed class LiveHistoryUserBase
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("face")]
    public string? Face { get; set; }

    [JsonPropertyName("name_color")]
    [JsonConverter(typeof(LiveNameColorConverter))]
    public string? NameColor { get; set; }
}

internal sealed class LiveHistoryTitle
{
    [JsonPropertyName("title_css_id")]
    public string? Id { get; set; }

    [JsonPropertyName("old_title_css_id")]
    public string? LegacyId { get; set; }
}

internal sealed class LiveHistoryGuard
{
    [JsonPropertyName("level")]
    public int Level { get; set; }
}

internal sealed class LiveHistoryMedal
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("is_light")]
    public int IsLight { get; set; }

    [JsonPropertyName("medal_name")]
    public string? LegacyName { get; set; }

    [JsonPropertyName("medal_level")]
    public int LegacyLevel { get; set; }

    [JsonPropertyName("v2_medal_color_start")]
    public string? StartColor { get; set; }

    [JsonPropertyName("v2_medal_color_end")]
    public string? EndColor { get; set; }

    [JsonPropertyName("v2_medal_color")]
    public string? Color { get; set; }

    [JsonPropertyName("guard_icon")]
    public string? GuardIcon { get; set; }

    [JsonPropertyName("v2_medal_color_text")]
    public string? TextColor { get; set; }

    [JsonPropertyName("medal_color")]
    public string? LegacyColor { get; set; }
}

internal sealed class LiveHistoryEmoticon
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
