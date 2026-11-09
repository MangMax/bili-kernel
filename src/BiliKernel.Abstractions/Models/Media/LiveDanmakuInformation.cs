// Copyright (c) Richasy. All rights reserved.
// Licensed under the MIT License.

namespace Richasy.BiliKernel.Models.Media;

/// <summary>
/// 直播弹幕信息.
/// </summary>
public sealed class LiveDanmakuInformation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveDanmakuInformation"/> class.
    /// </summary>
    /// <param name="text">文本.</param>
    /// <param name="textColor">文本颜色.</param>
    /// <param name="userName">用户名.</param>
    /// <param name="level">用户等级.</param>
    /// <param name="levelColor">用户等级颜色 (Hex).</param>
    /// <param name="isAdmin">是否为管理员.</param>
    public LiveDanmakuInformation(
        string text,
        string textColor,
        string userName,
        string level,
        string levelColor,
        bool isAdmin)
    {
        Text = text;
        TextColor = textColor;
        UserName = userName;
        UserLevel = level;
        UserLevelColor = levelColor;
        IsAdmin = isAdmin;
    }

    /// <summary>
    /// 文本.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 文本颜色.
    /// </summary>
    public string TextColor { get; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; }

    /// <summary>
    /// 用户等级.
    /// </summary>
    public string UserLevel { get; }

    /// <summary>
    /// 用户等级数值（用于网页同款等级图）.
    /// </summary>
    public int UserLevelNumber { get; init; }

    /// <summary>
    /// 等级颜色.
    /// </summary>
    public string UserLevelColor { get; }

    /// <summary>
    /// 是否为管理员.
    /// </summary>
    public bool IsAdmin { get; }

    /// <summary>
    /// 用户标识.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// 勋章名称.
    /// </summary>
    public string? MedalName { get; init; }

    /// <summary>
    /// 勋章等级.
    /// </summary>
    public string? MedalLevel { get; init; }

    /// <summary>
    /// 是否为老爷或年费老爷.
    /// </summary>
    public bool IsVip { get; init; }

    /// <summary>
    /// 用户头衔.
    /// </summary>
    public string? UserTitle { get; init; }

    /// <summary>
    /// Gets 直播头衔标识，形如 title-233-1；图标需用 user_title/TitleList 映射.
    /// </summary>
    public string? UserTitleId { get; init; }

    /// <summary>
    /// 舰长等级：0 无，1 总督，2 提督，3 舰长.
    /// </summary>
    public int GuardLevel { get; init; }

    /// <summary>
    /// 粉丝勋章颜色（Hex）.
    /// </summary>
    public string? MedalColor { get; init; }

    /// <summary>
    /// 粉丝勋章所属主播昵称.
    /// </summary>
    public string? MedalAnchorName { get; init; }

    /// <summary>
    /// 昵称颜色（装扮）；为空时使用默认颜色.
    /// </summary>
    public string? NameColor { get; init; }

    /// <summary>
    /// 用户头像地址.
    /// </summary>
    public string? UserFace { get; init; }

    /// <summary>
    /// 本条弹幕携带的表情包索引；键为文本里的 [名称]，值为图片信息.
    /// </summary>
    public IReadOnlyDictionary<string, LiveDanmakuEmote>? Emotes { get; init; }
}

/// <summary>
/// 直播弹幕里的一个表情包.
/// </summary>
/// <param name="Url">图片地址.</param>
/// <param name="Width">原始宽度.</param>
/// <param name="Height">原始高度.</param>
/// <param name="Unique">表情唯一标识（emoticon_unique），部分弹幕用它而不是名称.</param>
public sealed record LiveDanmakuEmote(string Url, int Width, int Height, string? Unique = null);
