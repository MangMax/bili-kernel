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
